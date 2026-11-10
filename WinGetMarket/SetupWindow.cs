using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace WinGetMarket;
public sealed class SetupWindow : Window
{
    private readonly Button install = new() { Content = "Install or repair WinGet" };
    private readonly Button verify = new() { Content = "Check again" };
    private readonly Button next = new() { Content = "Open WinGet Market", IsEnabled = false };
    private readonly Button store = new() { Content = "Open App Installer in Microsoft Store" };
    private readonly ProgressBar busy = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly TextBlock status = new() { Text = "Checking your Windows Package Manager…", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox log = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 130, Visibility = Visibility.Collapsed };
    private readonly Action completed;
    private bool verified;
    private bool running;
    public SetupWindow(Action completed)
    {
        this.completed = completed;
        Title = "Set up WinGet Market";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(760, 760));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WinGetMarket.ico"));
        var body = new StackPanel { Spacing = 18, Padding = new(36), MaxWidth = 680, HorizontalAlignment = HorizontalAlignment.Center };
        body.Children.Add(new Image { Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/WinGetMarketLogo.png")), Width = 240, Height = 80, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left });
        body.Children.Add(new TextBlock { Text = "Let's get your app store ready", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = "WinGet Market uses Windows Package Manager to find, install, and update apps. We'll check it before you continue.", TextWrapping = TextWrapping.Wrap });
        body.Children.Add(status);
        body.Children.Add(busy);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        install.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        actions.Children.Add(install); actions.Children.Add(verify); body.Children.Add(actions);
        body.Children.Add(new TextBlock { Text = "Install or repair downloads Microsoft's WinGet client module from PowerShell Gallery and installs App Installer and its dependencies for your Windows account. Internet access is required. Windows or organization policies may block installation.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(store);
        body.Children.Add(log);
        body.Children.Add(next);
        var root = new Grid { RequestedTheme = ElementTheme.Dark, Background = new SolidColorBrush(Color.FromArgb(255, 16, 17, 22)) };
        root.Children.Add(new ScrollViewer { Content = body }); Content = root;
        install.Click += async (_, _) => await Install();
        verify.Click += async (_, _) => await Check();
        store.Click += async (_, _) =>
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1")); status.Text = "After installing App Installer in Microsoft Store, select Check again."; }
            catch (Exception ex) { status.Text = $"Could not open Microsoft Store: {ex.Message}"; }
        };
        next.Click += async (_, _) =>
        {
            if (!verified || running) return;
            await Check();
            if (!verified) return;
            try { SetupService.Complete(); completed(); }
            catch (Exception ex) { status.Text = $"Could not finish setup: {ex.Message}"; }
        };
        root.Loaded += async (_, _) => await Check();
    }
    private void SetBusy(bool value)
    {
        running = value;
        busy.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        install.IsEnabled = verify.IsEnabled = store.IsEnabled = !value;
        next.IsEnabled = !value && verified;
    }
    private async Task Check()
    {
        if (running) return;
        SetBusy(true);
        try { var result = await SetupService.VerifyAsync(); verified = result.Success; status.Text = result.Message; }
        finally { SetBusy(false); }
    }
    private async Task Install()
    {
        if (running) return;
        SetBusy(true);
        try
        {
            var confirm = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "Install or repair WinGet?", Content = "This will download Microsoft's WinGet client module from PowerShell Gallery and install or repair App Installer and its dependencies for your Windows account.", PrimaryButtonText = "Install", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            verified = false;
            log.Text = ""; log.Visibility = Visibility.Visible;
            status.Text = "Installing WinGet. This may take a few minutes…";
            var result = await SetupService.InstallAsync(line => DispatcherQueue.TryEnqueue(() => { log.Text = (log.Text + line + Environment.NewLine); if (log.Text.Length > 14000) log.Text = log.Text[^14000..]; }));
            var check = await SetupService.VerifyAsync();
            verified = check.Success;
            status.Text = check.Success ? check.Message : $"Installation did not produce a working WinGet (0x{result.ExitCode:X8}). Check the details below, retry, or use Microsoft Store. {check.Message}";
            if (!check.Success && string.IsNullOrWhiteSpace(log.Text)) log.Text = result.Output;
        }
        catch (Exception ex) { status.Text = $"Installation failed: {ex.Message} You can retry or install App Installer using Microsoft Store."; }
        finally { SetBusy(false); }
    }
}
