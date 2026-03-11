using System;
using System.IO;
using UnityEngine;

namespace VRAssistant.Audio
{
    /// <summary>
    /// Captures microphone audio from Quest 3 and provides WAV data.
    /// Supports push-to-talk and voice activity detection (VAD).
    /// </summary>
    public class MicrophoneCapture : MonoBehaviour
    {
        [Header("Recording Settings")]
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private int maxRecordingSeconds = 15;

        [Header("Voice Activity Detection")]
        [SerializeField] private bool useVAD = true;
        [SerializeField] private float silenceThreshold = 0.01f;
        [SerializeField] private float silenceTimeout = 1.5f;
        [SerializeField] private float minRecordingDuration = 0.5f;

        [Header("Push to Talk")]
        [SerializeField] private OVRInput.Button pushToTalkButton = OVRInput.Button.PrimaryIndexTrigger;
        [SerializeField] private bool usePushToTalk = true;

        // State
        public bool IsRecording { get; private set; }
        public float CurrentVolume { get; private set; }
        public event Action<byte[]> OnRecordingComplete;
        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;

        private AudioClip _micClip;
        private string _micDevice;
        private bool _isCapturing;
        private float _silenceTimer;
        private float _recordingStartTime;
        private int _lastSamplePos;

        // ─── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[Mic] No microphone found! Check Quest permissions.");
                return;
            }

            _micDevice = Microphone.devices[0];
            Debug.Log($"[Mic] Using device: {_micDevice}");

            // Auto-start VAD if push-to-talk is disabled
            if (!usePushToTalk)
            {
                StartContinuousListening();
            }
        }
        
        private void Update()
        {
            if (usePushToTalk)
            {
                HandlePushToTalk();
            }

            if (_isCapturing && useVAD && !usePushToTalk)
            {
                HandleVAD();
            }

            // Update volume meter
            if (_isCapturing)
            {
                CurrentVolume = GetCurrentMicLevel();
            }
        }

        private void OnDestroy()
        {
            StopCapture();
        }

        // ─── Push to Talk ──────────────────────────────────────

        private void HandlePushToTalk()
        {
            if (OVRInput.GetDown(pushToTalkButton))
            {
                StartRecording();
            }
            else if (OVRInput.GetUp(pushToTalkButton))
            {
                StopRecording();
            }
        }

        // ─── Voice Activity Detection ──────────────────────────

        /// <summary>
        /// Start continuous listening (VAD mode).
        /// Call this once; recording auto-starts/stops on voice detection.
        /// </summary>
        public void StartContinuousListening()
        {
            usePushToTalk = false;
            StartCapture();
        }

        private void HandleVAD()
        {
            float level = GetCurrentMicLevel();

            if (!IsRecording)
            {
                // Waiting for speech
                if (level > silenceThreshold)
                {
                    IsRecording = true;
                    _recordingStartTime = Time.time;
                    _lastSamplePos = Microphone.GetPosition(_micDevice);
                    _silenceTimer = 0f;
                    Debug.Log("[Mic] VAD: Speech detected, recording...");
                    OnRecordingStarted?.Invoke();
                }
            }
            else
            {
                // Currently recording
                if (level < silenceThreshold)
                {
                    _silenceTimer += Time.deltaTime;

                    if (_silenceTimer >= silenceTimeout)
                    {
                        float duration = Time.time - _recordingStartTime;
                        if (duration >= minRecordingDuration)
                        {
                            Debug.Log($"[Mic] VAD: Silence detected, finishing ({duration:F1}s)");
                            FinishRecording();
                        }
                        else
                        {
                            // Too short, discard
                            IsRecording = false;
                            _silenceTimer = 0f;
                        }
                    }
                }
                else
                {
                    _silenceTimer = 0f;
                }

                // Max duration safety
                if (Time.time - _recordingStartTime > maxRecordingSeconds)
                {
                    Debug.Log("[Mic] Max recording duration reached");
                    FinishRecording();
                }
            }
        }

        // ─── Recording Control ─────────────────────────────────

        public void StartRecording()
        {
            if (IsRecording) return;

            StartCapture();
            IsRecording = true;
            _recordingStartTime = Time.time;
            _lastSamplePos = Microphone.GetPosition(_micDevice);
            _silenceTimer = 0f;
            Debug.Log("[Mic] Recording started");
            OnRecordingStarted?.Invoke();
        }

        public void StopRecording()
        {
            if (!IsRecording) return;

            float duration = Time.time - _recordingStartTime;
            if (duration >= minRecordingDuration)
            {
                FinishRecording();
            }
            else
            {
                IsRecording = false;
                Debug.Log("[Mic] Recording too short, discarded");
            }
        }

        private void StartCapture()
        {
            if (_isCapturing) return;

            _micClip = Microphone.Start(_micDevice, true, maxRecordingSeconds, sampleRate);
            _isCapturing = true;

            // Wait for mic to start
            int safety = 0;
            while (Microphone.GetPosition(_micDevice) <= 0 && safety < 1000)
            {
                safety++;
            }
        }

        private void StopCapture()
        {
            if (!_isCapturing) return;

            Microphone.End(_micDevice);
            _isCapturing = false;
            IsRecording = false;
        }

        private void FinishRecording()
        {
            IsRecording = false;

            if (_micClip == null) return;

            int currentPos = Microphone.GetPosition(_micDevice);
            int startPos = _lastSamplePos;

            // Calculate samples to extract
            int totalSamples = _micClip.samples;
            int sampleCount;

            if (currentPos >= startPos)
            {
                sampleCount = currentPos - startPos;
            }
            else
            {
                // Wrapped around
                sampleCount = (totalSamples - startPos) + currentPos;
            }

            if (sampleCount <= 0)
            {
                Debug.LogWarning("[Mic] No samples captured");
                return;
            }

            // Extract audio data
            float[] samples = new float[sampleCount];

            if (currentPos >= startPos)
            {
                _micClip.GetData(samples, startPos);
            }
            else
            {
                // Handle wrap-around
                int firstPart = totalSamples - startPos;
                float[] temp1 = new float[firstPart];
                float[] temp2 = new float[currentPos];

                _micClip.GetData(temp1, startPos);
                _micClip.GetData(temp2, 0);

                Array.Copy(temp1, 0, samples, 0, firstPart);
                Array.Copy(temp2, 0, samples, firstPart, currentPos);
            }

            // Convert to WAV bytes
            byte[] wavBytes = ConvertToWav(samples, sampleRate, 1);

            Debug.Log($"[Mic] Recording complete: {sampleCount} samples, {wavBytes.Length} bytes WAV");

            OnRecordingStopped?.Invoke();
            OnRecordingComplete?.Invoke(wavBytes);
        }

        // ─── Audio Level ───────────────────────────────────────

        private float GetCurrentMicLevel()
        {
            if (_micClip == null || !_isCapturing) return 0f;

            int pos = Microphone.GetPosition(_micDevice);
            if (pos <= 0) return 0f;

            int sampleWindow = Mathf.Min(256, pos);
            float[] samples = new float[sampleWindow];

            int offset = Mathf.Max(0, pos - sampleWindow);
            _micClip.GetData(samples, offset);

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += Mathf.Abs(samples[i]);
            }

            return sum / sampleWindow;
        }

        // ─── WAV Encoding ──────────────────────────────────────

        private static byte[] ConvertToWav(float[] samples, int sampleRate, int channels)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            int bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;
            int dataSize = samples.Length * blockAlign;

            // WAV Header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);                       // Chunk size
            writer.Write((short)1);                 // PCM format
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);

            // data chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            // Audio data (float → int16)
            foreach (float sample in samples)
            {
                short intSample = (short)Mathf.Clamp(sample * 32767f, -32768f, 32767f);
                writer.Write(intSample);
            }

            return stream.ToArray();
        }
    }
}
