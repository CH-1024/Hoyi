using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using HY.MAUI.Extensions;
using HY.MAUI.Models;
using HY.MAUI.Models.MsgVM;
using HY.MAUI.PageModels.Chat;
using HY.MAUI.Controls;
using HY.MAUI.Pages.Chat.Popups;

namespace HY.MAUI.Pages.Chat;

public partial class MessagePage : ContentPage
{
    bool _expanded = false;
    double _expandHeight = -1;
    bool _isAnimating;

    public MessagePage(MessagePageModel message)
    {
        InitializeComponent();

        BindingContext = message;
    }

    private async void Popup_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        var option = new PopupOptions
        {
            // 背景色
            PageOverlayColor = Colors.Transparent,

            Shadow = null,
            Shape = null,

            // 边框形状
            //Shape = new RoundRectangle
            //{
            //    CornerRadius = new CornerRadius(5, 5, 0, 0),
            //    Stroke = Colors.Blue,
            //    StrokeThickness = 4
            //},

            // 边框阴影
            //Shadow = new Shadow
            //{
            //    Brush = Brush.Black,
            //    Offset = new Point(0, 0),
            //    Opacity = 1f
            //}
        };

        var pop = new AnimPopup1();
        await this.ShowPopupAsync(pop, option);
    }




    private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        await CollapseWithStatus();
    }

    private async void Record_Btn_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        await CollapseWithStatus();
    }


    private async void Switch_Expander_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        // 第一次计算真实高度
        if (_expandHeight < 0)
        {
            expander.HeightRequest = -1; // 让它自然布局以获取正确的高度

            _expandHeight = await GetContentHeight();

            await ExpandWithStatus();
        }
        else
        {
            if (_expanded) await CollapseWithStatus();
            else await ExpandWithStatus();
        }
    }

    private async void Send_Text_Clicked(object sender, EventArgs e)
    {
        await CollapseWithStatus();

        if (BindingContext is MessagePageModel vm)
        {
            vm.SendTextCommand.Execute(null);
        }
    }

    private async void Send_Image_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        await CollapseWithStatus();

        if (BindingContext is MessagePageModel vm)
        {
            vm.SendImageCommand.Execute(null);
        }
    }

    private async void Send_Video_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        await CollapseWithStatus();

        if (BindingContext is MessagePageModel vm)
        {
            vm.SendVideoCommand.Execute(null);
        }
    }

    private async void Send_VideoCall_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        await CollapseWithStatus();

        if (BindingContext is MessagePageModel vm)
        {
            vm.SendVideoCallCommand.Execute(null);
        }
    }

    private async void Send_VoiceCall_Clicked(object sender, EventArgs e)
    {
        if (editor.IsSoftInputShowing())
        {
            await editor.HideSoftInputAsync(CancellationToken.None);
        }

        await CollapseWithStatus();

        if (BindingContext is MessagePageModel vm)
        {
            vm.SendVoiceCallCommand.Execute(null);
        }
    }


    async Task ExpandWithStatus()
    {
        if (!_expanded)
        {
            await ExpandAnimate();
            _expanded = true;
        }
    }

    async Task CollapseWithStatus()
    {
        if (_expanded)
        {
            await CollapseAnimate();
            _expanded = false;
        }
    }

    async Task<double> GetContentHeight()
    {
        await Task.Yield(); // 等待一帧

        var size = expander.Measure(expander.Width, double.PositiveInfinity);

        return size.Height;
    }

    async Task ExpandAnimate()
    {
        if (_isAnimating) return;

        _isAnimating = true;

        await expander.AnimateAsync("expand", v => expander.HeightRequest = v, 0, _expandHeight, length: 250, easing: Easing.CubicOut);

        _isAnimating = false;
    }

    async Task CollapseAnimate()
    {
        if (_isAnimating) return;

        _isAnimating = true;

        await expander.AnimateAsync("collapse", v => expander.HeightRequest = v, _expandHeight, 0, length: 200, easing: Easing.CubicIn);

        _isAnimating = false;
    }

}
