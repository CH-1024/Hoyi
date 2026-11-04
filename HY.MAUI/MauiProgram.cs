using CommunityToolkit.Maui;
using HY.MAUI.Communication;
using HY.MAUI.Communication.Auth;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Controls;
using HY.MAUI.Interfaces;
using HY.MAUI.PageModels.Chat;
using HY.MAUI.PageModels.Contact;
using HY.MAUI.PageModels.Login;
using HY.MAUI.PageModels.Mine;
using HY.MAUI.Pages.Chat;
using HY.MAUI.Pages.Contact;
using HY.MAUI.Pages.Login;
using HY.MAUI.Pages.Mine;
using HY.MAUI.Services;
using HY.MAUI.Setups;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace HY.MAUI
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitCamera()
                .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.AddConfigureMauiHandlers();
            builder.Services.AddLocalServiceSetup();
            builder.Services.AddCommunicationSetup();
            builder.Services.AddPageAndPageModelSetup();


            var app = builder.Build();

            Services = app.Services;

            return app;
        }
    }
}
