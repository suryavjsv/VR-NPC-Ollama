using UnityEngine;
using TMPro;

namespace VRAssistant.Local
{
    /// <summary>
    /// Simple subtitle UI for the on-device local scene.
    /// Shows player speech, NPC speech, and status messages.
    ///
    /// SETUP:
    /// 1. Create a Canvas (Screen Space - Overlay for testing, World Space for VR)
    /// 2. Add three TextMeshPro text elements
    /// 3. Assign references
    /// </summary>
    public class LocalSubtitleUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI playerText;
        [SerializeField] private TextMeshProUGUI npcText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings")]
        [SerializeField] private float subtitleDuration = 8f;
        [SerializeField] private Color playerColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color npcColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color statusColor = new Color(0.7f, 0.7f, 0.7f);

        [Header("Billboard")]
        [SerializeField] private bool faceCamera = true;

        private float _playerTimer;
        private float _npcTimer;
        private float _statusTimer;
        private Transform _cameraTransform;

        private void Start()
        {
            _cameraTransform = Camera.main?.transform;

            if (playerText != null) playerText.color = playerColor;
            if (npcText != null) npcText.color = npcColor;
            if (statusText != null) statusText.color = statusColor;

            // TEMP TEST
            if (statusText != null) statusText.text = "TEST TEXT VISIBLE";
            if (playerText != null) playerText.text = "PLAYER TEXT TEST";
            if (npcText != null) npcText.text = "NPC TEXT TEST";

            //ClearAll();  // comment this out temporarily too!
        }

        private void Update()
        {
            // Billboard
            if (faceCamera && _cameraTransform != null)
            {
                transform.LookAt(
                    transform.position + _cameraTransform.forward,
                    Vector3.up
                );
            }

            // Timers
            if (_playerTimer > 0)
            {
                _playerTimer -= Time.deltaTime;
                if (_playerTimer <= 0 && playerText != null)
                    playerText.text = "";
            }

            if (_npcTimer > 0)
            {
                _npcTimer -= Time.deltaTime;
                if (_npcTimer <= 0 && npcText != null)
                    npcText.text = "";
            }

            if (_statusTimer > 0)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0 && statusText != null)
                    statusText.text = "";
            }
        }

        public void ShowPlayerText(string text)
        {
            if (playerText != null)
            {
                playerText.text = $"You: {text}";
                _playerTimer = subtitleDuration;
            }
        }

        public void ShowNPCText(string text)
        {
            if (npcText != null)
            {
                string clean = text.Trim().Trim('"');
                npcText.text = $"NPC: {clean}";
                _npcTimer = subtitleDuration;
            }
        }

        public void ShowStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
                _statusTimer = 3f;
            }
        }

        public void ClearAll()
        {
            if (playerText != null) playerText.text = "";
            if (npcText != null) npcText.text = "";
            if (statusText != null) statusText.text = "";
        }
    }
}
