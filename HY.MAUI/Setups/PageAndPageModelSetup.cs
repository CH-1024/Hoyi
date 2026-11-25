using CommunityToolkit.Maui;
using HY.MAUI.PageModels.Chat;
using HY.MAUI.PageModels.Contact;
using HY.MAUI.PageModels.Login;
using HY.MAUI.PageModels.Mine;
using HY.MAUI.Pages.Chat;
using HY.MAUI.Pages.Contact;
using HY.MAUI.Pages.Login;
using HY.MAUI.Pages.Mine;
using HY.MAUI.Pages.Shells;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Setups
{
    public static class PageAndPageModelSetup
    {
        public static void AddPageAndPageModelSetup(this IServiceCollection services)
        {

            //AddTransientWithShellRoute	短暂	    每次导航需要新实例的页面，例如无状态或临时交互页面
            //AddScopedWithShellRoute	    每次导航	在单次导航作用域中需要共享状态的页面，例如表单编辑页面
            //AddSingletonWithShellRoute	全局唯一	需要全局共享状态的页面，例如设置页面、始终显示相同数据的仪表板页面

            services.AddTransient<AppShell, AppShellModel>();

            // shell flyout
            services.AddTransientWithShellRoute<NewPage1, NewPage1>(nameof(NewPage1));
            services.AddTransientWithShellRoute<NewPage2, NewPage2>(nameof(NewPage2));
            services.AddTransientWithShellRoute<NewPage3, NewPage3>(nameof(NewPage3));


            //shell 外 注册PM和P
            services.AddTransient<LoginPage, LoginPageModel>();
            services.AddTransient<RegisterPage, RegisterPageModel>();
            services.AddTransient<LoadingPage, LoadingPageModel>();


            //shell 根 只注册PM 不注册P
            services.AddTransient<ChatPageModel>();
            services.AddTransient<ContactPageModel>();
            services.AddTransient<MinePageModel>();


            //shell 子 需要注册PM和P 
            services.AddTransientWithShellRoute<SearchLocalChatPage, SearchLocalChatPageModel>(nameof(SearchLocalChatPage));
            services.AddTransientWithShellRoute<MessagePage, MessagePageModel>(nameof(MessagePage));
            services.AddTransientWithShellRoute<ImagePreviewPage, ImagePreviewPageModel>(nameof(ImagePreviewPage));
            services.AddTransientWithShellRoute<VideoPreviewPage, VideoPreviewPageModel>(nameof(VideoPreviewPage));
            services.AddTransientWithShellRoute<CallCreatePage, CallCreatePageModel>(nameof(CallCreatePage));
            services.AddTransientWithShellRoute<CallSelectPage, CallSelectPageModel>(nameof(CallSelectPage));
            services.AddTransientWithShellRoute<CallStartVideoPage, CallStartVideoPageModel>(nameof(CallStartVideoPage));
            services.AddTransientWithShellRoute<CallStartVoicePage, CallStartVoicePageModel>(nameof(CallStartVoicePage));
            services.AddTransientWithShellRoute<SearchLocalContactPage, SearchLocalContactPageModel>(nameof(SearchLocalContactPage));
            services.AddTransientWithShellRoute<SearchContactPage, SearchContactPageModel>(nameof(SearchContactPage));
            services.AddTransientWithShellRoute<ContactDetailPage, ContactDetailPageModel>(nameof(ContactDetailPage));
            services.AddTransientWithShellRoute<ContactRequestDetailPage, ContactRequestDetailPageModel>(nameof(ContactRequestDetailPage));
            services.AddTransientWithShellRoute<StrangerDetailPage, StrangerDetailPageModel>(nameof(StrangerDetailPage));
            services.AddTransientWithShellRoute<SettingPage, SettingPageModel>(nameof(SettingPage));
            services.AddTransientWithShellRoute<ProfilePage, ProfilePageModel>(nameof(ProfilePage));

        }

    }
}
