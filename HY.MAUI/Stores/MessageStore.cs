using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.Stores
{
    public class MessageStore
    {
        private readonly Dictionary<long, ObservableCollection<MessageVM>> _messages = new();

        public ObservableCollection<MessageVM> GetMessages(long chatId)
        {
            if (!_messages.TryGetValue(chatId, out var list))
            {
                list = new ObservableCollection<MessageVM>();
                _messages[chatId] = list;
            }
            return list;
        }

        public MessageVM? GetMessage(long messageID)
        {
            foreach (var chatMessages in _messages.Values)
            {
                var message = chatMessages.FirstOrDefault(m => m.Id == messageID);
                if (message != null) return message;
            }
            return null;
        }

        public void Add(long chatId, MessageVM messageVM) =>
            AddIfNotExists(chatId, messageVM);

        public bool AddIfNotExists(long chatId, MessageVM messageVM)
        {
            var messages = GetMessages(chatId);
            if (messageVM.Id > 0 && messages.Any(x => x.Id == messageVM.Id))
                return false;

            if (messageVM.Id <= 0 || messages.Count == 0)
            {
                messages.Add(messageVM);
                return true;
            }

            var index = 0;
            while (index < messages.Count &&
                   (messages[index].Id <= 0 || messages[index].Id < messageVM.Id))
                index++;

            messages.Insert(index, messageVM);
            return true;
        }

        public void Insert(long chatId, MessageVM messageVM, int index = 0) =>
            AddIfNotExists(chatId, messageVM);

        public void Clear() => _messages.Clear();

        public void UpdateUserHead(long id, string url)
        {
            foreach (var chatMessages in _messages.Values)
            {
                foreach (var message in chatMessages)
                {
                    if (message.Sender_Id == id)
                        message.Sender_Avatar = url;
                }
            }
        }
    }
}
