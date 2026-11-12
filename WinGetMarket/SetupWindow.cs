using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace WinGetMarket;
public sealed class SetupWindow : Window
{
    private readonly Button register = new() { Content = "Register App Installer" };
    private readonly Button module = new() { Content = "Install WinGet PowerShell module (optional)" };
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
        next.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        actions.Children.Add(register); actions.Children.Add(verify); body.Children.Add(actions);
        body.Children.Add(new TextBlock { Text = "If WinGet is unavailable, try registering App Installer. You can also install or update App Installer in Microsoft Store.", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(store);
        body.Children.Add(new TextBlock { Text = "The optional module adds WinGet commands to PowerShell. You can open WinGet Market as soon as App Installer is ready.", TextWrapping = TextWrapping.Wrap });
        body.Children.Add(module);
        body.Children.Add(log);
        body.Children.Add(next);
        var root = new Grid { RequestedTheme = ElementTheme.Dark, Background = new SolidColorBrush(Color.FromArgb(255, 16, 17, 22)) };
        root.Children.Add(new ScrollViewer { Content = body }); Content = root;
        register.Click += async (_, _) => await RegisterAppInstaller();
        module.Click += async (_, _) => await InstallPowerShellModule();
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
        register.IsEnabled = !value && !verified;
        module.IsEnabled = verify.IsEnabled = store.IsEnabled = !value;
        next.IsEnabled = !value && verified;
    }
    private async Task Check()
    {
        if (running) return;
        SetBusy(true);
        try { var result = await SetupService.VerifyAsync(); verified = result.Success; status.Text = result.Message; }
        finally { SetBusy(false); }
    }
    private void BeginLog()
    {
        log.Text = "";
        log.Visibility = Visibility.Visible;
    }

    private void ShowProgress(string line) => DispatcherQueue.TryEnqueue(() =>
    {
        log.Text += line + Environment.NewLine;
        if (log.Text.Length > 14000) log.Text = log.Text[^14000..];
    });

    private async Task RegisterAppInstaller()
    {
        if (running) return;
        SetBusy(true);
        try
        {
            BeginLog();
            status.Text = "Registering App Installer…";
            var result = await SetupService.RegisterAppInstallerAsync(ShowProgress);
            var check = await SetupService.VerifyAsync();
            verified = check.Success;
            status.Text = verified ? check.Message : $"WinGet is still unavailable. Open App Installer in Microsoft Store, then select Check again. {check.Message}";
            if (!verified && string.IsNullOrWhiteSpace(log.Text)) log.Text = result.Output;
        }
        catch (Exception ex) { status.Text = $"Registration failed: {ex.Message} Open App Installer in Microsoft Store."; }
        finally { SetBusy(false); }
    }

    private async Task InstallPowerShellModule()
    {
        if (running) return;
        SetBusy(true);
        try
        {
            var confirm = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Install WinGet PowerShell module?",
                Content = "Setup will get Microsoft.WinGet.Client from PowerShell Gallery if it is missing from your Windows account.",
                PrimaryButtonText = "Install",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            BeginLog();
            status.Text = "Setting up the WinGet PowerShell module…";
            var result = await SetupService.InstallPowerShellModuleAsync(ShowProgress);
            status.Text = result.ExitCode == 0
                ? "The WinGet PowerShell module is ready."
                : $"The optional module setup failed (0x{result.ExitCode:X8}). See the details below.";
            if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(log.Text)) log.Text = result.Output;
        }
        catch (Exception ex) { status.Text = $"The optional module setup failed: {ex.Message}"; }
        finally { SetBusy(false); }
    }
}
