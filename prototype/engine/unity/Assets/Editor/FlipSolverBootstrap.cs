using UnityEditor;
using UnityEngine;

namespace Liquid.FluidSolver.Editor
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
