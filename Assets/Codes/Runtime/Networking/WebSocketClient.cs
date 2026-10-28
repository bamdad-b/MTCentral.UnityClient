
using MTCentral;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MTCentral
{
    public sealed class WebSocketClient : MonoBehaviour
    {
        static string serverUrl = "wss://your-server.com/ws";
        public static WebSocketClient Instance;

        [Header("Reconnect")]
        [SerializeField, Min(1f)] private float reconnectDelay = 2f;
        [SerializeField, Min(1f)] private float maxReconnectDelay = 30f;

        private ClientWebSocket socket;
        private CancellationTokenSource lifetime;
        private SemaphoreSlim sendLock;

        private readonly ConcurrentQueue<Action> mainThreadQueue = new();

        private Task connectionTask;
        private bool stopping;

        public bool IsConnected =>
            socket?.State == WebSocketState.Open;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<string> MessageReceived;
        public event Action<string> Error;

        /// <summary>
        /// Call before enabling the component if you need to supply
        /// a runtime URL or token provider.
        /// </summary>
        public static void Configure()
        {
            serverUrl = NetworkUtility.Instance.baseUrl;
            if (serverUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                serverUrl = "ws" + serverUrl.Substring(4);
        }

        private void Awake()
        {
            sendLock = new SemaphoreSlim(1, 1);
            Instance = this;
        }

        private void OnEnable()
        {
            stopping = false;
            lifetime = new CancellationTokenSource();
            connectionTask = ConnectionLoopAsync(lifetime.Token);
        }

        private void Update()
        {
            while (mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && isActiveAndEnabled && !stopping)
            {
                // The connection loop already reconnects after failures.
                // If the socket was suspended, it will detect the failure.
                if (socket == null ||
                    socket.State != WebSocketState.Open)
                {
                    RaiseOnMainThread(() =>
                        Error?.Invoke("Connection may need to reconnect."));
                }
            }
        }

        private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
        {
            var delay = TimeSpan.FromSeconds(reconnectDelay);

            while (!cancellationToken.IsCancellationRequested)
            {
                ClientWebSocket current = null;

                try
                {
                    current = new ClientWebSocket();

                    // Matches the server's WebSocketAcceptContext.
                    //current.Options.AddSubProtocol("auth");

                    //var token = NetworkUtility.AccessToken;

                    //if (!string.IsNullOrWhiteSpace(token))
                    //{
                    //    current.Options.SetRequestHeader(
                    //        "Authorization",
                    //        $"Bearer {token}");
                    //}

                    socket = current;

                    await current.ConnectAsync(
                        new Uri(serverUrl + "/ws/massenger"),
                        cancellationToken);

                    await SendAsync(new WebSocketInitModel { Id = MTCentralService.userId.ToString() }, cancellationToken);

                    delay = TimeSpan.FromSeconds(reconnectDelay);

                    RaiseOnMainThread(() => Connected?.Invoke());

                    await ReceiveLoopAsync(
                        current,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    RaiseOnMainThread(() =>
                        Error?.Invoke($"WebSocket: {ex.Message}"));
                }
                finally
                {
                    if (ReferenceEquals(socket, current))
                        socket = null;

                    if (current != null)
                    {
                        current.Dispose();

                        RaiseOnMainThread(() =>
                            Disconnected?.Invoke());
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delay = TimeSpan.FromSeconds(
                    Math.Min(delay.TotalSeconds * 2,
                             maxReconnectDelay));
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket current, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            while (current.State == WebSocketState.Open &&
                   !cancellationToken.IsCancellationRequested)
            {
                using var message = new MemoryStream();

                WebSocketReceiveResult result;

                do
                {
                    result = await current.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(
                        message.GetBuffer(), 0, (int)message.Length);

                    RaiseOnMainThread(() =>
                        MessageReceived?.Invoke(text));
                }
            }
        }

        public Task SendAsync(object message, CancellationToken cancellationToken = default)
        {
            return SendJsonAsync(
                JsonUtility.ToJson(message),
                cancellationToken);
        }
        /// <summary>
        /// Sends an already serialized JSON message.
        /// </summary>
        public async Task SendJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON cannot be empty.", nameof(json));

            await sendLock.WaitAsync(cancellationToken);

            try
            {
                var current = socket;

                if (current == null ||
                    current.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException(
                        "WebSocket is not connected.");
                }

                var bytes = Encoding.UTF8.GetBytes(json);

                await current.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }
        }

        /// <summary>
        /// Sends a new direct-chat message using Unity JsonUtility.
        /// </summary>
        public Task SendNewMessageAsync(long chatId, string text, ChatMessageMode mode, CancellationToken cancellationToken = default)
        {
            var message = new WebSocketGeneralMessengerModel
            {
                MessageType = "new",
                Content = text,
                Chat_Id = chatId,
                Mode = mode
            };

            return SendJsonAsync(
                JsonUtility.ToJson(message),
                cancellationToken);
        }
        public Task DeleteMessageAsync(long chatMessageId, CancellationToken cancellationToken = default)
        {
            var message = new WebSocketGeneralMessengerModel
            {
                MessageType = "delete",
                ChatMessage_Id = chatMessageId,
            };

            return SendJsonAsync(
                JsonUtility.ToJson(message),
                cancellationToken);
        }
        public Task EditMessageAsync(long chatMessageId, string text, CancellationToken cancellationToken = default)
        {
            var message = new WebSocketGeneralMessengerModel
            {
                MessageType = "edit",
                ChatMessage_Id = chatMessageId,
                Content = text
            };

            return SendJsonAsync(
                JsonUtility.ToJson(message),
                cancellationToken);
        }

        private void RaiseOnMainThread(Action action)
        {
            mainThreadQueue.Enqueue(action);
        }

        private async void OnDisable()
        {
            stopping = true;

            var cts = lifetime;
            lifetime = null;

            if (cts != null)
            {
                cts.Cancel();

                var current = socket;

                if (current != null)
                    current.Abort();

                if (connectionTask != null)
                {
                    try
                    {
                        await connectionTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown.
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }

                cts.Dispose();
            }

            socket = null;
        }

        private void OnDestroy()
        {
            sendLock?.Dispose();
        }
    }
}
