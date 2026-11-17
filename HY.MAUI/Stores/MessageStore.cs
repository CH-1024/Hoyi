using HY.MAUI.Dtos;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.Stores
{
    public class MessageStore
    {
        readonly IGlobalCache _globalCache;


        private readonly Dictionary<long, ObservableCollection<MessageVM>> _messages = new();


        public MessageStore(IGlobalCache globalCache)
        {
            _globalCache = globalCache;
        }


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

        public void Add(long chatId, MessageVM messageVM)
        {
            AddIfNotExists(chatId, messageVM);
        }

        public bool AddIfNotExists(long chatId, MessageVM messageVM)
        {
            var chatMessages = GetMessages(chatId);

            if (messageVM.Id > 0 && chatMessages.Any(x => x.Id == messageVM.Id))
                return false;

            if (messageVM.Id <= 0 || chatMessages.Count == 0)
            {
                chatMessages.Add(messageVM);
                return true;
            }

            var index = 0;
            while (index < chatMessages.Count && (chatMessages[index].Id <= 0 || chatMessages[index].Id < messageVM.Id))
                index++;

            chatMessages.Insert(index, messageVM);
            return true;
        }

        public void Insert(long chatId, MessageVM messageVM, int index = 0)
        {
            var chatMessages = GetMessages(chatId);
            chatMessages.Insert(0, messageVM);
        }

        public void Remove(long chatId)
        {
            _messages.Remove(chatId);
        }

        public void Clear()
        {
            _messages.Clear();
        }

        public void UpdateUserHead(long id, string url)
        {
            foreach (var chatMessages in _messages.Values)
            {
                foreach (var message in chatMessages)
                {
                    if (message.Sender_Id == id)
                    {
                        message.Sender_Avatar = url;
                    }
                }
            }
        }
    }
}
