using System;
using System.Collections;
using UnityEngine;

namespace VRAssistant.Local
{
    /// <summary>
    /// TTS using Android TextToSpeech.speak() directly — no sherpa-onnx, no native crash.
    /// Audio plays through Quest 3 speakers. Lipsync driven by estimated duration timing.
    /// </summary>
    public class LocalTTSManager : MonoBehaviour
    {
        [Header("Lipsync (optional)")]
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private int jawBlendShapeIndex = 0;
        [SerializeField] private float lipSyncSpeed = 8f;

        public bool IsReady   { get; private set; } = false;
        public bool IsSpeaking { get; private set; } = false;

        public event Action OnSpeakStart;
        public event Action OnSpeakEnd;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _tts;
#endif

        private float   _jawTarget   = 0f;
        private float   _jawCurrent  = 0f;
        private bool    _jawAnimate  = false;
        private Coroutine _speakCoroutine;

        // ── Android TTS init callback ──────────────────────────────────────
#if UNITY_ANDROID && !UNITY_EDITOR
        private class TtsInitListener : AndroidJavaProxy
        {
            private readonly Action<int> _cb;
            public TtsInitListener(Action<int> cb)
                : base("android.speech.tts.TextToSpeech$OnInitListener") => _cb = cb;
            public void onInit(int status) => _cb(status);
        }
#endif

        // ── MonoBehaviour ──────────────────────────────────────────────────
        private void Update()
        {
            if (!_jawAnimate) return;
            _jawCurrent = Mathf.Lerp(_jawCurrent, _jawTarget, Time.deltaTime * lipSyncSpeed);
            if (faceRenderer != null && jawBlendShapeIndex >= 0)
                faceRenderer.SetBlendShapeWeight(jawBlendShapeIndex, _jawCurrent);
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _tts?.Call("stop");
            _tts?.Call("shutdown");
            _tts?.Dispose();
#endif
        }

        // ── Public API ─────────────────────────────────────────────────────
        public IEnumerator Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[TTS] Initialising Android TTS...");

            bool done  = false;
            bool ok    = false;

            var context = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                              .GetStatic<AndroidJavaObject>("currentActivity");

            _tts = new AndroidJavaObject(
                "android.speech.tts.TextToSpeech",
                context,
                new TtsInitListener(status => { ok = (status == 0); done = true; })
            );

            // Wait up to 5 seconds for init
            float t = 5f;
            while (!done && t > 0f) { t -= Time.deltaTime; yield return null; }

            if (!ok)
            {
                Debug.LogError("[TTS] Android TTS init failed or timed out!");
                yield break;
            }

            // Set English (US)
            var locale = new AndroidJavaClass("java.util.Locale")
                             .GetStatic<AndroidJavaObject>("US");
            int langResult = _tts.Call<int>("setLanguage", locale);
            if (langResult < 0)
            {
                Debug.LogError($"[TTS] Language not available: {langResult}");
                yield break;
            }

            // Optionally raise pitch/speed slightly for a more natural voice
            _tts.Call<int>("setSpeechRate",  1.0f);
            _tts.Call<int>("setPitch",       1.0f);

            IsReady = true;
            Debug.Log("[TTS] Android TTS ready ✓");
#else
            // Editor: pretend it's ready immediately
            yield return null;
            IsReady = true;
            Debug.Log("[TTS] Editor mode — Android TTS stubbed.");
#endif
        }

        public void Speak(string text)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[TTS] Speak called but not ready.");
                // Still fire OnSpeakEnd so NPC state machine doesn't get stuck
                OnSpeakEnd?.Invoke();
                return;
            }

            if (_speakCoroutine != null) StopCoroutine(_speakCoroutine);
            _speakCoroutine = StartCoroutine(SpeakCoroutine(text));
        }

        public void StopSpeaking()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _tts?.Call<int>("stop");
#endif
            if (_speakCoroutine != null) { StopCoroutine(_speakCoroutine); _speakCoroutine = null; }
            FinishLipSync();
            IsSpeaking = false;
        }

        // ── Internal ───────────────────────────────────────────────────────
        private IEnumerator SpeakCoroutine(string text)
        {
            IsSpeaking = true;
            OnSpeakStart?.Invoke();
            StartLipSync();

            Debug.Log($"[TTS] Speaking: {text}");

#if UNITY_ANDROID && !UNITY_EDITOR
            // QUEUE_FLUSH = 0  — interrupts any ongoing speech
            var bundle = new AndroidJavaObject("android.os.Bundle");
            string uttId = "utt_" + Time.frameCount;
            _tts.Call<int>("speak", text, 0, bundle, uttId);
#endif

            // Estimate duration from word count (~130 wpm)
            int words = text.Trim().Split(new char[]{' ','\n','\r'},
                            StringSplitOptions.RemoveEmptyEntries).Length;
            float duration = Mathf.Max(1.5f, (words / 130f) * 60f);

            // Animate jaw open→close during speech
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Oscillate jaw naturally
                _jawTarget = 50f + 40f * Mathf.Sin(elapsed * 7f);
                elapsed   += Time.deltaTime;
                yield return null;
            }

            FinishLipSync();
            IsSpeaking      = false;
            _speakCoroutine = null;
            Debug.Log("[TTS] Finished speaking.");
            OnSpeakEnd?.Invoke();
        }

        private void StartLipSync()
        {
            _jawAnimate = true;
            _jawTarget  = 50f;
        }

        private void FinishLipSync()
        {
            _jawAnimate = false;
            _jawTarget  = 0f;
            _jawCurrent = 0f;
            if (faceRenderer != null && jawBlendShapeIndex >= 0)
                faceRenderer.SetBlendShapeWeight(jawBlendShapeIndex, 0f);
        }
    }
}