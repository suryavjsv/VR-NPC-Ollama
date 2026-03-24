using System;
using System.Collections;
using UnityEngine;
using LLMUnity;
using VRAssistant.NPC;

namespace VRAssistant.Local
{
    /// <summary>
    /// On-device NPC Controller — runs entirely on Quest 3.
    /// Push-to-Talk: right trigger starts/stops listening.
    /// </summary>
    public class LocalNPCController : MonoBehaviour
    {
        [Header("AI References")]
        [SerializeField] private LLM llm;
        [SerializeField] private LLMAgent llmAgent;

        [Header("NPC References")]
        [SerializeField] private LipSyncController lipSync;
        [SerializeField] private NPCAnimator npcAnimator;
        [SerializeField] private AudioSource audioSource;

        [Header("STT Reference")]
        [SerializeField] private LocalSTTManager sttManager;

        [Header("TTS Reference")]
        [SerializeField] private LocalTTSManager ttsManager;

        [Header("UI Reference")]
        [SerializeField] private LocalSubtitleUI subtitleUI;

        [Header("NPC Settings")]
        [SerializeField] private string npcName = "Alex";
        [SerializeField] private float lookAtPlayerSpeed = 2f;
        [SerializeField] private float interactionDistance = 3f;
        [SerializeField] private bool autoGreet = true;

        // State machine
        public enum NPCState { Idle, Listening, Processing, Speaking }
        public NPCState CurrentState { get; private set; } = NPCState.Idle;
        public event Action<NPCState> OnStateChanged;

        private Transform _playerCamera;
        private bool _isInitialized = false;

        // ─── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            _playerCamera = Camera.main?.transform;

            if (sttManager != null)
            {
                sttManager.OnTranscriptionComplete += OnPlayerSpoke;
                sttManager.OnRecordingStarted      += OnRecordingStarted;
                sttManager.OnRecordingStopped      += OnRecordingStopped;
            }

            if (ttsManager != null)
            {
                ttsManager.OnSpeakStart += OnTTSStarted;
                ttsManager.OnSpeakEnd   += OnTTSFinished;
            }

            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            if (PermissionGate.Instance != null)
            {
                Debug.Log("[LocalNPC] Waiting for permissions...");
                if (subtitleUI != null)
                    subtitleUI.ShowStatus("Waiting for permissions...");
                yield return PermissionGate.Instance.WaitForPermissions();
            }

            Debug.Log("[LocalNPC] Waiting for LLM to load...");

            if (subtitleUI != null)
                subtitleUI.ShowStatus("Loading AI model...");

            // Initialize TTS Manager
            if (ttsManager != null)
            {
                yield return StartCoroutine(ttsManager.Initialize());
            }

            if (llm != null)
            {
                float waitStart = Time.realtimeSinceStartup;
                while (!llm.started && !llm.failed)
                {
                    float elapsed = Time.realtimeSinceStartup - waitStart;
                    if (subtitleUI != null)
                        subtitleUI.ShowStatus($"Loading AI model... ({elapsed:F0}s)");
                    yield return new WaitForSeconds(0.5f);
                }

                if (llm.failed)
                {
                    Debug.LogError("[LocalNPC] LLM failed to start!");
                    if (subtitleUI != null)
                        subtitleUI.ShowStatus("AI model failed to load");
                    yield break;
                }

                yield return new WaitForSeconds(1f);
            }
            else
            {
                Debug.LogWarning("[LocalNPC] No LLM reference — fallback wait.");
                yield return new WaitForSeconds(15f);
            }

            _isInitialized = true;
            Debug.Log($"[LocalNPC] {npcName} ready!");

            if (subtitleUI != null)
                subtitleUI.ShowStatus("Hold right trigger to speak");

            if (autoGreet)
            {
                yield return new WaitForSeconds(1f);
                SendTextToNPC("Greet the user briefly. Introduce yourself in 1-2 sentences and tell them to hold the right trigger to speak.");
            }
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // Look at player
            if (_playerCamera != null)
            {
                float distance = Vector3.Distance(transform.position, _playerCamera.position);
                if (distance <= interactionDistance)
                {
                    Vector3 lookDir = _playerCamera.position - transform.position;
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir);
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation, targetRot,
                            Time.deltaTime * lookAtPlayerSpeed
                        );
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (sttManager != null)
            {
                sttManager.OnTranscriptionComplete -= OnPlayerSpoke;
                sttManager.OnRecordingStarted      -= OnRecordingStarted;
                sttManager.OnRecordingStopped      -= OnRecordingStopped;
            }

            if (ttsManager != null)
            {
                ttsManager.OnSpeakStart -= OnTTSStarted;
                ttsManager.OnSpeakEnd   -= OnTTSFinished;
            }
        }

        // ─── State Management ──────────────────────────────────

        public void SetState(NPCState newState)
        {
            if (CurrentState == newState) return;

            Debug.Log($"[LocalNPC] State: {CurrentState} → {newState}");
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);

            if (npcAnimator != null)
                npcAnimator.SetNPCState((NPCController.NPCState)(int)newState);
        }

        // ─── PTT Callbacks ─────────────────────────────────────

        private void OnRecordingStarted()
        {
            // Don't interrupt NPC while speaking
            if (CurrentState == NPCState.Speaking) return;

            SetState(NPCState.Listening);

            if (subtitleUI != null)
                subtitleUI.ShowStatus("🎙 Listening...");
        }

        private void OnRecordingStopped()
        {
            if (subtitleUI != null)
                subtitleUI.ShowStatus("Transcribing...");
        }

        // ─── STT Callback ──────────────────────────────────────

        private void OnPlayerSpoke(string transcription)
        {
            if (string.IsNullOrEmpty(transcription)) return;
            if (CurrentState == NPCState.Speaking) return;

            Debug.Log($"[LocalNPC] Player said: \"{transcription}\"");

            if (subtitleUI != null)
                subtitleUI.ShowPlayerText(transcription);

            SetState(NPCState.Processing);
            SendTextToNPC(transcription);
        }

        // ─── LLM ──────────────────────────────────────────────

        private async void SendTextToNPC(string text)
        {
            if (llmAgent == null)
            {
                Debug.LogError("[LocalNPC] LLMAgent not assigned!");
                return;
            }

            if (!_isInitialized || (llm != null && !llm.started))
            {
                Debug.LogWarning("[LocalNPC] LLM not ready, skipping message.");
                return;
            }

            SetState(NPCState.Processing);

            if (subtitleUI != null)
                subtitleUI.ShowStatus("Thinking...");

            try
            {
                float startTime = Time.realtimeSinceStartup;
                string response = await llmAgent.Chat(text);
                float elapsed   = Time.realtimeSinceStartup - startTime;

                Debug.Log($"[LocalNPC] LLM ({elapsed:F2}s): \"{response}\"");

                if (string.IsNullOrEmpty(response))
                    response = "Sorry, I didn't catch that. Could you say it again?";

                response = CleanForSpeech(response);

                if (subtitleUI != null)
                    subtitleUI.ShowNPCText(response);

                SpeakResponse(response);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalNPC] LLM error: {e.Message}");
                SetState(NPCState.Idle);

                if (subtitleUI != null)
                    subtitleUI.ShowStatus("Hold right trigger to speak");
            }
        }

        // ─── TTS ───────────────────────────────────────────────

        private void SpeakResponse(string text)
        {
            if (ttsManager != null)
            {
                SetState(NPCState.Speaking);
                ttsManager.Speak(text);
            }
            else
            {
                Debug.LogWarning("[LocalNPC] No TTS manager");
                SetState(NPCState.Idle);

                if (subtitleUI != null)
                    subtitleUI.ShowStatus("Hold right trigger to speak");
            }
        }

        private void OnTTSStarted()
        {
            SetState(NPCState.Speaking);

            if (lipSync != null && audioSource != null)
                lipSync.StartLipSync(audioSource);
        }

        private void OnTTSFinished()
        {
            if (lipSync != null)
                lipSync.StopLipSync();

            SetState(NPCState.Idle);
            Debug.Log("[LocalNPC] Finished speaking");

            if (subtitleUI != null)
                subtitleUI.ShowStatus("Hold right trigger to speak");
        }

        // ─── Public API ────────────────────────────────────────

        public void SendMessage(string text)
        {
            if (!_isInitialized) return;
            SetState(NPCState.Processing);
            SendTextToNPC(text);
        }

        public void ResetConversation()
        {
            SetState(NPCState.Idle);
        }

        // ─── Utilities ─────────────────────────────────────────

        private string CleanForSpeech(string text)
        {
            text = text.Trim().Trim('"').Trim('\'');
            text = text.Replace("\"", "").Replace("'", "");
            text = text.Replace("\n", " ").Replace("\r", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.*?)\*\*", "$1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*(.*?)\*", "$1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"`(.*?)`", "$1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"#{1,6}\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\s*[-•*]\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }
    }
}