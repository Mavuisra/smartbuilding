using System.Windows;
using System.Windows.Media.Animation;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class SplashWindow : Window
{
    private TaskCompletionSource? _fadeOutTcs;

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BeginStoryboard((Storyboard)FindResource("FadeInStoryboard"));
        BeginStoryboard((Storyboard)FindResource("LogoPulseStoryboard"));
        BeginStoryboard((Storyboard)FindResource("RingRotateStoryboard"));
        BeginStoryboard((Storyboard)FindResource("DotWaveStoryboard"));
    }

    public void UpdateProgress(double percent, string status)
    {
        Dispatcher.Invoke(() =>
        {
            LoadProgress.Value = Math.Clamp(percent, 0, 100);
            StatusText.Text = status;
        });
    }

    public void ApplyBranding(string companyName, string subtitle)
    {
        Dispatcher.Invoke(() =>
        {
            Title = companyName;
            BrandTitleText.Text = companyName;
            BrandSubtitleText.Text = subtitle;
        });
    }

    public Task CloseAnimatedAsync()
    {
        _fadeOutTcs = new TaskCompletionSource();
        var fadeOut = (Storyboard)FindResource("FadeOutStoryboard")!;
        fadeOut.Completed += OnFadeOutCompleted;
        fadeOut.Begin(this);
        return _fadeOutTcs.Task;
    }

    private void OnFadeOutCompleted(object? sender, EventArgs e)
    {
        Close();
        _fadeOutTcs?.TrySetResult();
    }
}
