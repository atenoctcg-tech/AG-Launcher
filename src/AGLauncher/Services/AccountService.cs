using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace AGLauncher.Services;
public static class AccountService
{
 private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
 public static string BaseUrl { get; set; } = "";
 public static string Token { get; private set; } = "";
 public static UserProfile? User { get; private set; }
 public static bool Configured => Uri.TryCreate(BaseUrl,UriKind.Absolute,out var uri) && uri.Scheme == "https";
 public static async Task<JsonElement> Call(string path, object? data=null, string? method=null)
 {
  if(!Configured) throw new InvalidOperationException("The studio account server is not connected yet. An administrator must configure its HTTPS URL before email sign-in is available.");
  using var request=new HttpRequestMessage(new HttpMethod(method ?? (data == null ? "GET" : "POST")),BaseUrl.TrimEnd('/')+path);
  if(Token.Length>0)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",Token);
  if(data != null)request.Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json");
  using var response=await Http.SendAsync(request);
  var body=await response.Content.ReadAsStringAsync();
  using var document=JsonDocument.Parse(body);var result=document.RootElement.Clone();
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException(result.TryGetProperty("error",out var error)?error.GetString():"Account request failed.");
  if(result.TryGetProperty("token",out var token))Token=token.GetString() ?? "";
  if(result.TryGetProperty("user",out var user))User=JsonSerializer.Deserialize<UserProfile>(user.GetRawText(),JsonUtil.Options);
  return result;
 }
 public static void Clear() { Token="";User=null; }
}
