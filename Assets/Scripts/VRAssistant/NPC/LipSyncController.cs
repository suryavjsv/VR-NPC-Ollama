using UnityEngine;
using System;

namespace VRAssistant.NPC
{
    /// <summary>
    /// Drives viseme blend shapes from audio amplitude analysis.
    /// No OVR Lip Sync dependency — works with any Unity project.
    ///
    /// How it works:
    /// - OnAudioFilterRead captures audio samples from the AudioSource
    /// - Amplitude is analyzed to detect vowel-like openness
    /// - Visemes are selected based on amplitude bands and randomized
    ///   to create natural-looking mouth movement
    /// - Blend shapes are smoothly interpolated each frame
    ///
    /// SETUP:
    /// 1. Attach to the same GameObject as the AudioSource
    /// 2. Assign the SkinnedMeshRenderer with viseme blend shapes
    /// 3. Map the 15 viseme blend shape indices
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LipSyncController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The SkinnedMeshRenderer with viseme blend shapes")]
        [SerializeField] private SkinnedMeshRenderer faceMesh;

        [Header("Viseme Blend Shape Mapping")]
        [Tooltip("Blend shape index for each of the 15 visemes. Set to -1 to skip.")]
        [SerializeField] private int[] visemeBlendShapeIndices = new int[15];

        [Header("Settings")]
        [SerializeField] private float blendShapeMultiplier = 1.5f;
        [SerializeField] private float smoothing = 0.65f;
        [SerializeField] private float gain = 1.0f;
        [SerializeField] private float silenceThreshold = 0.01f;

        [Header("Mouth Behavior")]
        [Tooltip("How quickly the mouth opens")]
        [SerializeField] private float attackSpeed = 15f;
        [Tooltip("How quickly the mouth closes")]
        [SerializeField] private float releaseSpeed = 8f;
        [Tooltip("How often to switch active viseme (seconds)")]
        [SerializeField] private float visemeSwitchInterval = 0.12f;

        // Audio analysis (written from audio thread)
        private float _currentAmplitude = 0f;
        private float _peakAmplitude = 0f;
        private readonly object _lock = new object();

        // Viseme state (main thread only)
        private float[] _currentVisemes = new float[15];
        private float[] _targetVisemes = new float[15];
        private int _activeViseme = 0;
        private float _visemeSwitchTimer = 0f;
        private bool _isActive = false;
        private float _smoothedAmplitude = 0f;

        // Viseme grouping by mouth shape type
        private static readonly int[][] VisemeGroups = new int[][]
        {
            new[] { 0 },           // sil - silence
            new[] { 1, 2, 7 },     // PP, FF, SS - lips together / narrow
            new[] { 3, 4, 5, 6 },  // TH, DD, kk, CH - tongue/teeth
            new[] { 8, 9 },        // nn, RR - mid open
            new[] { 10, 11, 12 },  // aa, E, ih - open vowels
            new[] { 13, 14 },      // oh, ou - rounded open
        };

        // ─── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            for (int i = 0; i < 15; i++)
            {
                _currentVisemes[i] = 0f;
                _targetVisemes[i] = 0f;
            }
        }

        private void LateUpdate()
        {
            if (!_isActive || faceMesh == null) return;

            // Get amplitude from audio thread
            float amplitude;
            lock (_lock)
            {
                amplitude = _currentAmplitude;
            }

            // Smooth the amplitude
            float targetSpeed = amplitude > _smoothedAmplitude ? attackSpeed : releaseSpeed;
            _smoothedAmplitude = Mathf.Lerp(_smoothedAmplitude, amplitude, Time.deltaTime * targetSpeed);

            // Determine mouth openness (0-1)
            float openness = Mathf.Clamp01(_smoothedAmplitude * blendShapeMultiplier / 0.3f);

            // Switch visemes periodically for natural movement
            _visemeSwitchTimer += Time.deltaTime;
            if (_visemeSwitchTimer >= visemeSwitchInterval && openness > silenceThreshold)
            {
                _visemeSwitchTimer = 0f;
                SelectVisemeFromAmplitude(openness);
            }

            // If silent, go to silence viseme
            if (openness <= silenceThreshold)
            {
                _activeViseme = 0;
                _visemeSwitchTimer = 0f;
            }

            // Build target viseme weights
            for (int i = 0; i < 15; i++)
            {
                _targetVisemes[i] = 0f;
            }

            if (openness > silenceThreshold)
            {
                // Primary viseme gets most weight
                _targetVisemes[_activeViseme] = openness;

                // Add subtle jawOpen effect via aa viseme for realism
                int aaIndex = 10;
                if (_activeViseme != aaIndex)
                {
                    _targetVisemes[aaIndex] = openness * 0.3f;
                }
            }

            // Smooth and apply blend shapes
            for (int i = 0; i < 15; i++)
            {
                _currentVisemes[i] = Mathf.Lerp(
                    _currentVisemes[i],
                    _targetVisemes[i],
                    1f - smoothing
                );

                if (i < visemeBlendShapeIndices.Length && visemeBlendShapeIndices[i] >= 0)
                {
                    faceMesh.SetBlendShapeWeight(
                        visemeBlendShapeIndices[i],
                        Mathf.Clamp(_currentVisemes[i] * 100f, 0f, 100f)
                    );
                }
            }
        }

        // ─── Viseme Selection ──────────────────────────────────

        private void SelectVisemeFromAmplitude(float openness)
        {
            int groupIndex;

            if (openness < 0.15f)
                groupIndex = 1;      // lips together (PP, FF, SS)
            else if (openness < 0.3f)
                groupIndex = 2;      // tongue/teeth (TH, DD, kk, CH)
            else if (openness < 0.5f)
                groupIndex = 3;      // mid open (nn, RR)
            else if (openness < 0.75f)
                groupIndex = 4;      // open vowels (aa, E, ih)
            else
                groupIndex = 5;      // rounded open (oh, ou)

            // Add randomness for natural look
            float rand = UnityEngine.Random.value;
            if (rand < 0.2f && groupIndex > 1)
                groupIndex--;
            else if (rand > 0.8f && groupIndex < VisemeGroups.Length - 1)
                groupIndex++;

            int[] group = VisemeGroups[groupIndex];
            _activeViseme = group[UnityEngine.Random.Range(0, group.Length)];
        }

        // ─── Audio Processing ──────────────────────────────────

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isActive) return;

            float sum = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i] * gain;
                sum += sample * sample;
            }

            float rms = Mathf.Sqrt(sum / data.Length);

            lock (_lock)
            {
                _currentAmplitude = rms;
                if (rms > _peakAmplitude)
                    _peakAmplitude = rms;
            }
        }

        // ─── Public API ────────────────────────────────────────

        public void StartLipSync(AudioSource source)
        {
            _isActive = true;
            _smoothedAmplitude = 0f;
            _visemeSwitchTimer = 0f;

            lock (_lock)
            {
                _currentAmplitude = 0f;
                _peakAmplitude = 0f;
            }

            Debug.Log("[LipSync] Started (amplitude-based)");
        }

        public void StopLipSync()
        {
            _isActive = false;

            if (faceMesh != null)
            {
                for (int i = 0; i < visemeBlendShapeIndices.Length; i++)
                {
                    if (visemeBlendShapeIndices[i] >= 0)
                    {
                        faceMesh.SetBlendShapeWeight(visemeBlendShapeIndices[i], 0f);
                    }
                }
            }

            Array.Clear(_currentVisemes, 0, _currentVisemes.Length);
            Array.Clear(_targetVisemes, 0, _targetVisemes.Length);
            _smoothedAmplitude = 0f;

            Debug.Log("[LipSync] Stopped");
        }

        [ContextMenu("Auto-Map Blend Shapes")]
        public void AutoMapBlendShapes()
        {
            if (faceMesh == null)
            {
                Debug.LogError("[LipSync] No SkinnedMeshRenderer assigned");
                return;
            }

            string[][] namePatterns = {
                new[] { "sil", "silence", "rest", "viseme_sil", "viseme_00" },
                new[] { "pp", "viseme_pp", "p_b_m", "viseme_01" },
                new[] { "ff", "viseme_ff", "f_v", "viseme_02" },
                new[] { "th", "viseme_th", "viseme_03" },
                new[] { "dd", "viseme_dd", "t_d", "viseme_04" },
                new[] { "kk", "viseme_kk", "k_g", "viseme_05" },
                new[] { "ch", "viseme_ch", "ch_j_sh", "viseme_06" },
                new[] { "ss", "viseme_ss", "s_z", "viseme_07" },
                new[] { "nn", "viseme_nn", "n_l", "viseme_08" },
                new[] { "rr", "viseme_rr", "viseme_09" },
                new[] { "aa", "viseme_aa", "viseme_10" },
                new[] { "e", "viseme_e", "viseme_11" },
                new[] { "ih", "viseme_ih", "viseme_12" },
                new[] { "oh", "viseme_oh", "viseme_13" },
                new[] { "ou", "viseme_ou", "viseme_14" }
            };

            string[] visemeLabels = {
                "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS",
                "nn", "RR", "aa", "E", "ih", "oh", "ou"
            };

            var mesh = faceMesh.sharedMesh;
            int blendShapeCount = mesh.blendShapeCount;
            int mapped = 0;

            for (int v = 0; v < 15; v++)
            {
                visemeBlendShapeIndices[v] = -1;
                bool found = false;

                foreach (string pattern in namePatterns[v])
                {
                    for (int b = 0; b < blendShapeCount; b++)
                    {
                        string bsName = mesh.GetBlendShapeName(b).ToLower();
                        if (bsName.Contains(pattern.ToLower()))
                        {
                            visemeBlendShapeIndices[v] = b;
                            Debug.Log($"[LipSync] Mapped {visemeLabels[v]} -> '{mesh.GetBlendShapeName(b)}' (index {b})");
                            mapped++;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (!found)
                    Debug.LogWarning($"[LipSync] No blend shape found for viseme: {visemeLabels[v]}");
            }

            Debug.Log($"[LipSync] Auto-mapped {mapped}/15 visemes");
        }

        [ContextMenu("List All Blend Shapes")]
        public void ListBlendShapes()
        {
            if (faceMesh == null)
            {
                Debug.LogError("[LipSync] No SkinnedMeshRenderer assigned");
                return;
            }

            var mesh = faceMesh.sharedMesh;
            Debug.Log($"[LipSync] Blend shapes on '{faceMesh.name}' ({mesh.blendShapeCount} total):");
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                Debug.Log($"  [{i}] {mesh.GetBlendShapeName(i)}");
            }
        }
    }
}