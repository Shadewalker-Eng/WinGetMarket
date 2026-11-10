using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinGetMarket;
public record Package(string Name, string Id, string Version = "Latest", string Category = "App", string Description = "Available from the WinGet community catalog.");
public record CommandResult(int ExitCode, string Output);
public static class WingetService
{
    // Explicit empty query requests the full catalog on current WinGet versions.
    public static string[] UpdateAllArguments() => ["upgrade", "--all", "--source", "winget", "--disable-interactivity"];
    public static string[] AllAppsArguments() => ["search", "--query", "", "--source", "winget", "--disable-interactivity"];
    public static string Executable => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe"))
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe") : "winget.exe";
    public static Task<CommandResult> RunAsync(IEnumerable<string> arguments, Action<string>? progress = null) => RunProcessAsync(Executable, arguments, progress);
    [SupportedOSPlatform("windows")]
    public static async Task<CommandResult> RunInstallOrUpgradeAsync(string[] arguments, Action<string>? progress = null)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            return await RunAsync(arguments, progress);

        // Shell elevation cannot redirect output. Use a write-only pipe from the elevated process.
        var pipeName = $"WinGetMarket-{Guid.NewGuid():N}";
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var encodedArguments = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(arguments));
        string command = $$"""
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            $client = $null
            $writer = $null
            function Send-Status([string] $line) {
                if ($writer) {
                    try { $writer.WriteLine($line) } catch [IO.IOException] {}
                }
            }
            try {
                $client = [IO.Pipes.NamedPipeClientStream]::new('.', '{{pipeName}}',
                    [IO.Pipes.PipeDirection]::Out, [IO.Pipes.PipeOptions]::None,
                    [Security.Principal.TokenImpersonationLevel]::Anonymous)
                $client.Connect(2000)
                $writer = [IO.StreamWriter]::new($client, [Text.UTF8Encoding]::new($false))
                $writer.AutoFlush = $true
            } catch {
                # The operation can proceed if a different administrator account cannot access the pipe.
            }
            try {
                $package = Get-AppxPackage -Name Microsoft.DesktopAppInstaller |
                    Where-Object PackageFamilyName -EQ 'Microsoft.DesktopAppInstaller_8wekyb3d8bbwe' |
                    Sort-Object Version -Descending | Select-Object -First 1
                if (!$package) { throw 'Microsoft App Installer is not registered for the administrator account.' }
                $winget = Join-Path $package.InstallLocation 'winget.exe'
                $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedArguments}}'))
                $wingetArguments = @(ConvertFrom-Json -InputObject $json)
                Send-Status 'WinGet is running…'
                $ErrorActionPreference = 'Continue'
                & $winget @wingetArguments 2>&1 | ForEach-Object { Send-Status ([string] $_) }
                exit $LASTEXITCODE
            } catch {
                Send-Status $_.Exception.Message
                exit 1
            } finally {
                if ($writer) { $writer.Dispose() }
                if ($client) { $client.Dispose() }
            }
            """;

        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(powershell)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.SystemDirectory
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));

        progress?.Invoke("Approve the Windows administrator prompt to start WinGet.");
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start elevated WinGet.");
            progress?.Invoke("Waiting for WinGet to finish…");
            using var drainDeadline = new CancellationTokenSource();
            var output = new StringBuilder();
            async Task ReadProgress()
            {
                try
                {
                    await pipe.WaitForConnectionAsync(drainDeadline.Token);
                    using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                    while (await reader.ReadLineAsync(drainDeadline.Token) is { } line)
                    {
                        line = line.Trim();
                        if (line.Length == 0) continue;
                        if (line.Length > 500) line = line[..500];
                        output.AppendLine(line);
                        if (output.Length > 2000) output.Remove(0, output.Length - 2000);
                        progress?.Invoke(line);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }
            var drain = ReadProgress();
            await process.WaitForExitAsync();
            drainDeadline.CancelAfter(TimeSpan.FromSeconds(2));
            await drain;
            return new(process.ExitCode, output.Length > 0 ? output.ToString().Trim() :
                process.ExitCode == 0 ? "" : "See WinGet logs with winget --logs for details.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Administrator approval was canceled. WinGet did not start.", ex);
        }
    }

    public static async Task<CommandResult> RunProcessAsync(string executable, IEnumerable<string> arguments, Action<string>? progress = null, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        process.Start();
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(30));
        var output = new StringBuilder();
        async Task Drain(StreamReader reader) { while (await reader.ReadLineAsync(deadline.Token) is { } line) { lock (output) output.AppendLine(line); progress?.Invoke(line); } }
        try { await Task.WhenAll(Drain(process.StandardOutput), Drain(process.StandardError), process.WaitForExitAsync(deadline.Token)); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch { } throw new TimeoutException("The operation timed out. Check installer windows before retrying."); }
        return new(process.ExitCode, output.ToString());
    }
    public static List<Package> Parse(string output)
    {
        var lines = Regex.Replace(output, @"\x1B\[[0-?]*[ -/]*[@-~]", "").Split('\n');
        var result = new List<Package>();
        // Use the actual table divider and fixed column positions; app names contain spaces.
        for (int i = 1; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i].Trim(), @"^-{5,}$")) continue;
            var columns = Regex.Matches(lines[i - 1].TrimEnd('\r'), @"\S+(?: \S+)*?(?=\s{2,}|$)");
            if (columns.Count < 3) continue;
            int idStart = columns[1].Index, versionStart = columns[2].Index;
            int versionEnd = columns.Count > 3 ? columns[3].Index : int.MaxValue;
            foreach (var raw in lines.Skip(i + 1))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length <= versionStart) continue;
                string name = line[..idStart].Trim(), id = line[idStart..versionStart].Trim();
                string version = line[versionStart..Math.Min(line.Length, versionEnd)].Trim();
                if (id.Length == 0 || id.Contains(' ') || id.Contains('…') || name.Length == 0) continue;
                result.Add(new(name, id, version));
            }
            break;
        }
        return result.DistinctBy(p => p.Id).ToList();
    }
}





