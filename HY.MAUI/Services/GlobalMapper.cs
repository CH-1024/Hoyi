using HY.MAUI.Dtos;
using HY.MAUI.Extensions;
using HY.MAUI.Extensions.Mapping;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using HY.MAUI.Stores;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Services
{
    public class GlobalMapper : IGlobalMapper
    {
        private readonly IGlobalCache _globalCache;

        readonly ContactStore _contactStore;

        public GlobalMapper(IGlobalCache globalCache, ContactStore contactStore)
        {
            _globalCache = globalCache;
            _contactStore = contactStore;
        }


        // Chat
        public ChatVM ToVM(ChatDto dto)
        {
            return dto.ToVM();
        }


        // Contact
        public ContactVM ToVM(ContactDto dto)
        {
            return dto.ToVM();
        }


        // ContactRequest
        public ContactRequestVM ToVM(ContactRequestDto dto)
        {
            var currentUser = _globalCache.GetCurrentUser();

            return dto.ToVM(currentUser.Id);
        }


        // Message
        public MessageVM ToVM(MessageDto dto)
        {
            var currentUser = _globalCache.GetCurrentUser();
            var contact = _contactStore.GetContact(dto.Sender_Id);

            return dto.ToVM(currentUser.Id, contact?.Avatar, contact?.Nickname);
        }

        public MessageDto ToDto(MessageVM vm)
        {
            return vm.ToDto();
        }


        // User
        public UserVM ToVM(UserDto dto)
        {
            return dto.ToVM();
        }
    }
}
