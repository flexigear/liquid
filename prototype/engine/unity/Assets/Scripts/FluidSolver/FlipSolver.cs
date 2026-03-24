using UnityEngine;

namespace Liquid.FluidSolver
{
    public class FlipSolver : MonoBehaviour
    {
        private const int ParticlesPerCell = 8;
        private const int ParticleFloatStride = 6;
        private const int ParticleThreadGroupSize = 256;
        private const int GridThreadGroupSize = 8;
        private const int JacobiIterations = 50;
        private const float GizmoRadius = 0.01f;

        [SerializeField] private ComputeShader classifyCellsCS;
        [SerializeField] private ComputeShader p2gCS;
        [SerializeField] private ComputeShader addForcesCS;
        [SerializeField] private ComputeShader pressureSolveCS;
        [SerializeField] private ComputeShader g2pCS;
        [SerializeField] private ComputeShader advectCS;

        private ComputeBuffer particleBuffer;
        private ComputeBuffer gridVelX;
        private ComputeBuffer gridVelY;
        private ComputeBuffer gridVelZ;
        private ComputeBuffer gridVelXOld;
        private ComputeBuffer gridVelYOld;
        private ComputeBuffer gridVelZOld;
        private ComputeBuffer gridPressure;
        private ComputeBuffer gridPressureOld;
        private ComputeBuffer gridCellType;
        private ComputeBuffer gridWeightX;
        private ComputeBuffer gridWeightY;
        private ComputeBuffer gridWeightZ;
        private int particleCount;

        private ComputeBuffer gridVelXInt;
        private ComputeBuffer gridVelYInt;
        private ComputeBuffer gridVelZInt;
        private ComputeBuffer gridWeightXInt;
        private ComputeBuffer gridWeightYInt;
        private ComputeBuffer gridWeightZInt;
        private ComputeBuffer gridDivergence;

        private float[] gridVelXCopy;
        private float[] gridVelYCopy;
        private float[] gridVelZCopy;
        private float[] particleReadback;

        private int clearGridKernel = -1;
        private int markFluidCellsKernel = -1;
        private int p2gMainKernel = -1;
        private int normalizeKernel = -1;
        private int addForcesKernel = -1;
        private int computeDivergenceKernel = -1;
        private int jacobiKernel = -1;
        private int applyPressureKernel = -1;
        private int g2pKernel = -1;
        private int advectKernel = -1;
        private int enforceBoundaryKernel = -1;

        private bool missingShaderWarningLogged;

        private void Start()
        {
            AllocateBuffers();
            InitializeParticles();
            CacheKernelIds();
        }

        private void FixedUpdate()
        {
            if (!IsReadyToSimulate())
            {
                return;
            }

            DispatchClearGrid();
            DispatchMarkFluidCells();
            DispatchParticleToGrid();
            CopyGridVelocitiesToOld();
            DispatchAddForces();
            DispatchEnforceBoundary();
            DispatchPressureSolve();
            DispatchEnforceBoundary();
            DispatchGridToParticle();
            DispatchAdvectParticles();
        }

        private void OnDestroy()
        {
            ReleaseBuffer(ref particleBuffer);
            ReleaseBuffer(ref gridVelX);
            ReleaseBuffer(ref gridVelY);
            ReleaseBuffer(ref gridVelZ);
            ReleaseBuffer(ref gridVelXOld);
            ReleaseBuffer(ref gridVelYOld);
            ReleaseBuffer(ref gridVelZOld);
            ReleaseBuffer(ref gridPressure);
            ReleaseBuffer(ref gridPressureOld);
            ReleaseBuffer(ref gridCellType);
            ReleaseBuffer(ref gridWeightX);
            ReleaseBuffer(ref gridWeightY);
            ReleaseBuffer(ref gridWeightZ);
            ReleaseBuffer(ref gridVelXInt);
            ReleaseBuffer(ref gridVelYInt);
            ReleaseBuffer(ref gridVelZInt);
            ReleaseBuffer(ref gridWeightXInt);
            ReleaseBuffer(ref gridWeightYInt);
            ReleaseBuffer(ref gridWeightZInt);
            ReleaseBuffer(ref gridDivergence);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || particleBuffer == null || particleCount <= 0)
            {
                return;
            }

            if (particleReadback == null || particleReadback.Length != particleCount * ParticleFloatStride)
            {
                particleReadback = new float[particleCount * ParticleFloatStride];
            }

            particleBuffer.GetData(particleReadback);

            Gizmos.color = Color.blue;
            for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
            {
                int baseIndex = particleIndex * ParticleFloatStride;
                Vector3 position = new Vector3(
                    particleReadback[baseIndex + 0],
                    particleReadback[baseIndex + 1],
                    particleReadback[baseIndex + 2]);

                Gizmos.DrawSphere(position, GizmoRadius);
            }
        }

        private void AllocateBuffers()
        {
            int gridRes = FlipSolverConstants.GRID_RES;
            int cellCount = gridRes * gridRes * gridRes;
            int xFaceCount = (gridRes + 1) * gridRes * gridRes;
            int yFaceCount = gridRes * (gridRes + 1) * gridRes;
            int zFaceCount = gridRes * gridRes * (gridRes + 1);

            gridVelX = new ComputeBuffer(xFaceCount, sizeof(float));
            gridVelY = new ComputeBuffer(yFaceCount, sizeof(float));
            gridVelZ = new ComputeBuffer(zFaceCount, sizeof(float));
            gridVelXOld = new ComputeBuffer(xFaceCount, sizeof(float));
            gridVelYOld = new ComputeBuffer(yFaceCount, sizeof(float));
            gridVelZOld = new ComputeBuffer(zFaceCount, sizeof(float));
            gridPressure = new ComputeBuffer(cellCount, sizeof(float));
            gridPressureOld = new ComputeBuffer(cellCount, sizeof(float));
            gridCellType = new ComputeBuffer(cellCount, sizeof(int));
            gridWeightX = new ComputeBuffer(xFaceCount, sizeof(float));
            gridWeightY = new ComputeBuffer(yFaceCount, sizeof(float));
            gridWeightZ = new ComputeBuffer(zFaceCount, sizeof(float));

            gridVelXInt = new ComputeBuffer(xFaceCount, sizeof(int));
            gridVelYInt = new ComputeBuffer(yFaceCount, sizeof(int));
            gridVelZInt = new ComputeBuffer(zFaceCount, sizeof(int));
            gridWeightXInt = new ComputeBuffer(xFaceCount, sizeof(int));
            gridWeightYInt = new ComputeBuffer(yFaceCount, sizeof(int));
            gridWeightZInt = new ComputeBuffer(zFaceCount, sizeof(int));
            gridDivergence = new ComputeBuffer(cellCount, sizeof(float));

            gridVelXCopy = new float[xFaceCount];
            gridVelYCopy = new float[yFaceCount];
            gridVelZCopy = new float[zFaceCount];

            float[] zeroPressure = new float[cellCount];
            gridPressure.SetData(zeroPressure);
            gridPressureOld.SetData(zeroPressure);
        }

        private void InitializeParticles()
        {
            const int minI = 24;
            const int maxI = 40;
            const int minJ = 40;
            const int maxJ = 56;
            const int minK = 24;
            const int maxK = 40;

            particleCount = (maxI - minI) * (maxJ - minJ) * (maxK - minK) * ParticlesPerCell;
            float[] particleData = new float[particleCount * ParticleFloatStride];
            particleReadback = new float[particleData.Length];

            float halfCell = FlipSolverConstants.CELL_SIZE * 0.5f;
            System.Random random = new System.Random(12345);
            int particleIndex = 0;

            for (int k = minK; k < maxK; k++)
            {
                for (int j = minJ; j < maxJ; j++)
                {
                    for (int i = minI; i < maxI; i++)
                    {
                        Vector3 cellCenter = new Vector3(
                            FlipSolverConstants.DOMAIN_MIN + (i + 0.5f) * FlipSolverConstants.CELL_SIZE,
                            FlipSolverConstants.DOMAIN_MIN + (j + 0.5f) * FlipSolverConstants.CELL_SIZE,
                            FlipSolverConstants.DOMAIN_MIN + (k + 0.5f) * FlipSolverConstants.CELL_SIZE);

                        for (int p = 0; p < ParticlesPerCell; p++)
                        {
                            int baseIndex = particleIndex * ParticleFloatStride;
                            Vector3 jitter = new Vector3(
                                NextJitter(random, halfCell),
                                NextJitter(random, halfCell),
                                NextJitter(random, halfCell));

                            Vector3 position = cellCenter + jitter;

                            particleData[baseIndex + 0] = position.x;
                            particleData[baseIndex + 1] = position.y;
                            particleData[baseIndex + 2] = position.z;
                            particleData[baseIndex + 3] = 0.0f;
                            particleData[baseIndex + 4] = 0.0f;
                            particleData[baseIndex + 5] = 0.0f;
                            particleIndex++;
                        }
                    }
                }
            }

            particleBuffer = new ComputeBuffer(particleCount * ParticleFloatStride, sizeof(float));
            particleBuffer.SetData(particleData);
        }

        private void CacheKernelIds()
        {
            if (classifyCellsCS != null)
            {
                clearGridKernel = classifyCellsCS.FindKernel("ClearGrid");
                markFluidCellsKernel = classifyCellsCS.FindKernel("MarkFluidCells");
            }

            if (p2gCS != null)
            {
                p2gMainKernel = p2gCS.FindKernel("P2GMain");
                normalizeKernel = p2gCS.FindKernel("Normalize");
            }

            if (addForcesCS != null)
            {
                addForcesKernel = addForcesCS.FindKernel("CSMain");
            }

            if (pressureSolveCS != null)
            {
                computeDivergenceKernel = pressureSolveCS.FindKernel("ComputeDivergence");
                jacobiKernel = pressureSolveCS.FindKernel("JacobiIteration");
                applyPressureKernel = pressureSolveCS.FindKernel("ApplyPressure");
                enforceBoundaryKernel = pressureSolveCS.FindKernel("EnforceBoundary");
            }

            if (g2pCS != null)
            {
                g2pKernel = g2pCS.FindKernel("CSMain");
            }

            if (advectCS != null)
            {
                advectKernel = advectCS.FindKernel("CSMain");
            }
        }

        private bool IsReadyToSimulate()
        {
            bool ready =
                classifyCellsCS != null &&
                p2gCS != null &&
                addForcesCS != null &&
                pressureSolveCS != null &&
                g2pCS != null &&
                advectCS != null &&
                particleBuffer != null;

            if (!ready && !missingShaderWarningLogged)
            {
                Debug.LogWarning("FlipSolver is missing one or more ComputeShader references.");
                missingShaderWarningLogged = true;
            }

            return ready;
        }

        private void DispatchClearGrid()
        {
            SetCommonGridParams(classifyCellsCS);
            float bhe = FlipSolverConstants.BOX_HALF_EXTENT;
            classifyCellsCS.SetFloats("boxHalfExtent", bhe, bhe, bhe);
            classifyCellsCS.SetBuffer(clearGridKernel, "cellType", gridCellType);
            classifyCellsCS.SetBuffer(clearGridKernel, "velX", gridVelX);
            classifyCellsCS.SetBuffer(clearGridKernel, "velY", gridVelY);
            classifyCellsCS.SetBuffer(clearGridKernel, "velZ", gridVelZ);
            classifyCellsCS.SetBuffer(clearGridKernel, "weightX", gridWeightX);
            classifyCellsCS.SetBuffer(clearGridKernel, "weightY", gridWeightY);
            classifyCellsCS.SetBuffer(clearGridKernel, "weightZ", gridWeightZ);
            classifyCellsCS.SetBuffer(clearGridKernel, "velXInt", gridVelXInt);
            classifyCellsCS.SetBuffer(clearGridKernel, "velYInt", gridVelYInt);
            classifyCellsCS.SetBuffer(clearGridKernel, "velZInt", gridVelZInt);
            classifyCellsCS.SetBuffer(clearGridKernel, "weightXInt", gridWeightXInt);
            classifyCellsCS.SetBuffer(clearGridKernel, "weightYInt", gridWeightYInt);
            classifyCellsCS.SetBuffer(clearGridKernel, "weightZInt", gridWeightZInt);
            classifyCellsCS.Dispatch(
                clearGridKernel,
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));
        }

        private void DispatchMarkFluidCells()
        {
            SetCommonGridParams(classifyCellsCS);
            classifyCellsCS.SetInt("particleCount", particleCount);
            classifyCellsCS.SetBuffer(markFluidCellsKernel, "cellType", gridCellType);
            classifyCellsCS.SetBuffer(markFluidCellsKernel, "particleBuf", particleBuffer);
            classifyCellsCS.Dispatch(markFluidCellsKernel, DivUp(particleCount, ParticleThreadGroupSize), 1, 1);
        }

        private void DispatchParticleToGrid()
        {
            SetCommonGridParams(p2gCS);
            p2gCS.SetInt("particleCount", particleCount);

            p2gCS.SetBuffer(p2gMainKernel, "velXInt", gridVelXInt);
            p2gCS.SetBuffer(p2gMainKernel, "velYInt", gridVelYInt);
            p2gCS.SetBuffer(p2gMainKernel, "velZInt", gridVelZInt);
            p2gCS.SetBuffer(p2gMainKernel, "weightXInt", gridWeightXInt);
            p2gCS.SetBuffer(p2gMainKernel, "weightYInt", gridWeightYInt);
            p2gCS.SetBuffer(p2gMainKernel, "weightZInt", gridWeightZInt);
            p2gCS.SetBuffer(p2gMainKernel, "particleBuf", particleBuffer);
            p2gCS.Dispatch(p2gMainKernel, DivUp(particleCount, ParticleThreadGroupSize), 1, 1);

            p2gCS.SetBuffer(normalizeKernel, "velXInt", gridVelXInt);
            p2gCS.SetBuffer(normalizeKernel, "velYInt", gridVelYInt);
            p2gCS.SetBuffer(normalizeKernel, "velZInt", gridVelZInt);
            p2gCS.SetBuffer(normalizeKernel, "weightXInt", gridWeightXInt);
            p2gCS.SetBuffer(normalizeKernel, "weightYInt", gridWeightYInt);
            p2gCS.SetBuffer(normalizeKernel, "weightZInt", gridWeightZInt);
            p2gCS.SetBuffer(normalizeKernel, "velX", gridVelX);
            p2gCS.SetBuffer(normalizeKernel, "velY", gridVelY);
            p2gCS.SetBuffer(normalizeKernel, "velZ", gridVelZ);
            p2gCS.SetBuffer(normalizeKernel, "weightX", gridWeightX);
            p2gCS.SetBuffer(normalizeKernel, "weightY", gridWeightY);
            p2gCS.SetBuffer(normalizeKernel, "weightZ", gridWeightZ);
            p2gCS.Dispatch(
                normalizeKernel,
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));
        }

        private void CopyGridVelocitiesToOld()
        {
            CopyFloatBuffer(gridVelX, gridVelXOld, gridVelXCopy);
            CopyFloatBuffer(gridVelY, gridVelYOld, gridVelYCopy);
            CopyFloatBuffer(gridVelZ, gridVelZOld, gridVelZCopy);
        }

        private void DispatchAddForces()
        {
            addForcesCS.SetInt("GRID_RES", FlipSolverConstants.GRID_RES);
            addForcesCS.SetFloat("gravity", FlipSolverConstants.GRAVITY);
            addForcesCS.SetFloat("dt", FlipSolverConstants.DT);
            addForcesCS.SetBuffer(addForcesKernel, "velY", gridVelY);
            addForcesCS.Dispatch(
                addForcesKernel,
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES + 1, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));
        }

        private void DispatchPressureSolve()
        {
            pressureSolveCS.SetInt("GRID_RES", FlipSolverConstants.GRID_RES);
            pressureSolveCS.SetFloat("CELL_SIZE", FlipSolverConstants.CELL_SIZE);
            pressureSolveCS.SetFloat("dt", FlipSolverConstants.DT);
            pressureSolveCS.SetFloat("density", FlipSolverConstants.DENSITY);

            pressureSolveCS.SetBuffer(computeDivergenceKernel, "velX", gridVelX);
            pressureSolveCS.SetBuffer(computeDivergenceKernel, "velY", gridVelY);
            pressureSolveCS.SetBuffer(computeDivergenceKernel, "velZ", gridVelZ);
            pressureSolveCS.SetBuffer(computeDivergenceKernel, "divergence", gridDivergence);
            pressureSolveCS.SetBuffer(computeDivergenceKernel, "cellType", gridCellType);
            pressureSolveCS.Dispatch(
                computeDivergenceKernel,
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));

            ComputeBuffer pressureIn = gridPressureOld;
            ComputeBuffer pressureOut = gridPressure;

            for (int iteration = 0; iteration < JacobiIterations; iteration++)
            {
                pressureSolveCS.SetBuffer(jacobiKernel, "divergence", gridDivergence);
                pressureSolveCS.SetBuffer(jacobiKernel, "cellType", gridCellType);
                pressureSolveCS.SetBuffer(jacobiKernel, "pressureIn", pressureIn);
                pressureSolveCS.SetBuffer(jacobiKernel, "pressureOut", pressureOut);
                pressureSolveCS.Dispatch(
                    jacobiKernel,
                    DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                    DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                    DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));

                ComputeBuffer swap = pressureIn;
                pressureIn = pressureOut;
                pressureOut = swap;
            }

            pressureSolveCS.SetBuffer(applyPressureKernel, "cellType", gridCellType);
            pressureSolveCS.SetBuffer(applyPressureKernel, "pressureIn", pressureIn);
            pressureSolveCS.SetBuffer(applyPressureKernel, "velXOut", gridVelX);
            pressureSolveCS.SetBuffer(applyPressureKernel, "velYOut", gridVelY);
            pressureSolveCS.SetBuffer(applyPressureKernel, "velZOut", gridVelZ);
            pressureSolveCS.Dispatch(
                applyPressureKernel,
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));
        }

        private void DispatchGridToParticle()
        {
            SetCommonGridParams(g2pCS);
            g2pCS.SetBuffer(g2pKernel, "velX", gridVelX);
            g2pCS.SetBuffer(g2pKernel, "velY", gridVelY);
            g2pCS.SetBuffer(g2pKernel, "velZ", gridVelZ);
            g2pCS.SetBuffer(g2pKernel, "velXOld", gridVelXOld);
            g2pCS.SetBuffer(g2pKernel, "velYOld", gridVelYOld);
            g2pCS.SetBuffer(g2pKernel, "velZOld", gridVelZOld);
            g2pCS.SetBuffer(g2pKernel, "particleBuf", particleBuffer);
            g2pCS.SetInt("particleCount", particleCount);
            g2pCS.Dispatch(g2pKernel, DivUp(particleCount, ParticleThreadGroupSize), 1, 1);
        }

        private void DispatchAdvectParticles()
        {
            advectCS.SetInt("GRID_RES", FlipSolverConstants.GRID_RES);
            advectCS.SetFloat("DOMAIN_MIN", FlipSolverConstants.DOMAIN_MIN);
            advectCS.SetFloat("DOMAIN_MAX", FlipSolverConstants.DOMAIN_MAX);
            advectCS.SetFloat("CELL_SIZE", FlipSolverConstants.CELL_SIZE);
            advectCS.SetFloat("dt", FlipSolverConstants.DT);
            advectCS.SetInt("particleCount", particleCount);
            float bhe = FlipSolverConstants.BOX_HALF_EXTENT;
            advectCS.SetFloats("boxHalfExtent", bhe, bhe, bhe);
            advectCS.SetBuffer(advectKernel, "particleBuf", particleBuffer);
            advectCS.Dispatch(advectKernel, DivUp(particleCount, ParticleThreadGroupSize), 1, 1);
        }

        private void DispatchEnforceBoundary()
        {
            pressureSolveCS.SetInt("GRID_RES", FlipSolverConstants.GRID_RES);
            pressureSolveCS.SetBuffer(enforceBoundaryKernel, "cellType", gridCellType);
            pressureSolveCS.SetBuffer(enforceBoundaryKernel, "velXOut", gridVelX);
            pressureSolveCS.SetBuffer(enforceBoundaryKernel, "velYOut", gridVelY);
            pressureSolveCS.SetBuffer(enforceBoundaryKernel, "velZOut", gridVelZ);
            pressureSolveCS.Dispatch(
                enforceBoundaryKernel,
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize),
                DivUp(FlipSolverConstants.GRID_RES, GridThreadGroupSize));
        }

        private static void SetCommonGridParams(ComputeShader shader)
        {
            shader.SetInt("GRID_RES", FlipSolverConstants.GRID_RES);
            shader.SetFloat("CELL_SIZE", FlipSolverConstants.CELL_SIZE);
            shader.SetFloat("DOMAIN_MIN", FlipSolverConstants.DOMAIN_MIN);
        }

        private static void CopyFloatBuffer(ComputeBuffer source, ComputeBuffer destination, float[] cache)
        {
            source.GetData(cache);
            destination.SetData(cache);
        }

        private static float NextJitter(System.Random random, float halfExtent)
        {
            return ((float)random.NextDouble() * 2.0f - 1.0f) * halfExtent;
        }

        private static int DivUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }

        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();
            buffer = null;
        }
    }
}
