using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication;
using HY.MAUI.Communication.Auth;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Dtos;
using HY.MAUI.Interfaces;
using HY.MAUI.Mapping;
using HY.MAUI.Pages.Login;
using HY.MAUI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace HY.MAUI.PageModels.Login
{
    public partial class LoginPageModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoginService _loginService;
        private readonly ITokenProvider _tokenProvider;
        private readonly LoginApi _loginApi;

        private string userName;
        public string Username
        {
            get { return userName; }
            set { SetProperty(ref userName, value); }
        }

        private string password;
        public string Password
        {
            get { return password; }
            set { SetProperty(ref password, value); }
        }


        public LoginPageModel(IServiceProvider serviceProvider, ILoginService loginService, ITokenProvider tokenProvider, LoginApi loginApi)
        {
            _serviceProvider = serviceProvider;
            _loginService = loginService;
            _tokenProvider = tokenProvider;
            _loginApi = loginApi;
        }

        [RelayCommand]
        void Appearing()
        {
            _loginService.Logout();
            _tokenProvider.Clear();
        }

        [RelayCommand]
        async Task Login()
        {
            if (!await Request_Camera_Async())
            {
                _ = Shell.Current.DisplayAlertAsync("提示", $"请允许访问相机！", "退出");
                return;
            }
            if (!await Request_Microphone_Async())
            {
                _ = Shell.Current.DisplayAlertAsync("提示", $"请允许访问麦克风！", "退出");
                return;
            }
            if (!await Request_PostNotifications_Async())
            {
                _ = Shell.Current.DisplayAlertAsync("提示", $"请允许访问通知！", "退出");
                return;
            }


            //var p1 = await _loginApi.Ping1();
            //var p2 = await _loginApi.Ping2();

            var loginResp = await _loginApi.Login(Username, Password);
            if (loginResp?.IsSucc == true)
            {
                var tokens = loginResp.GetValue<TokenModel>("Tokens");
                await _tokenProvider.SetAsync(tokens!);

                var userDto = loginResp.GetValue<UserDto>("User")!;
                await _loginService.Login(userDto.ToVM());

                var loadingPage = _serviceProvider.GetRequiredService<LoadingPage>();
                Application.Current!.Windows[0].Page = new NavigationPage(loadingPage);
            }
            else
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("失败", loginResp?.Msg ?? "Unknown error", "OK");
            }
        }

        [RelayCommand]
        async Task Register()
        {
            var registerPage = _serviceProvider.GetRequiredService<RegisterPage>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(registerPage, true);
        }

        
        
        
        
        
        
        async Task<bool> Request_Camera_Async()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                return await Permissions.RequestAsync<Permissions.Camera>() == PermissionStatus.Granted;
            }
            return true;
        }

        async Task<bool> Request_Microphone_Async()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                return await Permissions.RequestAsync<Permissions.Microphone>() == PermissionStatus.Granted;
            }
            return true;
        }
        
        async Task<bool> Request_PostNotifications_Async()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                return await Permissions.RequestAsync<Permissions.PostNotifications>() == PermissionStatus.Granted;
            }
            return true;
        }
    }
}
