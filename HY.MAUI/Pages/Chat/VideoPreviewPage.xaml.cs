using HY.MAUI.PageModels.Chat;

namespace HY.MAUI.Pages.Chat;

public partial class VideoPreviewPage : ContentPage
{
    public VideoPreviewPage(VideoPreviewPageModel videoPreview)
    {
        InitializeComponent();

        BindingContext = videoPreview;
    }

    private void Video_Tapped(object sender, TappedEventArgs e)
    {

    }
}