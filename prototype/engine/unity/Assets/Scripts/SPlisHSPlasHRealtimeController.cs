using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Liquid
{
    [DefaultExecutionOrder(100)]
    public class SPlisHSPlasHRealtimeController : MonoBehaviour
    {
        private const string PluginName = "SPlisHSPlasHUnityBridge";

        [DllImport(PluginName)] private static extern int SPSB_Initialize(string scenePath, string outputPath);
        [DllImport(PluginName)] private static extern void SPSB_Shutdown();
        [DllImport(PluginName)] private static extern int SPSB_IsInitialized();
        [DllImport(PluginName)] private static extern int SPSB_Reset();
        [DllImport(PluginName)] private static extern int SPSB_SetContainerPose(
            float positionX, float positionY, float positionZ,
            float rotationX, float rotationY, float rotationZ, float rotationW,
            float velocityX, float velocityY, float velocityZ,
            float angularVelocityX, float angularVelocityY, float angularVelocityZ);
        [DllImport(PluginName)] private static extern int SPSB_Step(float deltaTime);
        [DllImport(PluginName)] private static extern int SPSB_GetContainerPose(
            out float positionX, out float positionY, out float positionZ,
            out float rotationX, out float rotationY, out float rotationZ, out float rotationW);
        [DllImport(PluginName)] private static extern int SPSB_GetParticleCount();
        [DllImport(PluginName)] private static extern int SPSB_CopyParticlePositions([Out] float[] positionsXYZ, int maxParticles);
        [DllImport(PluginName)] private static extern IntPtr SPSB_GetLastError();

        [SerializeField] private Transform containerPivot;
        [SerializeField] private TrackballContainerController containerController;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private string sceneFileName = "LiquidCubeRealtimeBridge.json";
        [SerializeField] private float particleSizeMultiplier = 2.85f;
        [SerializeField] private float simulationStep = 1.0f / 240.0f;
        [SerializeField] private int maxSubstepsPerFrame = 8;
        [SerializeField] private bool initializeOnStart = true;

        private ParticleSystem particleSystemComponent;
        private ParticleSystem.Particle[] particles;
        private float[] positionBuffer;
        private float simulationAccumulator;
        private bool bridgeInitialized;
        private int particleCount;

        public bool BridgeInitialized => bridgeInitialized;
        public int ParticleCount => particleCount;
        public ParticleSystem ParticleSystemComponent => particleSystemComponent;

        private void Awake()
        {
            simulationStep = Mathf.Min(simulationStep, 1.0f / 240.0f);
            maxSubstepsPerFrame = Mathf.Max(maxSubstepsPerFrame, 8);
            particleSizeMultiplier = Mathf.Max(particleSizeMultiplier, 2.85f);
            ResolveReferences();
            EnsureParticleSystem();
        }

        private void Start()
        {
            if (initializeOnStart)
            {
                InitializeBridge();
            }
        }

        private void FixedUpdate()
        {
            if (!bridgeInitialized)
            {
                return;
            }

            simulationAccumulator += Time.fixedDeltaTime;
            int substeps = 0;
            while (simulationAccumulator >= simulationStep && substeps < maxSubstepsPerFrame)
            {
                ApplyContainerPoseToBridge();
                if (SPSB_Step(simulationStep) == 0)
                {
                    Debug.LogError("SPlisHSPlasH step failed: " + GetLastError());
                    bridgeInitialized = false;
                    return;
                }

                simulationAccumulator -= simulationStep;
                substeps += 1;
            }

            if (substeps > 0)
            {
                SynchronizeContainerPivotFromBridge();
                RefreshParticles();
            }
        }

        private void OnDestroy()
        {
            if (bridgeInitialized)
            {
                SPSB_Shutdown();
                bridgeInitialized = false;
            }
        }

        public void InitializeBridge()
        {
            if (bridgeInitialized)
            {
                return;
            }

            ResolveReferences();
            ConfigureContainerController();

            string repoRoot = ResolveRepoRoot();
            string scenePath = Path.Combine(repoRoot, ".experiments", "SPlisHSPlasH", "data", "Scenes", sceneFileName);
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "SPlisHSPlasHUnityBridge"));
            Directory.CreateDirectory(outputPath);

            if (!File.Exists(scenePath))
            {
                Debug.LogError("SPlisHSPlasH scene file not found: " + scenePath);
                return;
            }

            if (SPSB_Initialize(scenePath, outputPath) == 0)
            {
                Debug.LogError("Failed to initialize SPlisHSPlasH bridge: " + GetLastError());
                return;
            }

            bridgeInitialized = SPSB_IsInitialized() != 0;
            if (!bridgeInitialized)
            {
                Debug.LogError("SPlisHSPlasH bridge reported uninitialized state.");
                return;
            }

            AllocateParticleBuffers();
            SynchronizeContainerPivotFromBridge();
            ConfigureContainerController();
            ApplyContainerPoseToBridge();
            RefreshParticles();
        }

        public void ResetSimulation()
        {
            if (!bridgeInitialized)
            {
                InitializeBridge();
                return;
            }

            simulationAccumulator = 0f;
            if (SPSB_Reset() == 0)
            {
                Debug.LogError("Failed to reset SPlisHSPlasH bridge: " + GetLastError());
                return;
            }

            AllocateParticleBuffers();
            SynchronizeContainerPivotFromBridge();
            ConfigureContainerController();
            ApplyContainerPoseToBridge();
            RefreshParticles();
        }

        private void ApplyContainerPoseToBridge()
        {
            if (!bridgeInitialized || containerPivot == null)
            {
                return;
            }

            Vector3 angularVelocity = containerController != null ? containerController.AngularVelocity : Vector3.zero;
            Quaternion rotation = containerController != null ? containerController.TargetRotation : containerPivot.rotation;
            Vector3 position = containerPivot.position;

            if (SPSB_SetContainerPose(
                    position.x, position.y, position.z,
                    rotation.x, rotation.y, rotation.z, rotation.w,
                    0f, 0f, 0f,
                    angularVelocity.x, angularVelocity.y, angularVelocity.z) == 0)
            {
                Debug.LogError("Failed to send container pose to SPlisHSPlasH bridge: " + GetLastError());
                bridgeInitialized = false;
            }
        }

        private void AllocateParticleBuffers()
        {
            particleCount = SPSB_GetParticleCount();
            if (particleCount <= 0)
            {
                Debug.LogError("SPlisHSPlasH returned no particles.");
                bridgeInitialized = false;
                return;
            }

            positionBuffer = new float[particleCount * 3];
            particles = new ParticleSystem.Particle[particleCount];

            float particleSize = 0.03f * particleSizeMultiplier;
            Color particleColor = new Color(1f, 1f, 1f, 0.82f);
            for (int index = 0; index < particleCount; index += 1)
            {
                particles[index].remainingLifetime = float.MaxValue;
                particles[index].startLifetime = float.MaxValue;
                particles[index].startSize = particleSize;
                particles[index].startColor = particleColor;
            }

            ParticleSystem.MainModule main = particleSystemComponent.main;
            main.maxParticles = particleCount;

            particleSystemComponent.Clear();
            particleSystemComponent.Emit(particleCount);
            particleSystemComponent.SetParticles(particles, particles.Length);
        }

        private void RefreshParticles()
        {
            if (!bridgeInitialized || positionBuffer == null || particles == null)
            {
                return;
            }

            int written = SPSB_CopyParticlePositions(positionBuffer, particleCount);
            if (written <= 0)
            {
                Debug.LogError("Failed to fetch SPlisHSPlasH particles: " + GetLastError());
                bridgeInitialized = false;
                return;
            }

            for (int index = 0; index < written; index += 1)
            {
                int bufferIndex = index * 3;
                particles[index].position = new Vector3(
                    positionBuffer[bufferIndex + 0],
                    positionBuffer[bufferIndex + 1],
                    positionBuffer[bufferIndex + 2]);
            }

            particleSystemComponent.SetParticles(particles, written);
        }

        private void SynchronizeContainerPivotFromBridge()
        {
            if (!bridgeInitialized || containerPivot == null)
            {
                return;
            }

            if (SPSB_GetContainerPose(
                    out float positionX, out float positionY, out float positionZ,
                    out float rotationX, out float rotationY, out float rotationZ, out float rotationW) == 0)
            {
                Debug.LogError("Failed to fetch SPlisHSPlasH container pose: " + GetLastError());
                bridgeInitialized = false;
                return;
            }

            Vector3 position = new Vector3(positionX, positionY, positionZ);
            Quaternion rotation = new Quaternion(rotationX, rotationY, rotationZ, rotationW);

            if (containerPivot.TryGetComponent(out Rigidbody rigidbody) && rigidbody.isKinematic)
            {
                rigidbody.position = position;
                rigidbody.rotation = rotation;
                return;
            }

            containerPivot.SetPositionAndRotation(position, rotation);
        }

        private void EnsureParticleSystem()
        {
            if (particleSystemComponent != null)
            {
                return;
            }

            GameObject particleObject = new GameObject("SPlisHSPlasHRealtimeParticles");
            particleObject.transform.SetParent(transform, false);

            particleSystemComponent = particleObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particleSystemComponent.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 6000;
            main.startLifetime = float.MaxValue;
            main.startSpeed = 0f;
            main.startSize = 0.06f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particleSystemComponent.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particleSystemComponent.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (particleMaterial != null)
            {
                renderer.sharedMaterial = particleMaterial;
            }
        }

        private void ResolveReferences()
        {
            if (containerPivot == null)
            {
                GameObject pivotObject = GameObject.Find("ContainerPivot");
                if (pivotObject != null)
                {
                    containerPivot = pivotObject.transform;
                }
            }

            if (containerController == null)
            {
                containerController = FindFirstObjectByType<TrackballContainerController>();
            }

            if (containerController != null)
            {
                containerController.SetWorldTiltMode(true);
            }
        }

        private void ConfigureContainerController()
        {
            if (containerController == null || containerPivot == null)
            {
                return;
            }

            Rigidbody pivotRigidbody = containerPivot.GetComponent<Rigidbody>();
            containerController.ConfigureTarget(containerPivot, pivotRigidbody, false);
            containerController.SetWorldTiltMode(true);
            containerController.ConfigureMotionResponse(0.1f, 3.2f, 8.0f);
        }

        private static string ResolveRepoRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", ".."));
        }

        private static string GetLastError()
        {
            IntPtr pointer = SPSB_GetLastError();
            return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer);
        }
    }
}
