using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Models;
using HY.MAUI.Models.MsgVM;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HY.MAUI.Extensions.Mapping
{
    public static class MessageMapping
    {
        public static MessageVM ToVM(this MessageDto dto, long currentUserId, string? senderAvatar, string? senderNickname)
        {
            var isSelf  = dto.Sender_Id == currentUserId;

            if (dto.Message_Type == MessageType.Text)
            {
                return new TextMessageVM
                {
                    Id = dto.Id,
                    Chat_Type = dto.Chat_Type,
                    Sender_Id = dto.Sender_Id,
                    Sender_Avatar = senderAvatar,
                    Sender_Nickname = senderNickname,
                    Target_Id = dto.Target_Id,
                    Message_Status = dto.Message_Status,
                    Created_At = dto.Created_At,
                    IsSelf = isSelf,

                    Content = dto.Content,
                };
            }
            else if (dto.Message_Type == MessageType.Image)
            {
                var extraData = JsonSerializer.Deserialize<Dictionary<string, object?>>(dto.Extra ?? "{}");
                return new ImageMessageVM
                {
                    Id = dto.Id,
                    Chat_Type = dto.Chat_Type,
                    Sender_Id = dto.Sender_Id,
                    Sender_Avatar = senderAvatar,
                    Sender_Nickname = senderNickname,
                    Target_Id = dto.Target_Id,
                    Message_Status = dto.Message_Status,
                    Created_At = dto.Created_At,
                    IsSelf = isSelf,
                    File_Id = extraData != null && extraData.TryGetValue("File_Id", out var originalUrl) ? originalUrl?.ToString() : null,
                };
            }
            else if (dto.Message_Type == MessageType.Video)
            {
                var extraData = JsonSerializer.Deserialize<Dictionary<string, object?>>(dto.Extra ?? "{}");
                return new VideoMessageVM
                {
                    Id = dto.Id,
                    Chat_Type = dto.Chat_Type,
                    Sender_Id = dto.Sender_Id,
                    Sender_Avatar = senderAvatar,
                    Sender_Nickname = senderNickname,
                    Target_Id = dto.Target_Id,
                    Message_Status = dto.Message_Status,
                    Created_At = dto.Created_At,
                    IsSelf = isSelf,
                    File_Id = extraData != null && extraData.TryGetValue("File_Id", out var originalUrl) ? originalUrl?.ToString() : null,
                };
            }
            else if (dto.Message_Type == MessageType.System)
            {
                return new SystemMessageVM
                {
                    Id = dto.Id,
                    Chat_Type = dto.Chat_Type,
                    Sender_Id = dto.Sender_Id,
                    //Sender_Avatar = senderAvatar,
                    //Sender_Nickname = senderNickname,
                    Target_Id = dto.Target_Id,
                    Message_Status = dto.Message_Status,
                    Created_At = dto.Created_At,
                    IsSelf = isSelf,

                    Text = dto.Content,
                };
            }
            else if (dto.Message_Type == MessageType.VideoCall)
            {
                var extraData = JsonSerializer.Deserialize<Dictionary<string, object?>>(dto.Extra ?? "{}");
                return new CallVideoMessageVM
                {
                    Id = dto.Id,
                    Chat_Type = dto.Chat_Type,
                    Sender_Id = dto.Sender_Id,
                    Sender_Avatar = senderAvatar,
                    Sender_Nickname = senderNickname,
                    Target_Id = dto.Target_Id,
                    Message_Status = dto.Message_Status,
                    Created_At = dto.Created_At,
                    IsSelf = isSelf,

                    CallId = extraData != null && extraData.TryGetValue("CallId", out var callId) ? callId?.ToString() : null,
                    Call_Status = extraData != null && extraData.TryGetValue("CallStatus", out var callStatus) ? (CallStatus)int.Parse(callStatus?.ToString() ?? "0") : CallStatus.Abnormal,
                    Duration = extraData != null && extraData.TryGetValue("Duration", out var duration) ? TimeSpan.FromSeconds(double.Parse(duration?.ToString() ?? "0")) : TimeSpan.Zero
                };
            }
            else if (dto.Message_Type == MessageType.VoiceCall)
            {
                var extraData = JsonSerializer.Deserialize<Dictionary<string, object?>>(dto.Extra ?? "{}");
                return new CallVoiceMessageVM
                {
                    Id = dto.Id,
                    Chat_Type = dto.Chat_Type,
                    Sender_Id = dto.Sender_Id,
                    Sender_Avatar = senderAvatar,
                    Sender_Nickname = senderNickname,
                    Target_Id = dto.Target_Id,
                    Message_Status = dto.Message_Status,
                    Created_At = dto.Created_At,
                    IsSelf = isSelf,

                    CallId = extraData != null && extraData.TryGetValue("CallId", out var callId) ? callId?.ToString() : null,
                    Call_Status = extraData != null && extraData.TryGetValue("CallStatus", out var callStatus) ? (CallStatus)int.Parse(callStatus?.ToString() ?? "0") : CallStatus.Abnormal,
                    Duration = extraData != null && extraData.TryGetValue("Duration", out var duration) ? TimeSpan.FromSeconds(double.Parse(duration?.ToString() ?? "0")) : TimeSpan.Zero
                };
            }
            else
                throw new NotImplementedException("Message type not implemented in mapping.");
        }

        public static MessageDto ToDto(this MessageVM model)
        {
            if (model is TextMessageVM textMsg)
            {
                return new MessageDto
                {
                    Id = textMsg.Id,
                    Chat_Type = textMsg.Chat_Type,
                    Sender_Id = textMsg.Sender_Id,
                    Target_Id = textMsg.Target_Id,
                    Message_Type = MessageType.Text,
                    Content = textMsg.Content,
                    Extra = null,
                    Message_Status = textMsg.Message_Status,
                    Created_At = textMsg.Created_At
                };
            }
            else if (model is ImageMessageVM imageMsg)
            {
                return new MessageDto
                {
                    Id = imageMsg.Id,
                    Chat_Type = imageMsg.Chat_Type,
                    Sender_Id = imageMsg.Sender_Id,
                    Target_Id = imageMsg.Target_Id,
                    Message_Type = MessageType.Image,
                    Content = null,
                    Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        { "File_Id", imageMsg.File_Id },
                    }),
                    Message_Status = imageMsg.Message_Status,
                    Created_At = imageMsg.Created_At
                };
            }
            else if (model is VideoMessageVM videoMsg)
            {
                return new MessageDto
                {
                    Id = videoMsg.Id,
                    Chat_Type = videoMsg.Chat_Type,
                    Sender_Id = videoMsg.Sender_Id,
                    Target_Id = videoMsg.Target_Id,
                    Message_Type = MessageType.Video,
                    Content = null,
                    Extra = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        { "File_Id", videoMsg.File_Id },
                    }),
                    Message_Status = videoMsg.Message_Status,
                    Created_At = videoMsg.Created_At
                };
            }
            else if (model is SystemMessageVM systemMsg)
            {
                // 客户端无权发送此消息
                throw new NotImplementedException("System message cannot be sent from client.");
            }
            else if (model is CallVideoMessageVM videoCallMsg)
            {
                // 客户端无权发送此消息
                throw new NotImplementedException("Video message cannot be sent from client.");
            }
            else if (model is CallVoiceMessageVM voiceCallMsg)
            {
                // 客户端无权发送此消息
                throw new NotImplementedException("Voice message cannot be sent from client.");
            }
            else
                throw new NotImplementedException("Message type not implemented in mapping.");
        }


    }
}
