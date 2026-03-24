using UnityEngine;

namespace Liquid.FluidSolver
{
    public static class SDFBaker
    {
        public static Texture3D BakeMeshSDF(Mesh mesh, int resolution, bool invertSDF, ComputeShader bakeSdfCS, out float[] rawSdfData)
        {
            mesh.RecalculateBounds();
            Vector3 meshCenter = mesh.bounds.center;
            Vector3 meshHalfSize = mesh.bounds.extents;
            float maxHalfExtent = Mathf.Max(meshHalfSize.x, Mathf.Max(meshHalfSize.y, meshHalfSize.z));

            float domainHalfExtent = FlipSolverConstants.BOX_HALF_EXTENT;
            float fitScale = domainHalfExtent / maxHalfExtent;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            int triCount = triangles.Length / 3;

            Vector3[] triVerts = new Vector3[triCount * 3];
            for (int t = 0; t < triCount; t++)
            {
                triVerts[t * 3 + 0] = vertices[triangles[t * 3 + 0]];
                triVerts[t * 3 + 1] = vertices[triangles[t * 3 + 1]];
                triVerts[t * 3 + 2] = vertices[triangles[t * 3 + 2]];
            }

            ComputeBuffer triVertsBuf = new ComputeBuffer(triVerts.Length, sizeof(float) * 3);
            triVertsBuf.SetData(triVerts);

            int totalVoxels = resolution * resolution * resolution;
            ComputeBuffer outputBuf = new ComputeBuffer(totalVoxels, sizeof(float));

            int kernel = bakeSdfCS.FindKernel("BakeSDF");
            bakeSdfCS.SetInt("_Resolution", resolution);
            bakeSdfCS.SetInt("_TriCount", triCount);
            bakeSdfCS.SetFloat("_CellSize", 2.0f / resolution);
            bakeSdfCS.SetFloat("_FitScale", fitScale);
            bakeSdfCS.SetFloats("_MeshCenter", meshCenter.x, meshCenter.y, meshCenter.z);
            bakeSdfCS.SetInt("_InvertSDF", 0);
            bakeSdfCS.SetBuffer(kernel, "_TriVerts", triVertsBuf);
            bakeSdfCS.SetBuffer(kernel, "_SDFOutput", outputBuf);

            int groups = (resolution + 7) / 8;
            bakeSdfCS.Dispatch(kernel, groups, groups, groups);

            float[] sdfData = new float[totalVoxels];
            outputBuf.GetData(sdfData);

            triVertsBuf.Release();
            outputBuf.Release();

            int negativeCount = 0;
            int zeroCount = 0;
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            for (int i = 0; i < totalVoxels; i++)
            {
                if (sdfData[i] < 0.0f) negativeCount++;
                if (sdfData[i] == 0.0f) zeroCount++;
                if (sdfData[i] < minVal) minVal = sdfData[i];
                if (sdfData[i] > maxVal) maxVal = sdfData[i];
            }

            Debug.Log($"[SDFBaker] invertSDF={invertSDF}, fitScale={fitScale:F4}, meshCenter=({meshCenter.x:F3},{meshCenter.y:F3},{meshCenter.z:F3})");
            Debug.Log($"[SDFBaker] triCount={triCount}, voxels={totalVoxels}, negative={negativeCount}, zero={zeroCount}, positive={totalVoxels - negativeCount - zeroCount}");
            Debug.Log($"[SDFBaker] SDF range: [{minVal:F4}, {maxVal:F4}]");

            if (negativeCount > totalVoxels / 2)
            {
                Debug.Log("[SDFBaker] Auto-inverting SDF (negative > 50%)");
                for (int i = 0; i < totalVoxels; i++)
                {
                    sdfData[i] = -sdfData[i];
                }
            }

            rawSdfData = sdfData;

            Texture3D texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixelData(sdfData, 0);
            texture.Apply(false);
            return texture;
        }
    }
}
