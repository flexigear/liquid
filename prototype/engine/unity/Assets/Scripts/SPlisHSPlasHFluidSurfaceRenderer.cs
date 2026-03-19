using UnityEngine;
using UnityEngine.Rendering;

namespace Liquid
{
    [DefaultExecutionOrder(120)]
    public sealed class SPlisHSPlasHFluidSurfaceRenderer : MonoBehaviour
    {
        private const int FluidParticleLayer = 30;
        private const int FluidSurfaceLayer = 29;

        [SerializeField] private SPlisHSPlasHRealtimeController sourceController;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Material fluidMaskMaterial;
        [SerializeField] private Material fluidSurfaceMaterial;
        [SerializeField] [Range(0.4f, 1.0f)] private float renderScale = 0.75f;

        private Camera fluidCamera;
        private GameObject surfaceQuadObject;
        private Material surfaceMaterialInstance;
        private RenderTexture fluidSurfaceTexture;

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRuntimeObjects();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureRuntimeObjects();
            SyncFluidCamera();
            SyncSurfaceQuad();
            BindParticleMaskMaterial();
            BindSurfaceTexture();
        }

        private void OnDisable()
        {
            if (fluidCamera != null)
            {
                fluidCamera.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (surfaceMaterialInstance != null)
            {
                DestroyImmediate(surfaceMaterialInstance);
            }

            if (surfaceQuadObject != null)
            {
                DestroyImmediate(surfaceQuadObject);
            }

            if (fluidCamera != null)
            {
                DestroyImmediate(fluidCamera.gameObject);
            }

            if (fluidSurfaceTexture != null)
            {
                fluidSurfaceTexture.Release();
                DestroyImmediate(fluidSurfaceTexture);
            }
        }

        private void ResolveReferences()
        {
            if (sourceController == null)
            {
                sourceController = FindFirstObjectByType<SPlisHSPlasHRealtimeController>();
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (Camera candidate in cameras)
                {
                    if (candidate != null && candidate.name == "Main Camera")
                    {
                        mainCamera = candidate;
                        break;
                    }
                }
            }
        }

        private void EnsureRuntimeObjects()
        {
            if (mainCamera == null || sourceController == null || fluidMaskMaterial == null || fluidSurfaceMaterial == null)
            {
                return;
            }

            EnsureSurfaceTexture();
            EnsureFluidCamera();
            EnsureSurfaceQuad();
            BindParticleMaskMaterial();
            BindSurfaceTexture();

            mainCamera.cullingMask &= ~(1 << FluidParticleLayer);
            mainCamera.cullingMask |= 1 << FluidSurfaceLayer;
        }

        private void EnsureSurfaceTexture()
        {
            int width = Mathf.Max(256, Mathf.RoundToInt(Screen.width * renderScale));
            int height = Mathf.Max(256, Mathf.RoundToInt(Screen.height * renderScale));
            if (fluidSurfaceTexture != null &&
                fluidSurfaceTexture.width == width &&
                fluidSurfaceTexture.height == height)
            {
                return;
            }

            if (fluidSurfaceTexture != null)
            {
                fluidSurfaceTexture.Release();
                DestroyImmediate(fluidSurfaceTexture);
            }

            fluidSurfaceTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
            {
                name = "SPlisHSPlasHFluidSurface",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            fluidSurfaceTexture.Create();
        }

        private void EnsureFluidCamera()
        {
            if (fluidCamera == null)
            {
                GameObject fluidCameraObject = new GameObject("FluidSurfaceCamera");
                fluidCameraObject.hideFlags = HideFlags.HideAndDontSave;
                fluidCameraObject.transform.SetParent(mainCamera.transform, false);
                fluidCamera = fluidCameraObject.AddComponent<Camera>();
                AudioListener listener = fluidCameraObject.GetComponent<AudioListener>();
                if (listener != null)
                {
                    DestroyImmediate(listener);
                }
            }

            fluidCamera.CopyFrom(mainCamera);
            fluidCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            fluidCamera.depth = mainCamera.depth - 1f;
            fluidCamera.clearFlags = CameraClearFlags.SolidColor;
            fluidCamera.backgroundColor = Color.clear;
            fluidCamera.cullingMask = 1 << FluidParticleLayer;
            fluidCamera.targetTexture = fluidSurfaceTexture;
            fluidCamera.allowMSAA = false;
            fluidCamera.allowHDR = true;
            fluidCamera.enabled = true;
        }

        private void EnsureSurfaceQuad()
        {
            if (surfaceQuadObject == null)
            {
                surfaceQuadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                surfaceQuadObject.name = "FluidSurfaceComposite";
                surfaceQuadObject.hideFlags = HideFlags.HideAndDontSave;
                surfaceQuadObject.transform.SetParent(mainCamera.transform, false);
                DestroyImmediate(surfaceQuadObject.GetComponent<Collider>());

                MeshRenderer renderer = surfaceQuadObject.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                surfaceMaterialInstance = new Material(fluidSurfaceMaterial)
                {
                    name = "FluidSurfaceCompositeRuntime",
                };
                renderer.sharedMaterial = surfaceMaterialInstance;
            }

            SetLayerRecursively(surfaceQuadObject, FluidSurfaceLayer);
        }

        private void BindParticleMaskMaterial()
        {
            ParticleSystem particleSystem = sourceController.ParticleSystemComponent;
            if (particleSystem == null)
            {
                return;
            }

            SetLayerRecursively(particleSystem.gameObject, FluidParticleLayer);

            ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null && particleRenderer.sharedMaterial != fluidMaskMaterial)
            {
                particleRenderer.sharedMaterial = fluidMaskMaterial;
            }
        }

        private void BindSurfaceTexture()
        {
            if (surfaceMaterialInstance != null && fluidSurfaceTexture != null)
            {
                surfaceMaterialInstance.SetTexture("_FluidSurfaceTex", fluidSurfaceTexture);
            }
        }

        private void SyncFluidCamera()
        {
            if (fluidCamera == null || mainCamera == null)
            {
                return;
            }

            fluidCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            fluidCamera.fieldOfView = mainCamera.fieldOfView;
            fluidCamera.orthographic = mainCamera.orthographic;
            fluidCamera.orthographicSize = mainCamera.orthographicSize;
            fluidCamera.nearClipPlane = mainCamera.nearClipPlane;
            fluidCamera.farClipPlane = mainCamera.farClipPlane;
        }

        private void SyncSurfaceQuad()
        {
            if (surfaceQuadObject == null || mainCamera == null)
            {
                return;
            }

            Transform quadTransform = surfaceQuadObject.transform;
            float distance = Mathf.Max(mainCamera.nearClipPlane + 0.02f, 0.08f);
            float height = mainCamera.orthographic
                ? mainCamera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(mainCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float width = height * mainCamera.aspect;

            quadTransform.localPosition = new Vector3(0f, 0f, distance);
            quadTransform.localRotation = Quaternion.identity;
            quadTransform.localScale = new Vector3(width, height, 1f);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index += 1)
            {
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
            }
        }
    }
}
