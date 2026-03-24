using UnityEngine;
using System;

namespace VRAssistant.NPC
{
    /// <summary>
    /// Drives viseme blend shapes from audio amplitude analysis.
    /// No OVR Lip Sync dependency — works with any Unity project.
    ///
    /// Supports driving both face mesh and teeth mesh blend shapes
    /// simultaneously for realistic mouth movement.
    ///
    /// SETUP:
    /// 1. Attach to the same GameObject as the AudioSource
    /// 2. Assign the face SkinnedMeshRenderer (e.g., AvatarHead)
    /// 3. Assign the teeth SkinnedMeshRenderer (e.g., AvatarTeethLower) [optional]
    /// 4. Map the 15 viseme blend shape indices for each mesh
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LipSyncController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The SkinnedMeshRenderer with viseme blend shapes (face/head)")]
        [SerializeField] private SkinnedMeshRenderer faceMesh;

        [Tooltip("The SkinnedMeshRenderer for lower teeth (optional)")]
        [SerializeField] private SkinnedMeshRenderer teethMesh;

        [Header("Face Viseme Blend Shape Mapping")]
        [Tooltip("Blend shape index for each of the 15 visemes on face mesh. Set to -1 to skip.")]
        [SerializeField] private int[] visemeBlendShapeIndices = new int[15];

        [Header("Teeth Viseme Blend Shape Mapping")]
        [Tooltip("Blend shape index for each of the 15 visemes on teeth mesh. Set to -1 to skip.")]
        [SerializeField] private int[] teethBlendShapeIndices = new int[15];

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

        // ─── Viseme Names (for reference) ──────────────────────
        // 0:sil  1:PP  2:FF  3:TH  4:DD  5:kk  6:CH  7:SS
        // 8:nn   9:RR  10:aa 11:E  12:ih 13:oh 14:ou

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

                float weight = Mathf.Clamp(_currentVisemes[i] * 100f, 0f, 100f);

                // Drive face mesh
                if (i < visemeBlendShapeIndices.Length && visemeBlendShapeIndices[i] >= 0)
                {
                    faceMesh.SetBlendShapeWeight(visemeBlendShapeIndices[i], weight);
                }

                // Drive teeth mesh
                if (teethMesh != null && i < teethBlendShapeIndices.Length && teethBlendShapeIndices[i] >= 0)
                {
                    teethMesh.SetBlendShapeWeight(teethBlendShapeIndices[i], weight);
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

            // Reset face blend shapes
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

            // Reset teeth blend shapes
            if (teethMesh != null)
            {
                for (int i = 0; i < teethBlendShapeIndices.Length; i++)
                {
                    if (teethBlendShapeIndices[i] >= 0)
                    {
                        teethMesh.SetBlendShapeWeight(teethBlendShapeIndices[i], 0f);
                    }
                }
            }

            Array.Clear(_currentVisemes, 0, _currentVisemes.Length);
            Array.Clear(_targetVisemes, 0, _targetVisemes.Length);
            _smoothedAmplitude = 0f;

            Debug.Log("[LipSync] Stopped");
        }

        // ─── Auto Mapping ──────────────────────────────────────

        private static readonly string[][] NamePatterns = {
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

        private static readonly string[] VisemeLabels = {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS",
            "nn", "RR", "aa", "E", "ih", "oh", "ou"
        };

        private int MapBlendShapes(SkinnedMeshRenderer mesh, int[] indices, string meshName)
        {
            if (mesh == null) return 0;

            var meshData = mesh.sharedMesh;
            int blendShapeCount = meshData.blendShapeCount;
            int mapped = 0;

            for (int v = 0; v < 15; v++)
            {
                indices[v] = -1;
                bool found = false;

                foreach (string pattern in NamePatterns[v])
                {
                    for (int b = 0; b < blendShapeCount; b++)
                    {
                        string bsName = meshData.GetBlendShapeName(b).ToLower();
                        if (bsName.Contains(pattern.ToLower()))
                        {
                            indices[v] = b;
                            Debug.Log($"[LipSync] {meshName} mapped {VisemeLabels[v]} -> '{meshData.GetBlendShapeName(b)}' (index {b})");
                            mapped++;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (!found)
                    Debug.LogWarning($"[LipSync] {meshName} no blend shape found for viseme: {VisemeLabels[v]}");
            }

            return mapped;
        }

        [ContextMenu("Auto-Map Blend Shapes")]
        public void AutoMapBlendShapes()
        {
            if (faceMesh == null)
            {
                Debug.LogError("[LipSync] No face SkinnedMeshRenderer assigned");
                return;
            }

            // Map face mesh
            int faceMapped = MapBlendShapes(faceMesh, visemeBlendShapeIndices, "Face");
            Debug.Log($"[LipSync] Face auto-mapped {faceMapped}/15 visemes");

            // Map teeth mesh if assigned
            if (teethMesh != null)
            {
                int teethMapped = MapBlendShapes(teethMesh, teethBlendShapeIndices, "Teeth");
                Debug.Log($"[LipSync] Teeth auto-mapped {teethMapped}/15 visemes");
            }
            else
            {
                Debug.Log("[LipSync] No teeth mesh assigned, skipping teeth mapping");
            }
        }

        [ContextMenu("List All Blend Shapes")]
        public void ListBlendShapes()
        {
            if (faceMesh != null)
            {
                var mesh = faceMesh.sharedMesh;
                Debug.Log($"[LipSync] Face blend shapes on '{faceMesh.name}' ({mesh.blendShapeCount} total):");
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    Debug.Log($"  [{i}] {mesh.GetBlendShapeName(i)}");
            }

            if (teethMesh != null)
            {
                var mesh = teethMesh.sharedMesh;
                Debug.Log($"[LipSync] Teeth blend shapes on '{teethMesh.name}' ({mesh.blendShapeCount} total):");
                for (int i = 0; i < mesh.blendShapeCount; i++)
                    Debug.Log($"  [{i}] {mesh.GetBlendShapeName(i)}");
            }
        }
    }
}