using CommunityToolkit.Mvvm.Input;
using HY.MAUI.Enums;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.PageModels.Chat.MessageCommands;

namespace HY.MAUI.Controls;

public partial class CallVideoBubble : ContentView
{
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(IAsyncRelayCommand), typeof(CallVideoBubble), null);
    public IAsyncRelayCommand Command
    {
        get => (IAsyncRelayCommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }



    public CallVideoBubble()
    {
        InitializeComponent();
    }







    private void ContactDetail_Tapped(object sender, TappedEventArgs e)
    {
        var param = new MessageCommandInvocation
        {
            Command = CommandNames.ContactDetail,
            Message = this.BindingContext as CallVideoMessageVM,
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

        if (border.BindingContext is not CallVideoMessageVM msg)
            return;

        var param = new MessageCommandInvocation
        {
            Command = CommandNames.TapVideoCallMessage,
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

        if (border.BindingContext is not CallVideoMessageVM msg)
            return;

        var flyout = CreateFlyout(msg);
        FlyoutBase.SetContextFlyout(border, flyout);
    }

    private async Task MenuFlyoutItem_Delete_Clicked(CallVideoMessageVM msg)
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



    private MenuFlyout CreateFlyout(CallVideoMessageVM msg)
    {
        var flyout = new MenuFlyout();

        var canDelete = msg.Message_Status == MessageStatus.Sented;
        if (canDelete)
        {
            flyout.Add(new MenuFlyoutItem
            {
                Text = "删除",
                Command = new Command(async () => await MenuFlyoutItem_Delete_Clicked(msg))
            });
        }

        return flyout;
    }
}