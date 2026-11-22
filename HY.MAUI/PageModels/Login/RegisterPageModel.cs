using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Communication.Http;
using HY.MAUI.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.PageModels.Login
{
    public partial class RegisterPageModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly LoginApi _loginApi;

        private string nickname = null;
        public string Nickname
        {
            get { return nickname; }
            set { SetProperty(ref nickname, value); }
        }

        private string userName = null;
        public string Username
        {
            get { return userName; }
            set { SetProperty(ref userName, value); }
        }

        private string password = null;
        public string Password
        {
            get { return password; }
            set { SetProperty(ref password, value); }
        }

        private string confirmPassword = null;
        public string ConfirmPassword
        {
            get { return confirmPassword; }
            set { SetProperty(ref confirmPassword, value); }
        }

        private string email = null;
        public string Email
        {
            get { return email; }
            set { SetProperty(ref email, value); }
        }

        private string phone = null;
        public string Phone
        {
            get { return phone; }
            set { SetProperty(ref phone, value); }
        }


        public RegisterPageModel(IServiceProvider serviceProvider, LoginApi loginApi)
        {
            _serviceProvider = serviceProvider;
            _loginApi = loginApi;
        }



        [RelayCommand]
        async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Nickname) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(ConfirmPassword) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Phone))
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("错误", "请填写所有字段", "确定");
                return;
            }
            if (Password != ConfirmPassword)
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("错误", "两次输入的密码不一致", "确定");
                return;
            }
            if (!Phone.IsPhone())
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("错误", "请输入有效的手机号", "确定");
                return;
            }

            var registerResp = await _loginApi.Register(Nickname, Username, Password, Email, Phone);
            if (registerResp?.IsSucc == true)
            {
                // 返回登录页
                await Application.Current!.Windows[0].Page!.Navigation.PopAsync(true);
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("成功", "注册成功，请登录", "确定");
            }
            else
            {
                _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("失败", $"注册失败[{registerResp?.Msg}]", "确定");
            }
        }

        [RelayCommand]
        async Task Back()
        {
            await Application.Current!.Windows[0].Page!.Navigation.PopAsync(true);
        }

    }
}
