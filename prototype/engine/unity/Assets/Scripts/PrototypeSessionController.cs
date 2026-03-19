using UnityEngine;
using UnityEngine.SceneManagement;

namespace Liquid
{
    public class PrototypeSessionController : MonoBehaviour
    {
        [SerializeField] private TrackballContainerController containerController;
        [SerializeField] private PrototypeOrbitCameraController cameraOrbitController;
        [SerializeField] private SPlisHSPlasHPlaybackController splisHSPlasHPlaybackController;
        [SerializeField] private SPlisHSPlasHRealtimeController splisHSPlasHRealtimeController;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        [SerializeField] private KeyCode reloadSceneKey = KeyCode.F5;

        private Vector3 initialCameraPosition;
        private Quaternion initialCameraRotation;

        private void Awake()
        {
            ResolveReferences();
            CaptureCurrentAsInitialState();
        }

        private void Update()
        {
            if (Input.GetKeyDown(resetKey))
            {
                ResetPrototypeState();
            }

            if (Input.GetKeyDown(reloadSceneKey))
            {
                ReloadActiveScene();
            }
        }

        [ContextMenu("Capture Current As Initial State")]
        public void CaptureCurrentAsInitialState()
        {
            ResolveReferences();

            if (cameraTransform != null)
            {
                initialCameraPosition = cameraTransform.position;
                initialCameraRotation = cameraTransform.rotation;
            }

            if (cameraOrbitController != null)
            {
                cameraOrbitController.CaptureCurrentAsInitialState();
            }

            if (containerController != null)
            {
                containerController.CaptureCurrentAsInitialState();
            }
        }

        [ContextMenu("Reset Prototype State")]
        public void ResetPrototypeState()
        {
            ResolveReferences();

            if (cameraTransform != null)
            {
                cameraTransform.position = initialCameraPosition;
                cameraTransform.rotation = initialCameraRotation;
            }

            if (cameraOrbitController != null)
            {
                cameraOrbitController.ResetOrbitState();
            }

            if (containerController != null)
            {
                containerController.ResetContainerState();
            }

            if (splisHSPlasHPlaybackController != null)
            {
                splisHSPlasHPlaybackController.ResetPlayback();
            }

            if (splisHSPlasHRealtimeController != null)
            {
                splisHSPlasHRealtimeController.ResetSimulation();
            }
        }

        public void ReloadActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
            {
                return;
            }

            SceneManager.LoadScene(activeScene.path);
        }

        private void ResolveReferences()
        {
            if (containerController == null)
            {
                containerController = FindFirstObjectByType<TrackballContainerController>();
            }

            if (cameraOrbitController == null && Camera.main != null)
            {
                cameraOrbitController = Camera.main.GetComponent<PrototypeOrbitCameraController>();
            }

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (splisHSPlasHPlaybackController == null)
            {
                splisHSPlasHPlaybackController = FindFirstObjectByType<SPlisHSPlasHPlaybackController>();
            }

            if (splisHSPlasHRealtimeController == null)
            {
                splisHSPlasHRealtimeController = FindFirstObjectByType<SPlisHSPlasHRealtimeController>();
            }
        }
    }
}
