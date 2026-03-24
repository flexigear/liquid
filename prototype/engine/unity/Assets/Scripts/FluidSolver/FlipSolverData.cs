using System.Runtime.InteropServices;
using UnityEngine;

namespace Liquid.FluidSolver
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FlipParticle
    {
        public const int Stride = sizeof(float) * 6;

        public Vector3 position;
        public Vector3 velocity;
    }

    public static class FlipSolverConstants
    {
        public const int GRID_RES = 64;
        public const float CELL_SIZE = 2.0f / GRID_RES;
        public const float DOMAIN_MIN = -1.0f;
        public const float DOMAIN_MAX = 1.0f;
        public const float GRAVITY = 9.81f;
        public const float DENSITY = 1000.0f;
        public const float DT = 0.016f;
    }

    public enum CellType
    {
        AIR = 0,
        FLUID = 1,
        SOLID = 2
    }
}
