using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRAssistant.NPC;

namespace VRAssistant.UI
{
    /// <summary>
    /// World-space UI panel that shows conversation status above the NPC.
    /// Displays: connection state, who's talking, subtitles, and latency.
    ///
    /// SETUP:
    /// 1. Create a Canvas (World Space) as child of NPC
    /// 2. Position it above the NPC's head
    /// 3. Add TextMeshPro elements for status, subtitles, etc.
    /// 4. Assign references in Inspector
    /// </summary>
    public class NPCStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCController npcController;
        [SerializeField] private Network.WebSocketClient webSocket;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI latencyText;
        [SerializeField] private Image micIndicator;
        [SerializeField] private Image connectionIndicator;

        [Header("Settings")]
        [SerializeField] private float subtitleDisplayTime = 5f;
        [SerializeField] private bool faceCamera = true;

        [Header("Colors")]
        [SerializeField] private Color idleColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color listeningColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color processingColor = new Color(0.9f, 0.7f, 0.1f);
        [SerializeField] private Color speakingColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color connectedColor = new Color(0.2f, 0.9f, 0.2f);
        [SerializeField] private Color disconnectedColor = new Color(0.9f, 0.2f, 0.2f);

        private float _subtitleTimer;
        private Transform _cameraTransform;

        private void Start()
        {
            _cameraTransform = Camera.main?.transform;

            if (npcController != null)
            {
                npcController.OnStateChanged += OnNPCStateChanged;
            }

            if (webSocket != null)
            {
                webSocket.OnConnected += () => UpdateConnectionUI(true);
                webSocket.OnDisconnected += () => UpdateConnectionUI(false);
                webSocket.OnTranscriptionReceived += (text) => ShowSubtitle($"You: {text}");
                webSocket.OnResponseTextReceived += (text) => ShowSubtitle($"NPC: {text}");
                webSocket.OnLatencyReport += OnLatencyUpdate;
            }

            // Initial state
            UpdateConnectionUI(false);
            UpdateStatusUI(NPCController.NPCState.Idle);
        }

        private void Update()
        {
            // Face camera (billboard)
            if (faceCamera && _cameraTransform != null)
            {
                transform.LookAt(
                    transform.position + _cameraTransform.forward,
                    Vector3.up
                );
            }

            // Subtitle timeout
            if (_subtitleTimer > 0)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0 && subtitleText != null)
                {
                    subtitleText.text = "";
                }
            }
        }

        private void OnNPCStateChanged(NPCController.NPCState state)
        {
            UpdateStatusUI(state);
        }

        private void UpdateStatusUI(NPCController.NPCState state)
        {
            if (statusText == null) return;

            switch (state)
            {
                case NPCController.NPCState.Idle:
                    statusText.text = "Ready";
                    statusText.color = idleColor;
                    if (micIndicator) micIndicator.color = idleColor;
                    break;

                case NPCController.NPCState.Listening:
                    statusText.text = "Listening...";
                    statusText.color = listeningColor;
                    if (micIndicator) micIndicator.color = listeningColor;
                    break;

                case NPCController.NPCState.Processing:
                    statusText.text = "Thinking...";
                    statusText.color = processingColor;
                    if (micIndicator) micIndicator.color = processingColor;
                    break;

                case NPCController.NPCState.Speaking:
                    statusText.text = "Speaking";
                    statusText.color = speakingColor;
                    if (micIndicator) micIndicator.color = speakingColor;
                    break;
            }
        }

        private void UpdateConnectionUI(bool connected)
        {
            if (connectionIndicator != null)
            {
                connectionIndicator.color = connected ? connectedColor : disconnectedColor;
            }
        }

        private void ShowSubtitle(string text)
        {
            if (subtitleText == null) return;

            // Truncate for VR readability
            if (text.Length > 120)
                text = text[..120] + "...";

            subtitleText.text = text;
            _subtitleTimer = subtitleDisplayTime;
        }

        private void OnLatencyUpdate(float stt, float llm, float tts, float total)
        {
            if (latencyText != null)
            {
                latencyText.text = $"{total:F1}s";
            }
        }
    }
}
