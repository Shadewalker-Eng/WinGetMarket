using WinGetMarket;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var header = $"{"Name",-25}{"Id",-35}{"Version",-15}Source";
var table = header + "\n" + new string('-', 82) + "\n" + $"{"Visual Studio Code",-25}{"Microsoft.VisualStudioCode",-35}{"1.99.0",-15}winget\n" + $"{"7-Zip",-25}{"7zip.7zip",-35}{"24.09",-15}winget\n";
var parsed = WingetService.Parse(table);
Check(parsed.Count == 2, "Expected two packages");
Check(parsed[0].Name == "Visual Studio Code" && parsed[0].Id == "Microsoft.VisualStudioCode", "Names with spaces must preserve the exact package ID");
Check(parsed[1].Version == "24.09", "Version boundary");
Check(WingetService.Parse("No installed package found.").Count == 0, "No-result output");
Check(WingetService.Parse("\u001b[32m" + table + "\u001b[0m").Count == 2, "ANSI escape handling");
Check(WingetService.Parse(header + "\n" + new string('-',82) + "\n" + $"{"Truncated",-25}{"Microsoft.Trunc…",-35}{"1.0",-15}winget").Count == 0, "Reject truncated IDs");
Console.WriteLine("PASS: table columns, spaced names, versions, empty results, ANSI output, truncated IDs.");
var allArgs = WingetService.AllAppsArguments();
Check(allArgs[1] == "--query" && allArgs[2] == "", "All apps must preserve an explicit empty selection argument");
if (args.Contains("--live-catalog"))
{
    var live = await WingetService.RunAsync(allArgs);
    Check(live.ExitCode == 0, $"Live catalog failed: {live.Output}");
    var apps = WingetService.Parse(live.Output);
    Check(apps.Count > 100, $"Expected a full catalog, got {apps.Count}");
    Check(apps.Any(p => p.Id == "Microsoft.VisualStudioCode"), "Catalog must include known package IDs");
    Console.WriteLine($"PASS: live catalog returned {apps.Count} packages through the app command runner.");
}
Check(WingetService.UpdateAllArguments().SequenceEqual(new[] { "upgrade", "--all", "--source", "winget", "--disable-interactivity" }), "Update All must target the community source without overriding pins or accepting agreements");
Console.WriteLine("PASS: Update All command scope and default safeguards.");
Check((await SetupService.VerifyAsync(() => Task.FromResult(new CommandResult(0, "v1.29.380\r\n")))).Success, "Setup recognizes a working WinGet version");
Check(!(await SetupService.VerifyAsync(() => Task.FromResult(new CommandResult(1, "v1.29.380")))).Success, "A failed version command must not complete setup");
Check(!(await SetupService.VerifyAsync(() => Task.FromResult(new CommandResult(0, "unrelated output")))).Success, "Unexpected output must not complete setup");
Check(!(await SetupService.VerifyAsync(() => throw new System.ComponentModel.Win32Exception("Executable missing"))).Success, "Missing WinGet must show setup instead of crashing");
Check(!(await SetupService.VerifyAsync(() => throw new TimeoutException("Timed out"))).Success, "Timed out verification must permit retry");
if (args.Contains("--live-setup"))
{
    var check = await SetupService.VerifyAsync();
    Check(check.Success, check.Message);
    Console.WriteLine(check.Message);
}
Console.WriteLine("PASS: setup verification success, command failure, malformed output, missing executable, and timeout.");
