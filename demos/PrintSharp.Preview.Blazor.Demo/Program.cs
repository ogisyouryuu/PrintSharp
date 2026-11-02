using PrintSharp.Preview.Blazor.Demo.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient("PrintApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PrintApiUrl"] ?? "http://127.0.0.1:5080");
    client.Timeout = TimeSpan.FromMinutes(2);
});
var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
// デモ用の同一オリジン経由で、ブラウザーのページ取得を API に転送します。
app.MapMethods("/api/v1/previews/{**path}", ["GET", "DELETE"], async (HttpContext context, IHttpClientFactory clients) =>
{
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
    if (context.Request.Headers.IfNoneMatch.Count > 0) request.Headers.TryAddWithoutValidation("If-None-Match", context.Request.Headers.IfNoneMatch.ToArray());
    using var response = await clients.CreateClient("PrintApi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    foreach (var header in response.Headers.Concat(response.Content.Headers))
        if (!header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) context.Response.Headers[header.Key] = header.Value.ToArray();
    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
