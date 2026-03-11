using UnityEngine;
using TMPro;
using VRAssistant.Network;

namespace VRAssistant.UI
{
    /// <summary>
    /// Simple subtitle display showing what you said and what the NPC said.
    /// 
    /// SETUP:
    /// 1. Create a Canvas (Screen Space - Overlay for editor testing)
    /// 2. Add two TextMeshPro - Text objects as children
    /// 3. Position one at bottom-left (Player), one at bottom-right (NPC)
    /// 4. Assign references in Inspector
    /// 5. Drag your NetworkManager into the WebSocket field
    /// </summary>
    public class SubtitleUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocket;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI playerText;
        [SerializeField] private TextMeshProUGUI npcText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings")]
        [SerializeField] private float subtitleDuration = 8f;
        [SerializeField] private Color playerColor = new Color(0.3f, 0.85f, 1f);
        [SerializeField] private Color npcColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color statusColor = new Color(0.7f, 0.7f, 0.7f);

        private float _playerTimer;
        private float _npcTimer;
        private float _statusTimer;

        private void Start()
        {
            if (webSocket == null)
            {
                Debug.LogError("[SubtitleUI] WebSocket reference not assigned!");
                return;
            }

            // Subscribe to events
            webSocket.OnTranscriptionReceived += OnPlayerSpoke;
            webSocket.OnResponseTextReceived += OnNPCSpoke;
            webSocket.OnStatusReceived += OnStatus;
            webSocket.OnConnected += () => ShowStatus("Connected to server");
            webSocket.OnDisconnected += () => ShowStatus("Disconnected");

            // Set colors
            if (playerText != null) playerText.color = playerColor;
            if (npcText != null) npcText.color = npcColor;
            if (statusText != null) statusText.color = statusColor;

            // Clear
            ClearAll();
        }

        private void Update()
        {
            // Fade out timers
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

        private void OnPlayerSpoke(string text)
        {
            if (playerText != null)
            {
                playerText.text = $"You: {text}";
                _playerTimer = subtitleDuration;
            }
        }

        private void OnNPCSpoke(string text)
        {
            if (npcText != null)
            {
                // Clean up double quotes from LLM
                string clean = text.Trim().Trim('"');
                npcText.text = $"NPC: {clean}";
                _npcTimer = subtitleDuration;
            }
        }

        private void OnStatus(string status)
        {
            ShowStatus(status);
        }

        private void ShowStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
                _statusTimer = 3f;
            }
        }

        private void ClearAll()
        {
            if (playerText != null) playerText.text = "";
            if (npcText != null) npcText.text = "";
            if (statusText != null) statusText.text = "";
        }

        private void OnDestroy()
        {
            if (webSocket != null)
            {
                webSocket.OnTranscriptionReceived -= OnPlayerSpoke;
                webSocket.OnResponseTextReceived -= OnNPCSpoke;
                webSocket.OnStatusReceived -= OnStatus;
            }
        }
    }
}