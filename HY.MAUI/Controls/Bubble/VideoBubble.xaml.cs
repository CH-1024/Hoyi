using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Enums;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.PageModels.Chat.MessageCommands;

namespace HY.MAUI.Controls;

public partial class VideoBubble : ContentView
{
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(IAsyncRelayCommand), typeof(VideoBubble), null);
    public IAsyncRelayCommand Command
    {
        get => (IAsyncRelayCommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }



    public VideoBubble()
    {
        InitializeComponent();
    }







    private void ContactDetail_Tapped(object sender, TappedEventArgs e)
    {
        var param = new MessageCommandInvocation
        {
            Command = CommandNames.ContactDetail,
            Message = this.BindingContext as VideoMessageVM,
        };

        if (Command != null && Command.CanExecute(param))
        {
            Command.Execute(param);
        }
    }

    private async void Bubble_Primary_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.BindingContext is not VideoMessageVM msg)
            return;

        if (msg.Message_Status == MessageStatus.Recalled)
            return;

        var param = new MessageCommandInvocation
        {
            Command = CommandNames.TapVideoMessage,
            Message = msg,
        };

        if (Command != null && Command.CanExecute(param))
        {
            await Command.ExecuteAsync(param);
        }
    }

    private void Bubble_Secondary_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border border)
            return;

        if (border.BindingContext is not VideoMessageVM msg)
            return;

        if (msg.Message_Status == MessageStatus.Recalled)
        {
            // 已撤回消息不显示菜单
            FlyoutBase.SetContextFlyout(border, null);
        }
        else
        {
            var flyout = CreateFlyout(msg);
            FlyoutBase.SetContextFlyout(border, flyout);
        }
    }

    private async void MenuFlyoutItem_Copy_Clicked(VideoMessageVM msg)
    {
        //await Clipboard.SetTextAsync(msg.Content ?? string.Empty);
    }

    private async Task MenuFlyoutItem_Delete_Clicked(VideoMessageVM msg)
    {
        var param = new MessageCommandInvocation
        {
            Command = CommandNames.DeleteMessage,
            Message = msg,
        };

        if (Command != null && Command.CanExecute(param))
        {
            await Command.ExecuteAsync(param);
        }
    }

    private async Task MenuFlyoutItem_Download_Clicked(VideoMessageVM msg)
    {
        var param = new MessageCommandInvocation
        {
            Command = CommandNames.DownloadFile,
            Message = msg,
        };

        if (Command != null && Command.CanExecute(param))
        {
            await Command.ExecuteAsync(param);
        }
    }
    
    private async Task MenuFlyoutItem_Recall_Clicked(VideoMessageVM msg)
    {
        if (msg.IsSelf)
        {
            var param = new MessageCommandInvocation
            {
                Command = CommandNames.RecallMessage,
                Message = msg,
            };

            if (Command != null && Command.CanExecute(param))
            {
                await Command.ExecuteAsync(param);
            }
        }
        else
        {
            _ = Application.Current!.Windows[0].Page!.DisplayAlertAsync("提示", "你只能撤回自己的消息", "确定");
        }
    }



    private MenuFlyout CreateFlyout(VideoMessageVM msg)
    {
        var flyout = new MenuFlyout();

        var canDownload = msg.Message_Status == MessageStatus.Sented;
        if (canDownload)
        {
            flyout.Add(new MenuFlyoutItem
            {
                Text = "下载",
                Command = new Command(async () => await MenuFlyoutItem_Download_Clicked(msg))
            });
        }
        
        flyout.Add(new MenuFlyoutItem
        {
            Text = "复制",
            Command = new Command(() => MenuFlyoutItem_Copy_Clicked(msg))
        });

        var canDelete = msg.Message_Status == MessageStatus.Sented;
        if (canDelete)
        {
            flyout.Add(new MenuFlyoutItem
            {
                Text = "删除",
                Command = new Command(async () => await MenuFlyoutItem_Delete_Clicked(msg))
            });
        }

        var canRecall = msg.IsSelf && DateTime.UtcNow <= msg.Created_At.AddMinutes(5) && msg.Message_Status == MessageStatus.Sented;
        if (canRecall)
        {
            flyout.Add(new MenuFlyoutItem
            {
                Text = "撤回",
                Command = new Command(async () => await MenuFlyoutItem_Recall_Clicked(msg))
            });
        }

        return flyout;
    }

}