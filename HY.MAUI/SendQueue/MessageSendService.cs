using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.SendQueue
{
    public class MessageSendService
    {
        private readonly MessageSendQueue _sendQueue;

        public MessageSendService(MessageSendQueue sendQueue)
        {
            _sendQueue = sendQueue;
        }

        public bool Enqueue(SendMessageTask task)
        {
            return _sendQueue.AddTask(task);
        }
    }
}
