using UnityEngine;

namespace Liquid
{
    public class TrackballContainerController : MonoBehaviour
    {
        [SerializeField] private Transform targetTransform;
        [SerializeField] private float dragSensitivity = 0.24f;
        [SerializeField] private float followSharpness = 14f;
        [SerializeField] private float maxAngularSpeed = 8f;
        [SerializeField] private float maxAngularAcceleration = 40f;

        private Quaternion targetRotation;
        private Quaternion initialRotation;
        private Vector3 previousAngularVelocity;
        private bool dragActive;
        private Vector2 previousPointerPosition;

        public Vector3 AngularVelocity { get; private set; }
        public Vector3 AngularAcceleration { get; private set; }

        private void Awake()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            targetRotation = targetTransform.rotation;
            initialRotation = targetRotation;
        }

        private void OnEnable()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            targetRotation = targetTransform.rotation;
            initialRotation = targetRotation;
            previousAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
        }

        private void Update()
        {
            HandlePointerInput();
            UpdateRotation(Time.deltaTime);
        }

        private void HandlePointerInput()
        {
            bool pointerDown = false;
            Vector2 pointerPosition = default;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                pointerDown = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                pointerPosition = touch.position;
            }
            else if (Input.GetMouseButton(0))
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
                previousPointerPosition = pointerPosition;
                return;
            }

            Vector2 delta = pointerPosition - previousPointerPosition;
            previousPointerPosition = pointerPosition;

            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 worldUp = Vector3.up;
            Vector3 worldRight = Camera.main != null ? Camera.main.transform.right : Vector3.right;

            // Make the container follow the drag direction instead of counter-rotating.
            Quaternion yaw = Quaternion.AngleAxis(-delta.x * dragSensitivity, worldUp);
            Quaternion pitch = Quaternion.AngleAxis(delta.y * dragSensitivity, worldRight);
            targetRotation = yaw * pitch * targetRotation;
        }

        private void UpdateRotation(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            Quaternion previousRotation = targetTransform.rotation;
            float blend = 1f - Mathf.Exp(-followSharpness * deltaTime);
            targetTransform.rotation = Quaternion.Slerp(previousRotation, targetRotation, blend);

            Quaternion deltaRotation = targetTransform.rotation * Quaternion.Inverse(previousRotation);
            deltaRotation.ToAngleAxis(out float deltaAngleDegrees, out Vector3 deltaAxis);

            if (float.IsNaN(deltaAxis.x) || deltaAxis == Vector3.zero)
            {
                AngularVelocity = Vector3.zero;
                AngularAcceleration = Vector3.zero;
                previousAngularVelocity = AngularVelocity;
                return;
            }

            if (deltaAngleDegrees > 180f)
            {
                deltaAngleDegrees -= 360f;
            }

            Vector3 angularVelocity = deltaAxis.normalized * (deltaAngleDegrees * Mathf.Deg2Rad / deltaTime);
            AngularVelocity = Vector3.ClampMagnitude(angularVelocity, maxAngularSpeed);
            AngularAcceleration = Vector3.ClampMagnitude((AngularVelocity - previousAngularVelocity) / deltaTime, maxAngularAcceleration);
            previousAngularVelocity = AngularVelocity;
        }

        public void CaptureCurrentAsInitialState()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            initialRotation = targetTransform.rotation;
            targetRotation = initialRotation;
        }

        public void ResetContainerState()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            targetTransform.rotation = initialRotation;
            targetRotation = initialRotation;
            previousAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
            dragActive = false;
            previousPointerPosition = default;
        }
    }
}
