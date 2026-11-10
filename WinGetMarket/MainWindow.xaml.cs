using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace WinGetMarket;
public sealed partial class MainWindow : Window
{
    private string page = "Discover";
    private bool working;
    private List<Package> catalog = [];
    private int visibleCount = 60;
    private readonly SemaphoreSlim gate = new(1);
    private static readonly Package[] curated = [
        new("Visual Studio Code", "Microsoft.VisualStudioCode", Category: "Development", Description: "A versatile editor for code, debugging, and your next big idea."),
        new("PowerToys", "Microsoft.PowerToys", Category: "Productivity", Description: "Small utilities that make a big difference to your Windows workflow."),
        new("VLC media player", "VideoLAN.VLC", Category: "Creative", Description: "A familiar home for almost any video or audio format."),
        new("Git", "Git.Git", Category: "Development", Description: "Version control for projects of every size."),
        new("7-Zip", "7zip.7zip", Category: "Productivity", Description: "Open, compress, and organize your archives."),
        new("OBS Studio", "OBSProject.OBSStudio", Category: "Creative", Description: "Record your screen and bring your live streams to life."),
        new("Windows Terminal", "Microsoft.WindowsTerminal", Category: "Development", Description: "Your shells and command-line tools, together in one place."),
        new("Notepad++", "Notepad++.Notepad++", Category: "Productivity", Description: "A lightweight text editor with room for powerful workflows."),
        new("Blender", "BlenderFoundation.Blender", Category: "Creative", Description: "Create something extraordinary in 3D.") ];
    public MainWindow()
    {
        InitializeComponent(); Title = "WinGet Market";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WinGetMarket.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 900));
        Render(curated);
    }
    private void Render(IEnumerable<Package> packages)
    {
        catalog = packages.ToList(); visibleCount = 60; RenderPage();
    }
    private void MoreAppsClicked(object sender, RoutedEventArgs e) { visibleCount += 60; RenderPage(); }
    private void RenderPage()
    {
        UpdateAll.IsEnabled = page == "Updates" && catalog.Count > 0 && !working;
        Cards.Items.Clear(); var list = catalog.Take(visibleCount).ToList(); Count.Text = catalog.Count > list.Count ? $"{list.Count} of {catalog.Count} apps" : $"{catalog.Count} apps";
        MoreApps.Visibility = catalog.Count > list.Count ? Visibility.Visible : Visibility.Collapsed; Empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var p in list)
        {
            var body = new StackPanel { Spacing = 12, Width = 246 };
            var badge = new Border { Width = 48, Height = 48, CornerRadius = new(12), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromArgb(255, 66, 85, 139)), Child = new TextBlock { Text = p.Name[..1], FontSize = 25, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            body.Children.Add(badge);
            body.Children.Add(new TextBlock { Text = p.Name, FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            body.Children.Add(new TextBlock { Text = p.Id, FontSize = 11, Foreground = new SolidColorBrush(Colors.LightSteelBlue), TextTrimming = TextTrimming.CharacterEllipsis });
            body.Children.Add(new TextBlock { Text = p.Description, TextWrapping = TextWrapping.Wrap, Height = 58, FontSize = 13, Foreground = new SolidColorBrush(Colors.LightGray) });
            var button = new Button { Content = page == "Library" ? "Manage  →" : page == "Updates" ? "Review update  →" : "View app  →", HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += async (_, _) => await Details(p);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (page is not "Library" and not "Updates")
            {
                var install = new Button { Content = "Install", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
                install.Click += async (_, _) => await RunPackageAction(p, "Install");
                actions.Children.Add(install);
            }
            actions.Children.Add(button);
            body.Children.Add(actions);
            Cards.Items.Add(new Border { Child = body, Padding = new(20), Margin = new(0, 0, 14, 14), CornerRadius = new(12), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(255, 49, 52, 66)), Background = new SolidColorBrush(Color.FromArgb(255, 27, 29, 39)) });
        }
    }
    private async void Navigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (Cards is null || args.SelectedItem is not NavigationViewItem item) return;
        page = item.Tag?.ToString() ?? "Discover"; Search.Text = ""; await Load();
    }
    private async Task Load()
    {
        UpdateAll.Visibility = page == "Updates" ? Visibility.Visible : Visibility.Collapsed;
        UpdateAll.IsEnabled = false;
        Hero.Visibility = page == "Discover" ? Visibility.Visible : Visibility.Collapsed;
        Heading.Text = page switch { "Discover" => "Made for your everyday", "Library" => "My library", "Updates" => "Ready for a refresh", _ => page };
        Subtitle.Text = page is "Library" or "Updates" ? "Live results from Windows Package Manager. Refresh after making changes." : "Curated picks. Package availability and installer terms are checked by WinGet.";
        if (page is "Library" or "Updates") await Query([page == "Library" ? "list" : "upgrade", "--source", "winget", "--disable-interactivity"]);
        else if (page == "All apps")
        {
            Subtitle.Text = "Browse the complete WinGet community catalog. Show more apps to continue browsing.";
            await Query(WingetService.AllAppsArguments());
        }
        else Render(curated.Where(p => page == "Discover" || p.Category == page));
    }
    private async void SearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.QueryText)) { await Load(); return; }
        UpdateAll.Visibility = Visibility.Collapsed;
        page = "Search"; Hero.Visibility = Visibility.Collapsed; Heading.Text = $"Results for “{args.QueryText}”";
        Subtitle.Text = "Live results from the WinGet community source.";
        await Query(["search", "--query", args.QueryText.Trim(), "--source", "winget", "--count", "60", "--disable-interactivity"]);
    }
    private void Tell(string message, InfoBarSeverity severity = InfoBarSeverity.Informational) { Status.Title = "WinGet Market"; Status.Message = message; Status.Severity = severity; Status.IsOpen = true; }
    private async Task Query(string[] args)
    {
        if (!await gate.WaitAsync(0)) { Tell("Another operation is running. Please wait."); return; }
        Busy.Visibility = Visibility.Visible;
        try
        {
            var result = await WingetService.RunAsync(args);
            Render(WingetService.Parse(result.Output));
            Tell(result.ExitCode == 0 ? "Catalog refreshed." : $"WinGet returned 0x{result.ExitCode:X8}. {result.Output}", result.ExitCode == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        catch (Exception ex) { Render([]); Tell($"Cannot reach WinGet: {ex.Message} Install or repair App Installer, then refresh.", InfoBarSeverity.Error); }
        finally { Busy.Visibility = Visibility.Collapsed; gate.Release(); }
    }
    private async Task Details(Package p)
    {
        if (working) return;
        string action = page == "Library" ? "Uninstall" : page == "Updates" ? "Update" : "Install";
        var text = new TextBox { Text = $"{p.Id}\nVersion: {p.Version}\n\n{p.Description}\n\nLoading package information…", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 420, MinWidth = 520 };
        var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = p.Name, Content = text, PrimaryButtonText = action, CloseButtonText = "Close", DefaultButton = ContentDialogButton.Close };
        async Task Fetch()
        {
            try { var r = await WingetService.RunAsync(["show", "--id", p.Id, "--exact", "--source", "winget", "--disable-interactivity"]); text.Text = $"{p.Id}\nVersion: {p.Version}\n\n{p.Description}\n\n{r.Output}"; }
            catch (Exception ex) { text.Text = $"{p.Id}\n\n{p.Description}\n\nPackage details unavailable: {ex.Message}"; }
        }
        var fetch = Fetch();
        var selected = await dialog.ShowAsync();
        await fetch;
        if (selected != ContentDialogResult.Primary) return;
        await RunPackageAction(p, action);
    }
    private async Task RunPackageAction(Package p, string action)
    {
        if (working) { Tell("Another operation is running. Please wait."); return; }
        var confirm = new ContentDialog { XamlRoot = Content.XamlRoot, Title = $"{action} {p.Name}?", Content = "WinGet will run the package installer or uninstaller. You may see Windows permission and license prompts. Continue only if you trust this publisher.", PrimaryButtonText = action, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        if (!await gate.WaitAsync(0)) { Tell("Another operation is running. Try again when it finishes."); return; }
        working = true; Busy.Visibility = Visibility.Visible;
        try
        {
            Tell($"{action} in progress: {p.Name}. Watch for installer prompts.");
            string[] args = [action == "Uninstall" ? "uninstall" : action == "Update" ? "upgrade" : "install", "--id", p.Id, "--exact", "--source", "winget", "--disable-interactivity"];
            void Progress(string line) => DispatcherQueue.TryEnqueue(() => Tell(line));
            var result = action == "Uninstall"
                ? await WingetService.RunAsync(args, Progress)
                : await WingetService.RunInstallOrUpgradeAsync(args, Progress);
            Tell(result.ExitCode == 0 ? $"{action} completed: {p.Name}. Refresh to reload package status." : $"{action} failed (0x{result.ExitCode:X8}). {result.Output}", result.ExitCode == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        catch (OperationCanceledException ex) { Tell(ex.Message); }
        catch (Exception ex) { Tell(ex.Message, InfoBarSeverity.Error); }
        finally { working = false; Busy.Visibility = Visibility.Collapsed; gate.Release(); }
    }
    private async void UpdateAllClicked(object sender, RoutedEventArgs e)
    {
        if (page != "Updates" || working || catalog.Count == 0) return;
        if (!await gate.WaitAsync(0)) { Tell("Another operation is running. Please wait."); return; }
        string? message = null;
        var severity = InfoBarSeverity.Informational;
        try
        {
            UpdateAll.IsEnabled = false;
            var confirm = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Update all eligible apps?",
                Content = "WinGet will update all eligible installed apps from the WinGet community source, including apps beyond the cards currently visible. Pinned apps and apps with unknown versions follow WinGet's default rules. Installer, license, and Windows permission prompts may appear.",
                PrimaryButtonText = "Update All",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            working = true;
            Busy.Visibility = Visibility.Visible;
            Tell("Updating all eligible apps. Watch for installer prompts.");
            var result = await WingetService.RunInstallOrUpgradeAsync(WingetService.UpdateAllArguments(),
                line => DispatcherQueue.TryEnqueue(() => Tell(line)));
            severity = result.ExitCode == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            message = result.ExitCode == 0
                ? "Update All completed. The updates list has been refreshed."
                : $"Update All returned 0x{result.ExitCode:X8}. Some apps may not have updated. {result.Output}";
        }
        catch (OperationCanceledException ex) { Tell(ex.Message); return; }
        catch (Exception ex) { message = $"Update All failed: {ex.Message}"; severity = InfoBarSeverity.Error; }
        finally
        {
            working = false;
            Busy.Visibility = Visibility.Collapsed;
            gate.Release();
            UpdateAll.IsEnabled = page == "Updates" && catalog.Count > 0;
        }
        if (page == "Updates") await Load();
        if (message is not null) Tell(message, severity);
    }
    private async void RefreshClicked(object sender, RoutedEventArgs e) => await Load();
    private void ExploreClicked(object sender, RoutedEventArgs e) { Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().First(x => x.Tag?.ToString() == "Development"); }
}




