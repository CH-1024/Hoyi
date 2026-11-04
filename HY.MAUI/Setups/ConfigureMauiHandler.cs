using HY.MAUI.Controls;
using System;
using System.Collections.Generic;
using System.Text;

namespace HY.MAUI.Setups
{
    public static class ConfigureMauiHandler
    {
        public static void AddConfigureMauiHandlers(this MauiAppBuilder builder)
        {

#if WINDOWS
            Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
            {
                handler.PlatformView.SingleSelectionFollowsFocus = false;
            });

            //Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
            //{
            // if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
            // {
            //  contentPanel.IsTabStop = true;
            // }
            //});
#endif


#if WINDOWS
            Microsoft.Maui.Handlers.ScrollViewHandler.Mapper.AppendToMapping(nameof(IScrollView.Content), (handler, view) =>
            {
                if (handler.PlatformView?.Content is Microsoft.Maui.Platform.ContentPanel panel) panel.IsTabStop = true;
            });
#endif

#if IOS || MACCATALYST
            Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("CancelButtonColor", (handler, view) =>
            {
                handler.PlatformView.SetShowsCancelButton(false, false);
            });
#endif


#if ANDROID
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<ChatEditor, Platforms.Android.ChatEditorHandler>();
            });
#endif

#if IOS
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<ChatEditor, Platforms.iOS.ChatEditorHandler>();
            });
#endif

#if MACCATALYST
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<SelectableLabel, Platforms.MacCatalyst.SelectableLabelHandler>();
                handlers.AddHandler<ChatEditor, Platforms.MacCatalyst.ChatEditorHandler>();
            });
#endif

#if WINDOWS
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<SelectableLabel, Platforms.Windows.SelectableLabelHandler>();
                handlers.AddHandler<ChatEditor, Platforms.Windows.ChatEditorHandler>();
            });
#endif

        }
    }
}
