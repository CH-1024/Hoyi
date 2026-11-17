using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using HY.MAUI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace HY.MAUI.Communication.Http
{
    public class MessageApi : BaseApi
    {
        private readonly IGlobalMapper _globalMapper;

        public MessageApi(HttpClient http, IGlobalMapper globalMapper) : base(http)
        {
            _globalMapper = globalMapper;
        }


        public async Task<Response?> GetMessages(long chatId, long skipMessageId = 0, int take = 50)
        {
            return await GetAsync($"{ApiUrl.GetMessages}?chatId={chatId}&skipMessageId={skipMessageId}&take={take}");
        }

        public async Task SendMessage(MessageVM messageVM)
        {
            try
            {
                var resp = await PostAsJsonAsync(ApiUrl.SendMessage, _globalMapper.ToDto(messageVM));
                if (resp?.IsSucc == true)
                {
                    var msgId = resp.GetValue<long>("MessageId");
                    var createdAt = resp.GetValue<DateTime>("CreatedAt");

                    messageVM.Id = msgId;
                    messageVM.Created_At = createdAt;
                    messageVM.Message_Status = MessageStatus.Sented;
                }
                else
                {
                    messageVM.Message_Status = MessageStatus.Failed;
                }
            }
            catch (Exception)
            {
                // 发送失败
                messageVM.Message_Status = MessageStatus.Failed;
            }
        }

        public async Task RecallMessage(MessageVM messageVM)
        {
            var statusOld = messageVM.Message_Status;
            try
            {
                messageVM.Message_Status = MessageStatus.Recalling;

                var resp = await PostAsync($"{ApiUrl.RecallMessage}?messageId={messageVM.Id}");
                if (resp?.IsSucc == true)
                {
                    messageVM.Message_Status = MessageStatus.Recalled;
                }
                else
                {
                    // 撤回失败，还原消息状态
                    messageVM.Message_Status = statusOld;
                }
            }
            catch (Exception)
            {
                // 撤回失败，还原消息状态
                messageVM.Message_Status = statusOld;
            }
        }

        public async Task DeleteMessage(MessageVM messageVM)
        {
            var statusOld = messageVM.Message_Status;
            try
            {
                messageVM.Message_Status = MessageStatus.Deleting;

                var resp = await PostAsync($"{ApiUrl.DeleteMessage}?messageId={messageVM.Id}");
                if (resp?.IsSucc == true)
                {
                    messageVM.Message_Status = MessageStatus.Deleted;
                }
                else
                {
                    // 删除失败，还原消息状态
                    messageVM.Message_Status = statusOld;
                }
            }
            catch (Exception)
            {
                // 删除失败，还原消息状态
                messageVM.Message_Status = statusOld;
            }
        }

    }
}
