using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VRAssistant.Network
{
    /// <summary>
    /// WebSocket client for communicating with the Python AI NPC server.
    /// Handles sending audio and receiving text + audio responses.
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private string serverIP = "192.168.1.100";
        [SerializeField] private int serverPort = 8765;
        [SerializeField] private bool autoConnect = true;
        [SerializeField] private float reconnectDelay = 3f;

        // Connection state
        public bool IsConnected => _ws?.State == WebSocketState.Open;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnStatusReceived;
        public event Action<string> OnTranscriptionReceived;
        public event Action<string> OnResponseTextReceived;
        public event Action<byte[], int> OnAudioResponseReceived;  // audio bytes, sample rate
        public event Action<float, float, float, float> OnLatencyReport; // stt, llm, tts, total

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new();
        private bool _expectingAudioBytes = false;
        private int _expectedAudioSampleRate = 22050;

        private string ServerUrl => $"ws://{serverIP}:{serverPort}/ws";

        // ─── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            if (autoConnect)
            {
                Connect();
            }
        }

        private void Update()
        {
            // Process callbacks on main thread
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        // ─── Connection ────────────────────────────────────────

        public async void Connect()
        {
            if (IsConnected) return;

            _cts = new CancellationTokenSource();

            try
            {
                _ws = new ClientWebSocket();
                _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

                Debug.Log($"[WebSocket] Connecting to {ServerUrl}...");
                await _ws.ConnectAsync(new Uri(ServerUrl), _cts.Token);
                Debug.Log("[WebSocket] Connected!");

                _mainThreadActions.Enqueue(() => OnConnected?.Invoke());

                // Start receive loop
                _ = ReceiveLoop();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocket] Connection failed: {e.Message}");
                _mainThreadActions.Enqueue(() => OnDisconnected?.Invoke());

                // Auto-reconnect
                await Task.Delay((int)(reconnectDelay * 1000));
                if (!_cts.IsCancellationRequested)
                {
                    Connect();
                }
            }
        }

        public void Disconnect()
        {
            _cts?.Cancel();

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing",
                        CancellationToken.None).Wait(2000);
                }
                catch { }
            }

            _ws?.Dispose();
            _ws = null;
            _mainThreadActions.Enqueue(() => OnDisconnected?.Invoke());
        }

        // ─── Sending ──────────────────────────────────────────

        /// <summary>
        /// Send recorded audio to the server for the full STT → LLM → TTS pipeline.
        /// </summary>
        public async void SendAudio(byte[] wavBytes)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[WebSocket] Not connected, cannot send audio");
                return;
            }

            try
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(wavBytes),
                    WebSocketMessageType.Binary,
                    true,
                    _cts.Token
                );
                Debug.Log($"[WebSocket] Sent {wavBytes.Length} bytes of audio");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocket] Send error: {e.Message}");
            }
        }

        /// <summary>
        /// Send a text command to the server (bypasses STT).
        /// </summary>
        public async void SendTextInput(string text)
        {
            if (!IsConnected) return;

            var json = $"{{\"type\":\"text_input\",\"text\":\"{EscapeJson(text)}\"}}";
            var bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocket] Send text error: {e.Message}");
            }
        }

        /// <summary>
        /// Reset conversation history on the server.
        /// </summary>
        public async void ResetConversation()
        {
            if (!IsConnected) return;

            var json = "{\"type\":\"reset\"}";
            var bytes = Encoding.UTF8.GetBytes(json);

            try
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocket] Reset error: {e.Message}");
            }
        }

        // ─── Receiving ─────────────────────────────────────────

        private async Task ReceiveLoop()
        {
            var buffer = new byte[1024 * 1024]; // 1MB buffer

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            _cts.Token
                        );
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var data = ms.ToArray();

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        HandleTextMessage(Encoding.UTF8.GetString(data));
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        HandleBinaryMessage(data);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[WebSocket] Server closed connection");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocket] Receive error: {e.Message}");
            }

            _mainThreadActions.Enqueue(() =>
            {
                OnDisconnected?.Invoke();
                // Auto-reconnect
                if (!_cts.IsCancellationRequested)
                {
                    Invoke(nameof(Connect), reconnectDelay);
                }
            });
        }

        private void HandleTextMessage(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<ServerMessage>(json);

                switch (msg.type)
                {
                    case "status":
                        _mainThreadActions.Enqueue(() =>
                            OnStatusReceived?.Invoke(msg.message));

                        // Parse latency if present
                        if (json.Contains("\"latency\""))
                        {
                            var latency = JsonUtility.FromJson<LatencyMessage>(json);
                            if (latency.latency != null)
                            {
                                _mainThreadActions.Enqueue(() =>
                                    OnLatencyReport?.Invoke(
                                        latency.latency.stt,
                                        latency.latency.llm,
                                        latency.latency.tts,
                                        latency.latency.total
                                    ));
                            }
                        }
                        break;

                    case "transcription":
                        _mainThreadActions.Enqueue(() =>
                            OnTranscriptionReceived?.Invoke(msg.text));
                        break;

                    case "response_text":
                        _mainThreadActions.Enqueue(() =>
                            OnResponseTextReceived?.Invoke(msg.text));
                        break;

                    case "audio_response":
                        // Next binary message will be audio
                        _expectingAudioBytes = true;
                        _expectedAudioSampleRate = msg.sample_rate > 0
                            ? msg.sample_rate : 22050;
                        break;

                    case "pong":
                        Debug.Log("[WebSocket] Pong received");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocket] Failed to parse message: {e.Message}\n{json}");
            }
        }

        private void HandleBinaryMessage(byte[] data)
        {
            if (_expectingAudioBytes)
            {
                _expectingAudioBytes = false;
                Debug.Log($"[WebSocket] Received audio: {data.Length} bytes");
                _mainThreadActions.Enqueue(() =>
                    OnAudioResponseReceived?.Invoke(data, _expectedAudioSampleRate));
            }
            else
            {
                Debug.LogWarning($"[WebSocket] Unexpected binary message: {data.Length} bytes");
            }
        }

        private string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     .Replace("\n", "\\n")
                     .Replace("\r", "\\r")
                     .Replace("\t", "\\t");
        }

        // ─── JSON Models ──────────────────────────────────────

        [Serializable]
        private class ServerMessage
        {
            public string type;
            public string message;
            public string text;
            public int sample_rate;
            public int num_bytes;
            public string format;
        }

        [Serializable]
        private class LatencyMessage
        {
            public string type;
            public string message;
            public LatencyData latency;
        }

        [Serializable]
        private class LatencyData
        {
            public float stt;
            public float llm;
            public float tts;
            public float total;
        }
    }
}
