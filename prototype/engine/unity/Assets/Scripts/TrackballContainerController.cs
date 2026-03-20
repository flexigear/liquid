using UnityEngine;

namespace Liquid
{
    [DefaultExecutionOrder(-200)]
    public class TrackballContainerController : MonoBehaviour
    {
        private enum RotationMode
        {
            WorldYawPitch = 0,
            GravityTilt = 1,
            WorldTiltXZ = 2
        }

        [SerializeField] private Transform targetTransform;
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField] private bool applyRotationToTarget = true;
        [SerializeField] private RotationMode rotationMode = RotationMode.WorldYawPitch;
        [SerializeField] private float dragSensitivity = 0.18f;
        [SerializeField] private float maxAngularSpeed = 4f;
        [SerializeField] private float maxAngularAcceleration = 30f;
        [SerializeField] private bool enableReleaseInertia;
        [SerializeField] private float releaseInertiaCarry = 0.08f;
        [SerializeField] private float releaseInertiaDeceleration = 12f;

        private Quaternion targetRotation;
        private Quaternion commandedRotation;
        private Quaternion initialRotation;
        private Vector3 previousAngularVelocity;
        private Vector3 releaseAngularVelocity;
        private bool dragActive;
        private Vector2 previousPointerPosition;

        public Vector3 AngularVelocity { get; private set; }
        public Vector3 AngularAcceleration { get; private set; }
        public Quaternion TargetRotation => targetRotation;
        public Quaternion CommandRotation => commandedRotation;

        private void Awake()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            if (targetRigidbody == null && targetTransform != null)
            {
                targetRigidbody = targetTransform.GetComponent<Rigidbody>();
            }

            targetRotation = GetCurrentRotation();
            commandedRotation = targetRotation;
            initialRotation = targetRotation;
        }

        private void OnEnable()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            if (targetRigidbody == null && targetTransform != null)
            {
                targetRigidbody = targetTransform.GetComponent<Rigidbody>();
            }

            targetRotation = GetCurrentRotation();
            commandedRotation = targetRotation;
            initialRotation = targetRotation;
            previousAngularVelocity = Vector3.zero;
            releaseAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
        }

        private void Update()
        {
            HandlePointerInput();
        }

        private void FixedUpdate()
        {
            UpdateRotation(Time.fixedDeltaTime);
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
                if (dragActive && enableReleaseInertia)
                {
                    releaseAngularVelocity = Vector3.ClampMagnitude(
                        AngularVelocity * Mathf.Clamp01(releaseInertiaCarry),
                        maxAngularSpeed);
                }

                dragActive = false;
                return;
            }

            if (!dragActive)
            {
                dragActive = true;
                releaseAngularVelocity = Vector3.zero;
                previousPointerPosition = pointerPosition;
                return;
            }

            Vector2 previousPointer = previousPointerPosition;
            Vector2 delta = pointerPosition - previousPointerPosition;
            previousPointerPosition = pointerPosition;

            if (delta.sqrMagnitude < 0.0001f)
            {
                UpdateAngularState(GetCurrentRotation(), GetCurrentRotation(), Mathf.Max(Time.deltaTime, 1e-6f));
                return;
            }

            Quaternion currentRotation = GetCurrentRotation();

            if (rotationMode == RotationMode.WorldYawPitch)
            {
                Quaternion arcballRotation = ComputeArcballRotation(previousPointer, previousPointerPosition);
                targetRotation = arcballRotation * targetRotation;
                ApplyDragRotationImmediately(currentRotation);
                return;
            }

            Vector3 horizontalAxis;
            Vector3 verticalAxis;

            if (rotationMode == RotationMode.GravityTilt)
            {
                horizontalAxis = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                verticalAxis = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            }
            else if (rotationMode == RotationMode.WorldTiltXZ)
            {
                horizontalAxis = Vector3.forward;
                verticalAxis = Vector3.right;
            }
            else
            {
                horizontalAxis = Vector3.up;
                verticalAxis = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            }

            // Make the container follow the drag direction instead of counter-rotating.
            Quaternion horizontalRotation = Quaternion.AngleAxis(-delta.x * dragSensitivity, horizontalAxis.normalized);
            Quaternion verticalRotation = Quaternion.AngleAxis(delta.y * dragSensitivity, verticalAxis.normalized);
            targetRotation = horizontalRotation * verticalRotation * targetRotation;
            ApplyDragRotationImmediately(currentRotation);
        }

        private Quaternion ComputeArcballRotation(Vector2 previousPointer, Vector2 currentPointer)
        {
            Camera referenceCamera = Camera.main;
            Vector3 previousArcballPoint = ProjectPointerToArcball(previousPointer);
            Vector3 currentArcballPoint = ProjectPointerToArcball(currentPointer);
            Vector3 localAxis = Vector3.Cross(currentArcballPoint, previousArcballPoint);

            if (localAxis.sqrMagnitude <= 1e-8f)
            {
                return Quaternion.identity;
            }

            float dot = Mathf.Clamp(Vector3.Dot(previousArcballPoint, currentArcballPoint), -1f, 1f);
            float angleDegrees = Mathf.Acos(dot) * Mathf.Rad2Deg;
            float sensitivityScale = dragSensitivity / 0.24f;
            angleDegrees *= Mathf.Max(0.1f, sensitivityScale);

            Vector3 worldAxis = referenceCamera != null
                ? referenceCamera.transform.TransformDirection(localAxis.normalized)
                : localAxis.normalized;

            return Quaternion.AngleAxis(angleDegrees, worldAxis);
        }

        private static Vector3 ProjectPointerToArcball(Vector2 pointerPosition)
        {
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            float radius = Mathf.Min(screenWidth, screenHeight) * 0.5f;
            Vector2 centered = new Vector2(
                (pointerPosition.x - screenWidth * 0.5f) / radius,
                (pointerPosition.y - screenHeight * 0.5f) / radius);
            float lengthSquared = centered.sqrMagnitude;
            float z;

            if (lengthSquared <= 1f)
            {
                z = Mathf.Sqrt(1f - lengthSquared);
            }
            else
            {
                centered.Normalize();
                z = 0f;
            }

            return new Vector3(centered.x, centered.y, z).normalized;
        }

        private void UpdateRotation(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (dragActive)
            {
                commandedRotation = targetRotation;
                return;
            }

            if (!dragActive && enableReleaseInertia && releaseAngularVelocity.sqrMagnitude > 1e-6f)
            {
                float spinStepRadians = releaseAngularVelocity.magnitude * deltaTime;
                if (spinStepRadians > 1e-6f)
                {
                    Quaternion inertiaRotation = Quaternion.AngleAxis(
                        spinStepRadians * Mathf.Rad2Deg,
                        releaseAngularVelocity.normalized);
                    targetRotation = inertiaRotation * targetRotation;
                }

                releaseAngularVelocity = Vector3.MoveTowards(
                    releaseAngularVelocity,
                    Vector3.zero,
                    releaseInertiaDeceleration * deltaTime);
            }

            Quaternion currentRotation = GetCurrentRotation();
            Quaternion deltaRotation = targetRotation * Quaternion.Inverse(currentRotation);
            deltaRotation.ToAngleAxis(out float deltaAngleDegrees, out Vector3 deltaAxis);

            if (float.IsNaN(deltaAxis.x) || deltaAxis == Vector3.zero)
            {
                AngularVelocity = Vector3.MoveTowards(AngularVelocity, Vector3.zero, maxAngularAcceleration * deltaTime);
                AngularAcceleration = (AngularVelocity - previousAngularVelocity) / deltaTime;
                previousAngularVelocity = AngularVelocity;
                return;
            }

            if (deltaAngleDegrees > 180f)
            {
                deltaAngleDegrees -= 360f;
            }

            float deltaAngleRadians = deltaAngleDegrees * Mathf.Deg2Rad;
            Vector3 desiredAngularVelocity = deltaAxis.normalized * (deltaAngleRadians / deltaTime);
            desiredAngularVelocity = Vector3.ClampMagnitude(desiredAngularVelocity, maxAngularSpeed);

            AngularVelocity = Vector3.MoveTowards(AngularVelocity, desiredAngularVelocity, maxAngularAcceleration * deltaTime);
            float angularStepRadians = AngularVelocity.magnitude * deltaTime;
            float maxStepRadians = Mathf.Abs(deltaAngleRadians);
            angularStepRadians = Mathf.Min(angularStepRadians, maxStepRadians);

            Quaternion newRotation = currentRotation;
            if (angularStepRadians > 1e-6f && AngularVelocity.sqrMagnitude > 1e-8f)
            {
                Quaternion stepRotation = Quaternion.AngleAxis(angularStepRadians * Mathf.Rad2Deg, AngularVelocity.normalized);
                newRotation = stepRotation * currentRotation;
            }
            else if (Mathf.Abs(deltaAngleRadians) <= 1e-4f)
            {
                newRotation = targetRotation;
                AngularVelocity = Vector3.zero;
            }

            ApplyRotation(newRotation);
            commandedRotation = newRotation;
            AngularAcceleration = (AngularVelocity - previousAngularVelocity) / deltaTime;
            previousAngularVelocity = AngularVelocity;
        }

        public void CaptureCurrentAsInitialState()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            if (targetRigidbody == null && targetTransform != null)
            {
                targetRigidbody = targetTransform.GetComponent<Rigidbody>();
            }

            initialRotation = GetCurrentRotation();
            commandedRotation = initialRotation;
            targetRotation = initialRotation;
        }

        public void ResetContainerState()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }

            if (targetRigidbody == null && targetTransform != null)
            {
                targetRigidbody = targetTransform.GetComponent<Rigidbody>();
            }

            ApplyRotation(initialRotation);
            commandedRotation = initialRotation;
            targetRotation = initialRotation;
            previousAngularVelocity = Vector3.zero;
            releaseAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
            dragActive = false;
            previousPointerPosition = default;
        }

        private Quaternion GetCurrentRotation()
        {
            if (targetRigidbody != null)
            {
                return targetRigidbody.rotation;
            }

            return targetTransform.rotation;
        }

        private void ApplyRotation(Quaternion rotation)
        {
            if (!applyRotationToTarget)
            {
                return;
            }

            if (targetRigidbody != null && targetRigidbody.isKinematic)
            {
                targetRigidbody.MoveRotation(rotation);
                return;
            }

            targetTransform.rotation = rotation;
        }

        private void ApplyDragRotationImmediately(Quaternion previousRotation)
        {
            ApplyImmediateRotation(targetRotation);
            commandedRotation = targetRotation;
            UpdateAngularState(previousRotation, targetRotation, Mathf.Max(Time.deltaTime, 1e-6f));
        }

        private void ApplyImmediateRotation(Quaternion rotation)
        {
            if (!applyRotationToTarget)
            {
                return;
            }

            if (targetRigidbody != null && targetRigidbody.isKinematic)
            {
                targetRigidbody.rotation = rotation;
                return;
            }

            ApplyRotation(rotation);
        }

        private void UpdateAngularState(Quaternion fromRotation, Quaternion toRotation, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                AngularVelocity = Vector3.zero;
                AngularAcceleration = Vector3.zero;
                previousAngularVelocity = Vector3.zero;
                return;
            }

            Quaternion deltaRotation = toRotation * Quaternion.Inverse(fromRotation);
            deltaRotation.ToAngleAxis(out float deltaAngleDegrees, out Vector3 deltaAxis);

            Vector3 newAngularVelocity = Vector3.zero;
            if (!float.IsNaN(deltaAxis.x) && deltaAxis != Vector3.zero)
            {
                if (deltaAngleDegrees > 180f)
                {
                    deltaAngleDegrees -= 360f;
                }

                float deltaAngleRadians = deltaAngleDegrees * Mathf.Deg2Rad;
                if (Mathf.Abs(deltaAngleRadians) > 1e-6f)
                {
                    newAngularVelocity = deltaAxis.normalized * (deltaAngleRadians / deltaTime);
                }
            }

            AngularVelocity = newAngularVelocity;
            AngularAcceleration = (AngularVelocity - previousAngularVelocity) / deltaTime;
            previousAngularVelocity = AngularVelocity;
        }

        public void SetApplyRotationToTarget(bool value)
        {
            applyRotationToTarget = value;
            Quaternion referenceRotation = GetLiveTargetRotation();

            commandedRotation = referenceRotation;
            targetRotation = referenceRotation;
            initialRotation = referenceRotation;
            previousAngularVelocity = Vector3.zero;
            releaseAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
        }

        public void ConfigureReleaseInertia(bool enabled, float carry, float deceleration)
        {
            enableReleaseInertia = enabled;
            releaseInertiaCarry = Mathf.Clamp01(carry);
            releaseInertiaDeceleration = Mathf.Max(0.01f, deceleration);
            releaseAngularVelocity = Vector3.zero;
        }

        public void ConfigureTarget(Transform newTargetTransform, Rigidbody newTargetRigidbody, bool shouldApplyRotation)
        {
            targetTransform = newTargetTransform != null ? newTargetTransform : transform;
            targetRigidbody = newTargetRigidbody != null ? newTargetRigidbody : targetTransform.GetComponent<Rigidbody>();
            applyRotationToTarget = shouldApplyRotation;

            Quaternion referenceRotation = GetLiveTargetRotation();
            commandedRotation = referenceRotation;
            targetRotation = referenceRotation;
            initialRotation = referenceRotation;
            previousAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
            dragActive = false;
            previousPointerPosition = default;
        }

        public void ConfigureMotionResponse(float newDragSensitivity, float newMaxAngularSpeed, float newMaxAngularAcceleration)
        {
            dragSensitivity = Mathf.Max(0.01f, newDragSensitivity);
            maxAngularSpeed = Mathf.Max(0.01f, newMaxAngularSpeed);
            maxAngularAcceleration = Mathf.Max(0.01f, newMaxAngularAcceleration);

            previousAngularVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;
            AngularAcceleration = Vector3.zero;
        }

        public void SetGravitySensitiveTiltMode(bool value)
        {
            rotationMode = value ? RotationMode.GravityTilt : RotationMode.WorldYawPitch;
        }

        public void SetWorldTiltMode(bool value)
        {
            rotationMode = value ? RotationMode.WorldTiltXZ : RotationMode.WorldYawPitch;
        }

        private Quaternion GetLiveTargetRotation()
        {
            if (targetRigidbody != null)
            {
                return targetRigidbody.rotation;
            }

            return targetTransform != null ? targetTransform.rotation : commandedRotation;
        }
    }
}
