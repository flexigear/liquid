using UnityEngine;

namespace Liquid
{
    public class PrototypeOrbitCameraController : MonoBehaviour
    {
        [SerializeField] private Transform targetTransform;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float fastMoveMultiplier = 2.5f;
        [SerializeField] private float minPitch = -75f;
        [SerializeField] private float maxPitch = 75f;

        private float yaw;
        private float pitch;
        private float initialYaw;
        private float initialPitch;
        private Vector3 initialPosition;

        public void SetTarget(Transform target)
        {
            targetTransform = target;

            if (targetTransform != null)
            {
                transform.LookAt(targetTransform.position + targetOffset, Vector3.up);
            }

            SyncStateFromTransform();
        }

        private void Awake()
        {
            SyncStateFromTransform();
            CaptureCurrentAsInitialState();
        }

        private void OnEnable()
        {
            SyncStateFromTransform();
        }

        private void LateUpdate()
        {
            HandleInput();
        }

        public void CaptureCurrentAsInitialState()
        {
            SyncStateFromTransform();
            initialPosition = transform.position;
            initialYaw = yaw;
            initialPitch = pitch;
        }

        public void ResetOrbitState()
        {
            transform.position = initialPosition;
            yaw = initialYaw;
            pitch = initialPitch;
            ApplyRotation();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                ApplyRotation();
            }

            Vector3 movementInput = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
            {
                movementInput += Vector3.forward;
            }
            if (Input.GetKey(KeyCode.S))
            {
                movementInput += Vector3.back;
            }
            if (Input.GetKey(KeyCode.A))
            {
                movementInput += Vector3.left;
            }
            if (Input.GetKey(KeyCode.D))
            {
                movementInput += Vector3.right;
            }
            if (Input.GetKey(KeyCode.E))
            {
                movementInput += Vector3.up;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                movementInput += Vector3.down;
            }

            if (movementInput.sqrMagnitude > 0f)
            {
                float speed = moveSpeed;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    speed *= fastMoveMultiplier;
                }

                Vector3 moveDirection =
                    transform.forward * movementInput.z +
                    transform.right * movementInput.x +
                    Vector3.up * movementInput.y;

                transform.position += moveDirection.normalized * speed * Time.unscaledDeltaTime;
            }
        }

        private void SyncStateFromTransform()
        {
            Vector3 eulerAngles = transform.rotation.eulerAngles;
            yaw = eulerAngles.y;
            pitch = NormalizeAngle(eulerAngles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
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
