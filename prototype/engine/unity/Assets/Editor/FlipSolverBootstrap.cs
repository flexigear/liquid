using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liquid.FluidSolver
{
    public static class FlipSolverBootstrap
    {
        [MenuItem("Liquid/Create FLIP Solver")]
        public static void CreateFlipSolver()
        {
            // 1. Create empty GameObject
            GameObject go = new GameObject("FlipSolver");

            // 2. Register undo before adding component
            Undo.RegisterCreatedObjectUndo(go, "Create FLIP Solver");

            // 3. Add FlipSolver component
            FlipSolver solver = go.AddComponent<FlipSolver>();

            // 4. Use SerializedObject + SerializedProperty to assign compute shaders
            SerializedObject serializedObject = new SerializedObject(solver);

            AssignShader(serializedObject, "classifyCellsCS", "ClassifyCells");
            AssignShader(serializedObject, "p2gCS",            "ParticleToGrid");
            AssignShader(serializedObject, "addForcesCS",      "AddForces");
            AssignShader(serializedObject, "pressureSolveCS",  "PressureSolve");
            AssignShader(serializedObject, "g2pCS",            "GridToParticle");
            AssignShader(serializedObject, "advectCS",         "AdvectParticles");

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            // 5. Select the new object
            Selection.activeGameObject = go;
        }

        [MenuItem("Liquid/Setup Fluid Renderer")]
        static void SetupFluidRenderer()
        {
            // Find the active URP asset
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Debug.LogError("No URP asset found. Make sure the project uses Universal Render Pipeline.");
                return;
            }

            // Get the renderer data via reflection (URP does not expose this publicly in older versions)
            var rendererDataList = typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(urpAsset) as ScriptableRendererData[];

            if (rendererDataList == null || rendererDataList.Length == 0)
            {
                Debug.LogError("Could not access URP renderer data list.");
                return;
            }

            var rendererData = rendererDataList[0];
            if (rendererData == null)
            {
                Debug.LogError("URP renderer data is null.");
                return;
            }

            // Check if FluidRendererFeature is already added
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is FluidRendererFeature)
                {
                    Debug.Log("FluidRendererFeature is already added to the URP renderer.");
                    return;
                }
            }

            // Add the feature
            var fluidFeature = ScriptableObject.CreateInstance<FluidRendererFeature>();
            fluidFeature.name = "FluidRendererFeature";

            // Save the feature as a sub-asset of the renderer data
            AssetDatabase.AddObjectToAsset(fluidFeature, rendererData);
            rendererData.rendererFeatures.Add(fluidFeature);

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            Debug.Log("FluidRendererFeature successfully added to URP renderer.");
        }

        private static void AssignShader(SerializedObject so, string fieldName, string shaderName)
        {
            string[] guids = AssetDatabase.FindAssets("t:ComputeShader " + shaderName);
            if (guids.Length == 0)
            {
                Debug.LogWarning("FlipSolverBootstrap: Could not find ComputeShader '" + shaderName + "'.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ComputeShader cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = cs;
            }
        }
    }
}
