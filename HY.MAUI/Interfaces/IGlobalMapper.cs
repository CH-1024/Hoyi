using HY.MAUI.Dtos;
using HY.MAUI.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Interfaces
{
    public interface IGlobalMapper
    {
        // Chat
        ChatVM ToVM(ChatDto dto);

        // Contact
        ContactVM ToVM(ContactDto dto);

        // ContactRequest
        ContactRequestVM ToVM(ContactRequestDto dto);

        // Message
        MessageVM ToVM(MessageDto dto);
        MessageDto ToDto(MessageVM vm);

        // User
        UserVM ToVM(UserDto dto);
    }
}
