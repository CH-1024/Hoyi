using HY.MAUI.Dtos;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Extensions.Mapping
{
    public static class ContactMapping
    {
        public static ContactVM ToVM(this ContactDto dto)
        {
            return new ContactVM
            {
                Contact_Id = dto.Contact_Id,
                Contact_HYid = dto.Contact_HYid,
                Nickname = dto.Nickname,
                Avatar = dto.Avatar,
                Region = dto.Region,
                Remark = dto.Remark,
                Contact_Status = dto.Contact_Status,
                Relation_Request_Status = dto.Relation_Request_Status,
                Created_At = dto.Created_At,
                Is_Online = dto.Is_Online,
                Relation_Status = dto.Relation_Status,
            };
        }
    }
}
