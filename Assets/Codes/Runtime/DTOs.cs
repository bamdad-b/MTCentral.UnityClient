
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

    public class NewDirectChatMessageDto
    {
        public Guid From { get; set; }
        public Guid To { get; set; }
        public string Data { get; set; }
        public ChatMessageMode Mode { get; set; }
    }
    public class NewChatMessageDto
    {
        Guid From { get; set; }
        long To { get; set; }
        string? Data { get; set; }
        ChatMessageMode Mode { get; set; }
    }

    public class ChatMessageDto
    {
        long Id { get; set; }
        long Seq { get; set; }
        string? Data { get; set; }
        DateTime DateTime { get; set; }
        long? RID { get; set; }
        string WHO { get; set; }
        ChatMessageMode Mode { get; set; }
        UserDescriptorDto User { get; set; }
    }

    [Serializable]
    public sealed class EmptyResponse { }

    public enum ChatMessageMode
    {
        Join, Text, Voice, Sticker, Image, Video, Special, Leave
    }
}
