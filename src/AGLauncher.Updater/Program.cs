using System.Diagnostics;
using System.IO.Compression;
static string? Arg(string[] args,string name){var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
try
{
    var pid=int.Parse(Arg(args,"--pid")??"0");var package=Arg(args,"--package")??throw new ArgumentException("--package missing");var target=Arg(args,"--target")??throw new ArgumentException("--target missing");var restart=Arg(args,"--restart")??throw new ArgumentException("--restart missing");
    try{var p=Process.GetProcessById(pid);await p.WaitForExitAsync();}catch{}
    await Task.Delay(700);Directory.CreateDirectory(target);var stage=Path.Combine(Path.GetTempPath(),"AGLauncherStage",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);ZipFile.ExtractToDirectory(package,stage,true);
    foreach(var file in Directory.EnumerateFiles(stage,"*",SearchOption.AllDirectories)){var rel=Path.GetRelativePath(stage,file);var dest=Path.Combine(target,rel);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(file,dest,true);}
    Process.Start(new ProcessStartInfo(restart){UseShellExecute=true,WorkingDirectory=target});
}
catch(Exception ex){File.WriteAllText(Path.Combine(Path.GetTempPath(),"AGLauncher-Updater-error.txt"),ex.ToString());}
