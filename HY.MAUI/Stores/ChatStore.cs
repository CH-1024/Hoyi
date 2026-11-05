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

        public ChatVM? GetChat(long chatId)
        {
            return Chats.FirstOrDefault(x => x.Id == chatId);
        }

        public ChatVM? GetChat(long currentUserId, MessageDto messageDto)
        {
            if (currentUserId == messageDto.Sender_Id)
            {
                return Chats.FirstOrDefault(c => c.Target_Id == messageDto.Target_Id && c.Type == messageDto.Chat_Type);
            }
            else if (currentUserId == messageDto.Target_Id)
            {
                return Chats.FirstOrDefault(c => c.Target_Id == messageDto.Sender_Id && c.Type == messageDto.Chat_Type);
            }

            return null;
        }

        public void Upsert(ChatVM chat)
        {
            var existing = Chats.FirstOrDefault(c => c.Id == chat.Id);
            if (existing != null)
            {
                // Update existing
                existing.Id = chat.Id;
                existing.Type = chat.Type;
                existing.Target_Id = chat.Target_Id;
                existing.Target_Name = chat.Target_Name;
                existing.Target_Avatar = chat.Target_Avatar;
                existing.Is_Top = chat.Is_Top;
                existing.Is_Deleted = chat.Is_Deleted;
                existing.Unread_Count = chat.Unread_Count;
                existing.Last_Msg = chat.Last_Msg;
            }
            else
            {
                Chats.Add(chat);
            }
        }

        public void UpsertAndSetTop(ChatVM chat)
        {
            var existing = Chats.FirstOrDefault(c => c.Id == chat.Id);
            if (existing != null)
            {
                // Update existing
                existing.Id = chat.Id;
                existing.Type = chat.Type;
                existing.Target_Id = chat.Target_Id;
                existing.Target_Name = chat.Target_Name;
                existing.Target_Avatar = chat.Target_Avatar;
                existing.Is_Top = chat.Is_Top;
                existing.Is_Deleted = chat.Is_Deleted;
                existing.Unread_Count = chat.Unread_Count;
                existing.Last_Msg = chat.Last_Msg;

                Chats.Move(Chats.IndexOf(existing), 0);
            }
            else
            {
                Chats.Insert(0, chat);
            }
        }

        public void SetTop(long chatId)
        {
            var existing = Chats.FirstOrDefault(c => c.Id == chatId);
            if (existing != null)
            {
                Chats.Move(Chats.IndexOf(existing), 0);
            }
        }

        public bool Remove(ChatType type, long targetId)
        {
            var chat = Chats.FirstOrDefault(c => c.Target_Id == targetId && c.Type == type);
            return Chats.Remove(chat);
        }

        public void Clear()
        {
            Chats.Clear();
        }

    }
}
