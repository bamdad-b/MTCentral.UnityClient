
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MTCentral
{
    public sealed class NetworkUtility : MonoBehaviour
    {
        public static NetworkUtility Instance;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public string baseUrl = "http://127.0.0.1:5000";
        public int timeoutSeconds = 30;
        internal static string AccessToken;

        public string BaseUrl
        {
            get => baseUrl.TrimEnd('/');
            set => baseUrl = value.TrimEnd('/');
        }

        public sealed class NetworkError
        {
            public long StatusCode { get; }
            public string Message { get; }
            public string ResponseBody { get; }
            public bool IsNetworkError { get; }

            public NetworkError(
                long statusCode,
                string message,
                string responseBody,
                bool isNetworkError)
            {
                StatusCode = statusCode;
                Message = message;
                ResponseBody = responseBody;
                IsNetworkError = isNetworkError;
            }

            public override string ToString() =>
                $"HTTP {StatusCode}: {Message}\n{ResponseBody}";
        }

        public RequestBuilder<TResponse> Create<TResponse>(
            string path)
        {
            return new RequestBuilder<TResponse>(
                this, ResolveUrl(path));
        }

        public RequestBuilder<TResponse> Get<TResponse>(string path) =>
            Create<TResponse>(path).Get();

        public RequestBuilder<TResponse> Post<TResponse>(
            string path, object body) =>
            Create<TResponse>(path).Post(body);

        public RequestBuilder<TResponse> Put<TResponse>(
            string path, object body) =>
            Create<TResponse>(path).Put(body);

        public RequestBuilder<TResponse> Delete<TResponse>(
            string path) =>
            Create<TResponse>(path).Delete();

        private string ResolveUrl(string path)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
                return absolute.ToString();

            return $"{BaseUrl}/{path.TrimStart('/')}";
        }

        internal void GetToken(Action callback)
        {
            Create<TokenDto>("/api/security/GetUserToken?userId=" + MTCentralService.userId).OnSuccess(t =>
            {
                AccessToken = t.token;
                WebSocketClient.Configure();
                var ws = gameObject.AddComponent<WebSocketClient>();
                callback();
            });
        }

        public sealed class RequestBuilder<TResponse>
        {
            private readonly NetworkUtility owner;
            private readonly string url;

            private string method = UnityWebRequest.kHttpVerbGET;
            private object body;
            private Action<TResponse> success;
            private Action<NetworkError> error;
            private readonly System.Collections.Generic
                .Dictionary<string, string> headers = new();

            private UnityWebRequest activeRequest;

            internal RequestBuilder(NetworkUtility owner, string url)
            {
                this.owner = owner;
                this.url = url;
            }

            public RequestBuilder<TResponse> Get()
            {
                method = UnityWebRequest.kHttpVerbGET;
                body = null;
                return this;
            }

            public RequestBuilder<TResponse> Post(object value)
            {
                method = UnityWebRequest.kHttpVerbPOST;
                body = value;
                return this;
            }

            public RequestBuilder<TResponse> Put(object value)
            {
                method = UnityWebRequest.kHttpVerbPUT;
                body = value;
                return this;
            }

            public RequestBuilder<TResponse> Delete()
            {
                method = UnityWebRequest.kHttpVerbDELETE;
                body = null;
                return this;
            }

            public RequestBuilder<TResponse> OnSuccess(
                Action<TResponse> callback)
            {
                success = callback;
                return this;
            }

            public RequestBuilder<TResponse> OnError(
                Action<NetworkError> callback)
            {
                error = callback;
                return this;
            }

            public RequestBuilder<TResponse> SetHeader(
                string name, string value)
            {
                headers[name] = value;
                return this;
            }

            public RequestBuilder<TResponse> BearerToken(string token)
            {
                return SetHeader("Authorization", $"Bearer {token}");
            }

            public void Send()
            {
                owner.StartCoroutine(SendRoutine());
            }

            public void Cancel()
            {
                activeRequest?.Abort();
            }

            private System.Collections.IEnumerator SendRoutine()
            {
                using var request = new UnityWebRequest(url, method);

                activeRequest = request;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = owner.timeoutSeconds;

                if (body != null &&
                    method != UnityWebRequest.kHttpVerbGET &&
                    method != UnityWebRequest.kHttpVerbHEAD)
                {
                    var json = JsonUtility.ToJson(body);
                    request.uploadHandler =
                        new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader(
                        "Content-Type", "application/json");
                }

                foreach (var header in headers)
                    request.SetRequestHeader(header.Key, header.Value);

                yield return request.SendWebRequest();

                activeRequest = null;

                var responseText = request.downloadHandler?.text ?? "";

                if (request.result != UnityWebRequest.Result.Success)
                {
                    error?.Invoke(new NetworkError(
                        request.responseCode,
                        request.error ?? "Request failed.",
                        responseText,
                        request.result ==
                            UnityWebRequest.Result.ConnectionError));

                    yield break;
                }

                try
                {
                    var response = Deserialize<TResponse>(responseText);
                    success?.Invoke(response);
                }
                catch (Exception ex)
                {
                    error?.Invoke(new NetworkError(
                        request.responseCode,
                        $"Response deserialization failed: {ex.Message}",
                        responseText,
                        false));
                }
            }

            private static T Deserialize<T>(string json)
            {
                var type = typeof(T);
                var actualType =
                    Nullable.GetUnderlyingType(type) ?? type;

                if (string.IsNullOrWhiteSpace(json) ||
                    json.Trim() == "null")
                {
                    return default;
                }

                // Handle scalar enum responses, such as UserRelationMode.
                if (actualType.IsEnum)
                {
                    var number = int.Parse(
                        json, CultureInfo.InvariantCulture);

                    return (T)Enum.ToObject(actualType, number);
                }

                // Handle primitive scalar responses.
                if (actualType.IsPrimitive ||
                    actualType == typeof(decimal))
                {
                    return (T)Convert.ChangeType(
                        json, actualType, CultureInfo.InvariantCulture);
                }

                // JsonUtility cannot deserialize a root-level JSON array.
                if (actualType.IsArray)
                {
                    var elementType = actualType.GetElementType();
                    var wrapperType = typeof(ArrayWrapper<>)
                        .MakeGenericType(elementType);

                    var wrappedJson = "{\"Items\":" + json + "}";
                    var wrapper = JsonUtility.FromJson(
                        wrappedJson, wrapperType);

                    return (T)wrapperType
                        .GetField(nameof(ArrayWrapper<int>.Items))
                        .GetValue(wrapper);
                }

                return JsonUtility.FromJson<T>(json);
            }
        }

        [Serializable]
        private sealed class ArrayWrapper<T>
        {
            public T[] Items;
        }
    }
}
