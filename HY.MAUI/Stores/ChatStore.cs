using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Models;
using HY.MAUI.Interfaces;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.Stores
{
    public class ChatStore
    {
        public ObservableCollection<ChatVM> Chats { get; } = new();

        public ChatVM? GetChat(long chatId) => Chats.FirstOrDefault(x => x.Id == chatId);

        public ChatVM? GetChat(long currentUserId, MessageDto messageDto)
        {
            if (messageDto.Chat_Type == ChatType.Group)
                return Chats.FirstOrDefault(c =>
                    c.Type == ChatType.Group && c.Target_Id == messageDto.Target_Id);

            if (messageDto.Chat_Type == ChatType.Private)
            {
                long targetId;

                if (currentUserId == messageDto.Sender_Id)
                    targetId = messageDto.Target_Id;
                else if (currentUserId == messageDto.Target_Id)
                    targetId = messageDto.Sender_Id;
                else
                    return null;

                return Chats.FirstOrDefault(c =>
                    c.Type == ChatType.Private && c.Target_Id == targetId);
            }

            return null;
        }

        public int GetUnreadCount() => Chats.Sum(c => c.Unread_Count);

        public void UpsertAndSetTop(ChatVM chat)
        {
            var existing = Chats.FirstOrDefault(c => c.Id == chat.Id);
            if (existing != null)
            {
                Copy(existing, chat);
                Chats.Move(Chats.IndexOf(existing), 0);
            }
            else
            {
                Chats.Insert(0, chat);
            }
        }

        public void Upsert(ChatVM chat)
        {
            var existing = Chats.FirstOrDefault(c => c.Id == chat.Id);
            if (existing != null)
                Copy(existing, chat);
            else
                Chats.Add(chat);
        }

        private static void Copy(ChatVM target, ChatVM source)
        {
            target.Id = source.Id;
            target.Type = source.Type;
            target.Target_Id = source.Target_Id;
            target.Target_Name = source.Target_Name;
            target.Target_Avatar = source.Target_Avatar;
            target.Is_Top = source.Is_Top;
            target.Is_Deleted = source.Is_Deleted;
            target.Unread_Count = source.Unread_Count;
            target.Last_Msg = source.Last_Msg;
        }

        public void ReplaceSnapshot(IEnumerable<ChatVM> chats)
        {
            var incoming = chats.ToList();
            var ids = incoming.Select(x => x.Id).ToHashSet();

            foreach (var stale in Chats.Where(x => !ids.Contains(x.Id)).ToList())
                Chats.Remove(stale);

            for (var i = 0; i < incoming.Count; i++)
            {
                Upsert(incoming[i]);
                var existing = GetChat(incoming[i].Id);
                if (existing == null) continue;

                var oldIndex = Chats.IndexOf(existing);
                if (oldIndex != i)
                    Chats.Move(oldIndex, Math.Min(i, Chats.Count - 1));
            }
        }

        public void SetTop(long chatId)
        {
            var existing = GetChat(chatId);
            if (existing != null)
                Chats.Move(Chats.IndexOf(existing), 0);
        }

        public bool Remove(ChatType type, long targetId)
        {
            var chat = Chats.FirstOrDefault(c => c.Target_Id == targetId && c.Type == type);
            return chat != null && Chats.Remove(chat);
        }

        public void Clear() => Chats.Clear();
    }
}
