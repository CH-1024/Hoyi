using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Interfaces;
using HY.MAUI.Models;
using HY.MAUI.Pages.Chat;
using HY.MAUI.Pages.Contact;
using HY.MAUI.Pages.Login;
using HY.MAUI.Stores;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace HY.MAUI.PageModels.Contact
{
    public partial class ContactPageModel : ObservableObject
    {
        private bool _isNavigatedTo;
        private bool _dataLoaded;
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalCache _globalCache;
        private readonly ContactApi _contactApi;
        private readonly ContactStore _contactStore;
        private readonly ContactRequestStore _contactRequestStore;


        private bool isBusy;
        public bool IsBusy
        {
            get { return isBusy; }
            set { SetProperty(ref isBusy, value); }
        }

        private ObservableCollection<ContactVM> contactCollection = null;
        public ObservableCollection<ContactVM> ContactCollection
        {
            get { return contactCollection; }
            set { SetProperty(ref contactCollection, value); }
        }

        private ContactVM? selectedContact = null;
        public ContactVM? SelectedContact
        {
            get { return selectedContact; }
            set { SetProperty(ref selectedContact, value); }
        }


        public ContactPageModel(IServiceProvider serviceProvider, IGlobalCache globalCache, ContactApi contactApi, ContactStore contactStore, ContactRequestStore contactRequestStore)
        {
            _serviceProvider = serviceProvider;
            _globalCache = globalCache;
            _contactApi = contactApi;
            _contactStore = contactStore;
            _contactRequestStore = contactRequestStore;
        }

        private async Task LoadData()
        {
            try
            {
                IsBusy = true;

                ContactCollection = _contactStore.Contacts;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        void NavigatedTo()
        {
            _isNavigatedTo = true;
            SelectedContact = null;
        }

        [RelayCommand]
        void NavigatedFrom() => _isNavigatedTo = false;

        [RelayCommand]
        async Task Appearing()
        {
            if (!_dataLoaded)
            {
                ContactCollection = _contactStore.Contacts;
                _dataLoaded = true;
            }
            //else if (!_isNavigatedTo)
            //{
            //    await Refresh();
            //}
        }


        [RelayCommand]
        async Task Disappearing()
        {
        }

        [RelayCommand]
        async Task Refresh()
        {
            if (_isNavigatedTo)
            {
                IsBusy = true;

                await Task.Delay(2000);

                IsBusy = false;
            }
        }


        [RelayCommand]
        async Task SelectionChanged()
        {
            if (SelectedContact == null) return;

            var parameters = new Dictionary<string, object>
            {
                { "ContactInfo", SelectedContact }
            };
            await Shell.Current.GoToAsync(nameof(ContactDetailPage), true, parameters);
        }


        [RelayCommand]
        async Task SearchContact()
        {
            var parameters = new Dictionary<string, object>
            {
                //{ "ContactInfo", SelectedContact }
            };
            await Shell.Current.GoToAsync(nameof(SearchContactPage), true, parameters);
        }


        [RelayCommand]
        async Task Request()
        {
            var parameters = new Dictionary<string, object>
            {
                { "Type", "申请中" }
            };
            await Shell.Current.GoToAsync(nameof(ContactRequestDetailPage), true, parameters);
        }

        [RelayCommand]
        async Task Respond()
        {
            var parameters = new Dictionary<string, object>
            {
                { "Type", "待同意" }
            };
            await Shell.Current.GoToAsync(nameof(ContactRequestDetailPage), true, parameters);
        }

    }

}
