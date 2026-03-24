using System;
using System.Collections;
using UnityEngine;
using Whisper;
using Whisper.Utils;

namespace VRAssistant.Local
{
    /// <summary>
    /// On-device STT using whisper.unity.
    /// Push-to-Talk: hold right controller primary trigger to record,
    /// release to transcribe.
    ///
    /// MicrophoneRecord Inspector settings for PTT:
    ///   - useVad: false   (we control start/stop manually)
    ///   - vadStop: false
    ///   - loop: false
    ///   - maxLengthSec: 15
    ///   - frequency: 16000
    /// </summary>
    public class LocalSTTManager : MonoBehaviour
    {
        [Header("Whisper References")]
        [SerializeField] private WhisperManager whisperManager;
        [SerializeField] private MicrophoneRecord microphoneRecord;

        [Header("Push-to-Talk Settings")]
        [Tooltip("Hold this OVR button to record. Default = PrimaryIndexTrigger on right controller.")]
        [SerializeField] private OVRInput.Button pushToTalkButton = OVRInput.Button.PrimaryIndexTrigger;
        [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;
        [Tooltip("Minimum recording duration in seconds before transcribing.")]
        [SerializeField] private float minRecordSeconds = 0.5f;

        // Events
        public event Action<string> OnTranscriptionComplete;
        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;

        // State
        public bool IsTranscribing { get; private set; }
        public bool IsRecording => microphoneRecord != null && microphoneRecord.IsRecording;

        private bool _isInitialized = false;
        private bool _wasButtonHeld = false;
        private float _recordStartTime = 0f;

        // ─── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            if (PermissionGate.Instance != null)
            {
                Debug.Log("[STT] Waiting for permissions...");
                yield return PermissionGate.Instance.WaitForPermissions();
            }

            if (whisperManager == null)
            {
                Debug.LogError("[STT] WhisperManager not assigned!");
                yield break;
            }

            if (microphoneRecord == null)
            {
                Debug.LogError("[STT] MicrophoneRecord not assigned!");
                yield break;
            }

            Debug.Log("[STT] Waiting for Whisper model to load...");
            while (!whisperManager.IsLoaded)
            {
                if (!whisperManager.IsLoading)
                {
                    Debug.LogError("[STT] Whisper model failed to load!");
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
            }

            // Subscribe to stop event for transcription
            microphoneRecord.OnRecordStop += OnMicRecordStop;

            // Disable echo
            microphoneRecord.echo = false;

            _isInitialized = true;
            Debug.Log("[STT] Push-to-Talk ready. Hold right trigger to speak.");
        }

        private void OnDestroy()
        {
            if (microphoneRecord != null)
                microphoneRecord.OnRecordStop -= OnMicRecordStop;
        }

        // ─── Push-to-Talk Input ────────────────────────────────

        private void Update()
        {
            if (!_isInitialized || IsTranscribing) return;

            bool buttonHeld = OVRInput.Get(pushToTalkButton, controller);

            // Button just pressed — start recording
            if (buttonHeld && !_wasButtonHeld)
            {
                StartRecording();
            }

            // Button just released — stop recording
            if (!buttonHeld && _wasButtonHeld)
            {
                StopRecording();
            }

            _wasButtonHeld = buttonHeld;
        }

        // ─── Public API ────────────────────────────────────────

        public void StartRecording()
        {
            if (!_isInitialized || microphoneRecord.IsRecording) return;

            Debug.Log("[STT] 🎙 Recording started (trigger held)");
            _recordStartTime = Time.realtimeSinceStartup;
            microphoneRecord.StartRecord();
            OnRecordingStarted?.Invoke();
        }

        public void StopRecording()
        {
            if (!_isInitialized || !microphoneRecord.IsRecording) return;

            float duration = Time.realtimeSinceStartup - _recordStartTime;

            if (duration < minRecordSeconds)
            {
                Debug.Log($"[STT] Recording too short ({duration:F2}s), discarding");
                microphoneRecord.StopRecord();
                OnRecordingStopped?.Invoke();
                return;
            }

            Debug.Log($"[STT] 🎙 Recording stopped ({duration:F2}s), transcribing...");
            microphoneRecord.StopRecord();
            OnRecordingStopped?.Invoke();
        }

        // ─── Transcription Callback ────────────────────────────

        private async void OnMicRecordStop(AudioChunk recordedAudio)
        {
            if (recordedAudio.Length < minRecordSeconds ||
                recordedAudio.Data == null ||
                recordedAudio.Data.Length == 0)
            {
                Debug.Log("[STT] Audio too short, skipping transcription");
                return;
            }

            Debug.Log($"[STT] Transcribing {recordedAudio.Length:F1}s of audio...");
            IsTranscribing = true;

            try
            {
                float startTime = Time.realtimeSinceStartup;

                var result = await whisperManager.GetTextAsync(
                    recordedAudio.Data,
                    recordedAudio.Frequency,
                    recordedAudio.Channels
                );

                float elapsed = Time.realtimeSinceStartup - startTime;

                if (result != null && !string.IsNullOrEmpty(result.Result))
                {
                    string text = result.Result.Trim();
                    Debug.Log($"[STT] Transcribed ({elapsed:F2}s): \"{text}\"");

                    if (!IsHallucination(text))
                        OnTranscriptionComplete?.Invoke(text);
                    else
                        Debug.Log($"[STT] Filtered hallucination: \"{text}\"");
                }
                else
                {
                    Debug.Log("[STT] No speech detected");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[STT] Transcription error: {e.Message}");
            }

            IsTranscribing = false;
        }

        // ─── Utilities ─────────────────────────────────────────

        private bool IsHallucination(string text)
        {
            string lower = text.ToLower().Trim();
            string[] hallucinations = {
                "thank you", "thanks for watching", "you",
                "the end", "thanks", "bye", ".", "...",
                "thank you for watching", "(music)",
                "[music]", "(applause)", "[applause]",
                "you.", "the", "a", "i", "[blank_audio]"
            };

            foreach (string h in hallucinations)
                if (lower == h) return true;

            return text.Length < 3;
        }
    }
}