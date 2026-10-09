using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MusicServer.Diagnostics;
using Xunit;

namespace MusicServer.Tests;

public sealed class DiagnosticNativeHttpTests
{
    private const string Prefix = "/api/v1/diagnostics/native";

    [Theory]
    [InlineData("Production", true, false, 404)]
    [InlineData("Development", false, false, 404)]
    [InlineData("Development", true, true, 403)]
    public async Task NativeIssuance_IsDevelopmentAndLoopbackOnly(string environment, bool enabled, bool remote, int status)
    {
        await using var factory = new DiagnosticFactory(environment, enabled, remote);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Prefix + "/session", new { operatorKey = DiagnosticFactory.Key });
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("Origin", "http://localhost")]
    [InlineData("Cookie", "music_diagnostic_session=example")]
    public async Task NativeIssuance_RejectsBrowserContext(string header, string value)
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(header, value);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Prefix + "/session",
            new { operatorKey = DiagnosticFactory.Key })).StatusCode);
    }

    [Fact]
    public async Task BadKeyAndAnonymousRequests_ReturnNoAuthorityOrMetadata()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Prefix + "/session",
            new { operatorKey = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/fixtures")).StatusCode);
    }

    [Fact]
    public async Task ExpiredAccess_RotatesOnceAndReplayRevokesFamily()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        var first = await LoginAsync(client);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Prefix + "/fixtures")).StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/fixtures")).StatusCode);
        using var rotation = await client.PostAsJsonAsync(Prefix + "/refresh", new { first.RefreshToken });
        rotation.EnsureSuccessStatusCode();
        var second = (await rotation.Content.ReadFromJsonAsync<NativeDiagnosticGrant>())!;
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Prefix + "/fixtures")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Prefix + "/refresh",
            new { first.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/fixtures")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Prefix + "/refresh",
            new { second.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task LogoutAfterAccessExpiry_IsIdempotentAndRevokesRefresh()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        var grant = await LoginAsync(client);
        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(Prefix + "/logout",
                new { grant.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Prefix + "/refresh",
            new { grant.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task AbsoluteExpiry_DoesNotExtendWithRefresh()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        var grant = await LoginAsync(client);
        factory.Clock.Advance(TimeSpan.FromSeconds(1801));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Prefix + "/refresh",
            new { grant.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task BrowserFixture_IsAvailableThroughProtectedNativeRangesOnly()
    {
        await using var factory = new DiagnosticFactory();
        using var browser = factory.CreateClient();
        using var bootstrap = JsonDocument.Parse(await browser.GetStringAsync("/api/v1/diagnostics/bootstrap"));
        browser.DefaultRequestHeaders.Add("Origin", DiagnosticFactory.Origin);
        browser.DefaultRequestHeaders.Add("X-CSRF-Token", bootstrap.RootElement.GetProperty("csrfToken").GetString());
        (await browser.PostAsJsonAsync("/api/v1/diagnostics/session", new { operatorKey = DiagnosticFactory.Key })).EnsureSuccessStatusCode();
        var bytes = Enumerable.Repeat((byte)55, 417 * 3).ToArray();
        for (var i = 0; i < bytes.Length; i += 417)
        { bytes[i] = 255; bytes[i + 1] = 251; bytes[i + 2] = 144; bytes[i + 3] = 100; }
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/diagnostics/fixtures")
            { Content = new ByteArrayContent(bytes) };
        uploadRequest.Content.Headers.ContentType = new("audio/mpeg");
        uploadRequest.Headers.Add("X-File-Size", bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        uploadRequest.Headers.Add("X-File-Name", "native-test.mp3");
        using var upload = await browser.SendAsync(uploadRequest);
        upload.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var id = result.RootElement.GetProperty("track").GetProperty("id").GetString();
        using var native = factory.CreateClient();
        var grant = await LoginAsync(native);
        var url = $"{Prefix}/fixtures/{id}/stream";
        Assert.Equal(HttpStatusCode.Unauthorized, (await native.GetAsync(url)).StatusCode);
        native.DefaultRequestHeaders.Authorization = new("Bearer", grant.AccessToken);
        using var range = new HttpRequestMessage(HttpMethod.Get, url);
        range.Headers.Range = new RangeHeaderValue(400, 800);
        using var response = await native.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(bytes[400..801], await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("bytes 400-800/1251", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var head = await native.SendAsync(new(HttpMethod.Head, url));
        Assert.Equal(1251, head.Content.Headers.ContentLength);
        // A bearer cannot silently replace browser cookie + CSRF authority.
        browser.DefaultRequestHeaders.Authorization = new("Bearer", grant.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/v1/diagnostics/fixtures")).StatusCode);
        browser.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/v1/diagnostics/native/revoke-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await native.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await native.PostAsJsonAsync(Prefix + "/refresh",
            new { grant.RefreshToken })).StatusCode);
    }

    private static async Task<NativeDiagnosticGrant> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(Prefix + "/session", new { operatorKey = DiagnosticFactory.Key });
        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Set-Cookie"));
        return (await response.Content.ReadFromJsonAsync<NativeDiagnosticGrant>())!;
    }
}
