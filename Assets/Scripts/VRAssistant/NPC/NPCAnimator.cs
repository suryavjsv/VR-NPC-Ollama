using UnityEngine;

namespace VRAssistant.NPC
{
    /// <summary>
    /// Controls NPC body animations based on conversation state.
    /// Uses Unity Animator with state-driven blend trees.
    ///
    /// ANIMATOR SETUP:
    /// Create an Animator Controller with these parameters:
    ///   - "State" (int): 0=Idle, 1=Listening, 2=Processing, 3=Speaking
    ///   - "GestureIndex" (int): Random gesture selection while speaking
    ///   - "IsTalking" (bool): True when NPC is speaking
    ///
    /// States:
    ///   Idle → Listening → Processing → Speaking → Idle
    ///
    /// Suggested animation clips:
    ///   - Idle:       Breathing, subtle weight shifting
    ///   - Listening:  Slight lean forward, head tilt, nod occasionally
    ///   - Processing: Hand on chin, looking up/thinking
    ///   - Speaking:   Hand gestures (3-4 variations), open posture
    /// </summary>
    public class NPCAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Gesture Settings")]
        [Tooltip("Number of speaking gesture variations available")]
        [SerializeField] private int gestureCount = 4;
        [SerializeField] private float gestureChangeInterval = 3f;
        [SerializeField] private float idleVariationInterval = 8f;

        [Header("Head Look")]
        [SerializeField] private Transform headBone;
        [SerializeField] private float headTrackSpeed = 2f;
        [SerializeField] private float maxHeadAngle = 30f;

        // Animator parameter hashes (cached for performance)
        private static readonly int StateParam = Animator.StringToHash("State");
        private static readonly int GestureParam = Animator.StringToHash("GestureIndex");
        private static readonly int IsTalkingParam = Animator.StringToHash("IsTalking");

        private NPCController.NPCState _currentState;
        private float _gestureTimer;
        private float _idleTimer;
        private Transform _playerCamera;
        private int _currentGesture;

        private void Start()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            _playerCamera = Camera.main?.transform;
        }

        private void Update()
        {
            // Cycle gestures while speaking
            if (_currentState == NPCController.NPCState.Speaking)
            {
                _gestureTimer += Time.deltaTime;
                if (_gestureTimer >= gestureChangeInterval)
                {
                    _gestureTimer = 0f;
                    SetRandomGesture();
                }
            }

            // Occasional idle variation
            if (_currentState == NPCController.NPCState.Idle)
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer >= idleVariationInterval)
                {
                    _idleTimer = 0f;
                    // Trigger subtle idle variation
                    if (animator != null)
                    {
                        animator.SetInteger(GestureParam, Random.Range(0, 2));
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // Subtle head tracking toward player
            if (headBone != null && _playerCamera != null)
            {
                Vector3 dirToPlayer = _playerCamera.position - headBone.position;
                Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);

                // Clamp rotation relative to body
                Quaternion localTarget = Quaternion.Inverse(transform.rotation) * targetRot;
                Vector3 euler = localTarget.eulerAngles;

                // Normalize angles to -180..180
                if (euler.x > 180f) euler.x -= 360f;
                if (euler.y > 180f) euler.y -= 360f;

                euler.x = Mathf.Clamp(euler.x, -maxHeadAngle, maxHeadAngle);
                euler.y = Mathf.Clamp(euler.y, -maxHeadAngle, maxHeadAngle);
                euler.z = 0f;

                Quaternion clampedRot = transform.rotation * Quaternion.Euler(euler);

                headBone.rotation = Quaternion.Slerp(
                    headBone.rotation,
                    clampedRot,
                    Time.deltaTime * headTrackSpeed
                );
            }
        }

        // ─── Public API ────────────────────────────────────────

        /// <summary>
        /// Set the NPC animation state. Called by NPCController.
        /// </summary>
        public void SetNPCState(NPCController.NPCState state)
        {
            _currentState = state;

            if (animator == null) return;

            animator.SetInteger(StateParam, (int)state);
            animator.SetBool(IsTalkingParam, state == NPCController.NPCState.Speaking);

            switch (state)
            {
                case NPCController.NPCState.Idle:
                    _gestureTimer = 0f;
                    _idleTimer = 0f;
                    break;

                case NPCController.NPCState.Listening:
                    // Could trigger a "lean in" or "nod" animation
                    break;

                case NPCController.NPCState.Processing:
                    // Thinking animation
                    animator.SetInteger(GestureParam, 0);
                    break;

                case NPCController.NPCState.Speaking:
                    _gestureTimer = 0f;
                    SetRandomGesture();
                    break;
            }
        }

        /// <summary>
        /// Trigger a specific gesture by index.
        /// </summary>
        public void TriggerGesture(int index)
        {
            if (animator == null) return;
            animator.SetInteger(GestureParam, Mathf.Clamp(index, 0, gestureCount - 1));
        }

        /// <summary>
        /// Trigger a nod animation (useful for "listening" feedback).
        /// </summary>
        public void TriggerNod()
        {
            if (animator != null)
            {
                animator.SetTrigger("Nod");
            }
        }

        private void SetRandomGesture()
        {
            int newGesture;
            do
            {
                newGesture = Random.Range(0, gestureCount);
            } while (newGesture == _currentGesture && gestureCount > 1);

            _currentGesture = newGesture;

            if (animator != null)
            {
                animator.SetInteger(GestureParam, _currentGesture);
            }
        }
    }
}
