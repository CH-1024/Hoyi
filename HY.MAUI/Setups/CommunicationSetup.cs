using HY.MAUI.Communication.Auth;
using HY.MAUI.Communication.Http;
using HY.MAUI.Communication.SignalR;
using HY.MAUI.Configurations;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Setups
{
    public static class CommunicationSetup
    {
        public static void AddCommunicationSetup(this IServiceCollection services)
        {
            // ==============================
            // Services
            // ==============================

            services.AddSingleton<ITokenProvider, TokenProvider>();
            services.AddSingleton<IAuthService, AuthService>();

            services.AddTransient<AuthHttpHandler>();
            services.AddTransient<UnsafeHttpClientHandler>();


            // ==============================
            // AuthService (无 Token)
            // ==============================

            services.AddHttpClient(nameof(AuthService), c =>
            {
                c.Timeout = TimeSpan.FromSeconds(ApiOptions.Timeout);
            })
            .ConfigurePrimaryHttpMessageHandler<UnsafeHttpClientHandler>();


            // ==============================
            // APIs (自动带 Token)
            // ==============================

            AddApiClient<LoginApi>(services, ApiOptions.Timeout);
            AddApiClient<ChatApi>(services, ApiOptions.Timeout);
            AddApiClient<MessageApi>(services, ApiOptions.Timeout);
            AddApiClient<CallApi>(services, ApiOptions.Timeout);
            AddApiClient<ContactApi>(services, ApiOptions.Timeout);
            AddApiClient<UserApi>(services, ApiOptions.Timeout);
            AddApiClient<FileApi>(services, ApiOptions.Timeout);
            AddApiClient<SyncApi>(services, ApiOptions.Timeout);


            // ==============================
            // SignalR
            // ==============================

            services.AddSingleton<ChatHubSignalR>();
        }


        /// <summary>
        /// 统一注册 API HttpClient
        /// </summary>
        private static void AddApiClient<T>(IServiceCollection services, double timeout) where T : class
        {
            services.AddHttpClient<T>(c =>
            {
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            //.ConfigurePrimaryHttpMessageHandler<UnsafeHttpClientHandler>()
            .AddHttpMessageHandler<AuthHttpHandler>();
        }
    }
}
