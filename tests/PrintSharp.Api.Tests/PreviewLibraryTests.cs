using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PrintSharp.Documents;
using PrintSharp.Preview.AspNetCore;
using Xunit;

namespace PrintSharp.Api.Tests;
public sealed class PreviewLibraryTests
{
    [Fact]
    public void DocumentEntryPointUsesHostOwnerResolverAndCanBeReplaced()
    {
        var services = new ServiceCollection().AddPrintSharpPreview(o => o.OwnerResolver = user => user.FindFirst("api_key")!.Value);
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IPrintPreviewStore>();
        var owner = new ClaimsPrincipal(new ClaimsIdentity([new Claim("api_key", "a")]));
        var other = new ClaimsPrincipal(new ClaimsIdentity([new Claim("api_key", "b")]));
        var session = store.Create(Document.Create(d => d.Page(p => p.Columns(100).Rows(20).Cell("A1", "ready"))), owner);
        Assert.Equal(1, session.PageCount);
        Assert.Equal(404, Assert.Throws<PreviewException>(() => store.Get(session.DocumentId, other)).Status);
        Assert.False(store.Delete(session.DocumentId, other));
        Assert.True(store.Delete(session.DocumentId, owner));
        Assert.Throws<PreviewException>(() => store.Get(session.DocumentId, owner));
    }
    [Fact]
    public async Task ExpiredSessionCannotBeRead()
    {
        using var store = new PreviewStore(Options.Create(new PrintPreviewOptions { TimeToLive = TimeSpan.FromMilliseconds(1) }));
        var owner = new ClaimsPrincipal();
        var session = store.Create(new Document(), owner);
        await Task.Delay(20);
        Assert.Equal(410, Assert.Throws<PreviewException>(() => store.Get(session.DocumentId, owner)).Status);
    }
}
