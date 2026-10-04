using System.Diagnostics;
using System.IO.Compression;

static string? Arg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

string? restartPath = null;
try
{
    var pid = int.Parse(Arg(args, "--pid") ?? "0");
    var package = Arg(args, "--package") ?? throw new ArgumentException("--package missing");
    var target = Arg(args, "--target") ?? throw new ArgumentException("--target missing");
    restartPath = Arg(args, "--restart") ?? throw new ArgumentException("--restart missing");

    try
    {
        var launcher = Process.GetProcessById(pid);
        await launcher.WaitForExitAsync();
    }
    catch { }

    await Task.Delay(500);
    Directory.CreateDirectory(target);

    var stage = Path.Combine(Path.GetTempPath(), "AGLauncherStage", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(stage);

    try
    {
        ZipFile.ExtractToDirectory(package, stage, true);

        foreach (var file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(stage, file);
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            var targetRoot = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!destination.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update package contains an invalid path.");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }
    finally
    {
        try { Directory.Delete(stage, true); } catch { }
    }

    Process.Start(new ProcessStartInfo(restartPath)
    {
        UseShellExecute = false,
        WorkingDirectory = target
    });
}
catch (Exception ex)
{
    try
    {
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "AGLauncher-Updater-error.txt"), ex.ToString());
    }
    catch { }

    if (!string.IsNullOrWhiteSpace(restartPath) && File.Exists(restartPath))
    {
        try
        {
            Process.Start(new ProcessStartInfo(restartPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(restartPath)!
            });
        }
        catch { }
    }
}
