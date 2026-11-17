using HY.MAUI.Dtos;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Extensions.Mapping
{
    public static class ChatMapping
    {
        public static ChatVM ToVM(this ChatDto dto)
        {
            return new ChatVM
            {
                Id = dto.Id,
                Type = dto.Type,
                Target_Id = dto.Target_Id,
                Target_Name = dto.Target_Name,
                Target_Avatar = dto.Target_Avatar,
                Is_Top = dto.Is_Top,
                Is_Deleted = dto.Is_Deleted,
                Unread_Count = dto.Unread_Count,
                Last_Msg = dto.Last_Msg?.ToVM(0, null, null),
            };
        }

    }
}
