
using System;

namespace MTCentral
{
    [Serializable]
    public sealed class UserRelationRequestDto
    {
        public string UserId;
        public string TargetUserId;

        public UserRelationRequestDto(
            string userId,
            string targetUserId)
        {
            UserId = userId;
            TargetUserId = targetUserId;
        }
    }

    [Serializable]
    public sealed class UserDescriptorDto
    {
        public string Id;
        public string Username;
    }

    public enum UserRelationMode
    {
        Requested,
        Friends,
        Blocked
    }

    [Serializable]
    public sealed class UserRelationDescriptorDto
    {
        public long Id;
        public UserDescriptorDto User1;
        public UserDescriptorDto User2;
        public UserRelationMode Mode;
    }

    [Serializable]
    public sealed class EmptyResponse { }
}
