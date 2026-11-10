using System.Diagnostics;
using System.Text;
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





