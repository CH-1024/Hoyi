using HY.ApiService.Dtos;
using HY.ApiService.Entities;
using HY.ApiService.Enums;
using Mapster;

namespace HY.ApiService.Setups
{
    public static class MapperSetup
    {
        public static void AddMapperSetup(this IServiceCollection services)
        {
            EntityToDto();
            DtoToEntity();
        }



        private static void EntityToDto()
        {
            TypeAdapterConfig<UserEntity, UserDto>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.HYid, src => src.HYid)
                .Map(dest => dest.Nickname, src => src.Nickname)
                .Map(dest => dest.Avatar, src => src.Avatar)
                .Map(dest => dest.Phone, src => src.Phone)
                .Map(dest => dest.Email, src => src.Email)
                .Map(dest => dest.Region, src => src.Region)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<MessageEntity, MessageDto>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Chat_Type, src => src.Chat_Type)
                //.Map(dest => dest.Chat_Id, src => src.Chat_Id)
                .Map(dest => dest.Sender_Id, src => src.Sender_Id)
                .Map(dest => dest.Target_Id, src => src.Target_Id)
                .Map(dest => dest.Message_Type, src => src.Message_Type)
                .Map(dest => dest.Content, src => src.Content)
                .Map(dest => dest.Extra, src => src.Extra)
                .Map(dest => dest.Message_Status, src => src.Message_Status)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<ContactEntity, ContactDto>.NewConfig()
                .Map(dest => dest.Contact_Id, src => src.Contact_Id)
                .Map(dest => dest.Contact_Request_Id, src => src.Contact_Request_Id)
                .Map(dest => dest.Remark, src => src.Remark)
                .Map(dest => dest.Relation_Status, src => src.Relation_Status)
                .Map(dest => dest.Created_At, src => src.Created_At)
                .Ignore(dest => dest.Nickname)
                .Ignore(dest => dest.Avatar)
                .Ignore(dest => dest.Region)
                .Ignore(dest => dest.Contact_Status)
                .Ignore(dest => dest.Is_Online)
                .Ignore(dest => dest.Relation_Request_Status);


            TypeAdapterConfig<ContactRequestEntity, ContactRequestDto>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Sender_Id, src => src.Sender_Id)
                .Map(dest => dest.Receiver_Id, src => src.Receiver_Id)
                .Map(dest => dest.Message, src => src.Message)
                .Map(dest => dest.Source, src => src.Source)
                .Map(dest => dest.Relation_Request_Status, src => src.Relation_Request_Status)
                .Map(dest => dest.Created_At, src => src.Created_At)
                .Map(dest => dest.Handled_At, src => src.Handled_At)
                .Ignore(dest => dest.Sender_Avatar)
                .Ignore(dest => dest.Sender_Nickname)
                .Ignore(dest => dest.Receiver_Avatar)
                .Ignore(dest => dest.Receiver_Nickname);


            TypeAdapterConfig<ChatEntity, ChatDto>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Type, src => src.Type)
                .Map(dest => dest.Target_Id, src => src.Target_Id)
                .Map(dest => dest.Unread_Count, src => src.Unread_Count)
                .Map(dest => dest.Is_Top, src => src.Is_Top)
                .Map(dest => dest.Is_Deleted, src => src.Is_Deleted)
                .Ignore(dest => dest.Target_Name)
                .Ignore(dest => dest.Target_Avatar)
                .Ignore(dest => dest.Last_Msg);


            TypeAdapterConfig<GroupEntity, GroupDto>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Name, src => src.Name)
                .Map(dest => dest.Owner_Id, src => src.Owner_Id)
                .Map(dest => dest.Avatar, src => src.Avatar)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<GroupMemberEntity, GroupMemberDto>.NewConfig()
                .Map(dest => dest.Group_Id, src => src.Group_Id)
                .Map(dest => dest.User_Id, src => src.User_Id)
                .Map(dest => dest.Role, src => src.Role)
                .Map(dest => dest.Nickname, src => src.Nickname)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<SyncEventEntity, SyncEventDto>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Event_Type, src => src.Event_Type)
                .Map(dest => dest.Entity_Id, src => src.Entity_Id)
                .Map(dest => dest.Related_Id, src => src.Related_Id)
                .Map(dest => dest.Created_At, src => src.Created_At);
        }


        private static void DtoToEntity()
        {
            TypeAdapterConfig<UserDto, UserEntity>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.HYid, src => src.HYid)
                .Map(dest => dest.Nickname, src => src.Nickname)
                .Map(dest => dest.Avatar, src => src.Avatar)
                .Map(dest => dest.Phone, src => src.Phone)
                .Map(dest => dest.Email, src => src.Email)
                .Map(dest => dest.Region, src => src.Region)
                .Map(dest => dest.Status, src => src.Status)
                .Map(dest => dest.Created_At, src => src.Created_At)
                .Ignore (dest => dest.Username)
                .Ignore (dest => dest.Password_Hash)
                .Ignore (dest => dest.Password_Salt);


            TypeAdapterConfig<MessageDto, MessageEntity>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Chat_Type, src => src.Chat_Type)
                //.Map(dest => dest.Chat_Id, src => src.Chat_Id)
                .Map(dest => dest.Sender_Id, src => src.Sender_Id)
                .Map(dest => dest.Target_Id, src => src.Target_Id)
                .Map(dest => dest.Message_Type, src => src.Message_Type)
                .Map(dest => dest.Content, src => src.Content)
                .Map(dest => dest.Extra, src => src.Extra)
                .Map(dest => dest.Message_Status, src => src.Message_Status)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<ContactDto, ContactEntity>.NewConfig()
                .Map(dest => dest.Contact_Id, src => src.Contact_Id)
                .Map(dest => dest.Contact_Request_Id, src => src.Contact_Request_Id)
                .Map(dest => dest.Remark, src => src.Remark)
                .Map(dest => dest.Relation_Status, src => src.Relation_Status)
                .Map(dest => dest.Created_At, src => src.Created_At)
                .Ignore(dest => dest.User_Id);


            TypeAdapterConfig<ContactRequestDto, ContactRequestEntity>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Sender_Id, src => src.Sender_Id)
                .Map(dest => dest.Receiver_Id, src => src.Receiver_Id)
                .Map(dest => dest.Message, src => src.Message)
                .Map(dest => dest.Source, src => src.Source)
                .Map(dest => dest.Relation_Request_Status, src => src.Relation_Request_Status)
                .Map(dest => dest.Created_At, src => src.Created_At)
                .Map(dest => dest.Handled_At, src => src.Handled_At);


            TypeAdapterConfig<ChatDto, ChatEntity>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Type, src => src.Type)
                .Map(dest => dest.Target_Id, src => src.Target_Id)
                .Map(dest => dest.Is_Top, src => src.Is_Top)
                .Map(dest => dest.Is_Deleted, src => src.Is_Deleted)
                .Map(dest => dest.Last_Msg_Id, src => src.Last_Msg == null ? 0 : src.Last_Msg.Id)
                .Map(dest => dest.Last_Msg_Time, src => src.Last_Msg == null ? (DateTime?)null : src.Last_Msg.Created_At)
                .Map(dest => dest.Unread_Count, src => src.Unread_Count)
                .Ignore(dest => dest.User_Id)
                .Ignore(dest => dest.Read_Msg_Id);


            TypeAdapterConfig<GroupDto, GroupEntity>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Name, src => src.Name)
                .Map(dest => dest.Owner_Id, src => src.Owner_Id)
                .Map(dest => dest.Avatar, src => src.Avatar)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<GroupMemberDto, GroupMemberEntity>.NewConfig()
                .Map(dest => dest.Group_Id, src => src.Group_Id)
                .Map(dest => dest.User_Id, src => src.User_Id)
                .Map(dest => dest.Role, src => src.Role)
                .Map(dest => dest.Nickname, src => src.Nickname)
                .Map(dest => dest.Created_At, src => src.Created_At);


            TypeAdapterConfig<SyncEventDto, SyncEventEntity>.NewConfig()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Event_Type, src => src.Event_Type)
                .Map(dest => dest.Entity_Id, src => src.Entity_Id)
                .Map(dest => dest.Related_Id, src => src.Related_Id)
                .Map(dest => dest.Created_At, src => src.Created_At)
                .Ignore(dest => dest.User_Id);
        }


    }
}