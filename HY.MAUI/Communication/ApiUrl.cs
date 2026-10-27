using HY.MAUI.Configurations;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Communication
{
    public static class ApiUrl
    {
        public static string Address => ApiOptions.Address;
        public static string Port => ApiOptions.Port;

        private static string SignalRBase => $"https://{Address}:{Port}";
        private static string HttpBase => $"https://{Address}:{Port}";




        #region SignalR

        public static string ChatHub => $"{SignalRBase}/chatHub/";

        #endregion



        #region Http

        // Test
        public static string Ping1 => $"{HttpBase}/auth/ping1/";
        public static string Ping2 => $"{HttpBase}/auth/ping2/";


        // Login & Auth
        public static string Refresh => $"{HttpBase}/auth/refresh/";
        public static string Login => $"{HttpBase}/auth/login/";
        public static string Logout => $"{HttpBase}/auth/logout/";


        // Chat
        public static string GetChats => $"{HttpBase}/chat/get/chats/";
        public static string ReadAll => $"{HttpBase}/chat/read/all/";


        // Message
        public static string GetMessages => $"{HttpBase}/message/gets/";
        public static string SendMessage => $"{HttpBase}/message/send/";
        public static string RecallMessage => $"{HttpBase}/message/recall/";
        public static string DeleteMessage => $"{HttpBase}/message/delete/";


        // Call
        public static string CreateCall => $"{HttpBase}/call/create/";
        public static string AcceptCall => $"{HttpBase}/call/accept/";
        public static string CancelCall => $"{HttpBase}/call/cancel/";
        public static string RejectCall => $"{HttpBase}/call/reject/";
        public static string HangUpCall => $"{HttpBase}/call/hangup/";
        public static string CallConnected => $"{HttpBase}/call/connected/";



        // File
        public static string Challenge => $"{HttpBase}/file/challenge/";
        public static string ChallengeConfirm => $"{HttpBase}/file/challenge/confirm/";
        public static string UploadImage => $"{HttpBase}/file/upload/image/";
        public static string UploadVideo => $"{HttpBase}/file/upload/video/";
        public static string UploadHead => $"{HttpBase}/file/upload/head/";


        // Contact
        public static string GetContactRequests => $"{HttpBase}/contact/get/contactrequests/";
        public static string GetContacts => $"{HttpBase}/contact/get/contacts/";
        public static string GetContact => $"{HttpBase}/contact/get/contact/";
        public static string SearchContact => $"{HttpBase}/contact/search/contact/";
        public static string RequestContact => $"{HttpBase}/contact/request/contact/";
        public static string RespondContact => $"{HttpBase}/contact/respond/contact/";
        public static string DeleteContact => $"{HttpBase}/contact/delete/contact/";


        // User
        public static string Register => $"{HttpBase}/user/register/";
        public static string UpdateHead => $"{HttpBase}/user/update/head/";
        public static string UpdateProfile => $"{HttpBase}/user/update/profile/";
        #endregion



        // Image
        public static string? Get_Origin_Image(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/image/origin/{file_Id}";
        public static string? Get_Compress_Image(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/image/compress/{file_Id}";
        public static string? Get_Thumb_Image(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/image/thumb/{file_Id}";




        // Video
        public static string? Get_Origin_Video(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/video/origin/{file_Id}";
        public static string? Get_Compress_Video(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/video/compress/{file_Id}";
        public static string? Get_Cover_Video(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/video/cover/{file_Id}";




        // Head
        public static string? Get_Head_Image(string? file_Id) => file_Id == null ? null : $"{HttpBase}/file/head/{file_Id}";

    }
}