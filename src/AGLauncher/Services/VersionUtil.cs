namespace AGLauncher.Services;
public static class VersionUtil
{
    public static Version Parse(string? value)
    {
        var clean=(value??"0.0.0").Trim().TrimStart('v','V').Split('-', '+')[0];
        return Version.TryParse(clean,out var v)?v:new Version(0,0,0);
    }
}
