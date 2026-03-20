using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Linq;
using com.zibra.common.Editor.Licensing;
using com.zibra.common.Editor.SDFObjects;
using com.zibra.common.SDFObjects;
using com.zibra.liquid.Manipulators;
using com.zibra.liquid.Solver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Liquid.Editor
{
    public static class Test1FbxSceneTools
    {
        private const string ScenePath = "Assets/Scenes/SquareContainerPrototype.unity";
        private const string Text1ScenePath = "Assets/Scenes/text1.unity";
        private const string ModelPath = "Assets/Scenes/Test1.fbx";
        private const string Text1ColliderMeshAssetPath = "Assets/Scenes/Test1InnerCollider.asset";
        private const string GlassMaterialPath = "Assets/Materials/PrototypeGlass.mat";
        private const string Text1GlassMaterialPath = "Assets/Materials/Test1CadGlass.mat";
        private const string ReplacementRootName = "Test1Container";
        private const string Text1ColliderRootName = "Test1InnerCollider";
        private const string LegacyFallbackName = "LegacyBoxCollisionFallback";
        private const int NeuralSdfGenerationTimeoutMs = 180000;
        private const int NeuralSdfPollIntervalMs = 100;

        public static void InspectTest1Fbx()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogError("Unable to load model: " + ModelPath);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Test1 FBX inspection");
            builder.AppendLine("Root: " + prefab.name);

            MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
            builder.AppendLine("MeshFilter count: " + meshFilters.Length);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                builder.AppendLine(
                    $"Mesh path: {GetPath(meshFilter.transform, prefab.transform)} | mesh={(mesh != null ? mesh.name : "<null>")} | vertices={(mesh != null ? mesh.vertexCount : 0)} | bounds={(mesh != null ? mesh.bounds.ToString() : "<null>")} | localScale={meshFilter.transform.localScale}");
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            builder.AppendLine("SkinnedMeshRenderer count: " + skinnedMeshRenderers.Length);
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
            {
                Mesh mesh = skinnedMeshRenderer.sharedMesh;
                builder.AppendLine(
                    $"Skinned path: {GetPath(skinnedMeshRenderer.transform, prefab.transform)} | mesh={(mesh != null ? mesh.name : "<null>")} | vertices={(mesh != null ? mesh.vertexCount : 0)} | localBounds={skinnedMeshRenderer.localBounds}");
            }

            Bounds? aggregateBounds = CalculatePrefabBounds(prefab);
            builder.AppendLine("Aggregate bounds: " + (aggregateBounds.HasValue ? aggregateBounds.Value.ToString() : "<none>"));

            Debug.Log(builder.ToString());
        }

        public static void ReplaceSquareContainerWithTest1()
        {
            ReplaceSceneContainerWithTest1(ScenePath);
        }

        public static void ReplaceText1ContainerWithTest1()
        {
            ReplaceSceneContainerWithTest1(Text1ScenePath);
        }

        public static void InspectText1ColliderAsset()
        {
            Mesh colliderMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Text1ColliderMeshAssetPath);
            if (colliderMesh == null)
            {
                Debug.LogError("Unable to load collider mesh: " + Text1ColliderMeshAssetPath);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Text1 collider asset inspection");
            builder.AppendLine("Mesh: " + colliderMesh.name);
            builder.AppendLine("Vertices: " + colliderMesh.vertexCount);
            builder.AppendLine("Bounds: " + colliderMesh.bounds);
            builder.AppendLine("Generation available: " + GenerationManager.Instance.IsGenerationAvailable());
            Debug.Log(builder.ToString());
        }

        public static string InspectLoadedText1CollisionState()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Active scene: " + SceneManager.GetActiveScene().path);

            GameObject pivot = GameObject.Find("ContainerPivot");
            GameObject liquidObject = GameObject.Find("ZibraLiquidVolume");
            GameObject replacement = GameObject.Find(ReplacementRootName);
            GameObject fallback = GameObject.Find(LegacyFallbackName);

            builder.AppendLine("ContainerPivot found: " + (pivot != null));
            builder.AppendLine("ZibraLiquidVolume found: " + (liquidObject != null));
            builder.AppendLine("Test1Container found: " + (replacement != null));
            builder.AppendLine("Legacy fallback found: " + (fallback != null));

            NeuralSDF[] neuralSdfs = UnityEngine.Object.FindObjectsByType<NeuralSDF>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            builder.AppendLine("NeuralSDF count: " + neuralSdfs.Length);
            foreach (NeuralSDF neuralSdf in neuralSdfs)
            {
                builder.AppendLine(
                    $"NeuralSDF path={GetPath(neuralSdf.transform, null)} enabled={neuralSdf.enabled} invert={neuralSdf.InvertSDF} hasRep={neuralSdf.HasRepresentation()}");
            }

            ZibraLiquid liquid = liquidObject != null ? liquidObject.GetComponent<ZibraLiquid>() : null;
            if (liquid != null)
            {
                var colliders = liquid.GetColliderList();
                builder.AppendLine("Liquid collider count: " + colliders.Count);
                foreach (ZibraLiquidCollider collider in colliders)
                {
                    string path = collider != null ? GetPath(collider.transform, null) : "<null>";
                    string sdfState = "<none>";
                    if (collider != null)
                    {
                        SDFObject sdf = collider.GetComponent<SDFObject>();
                        if (sdf != null)
                        {
                            sdfState = $"{sdf.GetType().Name} invert={sdf.InvertSDF} surfaceDistance={sdf.SurfaceDistance}";
                        }
                    }

                    builder.AppendLine($"Collider path={path} enabled={(collider != null && collider.enabled)} sdf={sdfState}");
                }
            }
            else
            {
                builder.AppendLine("Liquid collider count: <no liquid component>");
            }

            if (replacement != null)
            {
                Renderer[] renderers = replacement.GetComponentsInChildren<Renderer>(true);
                builder.AppendLine("Replacement renderer count: " + renderers.Length);
                foreach (Renderer renderer in renderers.Take(4))
                {
                    builder.AppendLine($"Renderer path={GetPath(renderer.transform, null)} enabled={renderer.enabled}");
                }
            }

            return builder.ToString();
        }

        private static void ReplaceSceneContainerWithTest1(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string glassMaterialPath = scenePath == Text1ScenePath ? Text1GlassMaterialPath : GlassMaterialPath;
            Material glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(glassMaterialPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            if (glassMaterial == null)
            {
                throw new MissingReferenceException("Unable to load glass material: " + glassMaterialPath);
            }

            if (prefab == null)
            {
                throw new MissingReferenceException("Unable to load model: " + ModelPath);
            }

            GameObject pivot = GameObject.Find("ContainerPivot");
            GameObject existingReplacement = GameObject.Find(ReplacementRootName);
            GameObject oldContainer = GameObject.Find("SquareGlassContainer") ??
                                      GameObject.Find(LegacyFallbackName) ??
                                      existingReplacement;
            GameObject liquidObject = GameObject.Find("ZibraLiquidVolume");

            if (pivot == null || oldContainer == null || liquidObject == null)
            {
                throw new MissingReferenceException("Scene is missing required objects.");
            }

            RemoveSceneObjectByName("Wall_Front_Sticker");
            RemoveSceneObjectByName(Text1ColliderRootName);
            if (existingReplacement != null && existingReplacement != oldContainer)
            {
                UnityEngine.Object.DestroyImmediate(existingReplacement);
            }

            GameObject staleFallback = GameObject.Find(LegacyFallbackName);
            if (staleFallback != null && staleFallback != oldContainer)
            {
                UnityEngine.Object.DestroyImmediate(staleFallback);
            }

            Bounds oldBounds = CalculateHierarchyBounds(oldContainer.transform);
            GameObject replacement = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (replacement == null)
            {
                throw new MissingReferenceException("Failed to instantiate model prefab.");
            }

            replacement.name = ReplacementRootName;
            replacement.transform.SetParent(pivot.transform, false);
            replacement.transform.localPosition = Vector3.zero;
            replacement.transform.localRotation = Quaternion.identity;
            replacement.transform.localScale = Vector3.one;

            ApplyMaterialToAllRenderers(replacement, glassMaterial);

            ScaleReplacementToBounds(replacement.transform, oldBounds);
            GameObject collisionRoot = replacement;
            if (scenePath == Text1ScenePath)
            {
                Mesh colliderMesh = CreateOrUpdateText1ColliderMeshAsset(prefab);
                GameObject colliderRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (colliderRoot == null)
                {
                    throw new MissingReferenceException("Failed to instantiate collider prefab.");
                }

                colliderRoot.name = Text1ColliderRootName;
                colliderRoot.transform.SetParent(pivot.transform, false);
                colliderRoot.transform.localPosition = replacement.transform.localPosition;
                colliderRoot.transform.localRotation = replacement.transform.localRotation;
                colliderRoot.transform.localScale = replacement.transform.localScale;
                DisableAllRenderers(colliderRoot);
                collisionRoot = colliderRoot;

                MeshFilter colliderMeshFilter = SelectBestMeshFilter(colliderRoot);
                if (colliderMeshFilter == null)
                {
                    throw new MissingReferenceException("No mesh filter found on Text1 collider root.");
                }

                colliderMeshFilter.sharedMesh = colliderMesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(colliderMeshFilter);
            }

            MeshFilter targetMeshFilter = SelectBestMeshFilter(collisionRoot);
            if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null)
            {
                throw new MissingReferenceException("No mesh filter found on replacement collider model.");
            }

            ZibraLiquidCollider collider = targetMeshFilter.gameObject.GetComponent<ZibraLiquidCollider>();
            if (collider == null)
            {
                collider = targetMeshFilter.gameObject.AddComponent<ZibraLiquidCollider>();
            }

            NeuralSDF neuralSdf = targetMeshFilter.gameObject.GetComponent<NeuralSDF>();
            if (neuralSdf == null)
            {
                neuralSdf = targetMeshFilter.gameObject.AddComponent<NeuralSDF>();
            }

            neuralSdf.InvertSDF = true;
            neuralSdf.SurfaceDistance = 0.0f;
            collider.Friction = 0.85f;
            bool hasGeneratedCollision = TryGenerateNeuralSdfBlocking(neuralSdf, out string generationError);

            ZibraLiquid liquid = liquidObject.GetComponent<ZibraLiquid>();
            if (hasGeneratedCollision)
            {
                var oldColliders = liquid.GetColliderList();
                for (int index = oldColliders.Count - 1; index >= 0; index -= 1)
                {
                    liquid.RemoveCollider(oldColliders[index]);
                }

                liquid.AddCollider(collider);
                if (oldContainer != replacement)
                {
                    UnityEngine.Object.DestroyImmediate(oldContainer);
                    oldContainer = null;
                }
            }
            else
            {
                if (oldContainer != replacement)
                {
                    DisableAllRenderers(oldContainer);
                    oldContainer.name = LegacyFallbackName;
                }
                Debug.LogWarning("Test1 container was placed, but NeuralSDF generation is not available yet. " +
                                 "Keeping legacy box collision as fallback. Reason: " + generationError);
            }

            EditorUtility.SetDirty(replacement);
            if (collisionRoot != replacement)
            {
                EditorUtility.SetDirty(collisionRoot);
            }
            EditorUtility.SetDirty(targetMeshFilter.gameObject);
            EditorUtility.SetDirty(liquid);
            if (oldContainer != null)
            {
                EditorUtility.SetDirty(oldContainer);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                (hasGeneratedCollision
                    ? "Replaced the legacy box with Test1 and enabled complex inner-wall collision."
                    : "Placed Test1 and prepared NeuralSDF components. Legacy box collision remains hidden as fallback.") +
                " Scene: " + scenePath +
                " NeuralSDF target: " + GetPath(targetMeshFilter.transform, replacement.transform));
        }

        private static bool TryGenerateNeuralSdfBlocking(NeuralSDF neuralSdf, out string errorMessage)
        {
            try
            {
                GenerateNeuralSdfBlocking(neuralSdf);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        private static void GenerateNeuralSdfBlocking(NeuralSDF neuralSdf)
        {
            if (neuralSdf.HasRepresentation())
            {
                return;
            }

            GenerationQueue.AddToQueue(neuralSdf);

            MethodInfo queueUpdate = typeof(GenerationQueue).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static);
            if (queueUpdate == null)
            {
                throw new MissingMethodException("Unable to find GenerationQueue.Update via reflection.");
            }

            int waitedMs = 0;
            while (GenerationQueue.GetQueueLength() > 0)
            {
                if (waitedMs >= NeuralSdfGenerationTimeoutMs)
                {
                    GenerationQueue.Abort();
                    throw new TimeoutException("Timed out while generating NeuralSDF for Test1.");
                }

                queueUpdate.Invoke(null, null);
                Thread.Sleep(NeuralSdfPollIntervalMs);
                waitedMs += NeuralSdfPollIntervalMs;
            }

            if (!neuralSdf.HasRepresentation())
            {
                throw new InvalidOperationException("NeuralSDF generation completed without a usable representation.");
            }

            EditorUtility.SetDirty(neuralSdf);
        }

        private static void DisableAllRenderers(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }
        }

        private static void RemoveSceneObjectByName(string objectName)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
        }

        private static Mesh CreateOrUpdateText1ColliderMeshAsset(GameObject prefab)
        {
            MeshFilter sourceMeshFilter = SelectBestMeshFilter(prefab);
            if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            {
                throw new MissingReferenceException("No mesh filter found on Test1 model for collider extraction.");
            }

            Mesh extractedMesh = ExtractLargestConnectedComponentMesh(sourceMeshFilter.sharedMesh);
            extractedMesh.name = "Test1InnerCollider";

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Text1ColliderMeshAssetPath);
            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(extractedMesh, Text1ColliderMeshAssetPath);
                return extractedMesh;
            }

            OverwriteMesh(existingMesh, extractedMesh);
            UnityEngine.Object.DestroyImmediate(extractedMesh);
            EditorUtility.SetDirty(existingMesh);
            AssetDatabase.SaveAssets();
            return existingMesh;
        }

        private static Mesh ExtractLargestConnectedComponentMesh(Mesh sourceMesh)
        {
            Vector3[] sourceVertices = sourceMesh.vertices;
            int[] sourceTriangles = sourceMesh.triangles;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector2[] sourceUvs = sourceMesh.uv;

            int triangleCount = sourceTriangles.Length / 3;
            var positionToTriangles = new Dictionary<string, List<int>>(sourceVertices.Length);
            for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex += 1)
            {
                for (int corner = 0; corner < 3; corner += 1)
                {
                    int vertexIndex = sourceTriangles[triangleIndex * 3 + corner];
                    string key = QuantizeVertex(sourceVertices[vertexIndex]);
                    if (!positionToTriangles.TryGetValue(key, out List<int> triangleList))
                    {
                        triangleList = new List<int>();
                        positionToTriangles[key] = triangleList;
                    }

                    triangleList.Add(triangleIndex);
                }
            }

            bool[] visitedTriangles = new bool[triangleCount];
            List<int> largestComponent = null;
            var queue = new Queue<int>();
            for (int startTriangle = 0; startTriangle < triangleCount; startTriangle += 1)
            {
                if (visitedTriangles[startTriangle])
                {
                    continue;
                }

                var component = new List<int>();
                queue.Enqueue(startTriangle);
                visitedTriangles[startTriangle] = true;
                while (queue.Count > 0)
                {
                    int triangleIndex = queue.Dequeue();
                    component.Add(triangleIndex);
                    for (int corner = 0; corner < 3; corner += 1)
                    {
                        int vertexIndex = sourceTriangles[triangleIndex * 3 + corner];
                        string key = QuantizeVertex(sourceVertices[vertexIndex]);
                        foreach (int adjacentTriangle in positionToTriangles[key])
                        {
                            if (visitedTriangles[adjacentTriangle])
                            {
                                continue;
                            }

                            visitedTriangles[adjacentTriangle] = true;
                            queue.Enqueue(adjacentTriangle);
                        }
                    }
                }

                if (largestComponent == null || component.Count > largestComponent.Count)
                {
                    largestComponent = component;
                }
            }

            if (largestComponent == null || largestComponent.Count == 0)
            {
                throw new InvalidOperationException("Unable to extract inner collider mesh from Test1.");
            }

            var usedVertices = new HashSet<int>();
            foreach (int triangleIndex in largestComponent)
            {
                usedVertices.Add(sourceTriangles[triangleIndex * 3 + 0]);
                usedVertices.Add(sourceTriangles[triangleIndex * 3 + 1]);
                usedVertices.Add(sourceTriangles[triangleIndex * 3 + 2]);
            }

            var remap = new Dictionary<int, int>(usedVertices.Count);
            var newVertices = new List<Vector3>(usedVertices.Count);
            var newNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length
                ? new List<Vector3>(usedVertices.Count)
                : null;
            var newUvs = sourceUvs != null && sourceUvs.Length == sourceVertices.Length
                ? new List<Vector2>(usedVertices.Count)
                : null;

            foreach (int oldIndex in usedVertices.OrderBy(index => index))
            {
                remap[oldIndex] = newVertices.Count;
                newVertices.Add(sourceVertices[oldIndex]);
                if (newNormals != null)
                {
                    newNormals.Add(sourceNormals[oldIndex]);
                }

                if (newUvs != null)
                {
                    newUvs.Add(sourceUvs[oldIndex]);
                }
            }

            var newTriangles = new List<int>(largestComponent.Count * 3);
            foreach (int triangleIndex in largestComponent)
            {
                newTriangles.Add(remap[sourceTriangles[triangleIndex * 3 + 0]]);
                newTriangles.Add(remap[sourceTriangles[triangleIndex * 3 + 1]]);
                newTriangles.Add(remap[sourceTriangles[triangleIndex * 3 + 2]]);
            }

            var extractedMesh = new Mesh();
            extractedMesh.SetVertices(newVertices);
            extractedMesh.SetTriangles(newTriangles, 0, true);
            if (newNormals != null)
            {
                extractedMesh.SetNormals(newNormals);
            }
            else
            {
                extractedMesh.RecalculateNormals();
            }

            if (newUvs != null)
            {
                extractedMesh.SetUVs(0, newUvs);
            }

            extractedMesh.RecalculateBounds();
            return extractedMesh;
        }

        private static string QuantizeVertex(Vector3 vertex)
        {
            int x = Mathf.RoundToInt(vertex.x * 100000.0f);
            int y = Mathf.RoundToInt(vertex.y * 100000.0f);
            int z = Mathf.RoundToInt(vertex.z * 100000.0f);
            return x + ":" + y + ":" + z;
        }

        private static void OverwriteMesh(Mesh destination, Mesh source)
        {
            destination.Clear();
            destination.SetVertices(source.vertices);
            destination.SetTriangles(source.triangles, 0, true);
            if (source.normals != null && source.normals.Length == source.vertexCount)
            {
                destination.SetNormals(source.normals);
            }
            else
            {
                destination.RecalculateNormals();
            }

            if (source.uv != null && source.uv.Length == source.vertexCount)
            {
                destination.SetUVs(0, new List<Vector2>(source.uv));
            }

            destination.RecalculateBounds();
        }

        private static void ApplyMaterialToAllRenderers(GameObject root, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderer.sharedMaterial = material;
                    continue;
                }

                for (int index = 0; index < materials.Length; index += 1)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static MeshFilter SelectBestMeshFilter(GameObject root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter best = null;
            int bestVertexCount = -1;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null)
                {
                    continue;
                }

                int vertexCount = meshFilter.sharedMesh.vertexCount;
                if (vertexCount > bestVertexCount)
                {
                    best = meshFilter;
                    bestVertexCount = vertexCount;
                }
            }

            return best;
        }

        private static void ScaleReplacementToBounds(Transform replacement, Bounds targetBounds)
        {
            Bounds replacementBounds = CalculateHierarchyBounds(replacement);
            Vector3 size = replacementBounds.size;
            if (size.x <= 1e-5f || size.y <= 1e-5f || size.z <= 1e-5f)
            {
                return;
            }

            Vector3 targetSize = targetBounds.size;
            float scaleX = targetSize.x / size.x;
            float scaleY = targetSize.y / size.y;
            float scaleZ = targetSize.z / size.z;
            float uniformScale = Mathf.Min(scaleX, Mathf.Min(scaleY, scaleZ));
            replacement.localScale = Vector3.one * uniformScale;

            replacementBounds = CalculateHierarchyBounds(replacement);
            Vector3 offset = targetBounds.center - replacementBounds.center;
            replacement.position += offset;
        }

        private static Bounds CalculateHierarchyBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index += 1)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static Bounds? CalculatePrefabBounds(GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index += 1)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static string GetPath(Transform current, Transform root)
        {
            var builder = new StringBuilder(current.name);
            while (current.parent != null && current.parent != root)
            {
                current = current.parent;
                builder.Insert(0, current.name + "/");
            }

            if (current.parent == root)
            {
                builder.Insert(0, root.name + "/");
            }

            return builder.ToString();
        }
    }
}
