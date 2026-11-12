using System.Text.RegularExpressions;
namespace WinGetMarket;
public record WingetCheck(bool Success, string Message);
public static class SetupService
{
    private static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinGetMarket", "setup-v2.complete");
    public static bool IsComplete => File.Exists(StatePath);
    public static void Complete()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath + ".tmp", DateTimeOffset.UtcNow.ToString("O"));
        File.Move(StatePath + ".tmp", StatePath, true);
    }
    public static async Task<WingetCheck> VerifyAsync(Func<Task<CommandResult>>? probe = null)
    {
        try
        {
            var result = await (probe?.Invoke() ?? WingetService.RunProcessAsync(WingetService.Executable, ["--version"], timeout: TimeSpan.FromSeconds(20)));
            var version = result.Output.Trim();
            return result.ExitCode == 0 && Regex.IsMatch(version, @"^v?\d+\.\d+\.\d+(?:[\w.+-]*)$")
                ? new(true, $"WinGet {version} is installed and responding. You're ready to browse apps.")
                : new(false, $"WinGet could not be verified (0x{result.ExitCode:X8}). {version}");
        }
        catch (Exception ex) { return new(false, $"WinGet is unavailable: {ex.Message}"); }
    }
    public static Task<CommandResult> RegisterAppInstallerAsync(Action<string> progress) =>
        RunPowerShellAsync(
            """
            $ErrorActionPreference = 'Stop'
            Write-Output 'Registering App Installer for your Windows account...'
            Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe
            """, progress, TimeSpan.FromMinutes(2));

    public static Task<CommandResult> InstallPowerShellModuleAsync(Action<string> progress) =>
        RunPowerShellAsync(
            """
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            if (!(Get-Module -ListAvailable -Name Microsoft.WinGet.Client)) {
                [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
                Write-Output 'Preparing the PowerShell Gallery package provider...'
                if (!(Get-PackageProvider -Name NuGet -ListAvailable -ErrorAction SilentlyContinue)) {
                    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Scope CurrentUser -Force | Out-Null
                }
                Write-Output 'Downloading Microsoft.WinGet.Client from PowerShell Gallery...'
                Install-Module -Name Microsoft.WinGet.Client -Repository PSGallery -Scope CurrentUser -Force -AllowClobber -Confirm:$false
            }
            Import-Module Microsoft.WinGet.Client
            Write-Output 'The WinGet PowerShell module is ready.'
            """, progress, TimeSpan.FromMinutes(15));

    private static Task<CommandResult> RunPowerShellAsync(string script, Action<string> progress, TimeSpan timeout)
    {
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(
            "[Console]::OutputEncoding = [Text.Encoding]::UTF8\n" + script));
        return WingetService.RunProcessAsync(powershell,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], progress, timeout);
    }
}
