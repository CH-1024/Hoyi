using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace HY.MAUI.SendQueue
{
    public class MessageSendQueue
    {
        private readonly ConcurrentDictionary<Guid, SendMessageTask> _tasks = new();
        private readonly Channel<SendMessageTask> _channel = Channel.CreateUnbounded<SendMessageTask>();

        private ChannelWriter<SendMessageTask> _writer => _channel.Writer;
        private ChannelReader<SendMessageTask> _reader => _channel.Reader;





        /// <summary>
        /// 添加任务
        /// </summary>
        public bool AddTask(SendMessageTask task)
        {
            if (!_tasks.TryAdd(task.TaskId, task))
                return false;

            if (!_writer.TryWrite(task))
            {
                _tasks.TryRemove(task.TaskId, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取待处理任务流
        /// </summary>
        public IAsyncEnumerable<SendMessageTask> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            return _reader.ReadAllAsync(cancellationToken);
        }

        /// <summary>
        /// 根据任务 ID 查找任务
        /// </summary>
        public SendMessageTask? FindTaskByTaskId(Guid taskId)
        {
            return _tasks.TryGetValue(taskId, out var task) ? task : null;
        }

        /// <summary>
        /// 移除任务
        /// </summary>
        public bool RemoveTask(Guid taskId)
        {
            return _tasks.TryRemove(taskId, out _);
        }
    }
}
