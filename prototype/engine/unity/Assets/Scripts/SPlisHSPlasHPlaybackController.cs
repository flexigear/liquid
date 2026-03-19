using System;
using System.IO;
using UnityEngine;

namespace Liquid
{
    public class SPlisHSPlasHPlaybackController : MonoBehaviour
    {
        [Serializable]
        private class PlaybackMetadata
        {
            public string source;
            public int frameCount;
            public int particleCount;
            public float frameRate;
            public float particleRadius;
            public float[] containerInnerSize;
            public int[] frameIndices;
            public string fluidPositionsFile;
            public string containerAnglesFile;
        }

        [SerializeField] private string relativeCacheDirectory = "SPlisHSPlasH/LiquidCubeShake/cache";
        [SerializeField] private Transform containerPivot;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private float particleSizeMultiplier = 2.15f;
        [SerializeField] private bool loopPlayback = true;
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private bool dragToScrub = true;
        [SerializeField] private float framesPerScreenWidth = 1.1f;

        private ParticleSystem particleSystemComponent;
        private ParticleSystem.Particle[] particles;
        private float[] fluidPositions;
        private float[] containerAngles;
        private PlaybackMetadata metadata;
        private float playbackTime;
        private float frameCursor;
        private int currentFrame = -1;
        private bool isLoaded;
        private bool isPlaying;
        private bool dragActive;
        private Vector2 previousPointerPosition;

        public bool IsLoaded => isLoaded;
        public int CurrentFrame => currentFrame;
        public int ParticleCount => metadata != null ? metadata.particleCount : 0;

        public void Configure(string cacheDirectory, Transform pivot, Material material)
        {
            relativeCacheDirectory = cacheDirectory;
            containerPivot = pivot;
            particleMaterial = material;
        }

        private void Awake()
        {
            EnsureParticleSystem();
            LoadCache();
        }

        private void Start()
        {
            if (isLoaded)
            {
                ResetPlayback();
            }
        }

        private void Update()
        {
            if (!isLoaded)
            {
                return;
            }

            HandleScrubInput();

            if (dragActive || !isPlaying)
            {
                return;
            }

            playbackTime += Time.deltaTime;
            float clipDuration = metadata.frameCount / metadata.frameRate;
            if (loopPlayback && clipDuration > 0f)
            {
                playbackTime %= clipDuration;
            }
            else if (playbackTime >= clipDuration)
            {
                playbackTime = clipDuration;
                isPlaying = false;
            }

            int frame = Mathf.Clamp(Mathf.FloorToInt(playbackTime * metadata.frameRate), 0, metadata.frameCount - 1);
            if (frame != currentFrame)
            {
                ApplyFrame(frame);
            }
        }

        public void ResetPlayback()
        {
            if (!isLoaded)
            {
                return;
            }

            playbackTime = 0f;
            frameCursor = 0f;
            dragActive = false;
            previousPointerPosition = default;
            isPlaying = playOnStart;
            ApplyFrame(0);
        }

        private void LoadCache()
        {
            string cacheDirectory = Path.Combine(Application.streamingAssetsPath, relativeCacheDirectory);
            string metadataPath = Path.Combine(cacheDirectory, "cache.json");

            if (!File.Exists(metadataPath))
            {
                Debug.LogWarning("SPlisHSPlasH playback metadata not found: " + metadataPath);
                return;
            }

            metadata = JsonUtility.FromJson<PlaybackMetadata>(File.ReadAllText(metadataPath));
            if (metadata == null || metadata.frameCount <= 0 || metadata.particleCount <= 0)
            {
                Debug.LogWarning("Failed to parse SPlisHSPlasH playback metadata: " + metadataPath);
                return;
            }

            fluidPositions = ReadFloatBuffer(Path.Combine(cacheDirectory, metadata.fluidPositionsFile));
            containerAngles = ReadFloatBuffer(Path.Combine(cacheDirectory, metadata.containerAnglesFile));
            if (fluidPositions.Length != metadata.frameCount * metadata.particleCount * 3)
            {
                Debug.LogWarning("Unexpected fluid position buffer length.");
                return;
            }

            if (containerAngles.Length != metadata.frameCount)
            {
                Debug.LogWarning("Unexpected container angle buffer length.");
                return;
            }

            EnsureParticleSystem();
            particles = new ParticleSystem.Particle[metadata.particleCount];
            float particleSize = metadata.particleRadius * particleSizeMultiplier;
            Color particleColor = new Color(0.16f, 0.68f, 0.95f, 0.38f);
            for (int index = 0; index < particles.Length; index += 1)
            {
                particles[index].remainingLifetime = float.MaxValue;
                particles[index].startLifetime = float.MaxValue;
                particles[index].startSize = particleSize;
                particles[index].startColor = particleColor;
            }

            particleSystemComponent.Emit(metadata.particleCount);
            particleSystemComponent.SetParticles(particles, particles.Length);
            isLoaded = true;
        }

        private void HandleScrubInput()
        {
            if (!dragToScrub)
            {
                return;
            }

            bool pointerDown = false;
            Vector2 pointerPosition = default;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                pointerDown = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                pointerPosition = touch.position;
            }
            else if (Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                pointerDown = true;
                pointerPosition = Input.mousePosition;
            }

            if (!pointerDown)
            {
                dragActive = false;
                return;
            }

            if (!dragActive)
            {
                dragActive = true;
                isPlaying = false;
                previousPointerPosition = pointerPosition;
                return;
            }

            Vector2 delta = pointerPosition - previousPointerPosition;
            previousPointerPosition = pointerPosition;

            if (Mathf.Abs(delta.x) < 0.01f)
            {
                return;
            }

            float screenWidth = Mathf.Max(Screen.width, 1);
            float frameDelta = delta.x / screenWidth * metadata.frameCount * framesPerScreenWidth;
            frameCursor += frameDelta;

            if (loopPlayback)
            {
                frameCursor = Mathf.Repeat(frameCursor, metadata.frameCount);
            }
            else
            {
                frameCursor = Mathf.Clamp(frameCursor, 0f, metadata.frameCount - 1);
            }

            int frame = Mathf.Clamp(Mathf.RoundToInt(frameCursor), 0, metadata.frameCount - 1);
            if (frame != currentFrame)
            {
                ApplyFrame(frame);
            }
        }

        private void EnsureParticleSystem()
        {
            if (particleSystemComponent != null)
            {
                return;
            }

            GameObject particleObject = new GameObject("SPlisHSPlasHParticles");
            particleObject.transform.SetParent(transform, false);

            particleSystemComponent = particleObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();

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

        private void ApplyFrame(int frame)
        {
            currentFrame = frame;
            frameCursor = frame;
            int frameOffset = frame * metadata.particleCount * 3;
            for (int particleIndex = 0; particleIndex < metadata.particleCount; particleIndex += 1)
            {
                int sourceIndex = frameOffset + particleIndex * 3;
                particles[particleIndex].position = new Vector3(
                    fluidPositions[sourceIndex + 0],
                    fluidPositions[sourceIndex + 1],
                    fluidPositions[sourceIndex + 2]);
            }

            particleSystemComponent.SetParticles(particles, particles.Length);

            if (containerPivot != null)
            {
                containerPivot.localRotation = Quaternion.Euler(0f, 0f, containerAngles[frame] * Mathf.Rad2Deg);
            }
        }

        private static float[] ReadFloatBuffer(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            float[] values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
