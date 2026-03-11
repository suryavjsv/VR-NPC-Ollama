using System;
using System.IO;
using UnityEngine;
using VRAssistant.Audio;
using VRAssistant.Network;

namespace VRAssistant.NPC
{
    /// <summary>
    /// Main NPC Controller — Orchestrates microphone → server → lip sync → animation.
    /// Attach this to your NPC GameObject.
    /// </summary>
    public class NPCController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocket;
        [SerializeField] private MicrophoneCapture microphone;
        [SerializeField] private LipSyncController lipSync;
        [SerializeField] private NPCAnimator npcAnimator;
        [SerializeField] private AudioSource audioSource;

        [Header("NPC Settings")]
        [SerializeField] private string npcName = "Assembly Guide";
        [SerializeField] private float lookAtPlayerSpeed = 2f;
        [SerializeField] private float interactionDistance = 3f;

        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;

        // State machine
        public enum NPCState { Idle, Listening, Processing, Speaking }
        public NPCState CurrentState { get; private set; } = NPCState.Idle;
        public event Action<NPCState> OnStateChanged;

        // Last messages (for debug UI)
        public string LastTranscription { get; private set; } = "";
        public string LastResponse { get; private set; } = "";
        public string LastLatency { get; private set; } = "";

        private Transform _playerCamera;
        private AudioClip _responseClip;

        // ─── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            // Find player camera
            _playerCamera = Camera.main?.transform;

            // Subscribe to WebSocket events
            webSocket.OnConnected += OnServerConnected;
            webSocket.OnDisconnected += OnServerDisconnected;
            webSocket.OnStatusReceived += OnServerStatus;
            webSocket.OnTranscriptionReceived += OnTranscription;
            webSocket.OnResponseTextReceived += OnResponseText;
            webSocket.OnAudioResponseReceived += OnAudioResponse;
            webSocket.OnLatencyReport += OnLatency;

            // Subscribe to microphone events
            microphone.OnRecordingComplete += OnRecordingComplete;
            microphone.OnRecordingStarted += () => SetState(NPCState.Listening);
            microphone.OnRecordingStopped += () => SetState(NPCState.Processing);
        }

        private void Update()
        {
            // Look at player when nearby
            if (_playerCamera != null)
            {
                float distance = Vector3.Distance(transform.position, _playerCamera.position);

                if (distance <= interactionDistance)
                {
                    Vector3 lookDir = _playerCamera.position - transform.position;
                    lookDir.y = 0; // Keep NPC upright
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir);
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            targetRot,
                            Time.deltaTime * lookAtPlayerSpeed
                        );
                    }
                }
            }

            // Check if NPC finished speaking
            if (CurrentState == NPCState.Speaking && !audioSource.isPlaying)
            {
                OnFinishedSpeaking();
            }
        }

        private void OnDestroy()
        {
            if (webSocket != null)
            {
                webSocket.OnConnected -= OnServerConnected;
                webSocket.OnDisconnected -= OnServerDisconnected;
                webSocket.OnStatusReceived -= OnServerStatus;
                webSocket.OnTranscriptionReceived -= OnTranscription;
                webSocket.OnResponseTextReceived -= OnResponseText;
                webSocket.OnAudioResponseReceived -= OnAudioResponse;
                webSocket.OnLatencyReport -= OnLatency;
            }

            if (microphone != null)
            {
                microphone.OnRecordingComplete -= OnRecordingComplete;
            }
        }

        // ─── State Management ──────────────────────────────────

        private void SetState(NPCState newState)
        {
            if (CurrentState == newState) return;

            Debug.Log($"[NPC] State: {CurrentState} → {newState}");
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);

            // Drive animations from state
            if (npcAnimator != null)
            {
                npcAnimator.SetNPCState(newState);
            }
        }

        // ─── Server Events ─────────────────────────────────────

        private void OnServerConnected()
        {
            Debug.Log($"[NPC] {npcName} connected to AI server");
            SetState(NPCState.Idle);

            // Play a greeting
            webSocket.SendTextInput("Greet the user who just put on their VR headset. Introduce yourself briefly as their assembly assistant.");
        }

        private void OnServerDisconnected()
        {
            Debug.LogWarning($"[NPC] {npcName} lost server connection");
            SetState(NPCState.Idle);
        }

        private void OnServerStatus(string status)
        {
            switch (status)
            {
                case "transcribing":
                    SetState(NPCState.Processing);
                    break;
                case "thinking":
                    SetState(NPCState.Processing);
                    break;
                case "speaking":
                    // Audio about to arrive
                    break;
                case "idle":
                    if (CurrentState != NPCState.Speaking)
                        SetState(NPCState.Idle);
                    break;
                case "no_speech_detected":
                    SetState(NPCState.Idle);
                    break;
            }
        }

        private void OnTranscription(string text)
        {
            LastTranscription = text;
            Debug.Log($"[NPC] Player said: \"{text}\"");
        }

        private void OnResponseText(string text)
        {
            LastResponse = text;
            Debug.Log($"[NPC] {npcName} says: \"{text}\"");
        }

        private void OnAudioResponse(byte[] wavBytes, int sampleRate)
        {
            // Convert WAV bytes to AudioClip
            AudioClip clip = WavToAudioClip(wavBytes, sampleRate);

            if (clip != null)
            {
                PlayResponse(clip);
            }
            else
            {
                Debug.LogError("[NPC] Failed to decode audio response");
                SetState(NPCState.Idle);
            }
        }

        private void OnLatency(float stt, float llm, float tts, float total)
        {
            LastLatency = $"STT: {stt:F2}s | LLM: {llm:F2}s | TTS: {tts:F2}s | Total: {total:F2}s";
            Debug.Log($"[NPC] Latency — {LastLatency}");
        }

        // ─── Microphone Events ─────────────────────────────────

        private void OnRecordingComplete(byte[] wavBytes)
        {
            Debug.Log($"[NPC] Sending {wavBytes.Length} bytes to server");
            SetState(NPCState.Processing);
            webSocket.SendAudio(wavBytes);
        }

        // ─── Audio Playback ────────────────────────────────────

        private void PlayResponse(AudioClip clip)
        {
            SetState(NPCState.Speaking);

            audioSource.clip = clip;
            audioSource.Play();

            // Tell lip sync to start
            if (lipSync != null)
            {
                lipSync.StartLipSync(audioSource);
            }

            Debug.Log($"[NPC] Playing response ({clip.length:F1}s)");
        }

        private void OnFinishedSpeaking()
        {
            SetState(NPCState.Idle);

            if (lipSync != null)
            {
                lipSync.StopLipSync();
            }

            Debug.Log("[NPC] Finished speaking, ready for input");
        }

        // ─── WAV Decoding ──────────────────────────────────────

        private AudioClip WavToAudioClip(byte[] wavData, int expectedSampleRate)
        {
            try
            {
                using var stream = new MemoryStream(wavData);
                using var reader = new BinaryReader(stream);

                // Read WAV header
                string riff = new string(reader.ReadChars(4));     // "RIFF"
                int fileSize = reader.ReadInt32();
                string wave = new string(reader.ReadChars(4));     // "WAVE"

                if (riff != "RIFF" || wave != "WAVE")
                {
                    Debug.LogError("[NPC] Invalid WAV header");
                    return null;
                }

                // Read chunks
                int channels = 1;
                int sampleRate = expectedSampleRate;
                int bitsPerSample = 16;
                byte[] audioData = null;

                while (stream.Position < stream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        int format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        int byteRate = reader.ReadInt32();
                        int blockAlign = reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();

                        // Skip any extra format bytes
                        if (chunkSize > 16)
                            reader.ReadBytes(chunkSize - 16);
                    }
                    else if (chunkId == "data")
                    {
                        audioData = reader.ReadBytes(chunkSize);
                    }
                    else
                    {
                        // Skip unknown chunks
                        reader.ReadBytes(chunkSize);
                    }
                }

                if (audioData == null || audioData.Length == 0)
                {
                    Debug.LogError("[NPC] No audio data in WAV");
                    return null;
                }

                // Convert bytes to float samples
                int bytesPerSample = bitsPerSample / 8;
                int sampleCount = audioData.Length / bytesPerSample;
                float[] samples = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    if (bitsPerSample == 16)
                    {
                        short s = BitConverter.ToInt16(audioData, i * 2);
                        samples[i] = s / 32768f;
                    }
                    else if (bitsPerSample == 8)
                    {
                        samples[i] = (audioData[i] - 128) / 128f;
                    }
                }

                // Create AudioClip
                AudioClip clip = AudioClip.Create(
                    "NPCResponse",
                    sampleCount / channels,
                    channels,
                    sampleRate,
                    false
                );
                clip.SetData(samples, 0);

                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NPC] WAV decode error: {e.Message}");
                return null;
            }
        }

        // ─── Public API ────────────────────────────────────────

        /// <summary>
        /// Send a text message to the NPC (bypasses microphone/STT).
        /// </summary>
        public void SendMessage(string text)
        {
            if (!webSocket.IsConnected)
            {
                Debug.LogWarning("[NPC] Not connected to server");
                return;
            }

            SetState(NPCState.Processing);
            webSocket.SendTextInput(text);
        }

        /// <summary>
        /// Reset the conversation context.
        /// </summary>
        public void ResetConversation()
        {
            webSocket.ResetConversation();
            LastTranscription = "";
            LastResponse = "";
            LastLatency = "";
            SetState(NPCState.Idle);
        }

        // ─── Debug UI ──────────────────────────────────────────

        private void OnGUI()
        {
            if (!showDebugUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 250));
            GUILayout.Label($"<b>NPC: {npcName}</b>");
            GUILayout.Label($"State: {CurrentState}");
            GUILayout.Label($"Connected: {webSocket.IsConnected}");
            GUILayout.Label($"Mic Level: {microphone.CurrentVolume:F3}");
            GUILayout.Label($"You: \"{LastTranscription}\"");
            GUILayout.Label($"NPC: \"{(LastResponse.Length > 80 ? LastResponse[..80] + "..." : LastResponse)}\"");
            GUILayout.Label($"Latency: {LastLatency}");
            GUILayout.EndArea();
        }
    }
}
