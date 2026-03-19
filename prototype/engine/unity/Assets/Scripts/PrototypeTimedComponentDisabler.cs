using UnityEngine;

namespace Liquid
{
    public class PrototypeTimedComponentDisabler : MonoBehaviour
    {
        [SerializeField] private Behaviour targetBehaviour;
        [SerializeField] private float disableAfterSeconds = 1.5f;

        private float elapsedTime;

        private void OnEnable()
        {
            elapsedTime = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying || targetBehaviour == null || !targetBehaviour.enabled)
            {
                return;
            }

            elapsedTime += Time.deltaTime;
            if (elapsedTime < disableAfterSeconds)
            {
                return;
            }

            targetBehaviour.enabled = false;
            enabled = false;
        }

        public void Configure(Behaviour behaviour, float delaySeconds)
        {
            targetBehaviour = behaviour;
            disableAfterSeconds = delaySeconds;
            elapsedTime = 0f;
            enabled = true;
        }
    }
}
