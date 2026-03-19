#include "Simulator/SimulatorBase.h"
#include "Simulator/SceneConfiguration.h"
#include "Simulator/PositionBasedDynamicsWrapper/PBDBoundarySimulator.h"
#include "Simulation/Constraints.h"
#include "SPlisHSPlasH/BoundaryModel.h"
#include "SPlisHSPlasH/FluidModel.h"
#include "SPlisHSPlasH/RigidBodyObject.h"
#include "SPlisHSPlasH/Simulation.h"
#include "SPlisHSPlasH/TimeManager.h"

#include <algorithm>
#include <cmath>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

using namespace SPH;

namespace
{
    struct BridgeContext
    {
        std::unique_ptr<SimulatorBase> simulator;
        FluidModel *fluidModel = nullptr;
        BoundaryModel *boundaryModel = nullptr;
        RigidBodyObject *rigidBody = nullptr;
        std::string scenePath;
        std::string outputPath;
        std::vector<std::string> arguments;
        std::string lastError;
        Vector3r targetPosition = Vector3r::Zero();
        Quaternionr targetRotation = Quaternionr::Identity();
        Vector3r targetVelocity = Vector3r::Zero();
        Vector3r targetAngularVelocity = Vector3r::Zero();
        bool hasTargetState = false;
        PBD::TargetVelocityMotorHingeJoint *motorJointX = nullptr;
        PBD::TargetVelocityMotorHingeJoint *motorJointZ = nullptr;
    };

    std::mutex g_mutex;
    std::unique_ptr<BridgeContext> g_context;

    constexpr Real OrientationVelocityGain = static_cast<Real>(12.0);
    constexpr Real AngularVelocityBlendRate = static_cast<Real>(24.0);
    constexpr Real MaxServoAngularSpeed = static_cast<Real>(16.0);
    constexpr Real RealtimePbdDamping = static_cast<Real>(0.0125);

    bool ResolveSimulationPointers(BridgeContext &context)
    {
        Simulation *simulation = Simulation::getCurrent();
        if (simulation == nullptr)
        {
            context.lastError = "Simulation singleton is not initialized.";
            return false;
        }

        if (simulation->numberOfFluidModels() == 0)
        {
            context.lastError = "Scene contains no fluid models.";
            return false;
        }

        if (simulation->numberOfBoundaryModels() == 0)
        {
            context.lastError = "Scene contains no boundary models.";
            return false;
        }

        context.fluidModel = simulation->getFluidModel(0);

        const Utilities::SceneLoader::Scene &scene = SceneConfiguration::getCurrent()->getScene();
        const unsigned int boundaryCount = std::min<unsigned int>(
            static_cast<unsigned int>(scene.boundaryModels.size()),
            simulation->numberOfBoundaryModels());

        unsigned int selectedBoundaryIndex = 0;
        bool foundDynamicWall = false;
        bool foundAnyWall = false;
        for (unsigned int index = 0; index < boundaryCount; ++index)
        {
            Utilities::BoundaryParameterObject *boundaryParameters = scene.boundaryModels[index];
            if (boundaryParameters == nullptr)
            {
                continue;
            }

            if (boundaryParameters->isWall && boundaryParameters->dynamic)
            {
                selectedBoundaryIndex = index;
                foundDynamicWall = true;
                break;
            }

            if (boundaryParameters->isWall)
            {
                selectedBoundaryIndex = index;
                foundAnyWall = true;
            }
        }

        if (!foundDynamicWall && !foundAnyWall && boundaryCount > 0)
        {
            selectedBoundaryIndex = boundaryCount - 1;
        }

        context.boundaryModel = simulation->getBoundaryModel(selectedBoundaryIndex);
        context.rigidBody = context.boundaryModel->getRigidBodyObject();
        context.motorJointX = nullptr;
        context.motorJointZ = nullptr;

        if (context.fluidModel == nullptr || context.boundaryModel == nullptr || context.rigidBody == nullptr)
        {
            context.lastError = "Failed to resolve fluid or boundary objects.";
            return false;
        }

        auto *boundarySimulator = dynamic_cast<PBDBoundarySimulator*>(context.simulator->getBoundarySimulator());
        if (boundarySimulator != nullptr)
        {
            boundarySimulator->getPBDWrapper()->setDampingCoeff(RealtimePbdDamping);

            PBD::SimulationModel::ConstraintVector &constraints =
                boundarySimulator->getPBDWrapper()->getSimulationModel().getConstraints();

            std::vector<PBD::TargetVelocityMotorHingeJoint*> motorJoints;
            motorJoints.reserve(2);
            for (PBD::Constraint *constraint : constraints)
            {
                if (constraint != nullptr && constraint->getTypeId() == PBD::TargetVelocityMotorHingeJoint::TYPE_ID)
                {
                    motorJoints.push_back(static_cast<PBD::TargetVelocityMotorHingeJoint*>(constraint));
                }
            }

            if (motorJoints.size() >= 2)
            {
                context.motorJointX = motorJoints[0];
                context.motorJointZ = motorJoints[1];
            }
        }

        return true;
    }

    void UpdateBoundaryVelocities(BridgeContext &context)
    {
        Simulation *simulation = Simulation::getCurrent();
        if (simulation == nullptr)
        {
            return;
        }

        if (simulation->getBoundaryHandlingMethod() == BoundaryHandlingMethods::Bender2019)
        {
            context.simulator->updateVMVelocity();
        }
        else if (simulation->getBoundaryHandlingMethod() == BoundaryHandlingMethods::Koschier2017)
        {
            context.simulator->updateDMVelocity();
        }
        else if (simulation->getBoundaryHandlingMethod() == BoundaryHandlingMethods::Akinci2012)
        {
            context.simulator->updateBoundaryParticles(false);
        }
    }

    void ApplyDynamicBodyServo(BridgeContext &context, const Real deltaTime)
    {
        if (!context.hasTargetState || context.rigidBody == nullptr || !context.rigidBody->isDynamic())
        {
            return;
        }

        Quaternionr currentRotation = context.rigidBody->getRotation();
        Quaternionr targetRotation = context.targetRotation;
        if (currentRotation.coeffs().dot(targetRotation.coeffs()) < static_cast<Real>(0.0))
        {
            targetRotation.coeffs() *= static_cast<Real>(-1.0);
        }

        Quaternionr deltaRotation = targetRotation * currentRotation.conjugate();
        deltaRotation.normalize();
        if (deltaRotation.w() < static_cast<Real>(0.0))
        {
            deltaRotation.coeffs() *= static_cast<Real>(-1.0);
        }

        const Vector3r deltaVector = deltaRotation.vec();
        const Real deltaVectorNorm = deltaVector.norm();

        Vector3r correctionAngularVelocity = Vector3r::Zero();
        if (deltaVectorNorm > static_cast<Real>(1e-6))
        {
            const Real angle = static_cast<Real>(2.0) *
                std::atan2(deltaVectorNorm, std::max(static_cast<Real>(1e-6), deltaRotation.w()));
            const Vector3r axis = deltaVector / deltaVectorNorm;
            correctionAngularVelocity = axis * (angle * OrientationVelocityGain);
        }

        Vector3r desiredAngularVelocity = context.targetAngularVelocity + correctionAngularVelocity;
        const Real desiredSpeed = desiredAngularVelocity.norm();
        if (desiredSpeed > MaxServoAngularSpeed && desiredSpeed > static_cast<Real>(1e-6))
        {
            desiredAngularVelocity *= MaxServoAngularSpeed / desiredSpeed;
        }

        if (context.motorJointX != nullptr && context.motorJointZ != nullptr)
        {
            context.motorJointX->setTarget(desiredAngularVelocity.x());
            context.motorJointZ->setTarget(desiredAngularVelocity.z());
            context.rigidBody->setVelocity(Vector3r::Zero());
            return;
        }

        const Vector3r currentAngularVelocity = context.rigidBody->getAngularVelocity();
        const Real maxAngularVelocityDelta = AngularVelocityBlendRate * deltaTime;
        Vector3r nextAngularVelocity = currentAngularVelocity;
        const Vector3r velocityDelta = desiredAngularVelocity - currentAngularVelocity;
        const Real velocityDeltaMagnitude = velocityDelta.norm();
        if (velocityDeltaMagnitude > maxAngularVelocityDelta && velocityDeltaMagnitude > static_cast<Real>(1e-6))
        {
            nextAngularVelocity += velocityDelta * (maxAngularVelocityDelta / velocityDeltaMagnitude);
        }
        else
        {
            nextAngularVelocity = desiredAngularVelocity;
        }

        context.rigidBody->setAngularVelocity(nextAngularVelocity);
        context.rigidBody->setVelocity(Vector3r::Zero());
    }

    void CleanupContext()
    {
        if (!g_context)
        {
            return;
        }

        if (g_context->simulator)
        {
            g_context->simulator->cleanup();
            g_context->simulator.reset();
        }

        g_context.reset();
    }

    void SetError(const std::string &message)
    {
        if (!g_context)
        {
            g_context = std::make_unique<BridgeContext>();
        }
        g_context->lastError = message;
    }
}

extern "C"
{
    __declspec(dllexport) int SPSB_Initialize(const char *scenePath, const char *outputPath)
    {
        std::lock_guard<std::mutex> lock(g_mutex);

        CleanupContext();
        g_context = std::make_unique<BridgeContext>();

        if (scenePath == nullptr || scenePath[0] == '\0')
        {
            g_context->lastError = "scenePath is empty.";
            return 0;
        }

        try
        {
            g_context->scenePath = scenePath;
            g_context->outputPath = (outputPath != nullptr) ? outputPath : "";

            g_context->simulator = std::make_unique<SimulatorBase>();
            g_context->arguments = {
                "splishsplash_unity_bridge",
                "--scene-file",
                g_context->scenePath,
                "--no-gui",
                "--no-initial-pause"
            };

            if (!g_context->outputPath.empty())
            {
                g_context->arguments.push_back("--output-dir");
                g_context->arguments.push_back(g_context->outputPath);
            }

            g_context->simulator->init(g_context->arguments, "SPlisHSPlasHUnityBridge");
            g_context->simulator->initSimulation();
            g_context->simulator->deferredInit();

            if (!ResolveSimulationPointers(*g_context))
            {
                CleanupContext();
                return 0;
            }

            g_context->targetPosition = g_context->rigidBody->getPosition();
            g_context->targetRotation = g_context->rigidBody->getRotation();
            g_context->targetVelocity = Vector3r::Zero();
            g_context->targetAngularVelocity = Vector3r::Zero();
            g_context->hasTargetState = true;

            g_context->lastError.clear();
            return 1;
        }
        catch (const std::exception &exception)
        {
            SetError(exception.what());
            CleanupContext();
            return 0;
        }
        catch (...)
        {
            SetError("Unknown exception during initialization.");
            CleanupContext();
            return 0;
        }
    }

    __declspec(dllexport) void SPSB_Shutdown()
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        CleanupContext();
    }

    __declspec(dllexport) int SPSB_IsInitialized()
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        return g_context && g_context->simulator && g_context->fluidModel && g_context->rigidBody ? 1 : 0;
    }

    __declspec(dllexport) int SPSB_Reset()
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context || !g_context->simulator)
        {
            SetError("Bridge is not initialized.");
            return 0;
        }

        g_context->simulator->reset();
        if (!ResolveSimulationPointers(*g_context))
        {
            return 0;
        }

        g_context->targetPosition = g_context->rigidBody->getPosition();
        g_context->targetRotation = g_context->rigidBody->getRotation();
        g_context->targetVelocity = Vector3r::Zero();
        g_context->targetAngularVelocity = Vector3r::Zero();
        g_context->hasTargetState = true;

        return 1;
    }

    __declspec(dllexport) int SPSB_SetContainerPose(
        float positionX,
        float positionY,
        float positionZ,
        float rotationX,
        float rotationY,
        float rotationZ,
        float rotationW,
        float velocityX,
        float velocityY,
        float velocityZ,
        float angularVelocityX,
        float angularVelocityY,
        float angularVelocityZ)
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context || !g_context->rigidBody)
        {
            SetError("Bridge is not initialized.");
            return 0;
        }

        auto *rigidBody = g_context->rigidBody;
        g_context->targetPosition = Vector3r(positionX, positionY, positionZ);
        g_context->targetRotation = Quaternionr(rotationW, rotationX, rotationY, rotationZ);
        g_context->targetVelocity = Vector3r(velocityX, velocityY, velocityZ);
        g_context->targetAngularVelocity = Vector3r(angularVelocityX, angularVelocityY, angularVelocityZ);
        g_context->hasTargetState = true;

        if (!rigidBody->isDynamic())
        {
            rigidBody->setPosition(g_context->targetPosition);
            rigidBody->setRotation(g_context->targetRotation);
            rigidBody->setVelocity(g_context->targetVelocity);
            rigidBody->setAngularVelocity(g_context->targetAngularVelocity);
            rigidBody->updateMeshTransformation();
            UpdateBoundaryVelocities(*g_context);
        }

        return 1;
    }

    __declspec(dllexport) int SPSB_Step(float deltaTime)
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context || !g_context->simulator)
        {
            SetError("Bridge is not initialized.");
            return 0;
        }

        if (deltaTime <= 0.0f)
        {
            deltaTime = 1.0f / 60.0f;
        }

        TimeManager::getCurrent()->setTimeStepSize(deltaTime);
        ApplyDynamicBodyServo(*g_context, deltaTime);
        g_context->simulator->timeStepNoGUI();
        return 1;
    }

    __declspec(dllexport) int SPSB_GetContainerPose(
        float *positionX,
        float *positionY,
        float *positionZ,
        float *rotationX,
        float *rotationY,
        float *rotationZ,
        float *rotationW)
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context || !g_context->rigidBody)
        {
            SetError("Bridge is not initialized.");
            return 0;
        }

        const Vector3r &position = g_context->rigidBody->getPosition();
        const Quaternionr &rotation = g_context->rigidBody->getRotation();

        if (positionX) *positionX = static_cast<float>(position.x());
        if (positionY) *positionY = static_cast<float>(position.y());
        if (positionZ) *positionZ = static_cast<float>(position.z());
        if (rotationX) *rotationX = static_cast<float>(rotation.x());
        if (rotationY) *rotationY = static_cast<float>(rotation.y());
        if (rotationZ) *rotationZ = static_cast<float>(rotation.z());
        if (rotationW) *rotationW = static_cast<float>(rotation.w());

        return 1;
    }

    __declspec(dllexport) int SPSB_GetParticleCount()
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context || !g_context->fluidModel)
        {
            return 0;
        }

        return static_cast<int>(g_context->fluidModel->numActiveParticles());
    }

    __declspec(dllexport) int SPSB_CopyParticlePositions(float *positionsXYZ, int maxParticles)
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context || !g_context->fluidModel || positionsXYZ == nullptr)
        {
            return 0;
        }

        const unsigned int particleCount = g_context->fluidModel->numActiveParticles();
        const unsigned int count = std::min<unsigned int>(particleCount, static_cast<unsigned int>(maxParticles));
        for (unsigned int index = 0; index < count; ++index)
        {
            const Vector3r &position = g_context->fluidModel->getPosition(index);
            const unsigned int baseIndex = index * 3;
            positionsXYZ[baseIndex + 0] = static_cast<float>(position.x());
            positionsXYZ[baseIndex + 1] = static_cast<float>(position.y());
            positionsXYZ[baseIndex + 2] = static_cast<float>(position.z());
        }

        return static_cast<int>(count);
    }

    __declspec(dllexport) const char *SPSB_GetLastError()
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        if (!g_context)
        {
            return "";
        }

        return g_context->lastError.c_str();
    }
}
