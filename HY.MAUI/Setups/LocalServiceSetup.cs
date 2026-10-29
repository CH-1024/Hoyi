using HY.MAUI.Interfaces;
using HY.MAUI.SendQueue;
using HY.MAUI.Services;
using HY.MAUI.Stores;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Setups
{
    public static class LocalServiceSetup
    {
        public static void AddLocalServiceSetup(this IServiceCollection services)
        {
            services.AddSingleton<IGlobalCache, GlobalCache>();
            services.AddSingleton<ILoginService, LoginService>();

            services.AddSingleton<MessageSendQueue>();
            services.AddSingleton<MessageSendService>();
            services.AddSingleton<MessageSendWorker>();

            services.AddSingleton<ChatStore>();
            services.AddSingleton<ContactStore>();
            services.AddSingleton<ContactRequestStore>();
            services.AddSingleton<MessageStore>();


#if WINDOWS
            Microsoft.Maui.Handlers.ScrollViewHandler.Mapper.AppendToMapping(nameof(IScrollView.Content), (handler, view) =>
            {
                if (handler.PlatformView?.Content is ContentPanel panel) panel.IsTabStop = true;
            });
#endif

#if IOS || MACCATALYST
            Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("CancelButtonColor", (handler, view) =>
            {
                handler.PlatformView.SetShowsCancelButton(false, false);
            });
#endif


#if WINDOWS
            services.AddSingleton<IVideoRecorderService, Platforms.Windows.VideoRecorderService>();
#elif ANDROID
            services.AddSingleton<IVideoRecorderService, Platforms.Android.VideoRecorderService>();
#elif MACCATALYST
            services.AddSingleton<IVideoRecorderService, Platforms.MacCatalyst.VideoRecorderService>();
#elif IOS
            services.AddSingleton<IVideoRecorderService, Platforms.iOS.VideoRecorderService>();
#endif
            
            
#if WINDOWS
            services.AddSingleton<IAudioRecorderService, Platforms.Windows.AudioRecorderService>();
#elif ANDROID
            services.AddSingleton<IAudioRecorderService, Platforms.Android.AudioRecorderService>();
#elif MACCATALYST
            services.AddSingleton<IAudioRecorderService, Platforms.MacCatalyst.AudioRecorderService>();
#elif IOS
            services.AddSingleton<IAudioRecorderService, Platforms.iOS.AudioRecorderService>();
#endif
            
            
#if ANDROID
            services.AddTransient<INotificationManagerService, Platforms.Android.NotificationManagerService>();
#elif IOS
            services.AddTransient<INotificationManagerService, Platforms.iOS.NotificationManagerService>();
#elif MACCATALYST
            services.AddTransient<INotificationManagerService, Platforms.MacCatalyst.NotificationManagerService>();
#elif WINDOWS
            services.AddTransient<INotificationManagerService, Platforms.Windows.NotificationManagerService>();
#endif
            
        }
    }
}
