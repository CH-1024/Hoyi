using HY.MAUI.PageModels.Login;
using HY.MAUI.Pages.Login;

namespace HY.MAUI.Pages.Login
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage(LoginPageModel login)
        {
            InitializeComponent();

            BindingContext = login;
        }

        private void TapGestureRecognizer_OnTapped(object? sender, TappedEventArgs e)
        {
            autoLogin.IsChecked = !autoLogin.IsChecked;
        }
    }
}
