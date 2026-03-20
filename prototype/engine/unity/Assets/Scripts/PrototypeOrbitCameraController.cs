using UnityEngine;

namespace Liquid
{
    public class PrototypeOrbitCameraController : MonoBehaviour
    {
        [SerializeField] private Transform targetTransform;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float zoomSensitivity = 0.12f;
        [SerializeField] private float minPitch = -75f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float minDistance = 0.16f;
        [SerializeField] private float maxDistance = 0.65f;

        private float yaw;
        private float pitch;
        private float distance = 0.405f;
        private float initialYaw;
        private float initialPitch;
        private float initialDistance;
        private Vector3 initialTargetOffset;

        public void SetTarget(Transform target)
        {
            targetTransform = target;

            if (targetTransform != null)
            {
                Vector3 focusPoint = GetFocusPoint();
                transform.LookAt(focusPoint, Vector3.up);
            }

            SyncStateFromTransform();
            ApplyOrbit();
        }

        private void Awake()
        {
            SyncStateFromTransform();
            CaptureCurrentAsInitialState();
        }

        private void OnEnable()
        {
            SyncStateFromTransform();
            ApplyOrbit();
        }

        private void LateUpdate()
        {
            HandleInput();
            ApplyOrbit();
        }

        public void CaptureCurrentAsInitialState()
        {
            SyncStateFromTransform();
            initialYaw = yaw;
            initialPitch = pitch;
            initialDistance = distance;
            initialTargetOffset = targetOffset;
        }

        public void ResetOrbitState()
        {
            yaw = initialYaw;
            pitch = initialPitch;
            distance = initialDistance;
            targetOffset = initialTargetOffset;
            ApplyOrbit();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.0001f)
            {
                distance = Mathf.Clamp(distance - scrollDelta * zoomSensitivity, minDistance, maxDistance);
            }
        }

        private void SyncStateFromTransform()
        {
            if (targetTransform == null)
            {
                Vector3 eulerAngles = transform.rotation.eulerAngles;
                yaw = eulerAngles.y;
                pitch = NormalizeAngle(eulerAngles.x);
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                return;
            }

            Vector3 focusPoint = GetFocusPoint();
            Vector3 toCamera = transform.position - focusPoint;
            distance = Mathf.Clamp(toCamera.magnitude, minDistance, maxDistance);

            if (distance <= 1e-4f)
            {
                distance = Mathf.Max(minDistance, 0.1f);
                toCamera = Quaternion.Euler(pitch, yaw, 0f) * (Vector3.back * distance);
            }

            Quaternion orbitRotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            Vector3 orbitEulerAngles = orbitRotation.eulerAngles;
            yaw = orbitEulerAngles.y;
            pitch = NormalizeAngle(orbitEulerAngles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void ApplyOrbit()
        {
            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);

            if (targetTransform == null)
            {
                transform.rotation = orbitRotation;
                return;
            }

            Vector3 focusPoint = GetFocusPoint();
            transform.position = focusPoint + orbitRotation * (Vector3.back * distance);
            transform.LookAt(focusPoint, Vector3.up);
        }

        private Vector3 GetFocusPoint()
        {
            if (targetTransform == null)
            {
                return transform.position + transform.forward * distance;
            }

            return targetTransform.position + targetOffset;
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle;
        }
    }
}
