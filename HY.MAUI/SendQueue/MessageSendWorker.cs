using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Enums;
using HY.MAUI.Models.MsgVM;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.SendQueue
{
    public class MessageSendWorker
    {
        private readonly MessageSendQueue _queue;
        private readonly FileApi _fileApi;
        private readonly MessageApi _messageApi;
        readonly ChatHubSignalR _chatHub;

        public MessageSendWorker(MessageSendQueue queue, FileApi fileApi, MessageApi messageApi, ChatHubSignalR chatHub)
        {
            _queue = queue;
            _fileApi = fileApi;
            _messageApi = messageApi;
            _chatHub = chatHub;
        }

        private void OnUploadProgress_ChatHub(Guid taskId, double progress)
        {
            var task = _queue.FindTaskByTaskId(taskId);

            if (task?.Message is VideoMessageVM videoVM)
            {
                videoVM.UploadProgress = progress;
            }
            else if (task?.Message is ImageMessageVM imageVM)
            {
                imageVM.UploadProgress = progress;
            }
        }


        public async Task RunAsync(CancellationToken token)
        {
            _chatHub.OnUploadProgress_ChatHub += OnUploadProgress_ChatHub;

            try
            {
                await foreach (var task in _queue.ReadAllAsync(token))
                {
                    try
                    {
                        await ProcessAsync(task, token);
                    }
                    finally
                    {
                        //if (task.Status == SendTaskStatus.Sent || task.Status == SendTaskStatus.Cancelled)
                            _queue.RemoveTask(task.TaskId);
                    }
                }
            }
            finally
            {
                _chatHub.OnUploadProgress_ChatHub -= OnUploadProgress_ChatHub;
            }
        }

        private async Task ProcessAsync(SendMessageTask task, CancellationToken token)
        {
            try
            {
                task.Status = SendTaskStatus.Uploading;

                if (task.Message is TextMessageVM textVM)
                {
                    // 无操作
                }
                else if(task.Message is ImageMessageVM imageVM)
                {
                    // 上传文件
                    var resp = await _fileApi.UploadImage(task.TaskId, task.file, token);
                    if (resp?.IsSucc != true)
                    {
                        task.Status = SendTaskStatus.Failed;
                        imageVM.Message_Status = MessageStatus.Failed;
                        return;
                    }

                    imageVM.File_Id = resp!.GetValue<string>("File_Id");
                }
                else if (task.Message is VideoMessageVM videoVM)
                {
                    // 上传文件
                    var resp = await _fileApi.UploadVideo(task.TaskId, task.file, token);
                    if (resp?.IsSucc != true)
                    {
                        task.Status = SendTaskStatus.Failed;
                        videoVM.Message_Status = MessageStatus.Failed;
                        return;
                    }

                    videoVM.File_Id = resp!.GetValue<string>("File_Id");
                }
                else
                {
                    throw new Exception("待实现");
                }

                task.Status = SendTaskStatus.Sending;

                // 发送消息
                await _messageApi.SendMessage(task.Message);

                task.Status = SendTaskStatus.Sent;
            }
            catch (Exception ex)
            {
                task.Status = SendTaskStatus.Failed;
                task.Error = ex;
            }
        }
    }
}
