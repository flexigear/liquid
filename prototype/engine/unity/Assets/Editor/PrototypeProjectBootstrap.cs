using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Liquid;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.Presets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liquid.Editor
{
    public static class PrototypeProjectBootstrap
    {
        private const string SceneDirectory = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/SquareContainerPrototype.unity";
        private const string UrpGlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";
        private const string RendererAssetPath = "Assets/Settings/LiquidPrototypeURP_Renderer.asset";
        private const string GlassMaterialPath = "Assets/Materials/PrototypeGlass.mat";
        private const string FrameMaterialPath = "Assets/Materials/PrototypeFrame.mat";
        private const string MarkerMaterialPath = "Assets/Materials/PrototypeLiquidMarker.mat";
        private const string ReflectionCardMaterialPath = "Assets/Materials/PrototypeReflectionCard.mat";
        private const string ZibraMaterialPresetPath =
            "Assets/Plugins/Zibra/Liquid/Presets/URP/DefaultSampleSceneURPMaterialParameters.preset";
        private const string ZibraSolverPresetPath =
            "Assets/Plugins/Zibra/Liquid/Presets/Solver/DefaultSampleSceneSolverParameters.preset";
        private static readonly Vector3 ContainerInnerSize = new Vector3(2f, 2f, 2f);
        private const float WallThickness = 0.025f;
        private const float GlassVisualInset = 0.02f;
        private const float ZibraEmitterDisableDelaySeconds = 60.0f;
        private const int ReflectionOnlyLayer = 2;

        [MenuItem("Liquid/Bootstrap/Setup Project")]
        public static void SetupProject()
        {
            EnsureFolders();
            EnsureUrpPackage();
            EnsureOptionalSupportPackages();
            EnsureUrpCompatibilityDefine();
            ConfigureUrp();
            CreateBaselineScene();
            AssetDatabase.Refresh();
        }

        public static void SetupProjectFromBatch()
        {
            SetupProject();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(SceneDirectory);
            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Settings");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string[] segments = assetPath.Split('/');
            string current = segments[0];

            for (int index = 1; index < segments.Length; index += 1)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static void EnsureUrpPackage()
        {
            EnsurePackage("com.unity.render-pipelines.universal", "URP");
        }

        private static void EnsureOptionalSupportPackages()
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Plugins/Zibra")))
            {
                return;
            }

            EnsurePackage("com.unity.timeline", "Timeline");
        }

        private static void EnsurePackage(string packageName, string friendlyName)
        {
            string manifestPath = Path.GetFullPath("Packages/manifest.json");
            string manifestContents = File.ReadAllText(manifestPath);

            if (manifestContents.Contains(packageName))
            {
                return;
            }

            var request = Client.Add(packageName);
            while (!request.IsCompleted)
            {
                Thread.Sleep(200);
            }

            if (request.Status == StatusCode.Failure)
            {
                throw new IOException("Failed to add " + friendlyName + " package: " + request.Error.message);
            }
        }

        private static void EnsureUrpCompatibilityDefine()
        {
            const string compatibilityDefine = "URP_COMPATIBILITY_MODE";

            EnsureDefinePresent(NamedBuildTarget.Standalone, compatibilityDefine);
            EnsureDefinePresent(NamedBuildTarget.Android, compatibilityDefine);
            EnsureDefinePresent(NamedBuildTarget.iOS, compatibilityDefine);
        }

        private static void EnsureDefinePresent(NamedBuildTarget buildTarget, string defineSymbol)
        {
            string defineSymbols = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            string[] symbols = defineSymbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string existingSymbol in symbols)
            {
                if (string.Equals(existingSymbol, defineSymbol, StringComparison.Ordinal))
                {
                    return;
                }
            }

            string updatedSymbols = string.IsNullOrWhiteSpace(defineSymbols)
                ? defineSymbol
                : defineSymbols + ";" + defineSymbol;

            PlayerSettings.SetScriptingDefineSymbols(buildTarget, updatedSymbols);
        }

        private static void CreateBaselineScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material glassMaterial = EnsureGlassMaterial();
            Material frameMaterial = EnsureFrameMaterial();
            Material markerMaterial = EnsureMarkerMaterial();
            Material reflectionCardMaterial = EnsureReflectionCardMaterial();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.36f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.16f);
            RenderSettings.reflectionIntensity = 1.15f;

            GameObject lightObject = new GameObject("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.35f;
            directionalLight.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            PrototypeOrbitCameraController orbitCameraController = cameraObject.AddComponent<PrototypeOrbitCameraController>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.11f);
            camera.nearClipPlane = 0.03f;
            camera.cullingMask &= ~(1 << ReflectionOnlyLayer);
            cameraObject.transform.position = new Vector3(0f, 0.2f, -5.4f);
            cameraObject.transform.LookAt(Vector3.zero);

            GameObject reflectionProbeObject = new GameObject("PrototypeReflectionProbe");
            ReflectionProbe reflectionProbe = reflectionProbeObject.AddComponent<ReflectionProbe>();
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            reflectionProbe.size = new Vector3(8f, 8f, 8f);
            reflectionProbe.boxProjection = true;
            reflectionProbe.intensity = 1.2f;
            BuildReflectionRig(reflectionProbeObject.transform, reflectionCardMaterial);

            GameObject sessionObject = new GameObject("PrototypeSession");
            sessionObject.AddComponent<PrototypeSessionController>();
            sessionObject.AddComponent<PrototypeLiquidDebugOverlay>();

            GameObject pivot = new GameObject("ContainerPivot");
            TrackballContainerController trackballController = pivot.AddComponent<TrackballContainerController>();
            orbitCameraController.SetTarget(pivot.transform);

            GameObject containerRoot = new GameObject("SquareGlassContainer");
            containerRoot.transform.SetParent(pivot.transform, false);
            BuildSquareGlassContainer(containerRoot.transform, glassMaterial);
            BuildCubeFrame(containerRoot.transform, frameMaterial);

            bool hasZibraLiquid = TryCreateZibraLiquidSetup(pivot.transform, containerRoot.transform, reflectionProbe);
            if (!hasZibraLiquid)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "LiquidAnchorMarker";
                marker.transform.SetParent(containerRoot.transform, false);
                marker.transform.localScale = Vector3.one * 0.34f;
                marker.transform.localPosition = new Vector3(0f, -0.62f, 0f);
                ApplyMaterial(marker, markerMaterial);
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(markerCollider);
                }
            }

            trackballController.CaptureCurrentAsInitialState();
            orbitCameraController.CaptureCurrentAsInitialState();
            PrototypeSessionController sessionController = sessionObject.GetComponent<PrototypeSessionController>();
            sessionController.CaptureCurrentAsInitialState();

            EditorSceneManager.SaveScene(scene, ScenePath, true);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }

        private static void ConfigureUrp()
        {
            const string pipelineAssetPath = "Assets/Settings/LiquidPrototypeURP.asset";

            UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(pipelineAsset, pipelineAssetPath);
            }

            EnsureDefaultRenderer(pipelineAsset);
            pipelineAsset.supportsCameraDepthTexture = true;
            EnsureZibraUrpRenderFeature();
            EnsureUrpCompatibilityMode();
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            AssignPipelineToAllQualityLevels(pipelineAsset);
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureUrpCompatibilityMode()
        {
            UnityEngine.Object globalSettingsAsset = AssetDatabase.LoadMainAssetAtPath(UrpGlobalSettingsPath);
            if (globalSettingsAsset != null)
            {
                SerializedObject globalSettingsObject = new SerializedObject(globalSettingsAsset);
                SerializedProperty renderGraphProperty = globalSettingsObject.FindProperty("m_EnableRenderGraph");
                if (renderGraphProperty != null && renderGraphProperty.boolValue)
                {
                    renderGraphProperty.boolValue = false;
                    globalSettingsObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(globalSettingsAsset);
                }
            }

            UnityEngine.Object[] nestedSettings = AssetDatabase.LoadAllAssetsAtPath(UrpGlobalSettingsPath);
            foreach (UnityEngine.Object nestedSetting in nestedSettings)
            {
                if (nestedSetting == null ||
                    nestedSetting.GetType().FullName != "UnityEngine.Rendering.Universal.RenderGraphSettings")
                {
                    continue;
                }

                SerializedObject renderGraphSettingsObject = new SerializedObject(nestedSetting);
                SerializedProperty compatibilityProperty =
                    renderGraphSettingsObject.FindProperty("m_EnableRenderCompatibilityMode");

                if (compatibilityProperty == null || compatibilityProperty.boolValue)
                {
                    continue;
                }

                compatibilityProperty.boolValue = true;
                renderGraphSettingsObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(nestedSetting);
            }
        }

        private static void EnsureDefaultRenderer(UniversalRenderPipelineAsset pipelineAsset)
        {
            if (HasPersistentRenderer(pipelineAsset))
            {
                return;
            }

            MethodInfo resetMethod = typeof(UniversalRenderPipelineAsset).GetMethod(
                "Reset",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (resetMethod == null)
            {
                throw new MissingMethodException(
                    "Unable to find UniversalRenderPipelineAsset.Reset() via reflection.");
            }

            resetMethod.Invoke(pipelineAsset, null);
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();
        }

        private static bool HasPersistentRenderer(UniversalRenderPipelineAsset pipelineAsset)
        {
            SerializedObject serializedObject = new SerializedObject(pipelineAsset);
            SerializedProperty rendererList = serializedObject.FindProperty("m_RendererDataList");
            if (rendererList == null || rendererList.arraySize == 0)
            {
                return false;
            }

            UnityEngine.Object rendererReference = rendererList.GetArrayElementAtIndex(0).objectReferenceValue;
            if (rendererReference == null)
            {
                return false;
            }

            return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(rendererReference));
        }

        private static void AssignPipelineToAllQualityLevels(UniversalRenderPipelineAsset pipelineAsset)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(assets[0]);
            SerializedProperty qualityLevels = serializedObject.FindProperty("m_QualitySettings");
            if (qualityLevels == null)
            {
                return;
            }

            for (int index = 0; index < qualityLevels.arraySize; index += 1)
            {
                SerializedProperty qualityLevel = qualityLevels.GetArrayElementAtIndex(index);
                SerializedProperty pipelineProperty = qualityLevel.FindPropertyRelative("customRenderPipeline");
                if (pipelineProperty == null)
                {
                    continue;
                }

                pipelineProperty.objectReferenceValue = pipelineAsset;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline = pipelineAsset;
        }

        private static Material EnsureGlassMaterial()
        {
            return EnsureMaterialAsset(
                GlassMaterialPath,
                "Universal Render Pipeline/Lit",
                material =>
                {
                    material.SetColor("_BaseColor", new Color(0.95f, 0.985f, 1f, 0.04f));
                    material.SetFloat("_Smoothness", 0.98f);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_EnvironmentReflections", 1f);
                    material.SetFloat("_BlendModePreserveSpecular", 1f);
                    material.SetFloat("_SpecularHighlights", 1f);
                    material.SetFloat("_ReceiveShadows", 0f);
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_Cull", 2f);
                    material.SetFloat("_AlphaClip", 0f);
                    material.SetFloat("_ZWrite", 0f);
                    material.SetFloat("_SrcBlend", 5f);
                    material.SetFloat("_DstBlend", 10f);
                    material.SetFloat("_SrcBlendAlpha", 1f);
                    material.SetFloat("_DstBlendAlpha", 10f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)RenderQueue.Transparent;
                });
        }

        private static Material EnsureFrameMaterial()
        {
            return EnsureMaterialAsset(
                FrameMaterialPath,
                "Universal Render Pipeline/Lit",
                material =>
                {
                    material.SetColor("_BaseColor", new Color(0.82f, 0.9f, 0.98f, 0.85f));
                    material.SetFloat("_Smoothness", 0.85f);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_ReceiveShadows", 0f);
                });
        }

        private static Material EnsureMarkerMaterial()
        {
            return EnsureMaterialAsset(
                MarkerMaterialPath,
                "Universal Render Pipeline/Unlit",
                material =>
                {
                    material.SetColor("_BaseColor", new Color(0.12f, 0.72f, 0.98f, 1f));
                    material.renderQueue = (int)RenderQueue.Geometry;
                });
        }

        private static Material EnsureReflectionCardMaterial()
        {
            return EnsureMaterialAsset(
                ReflectionCardMaterialPath,
                "Universal Render Pipeline/Unlit",
                material =>
                {
                    material.SetColor("_BaseColor", new Color(0.82f, 0.86f, 0.92f, 1f));
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_Cull", 0f);
                    material.renderQueue = (int)RenderQueue.Geometry;
                });
        }

        private static Material EnsureMaterialAsset(string assetPath, string shaderName, Action<Material> configureMaterial)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new MissingReferenceException("Unable to find shader: " + shaderName);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                };

                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            configureMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void BuildSquareGlassContainer(Transform parent, Material glassMaterial)
        {
            float halfX = ContainerInnerSize.x * 0.5f;
            float halfY = ContainerInnerSize.y * 0.5f;
            float halfZ = ContainerInnerSize.z * 0.5f;
            float halfThickness = WallThickness * 0.5f;

            CreateWall(
                parent,
                "Wall_Front",
                new Vector3(0f, 0f, halfZ + halfThickness),
                new Vector3(ContainerInnerSize.x + WallThickness * 2f, ContainerInnerSize.y + WallThickness * 2f, WallThickness),
                glassMaterial);
            CreateWall(
                parent,
                "Wall_Back",
                new Vector3(0f, 0f, -(halfZ + halfThickness)),
                new Vector3(ContainerInnerSize.x + WallThickness * 2f, ContainerInnerSize.y + WallThickness * 2f, WallThickness),
                glassMaterial);
            CreateWall(
                parent,
                "Wall_Left",
                new Vector3(-(halfX + halfThickness), 0f, 0f),
                new Vector3(WallThickness, ContainerInnerSize.y + WallThickness * 2f, ContainerInnerSize.z),
                glassMaterial);
            CreateWall(
                parent,
                "Wall_Right",
                new Vector3(halfX + halfThickness, 0f, 0f),
                new Vector3(WallThickness, ContainerInnerSize.y + WallThickness * 2f, ContainerInnerSize.z),
                glassMaterial);
            CreateWall(
                parent,
                "Wall_Top",
                new Vector3(0f, halfY + halfThickness, 0f),
                new Vector3(ContainerInnerSize.x, WallThickness, ContainerInnerSize.z),
                glassMaterial);
            CreateWall(
                parent,
                "Wall_Bottom",
                new Vector3(0f, -(halfY + halfThickness), 0f),
                new Vector3(ContainerInnerSize.x, WallThickness, ContainerInnerSize.z),
                glassMaterial);
        }

        private static void BuildCubeFrame(Transform parent, Material frameMaterial)
        {
            float halfX = ContainerInnerSize.x * 0.5f + WallThickness * 0.5f;
            float halfY = ContainerInnerSize.y * 0.5f + WallThickness * 0.5f;
            float halfZ = ContainerInnerSize.z * 0.5f + WallThickness * 0.5f;
            float edgeThickness = 0.028f;

            CreateFrameBar(parent, "Frame_TopFront", new Vector3(0f, halfY, halfZ), new Vector3(ContainerInnerSize.x + WallThickness * 2f, edgeThickness, edgeThickness), frameMaterial);
            CreateFrameBar(parent, "Frame_TopBack", new Vector3(0f, halfY, -halfZ), new Vector3(ContainerInnerSize.x + WallThickness * 2f, edgeThickness, edgeThickness), frameMaterial);
            CreateFrameBar(parent, "Frame_BottomFront", new Vector3(0f, -halfY, halfZ), new Vector3(ContainerInnerSize.x + WallThickness * 2f, edgeThickness, edgeThickness), frameMaterial);
            CreateFrameBar(parent, "Frame_BottomBack", new Vector3(0f, -halfY, -halfZ), new Vector3(ContainerInnerSize.x + WallThickness * 2f, edgeThickness, edgeThickness), frameMaterial);

            CreateFrameBar(parent, "Frame_TopLeft", new Vector3(-halfX, halfY, 0f), new Vector3(edgeThickness, edgeThickness, ContainerInnerSize.z + WallThickness * 2f), frameMaterial);
            CreateFrameBar(parent, "Frame_TopRight", new Vector3(halfX, halfY, 0f), new Vector3(edgeThickness, edgeThickness, ContainerInnerSize.z + WallThickness * 2f), frameMaterial);
            CreateFrameBar(parent, "Frame_BottomLeft", new Vector3(-halfX, -halfY, 0f), new Vector3(edgeThickness, edgeThickness, ContainerInnerSize.z + WallThickness * 2f), frameMaterial);
            CreateFrameBar(parent, "Frame_BottomRight", new Vector3(halfX, -halfY, 0f), new Vector3(edgeThickness, edgeThickness, ContainerInnerSize.z + WallThickness * 2f), frameMaterial);

            CreateFrameBar(parent, "Frame_FrontLeft", new Vector3(-halfX, 0f, halfZ), new Vector3(edgeThickness, ContainerInnerSize.y + WallThickness * 2f, edgeThickness), frameMaterial);
            CreateFrameBar(parent, "Frame_FrontRight", new Vector3(halfX, 0f, halfZ), new Vector3(edgeThickness, ContainerInnerSize.y + WallThickness * 2f, edgeThickness), frameMaterial);
            CreateFrameBar(parent, "Frame_BackLeft", new Vector3(-halfX, 0f, -halfZ), new Vector3(edgeThickness, ContainerInnerSize.y + WallThickness * 2f, edgeThickness), frameMaterial);
            CreateFrameBar(parent, "Frame_BackRight", new Vector3(halfX, 0f, -halfZ), new Vector3(edgeThickness, ContainerInnerSize.y + WallThickness * 2f, edgeThickness), frameMaterial);
        }

        private static void BuildReflectionRig(Transform parent, Material reflectionCardMaterial)
        {
            GameObject rig = new GameObject("ReflectionRig");
            rig.transform.SetParent(parent, false);
            SetLayerRecursively(rig, ReflectionOnlyLayer);

            CreateReflectionCard(
                rig.transform,
                "ReflectionCard_Top",
                new Vector3(0f, 3.6f, 0.35f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(8.5f, 5.5f, 1f),
                reflectionCardMaterial);
            CreateReflectionCard(
                rig.transform,
                "ReflectionCard_Left",
                new Vector3(-4.1f, 0.15f, -0.3f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(7.5f, 4.2f, 1f),
                reflectionCardMaterial);
            CreateReflectionCard(
                rig.transform,
                "ReflectionCard_Right",
                new Vector3(4.1f, 0.15f, -0.3f),
                Quaternion.Euler(0f, -90f, 0f),
                new Vector3(7.5f, 4.2f, 1f),
                reflectionCardMaterial);
            CreateReflectionCard(
                rig.transform,
                "ReflectionCard_Front",
                new Vector3(0f, -0.1f, -4.2f),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector3(6.8f, 4.4f, 1f),
                reflectionCardMaterial);
            CreateReflectionCard(
                rig.transform,
                "ReflectionCard_Back",
                new Vector3(0f, -0.1f, 4.2f),
                Quaternion.identity,
                new Vector3(6.8f, 4.4f, 1f),
                reflectionCardMaterial);
        }

        private static void CreateFrameBar(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;
            ApplyMaterial(bar, material);

            Collider collider = bar.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void CreateReflectionCard(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = name;
            card.transform.SetParent(parent, false);
            card.transform.localPosition = localPosition;
            card.transform.localRotation = localRotation;
            card.transform.localScale = localScale;
            SetLayerRecursively(card, ReflectionOnlyLayer);
            ApplyMaterial(card, material);

            Collider collider = card.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = card.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void CreateWall(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject wall = new GameObject(name);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPosition;
            wall.transform.localScale = Vector3.one;

            GameObject wallColliderAnchor = new GameObject(name + "_Collider");
            wallColliderAnchor.transform.SetParent(wall.transform, false);
            wallColliderAnchor.transform.localPosition = Vector3.zero;
            wallColliderAnchor.transform.localRotation = Quaternion.identity;
            wallColliderAnchor.transform.localScale = localScale;

            CreateGlassWallVisual(
                wall.transform,
                name + "_Visual",
                Vector3.zero,
                GetGlassWallVisualScale(localScale),
                material);
        }

        private static void CreateGlassWallVisual(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject wallVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallVisual.name = name;
            wallVisual.transform.SetParent(parent, false);
            wallVisual.transform.localPosition = localPosition;
            wallVisual.transform.localRotation = Quaternion.identity;
            wallVisual.transform.localScale = localScale;
            ApplyMaterial(wallVisual, material);

            Collider visualCollider = wallVisual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(visualCollider);
            }

            Renderer renderer = wallVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Vector3 GetGlassWallVisualScale(Vector3 wallScale)
        {
            return new Vector3(
                ShrinkGlassAxis(wallScale.x),
                ShrinkGlassAxis(wallScale.y),
                ShrinkGlassAxis(wallScale.z));
        }

        private static float ShrinkGlassAxis(float axisScale)
        {
            if (axisScale <= WallThickness * 1.5f)
            {
                return axisScale;
            }

            return Mathf.Max(axisScale - GlassVisualInset, WallThickness);
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = material;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;

            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void EnsureZibraUrpRenderFeature()
        {
            Type renderFeatureType = ResolveZibraType("com.zibra.liquid.LiquidURPRenderComponent", "ZibraAI.ZibraEffects.Liquid");
            if (renderFeatureType == null)
            {
                return;
            }

            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererAssetPath);
            if (rendererData == null)
            {
                return;
            }

            foreach (ScriptableRendererFeature rendererFeature in rendererData.rendererFeatures)
            {
                if (rendererFeature != null && renderFeatureType.IsInstanceOfType(rendererFeature))
                {
                    return;
                }
            }

            ScriptableRendererFeature feature = ScriptableObject.CreateInstance(renderFeatureType) as ScriptableRendererFeature;
            if (feature == null)
            {
                throw new InvalidOperationException("Failed to create Zibra URP render feature.");
            }

            feature.name = "Zibra Liquid URP Render Component";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            SerializedObject serializedObject = new SerializedObject(rendererData);
            SerializedProperty rendererFeatures = serializedObject.FindProperty("m_RendererFeatures");
            SerializedProperty rendererFeatureMap = serializedObject.FindProperty("m_RendererFeatureMap");

            rendererFeatures.arraySize += 1;
            rendererFeatures.GetArrayElementAtIndex(rendererFeatures.arraySize - 1).objectReferenceValue = feature;

            rendererFeatureMap.arraySize += 1;
            rendererFeatureMap.GetArrayElementAtIndex(rendererFeatureMap.arraySize - 1).longValue = localId;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }

        private static bool TryCreateZibraLiquidSetup(Transform parent, Transform containerRoot, ReflectionProbe reflectionProbe)
        {
            Type liquidType = ResolveZibraType("com.zibra.liquid.Solver.ZibraLiquid", "ZibraAI.ZibraEffects.Liquid");
            Type emitterType = ResolveZibraType("com.zibra.liquid.Manipulators.ZibraLiquidEmitter", "ZibraAI.ZibraEffects.Liquid");
            Type colliderType = ResolveZibraType("com.zibra.liquid.Manipulators.ZibraLiquidCollider", "ZibraAI.ZibraEffects.Liquid");
            Type analyticSdfType = ResolveZibraType("com.zibra.common.SDFObjects.AnalyticSDF", "ZibraAI.ZibraEffects");
            Type materialParametersType = ResolveZibraType("com.zibra.liquid.DataStructures.ZibraLiquidMaterialParameters", "ZibraAI.ZibraEffects.Liquid");
            Type advancedRenderParametersType = ResolveZibraType("com.zibra.liquid.DataStructures.ZibraLiquidAdvancedRenderParameters", "ZibraAI.ZibraEffects.Liquid");
            Type solverParametersType = ResolveZibraType("com.zibra.liquid.DataStructures.ZibraLiquidSolverParameters", "ZibraAI.ZibraEffects.Liquid");

            if (liquidType == null ||
                emitterType == null ||
                colliderType == null ||
                analyticSdfType == null ||
                materialParametersType == null ||
                advancedRenderParametersType == null ||
                solverParametersType == null)
            {
                return false;
            }

            GameObject liquidObject = new GameObject("ZibraLiquidVolume");
            liquidObject.transform.SetParent(parent.parent, false);
            liquidObject.transform.localPosition = parent.localPosition;
            liquidObject.transform.localRotation = Quaternion.identity;
            liquidObject.transform.localScale = Vector3.one;

            Component liquid = liquidObject.AddComponent(liquidType);
            float rotatingCubeBounds = ContainerInnerSize.magnitude * 1.05f;
            SetFieldValue(liquid, "ContainerSize", Vector3.one * rotatingCubeBounds);
            SetFieldValue(liquid, "EnableContainerMovementFeedback", false);
            SetFieldValue(liquid, "ReflectionProbeBRP", reflectionProbe);

            Component materialParameters = liquidObject.GetComponent(materialParametersType);
            Component advancedRenderParameters = liquidObject.GetComponent(advancedRenderParametersType);
            Component solverParameters = liquidObject.GetComponent(solverParametersType);
            ApplyPresetIfPresent(materialParameters, ZibraMaterialPresetPath);
            ApplyPresetIfPresent(solverParameters, ZibraSolverPresetPath);
            ApplyRealisticWaterMaterial(materialParameters);
            ApplyRealisticWaterRendering(advancedRenderParameters);

            GameObject emitterObject = new GameObject("ZibraLiquidEmitter");
            emitterObject.transform.SetParent(liquidObject.transform, false);
            emitterObject.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            emitterObject.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);

            Component analyticSdf = emitterObject.AddComponent(analyticSdfType);
            SetEnumFieldValue(analyticSdf, "ChosenSDFType", "Cylinder");

            Component emitter = emitterObject.AddComponent(emitterType);
            SetFieldValue(emitter, "VolumePerSimTime", 0.002f);
            SetFieldValue(emitter, "InitialVelocity", Vector3.down * 1.5f);

            InvokeMethod(liquid, "AddManipulator", emitter);
            AttachContainerWallColliders(liquid, containerRoot, analyticSdfType, colliderType);

            PrototypeTimedComponentDisabler timedDisabler = emitterObject.AddComponent<PrototypeTimedComponentDisabler>();
            timedDisabler.Configure((Behaviour)emitter, ZibraEmitterDisableDelaySeconds);
            return true;
        }

        private static void AttachContainerWallColliders(
            Component liquid,
            Transform containerRoot,
            Type analyticSdfType,
            Type colliderType)
        {
            if (liquid == null || containerRoot == null)
            {
                return;
            }

            foreach (Transform wall in containerRoot)
            {
                if (!wall.name.StartsWith("Wall_", StringComparison.Ordinal))
                {
                    continue;
                }

                Transform wallColliderAnchor = wall.Find(wall.name + "_Collider");
                if (wallColliderAnchor == null)
                {
                    continue;
                }

                Component analyticSdf = wallColliderAnchor.GetComponent(analyticSdfType);
                if (analyticSdf == null)
                {
                    analyticSdf = wallColliderAnchor.gameObject.AddComponent(analyticSdfType);
                }

                SetEnumFieldValue(analyticSdf, "ChosenSDFType", "Box");

                Component liquidCollider = wallColliderAnchor.GetComponent(colliderType);
                if (liquidCollider == null)
                {
                    liquidCollider = wallColliderAnchor.gameObject.AddComponent(colliderType);
                }

                SetFieldValue(liquidCollider, "Friction", 0.05f);
                InvokeMethod(liquid, "AddCollider", liquidCollider);
            }
        }

        private static void ApplyRealisticWaterMaterial(Component materialParameters)
        {
            if (materialParameters == null)
            {
                return;
            }

            SetFieldValue(materialParameters, "UseCubemapRefraction", false);
            SetFieldValue(materialParameters, "Color", new Color(0.93f, 0.98f, 1.0f, 1f));
            SetFieldValue(materialParameters, "EmissiveColor", Color.black);
            SetFieldValue(materialParameters, "ReflectionColor", new Color(0.95f, 0.97f, 1.0f, 1f));
            SetFieldValue(materialParameters, "ScatteringAmount", 0.15f);
            SetFieldValue(materialParameters, "AbsorptionAmount", 12f);
            SetFieldValue(materialParameters, "Roughness", 0.02f);
            SetFieldValue(materialParameters, "Metalness", 0.08f);
            SetFieldValue(materialParameters, "FresnelStrength", 1.05f);
            SetFieldValue(materialParameters, "IndexOfRefraction", 1.333f);
            SetFieldValue(materialParameters, "FluidSurfaceBlur", 0.85f);
        }

        private static void ApplyRealisticWaterRendering(Component advancedRenderParameters)
        {
            if (advancedRenderParameters == null)
            {
                return;
            }

            SetFieldValue(advancedRenderParameters, "DisableRaymarch", false);
            SetFieldValue(advancedRenderParameters, "UnderwaterRender", false);
            SetFieldValue(advancedRenderParameters, "RayMarchingResolutionDownscale", 1.0f);
            SetEnumFieldValue(advancedRenderParameters, "RefractionBounces", "TwoBounces");
            SetFieldValue(advancedRenderParameters, "RayMarchIsoSurface", 0.65f);
            SetFieldValue(advancedRenderParameters, "RayMarchMaxSteps", 128);
            SetFieldValue(advancedRenderParameters, "RayMarchStepSize", 0.15f);
            SetFieldValue(advancedRenderParameters, "RayMarchStepFactor", 3.5f);
        }

        private static void ApplyPresetIfPresent(Component targetComponent, string presetAssetPath)
        {
            if (targetComponent == null)
            {
                return;
            }

            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(presetAssetPath);
            if (preset == null)
            {
                return;
            }

            preset.ApplyTo(targetComponent);
            EditorUtility.SetDirty(targetComponent);
        }

        private static Type ResolveZibraType(string typeName, string assemblyName)
        {
            return Type.GetType(typeName + ", " + assemblyName);
        }

        private static void SetFieldValue(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            field.SetValue(target, value);
        }

        private static void SetEnumFieldValue(object target, string fieldName, string enumValueName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            object enumValue = Enum.Parse(field.FieldType, enumValueName);
            field.SetValue(target, enumValue);
        }

        private static void InvokeMethod(object target, string methodName, object argument)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return;
            }

            method.Invoke(target, new[] { argument });
        }
    }
}
