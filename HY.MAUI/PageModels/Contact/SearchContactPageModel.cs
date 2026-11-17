using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Dtos;
using HY.MAUI.Enums;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using HY.MAUI.Pages.Contact;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.PageModels.Contact
{
    public partial class SearchContactPageModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalCache _globalCache;
        private readonly IGlobalMapper _globalMapper;

        private readonly ContactApi _contactApi;

        public SearchContactPageModel(IServiceProvider serviceProvider, IGlobalCache globalCache, IGlobalMapper globalMapper, ContactApi contactApi)
        {
            _serviceProvider = serviceProvider;
            _globalCache = globalCache;
            _globalMapper = globalMapper;
            _contactApi = contactApi;
        }


        [RelayCommand]
        async Task SearchContact(string identity)
        {
            var resp = await _contactApi.SearchContact(identity);
            if (resp?.IsSucc == true)
            {
                var contact = resp.GetValue<ContactDto>("Contact");

                if (contact!.Relation_Status == RelationStatus.Friend)
                {
                    // 是联系人
                    var parameters = new Dictionary<string, object>
                    {
                        { "ContactInfo", _globalMapper.ToVM(contact) }
                    };

                    await Shell.Current.GoToAsync(nameof(ContactDetailPage), true, parameters);
                }
                else
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "Source", 2 },
                        { "ContactInfo", _globalMapper.ToVM(contact) },
                    };
                    await Shell.Current.GoToAsync(nameof(StrangerDetailPage), true, parameters);
                }
            }
            else
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", resp?.Msg, "OK");
            }
        }

    }
}
