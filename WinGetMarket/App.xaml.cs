using Microsoft.UI.Xaml;
namespace WinGetMarket;
public partial class App : Application
{
    private Window? window;
    public App()
    {
        UnhandledException += (_, e) => Log(e.Exception.ToString() + "\n" + e.Message);
        InitializeComponent();
    }
    private static void Log(string message)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup-error.log"), $"{DateTimeOffset.Now:O}\n{message}\n"); } catch { }
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var check = await SetupService.VerifyAsync();
            if (SetupService.IsComplete && check.Success) { window = new MainWindow(); window.Activate(); }
            else
            {
                window = new SetupWindow(() =>
                {
                    var setup = window;
                    window = new MainWindow(); window.Activate(); setup?.Close();
                });
                window.Activate();
            }
        }
        catch (Exception ex) { Log(ex.ToString()); throw; }
    }
}

