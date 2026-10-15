
using System;
using System.Globalization;

namespace MTCentral
{
    public sealed class RelationAPI
    {
        private readonly NetworkUtility network;
        private readonly Func<string> getAccessToken;

        public RelationAPI(NetworkUtility network, Func<string> getAccessToken = null)
        {
            if (NetworkUtility.Instance is null)
            {
                MTCentralService.LogError("The MTCentral networking service is not initialized. Use MTCentralService.Initialize() beforehand.");
                throw new Exception("The MTCentral networking service is not initialized. Use MTCentralService.Initialize() beforehand.");
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
                $"api/social/UserRelation/{action}");

            var token = getAccessToken?.Invoke();

            if (!string.IsNullOrWhiteSpace(token))
                request.BearerToken(token);

            return request;
        }

        private static string Query(string value) =>
            Uri.EscapeDataString(value ?? "");

        private static string Query(int value) =>
            value.ToString(CultureInfo.InvariantCulture);

        public NetworkUtility.RequestBuilder<UserDescriptorDto[]>
            GetRelations(string searchKey = "", int pageSize = 100, int pageIndex = 0)
        {
            var path =
                $"api/social/UserRelation/GetRelations" +
                $"?userId={Query(MTCentralService.userId.ToString())}" +
                $"&searchKey={Query(searchKey)}" +
                $"&pageSize={Query(pageSize)}" +
                $"&pageIndex={Query(pageIndex)}";

            var request = network.Create<UserDescriptorDto[]>(path);

            var token = getAccessToken?.Invoke();
            if (!string.IsNullOrWhiteSpace(token))
                request.BearerToken(token);

            return request;
        }

        public NetworkUtility.RequestBuilder<UserRelationDescriptorDto[]>
            GetUserRelations(UserRelationMode? mode = null, int pageSize = 100, int pageIndex = 0)
        {
            var path =
                $"api/social/UserRelation/GetUserRelations" +
                $"?userId={Query(MTCentralService.userId.ToString())}" +
                $"&pageSize={Query(pageSize)}" +
                $"&pageIndex={Query(pageIndex)}";

            if (mode.HasValue)
                path += $"&mode={Query((int)mode.Value)}";

            var request = network.Create<UserRelationDescriptorDto[]>(path);

            var token = getAccessToken?.Invoke();
            if (!string.IsNullOrWhiteSpace(token))
                request.BearerToken(token);

            return request;
        }

        public NetworkUtility.RequestBuilder<EmptyResponse> RequestFriend(string targetUserId)
        {
            return Request<EmptyResponse>("RequestFriend")
                .Post(new UserRelationRequestDto(MTCentralService.userId.ToString(), targetUserId));
        }

        public NetworkUtility.RequestBuilder<EmptyResponse> AcceptFriend(string targetUserId)
        {
            return Request<EmptyResponse>("AcceptFriend")
                .Post(new UserRelationRequestDto(MTCentralService.userId.ToString(), targetUserId));
        }

        public NetworkUtility.RequestBuilder<EmptyResponse> Block(string targetUserId)
        {
            return Request<EmptyResponse>("Block")
                .Post(new UserRelationRequestDto(MTCentralService.userId.ToString(), targetUserId));
        }

        public NetworkUtility.RequestBuilder<EmptyResponse> UnFriend(string targetUserId)
        {
            return Request<EmptyResponse>("UnFriend")
                .Post(new UserRelationRequestDto(MTCentralService.userId.ToString(), targetUserId));
        }

        public NetworkUtility.RequestBuilder<UserRelationMode?> GetStatus(string targetUserId)
        {
            var path =
                $"api/social/UserRelation/GetStatus" +
                $"?userId={Query(MTCentralService.userId.ToString())}" +
                $"&targetUserId={Query(targetUserId)}";

            var request = network.Create<UserRelationMode?>(path);

            var token = getAccessToken?.Invoke();
            if (!string.IsNullOrWhiteSpace(token))
                request.BearerToken(token);

            return request;
        }
    }
}
