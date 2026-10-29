using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.SendQueue
{
    public class SendMessageTask
    {
        public Guid TaskId { get; } = Guid.NewGuid();

        public MessageVM Message { get; set; }

        /// <summary>
        /// 本地文件
        /// </summary>
        public FileResult? file { get; set; }

        /// <summary>
        /// 发送状态
        /// </summary>
        public SendTaskStatus Status { get; set; }

        public double Progress { get; set; }

        public Exception? Error { get; set; }
    }
}
