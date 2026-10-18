using MTCentral;
using System;
using System.Globalization;

namespace MTCentral
{
    public sealed class MessengerAPI
    {
        private readonly NetworkUtility network;
        private readonly Func<string> getAccessToken;

        public MessengerAPI(
            NetworkUtility network,
            Func<string> getAccessToken = null)
        {
            if (NetworkUtility.Instance is null)
            {
                MTCentralService.LogError(
                    "The MTCentral networking service is not initialized. " +
                    "Use MTCentralService.Initialize() beforehand.");

                throw new Exception(
                    "The MTCentral networking service is not initialized. " +
                    "Use MTCentralService.Initialize() beforehand.");
            }

            this.network = network
                ? network
                : throw new ArgumentNullException(nameof(network));

            this.getAccessToken = getAccessToken;
        }

        private NetworkUtility.RequestBuilder<T> Request<T>(
            string action)
        {
            var request = network.Create<T>(
                $"api/social/messenger/{action}");

            var token = getAccessToken?.Invoke();

            if (!string.IsNullOrWhiteSpace(token))
                request.BearerToken(token);

            return request;
        }

        private static string Query(string value) =>
            Uri.EscapeDataString(value ?? "");

        private static string Query(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string Query(long value) =>
            value.ToString(CultureInfo.InvariantCulture);

        public NetworkUtility.RequestBuilder<ChatMessageDto[]>
            GetUserChats(Guid userId, int pageSize = 100, int pageIndex = 0)
        {
            var path =
                $"api/social/messenger/GetUserChats" +
                $"?userId={Query(MTCentralService.userId.ToString())}" +
                $"&pageSize={Query(pageSize)}" +
                $"&pageIndex={Query(pageIndex)}";

            return Request<ChatMessageDto[]>(path);
        }

        public NetworkUtility.RequestBuilder<ChatMessageDto[]>
            GetMessages(
                long chatId,
                long? sinceSequence = null,
                int take = 100)
        {
            var path =
                $"api/social/messenger/GetMessages" +
                $"?chatId={Query(chatId)}" +
                $"&take={Query(take)}";

            if (sinceSequence.HasValue)
                path += $"&sinceSequence={Query(sinceSequence.Value)}";

            return Request<ChatMessageDto[]>(path);
        }

        public NetworkUtility.RequestBuilder<EmptyResponse>NewMessage(NewDirectChatMessageDto message)
        {
            return Request<EmptyResponse>("NewMessage")
                .Post(message);
        }

        public NetworkUtility.RequestBuilder<EmptyResponse>NewMessage(Guid targetUserId, string text, ChatMessageMode mode = ChatMessageMode.Text)
        {
            return NewMessage(new NewDirectChatMessageDto
            {
                From = MTCentralService.userId,
                To = targetUserId,
                Data = text,
                Mode = mode
            });
        }

    
    }
}