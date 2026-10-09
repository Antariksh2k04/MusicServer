using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MusicServer.Tests;

public sealed class DiagnosticHttpTests
{
    private const string Prefix = "/api/v1/diagnostics";

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task UnconfiguredOrProduction_DoesNotExposeDiagnostics(string environment, bool enabled)
    {
        await using var factory = new DiagnosticFactory(environment, enabled);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Prefix + "/bootstrap")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/health/ready")).StatusCode);
    }

    [Fact]
    public async Task NonLoopbackClient_IsDenied()
    {
        await using var factory = new DiagnosticFactory(remote: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClient().GetAsync(Prefix + "/bootstrap")).StatusCode);
    }

    [Theory]
    [InlineData("/fixtures")]
    [InlineData("/session")]
    [InlineData("/fixtures/00000000-0000-0000-0000-000000000001/stream")]
    public async Task AnonymousRequest_IsDenied(string path)
    {
        await using var factory = new DiagnosticFactory();
        using var response = await factory.CreateClient().GetAsync(Prefix + path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task LoginAndUpload_RequireCsrfAndExactOrigin()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        var missing = await client.PostAsJsonAsync(Prefix + "/session", new { operatorKey = DiagnosticFactory.Key });
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        await LoginAsync(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(client, Frames())).StatusCode);
    }

    [Fact]
    public async Task WrongOperatorKey_DoesNotCreateSession()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await BootstrapAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Prefix + "/session", new { operatorKey = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Prefix + "/fixtures")).StatusCode);
    }

    [Fact]
    public async Task ImportedFixture_StreamsOriginalBytesAndDeduplicates()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var bytes = Frames();
        using var upload = await UploadAsync(client, bytes);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        using var body = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("track").GetProperty("id").GetString();
        Assert.Equal("fixture", body.RootElement.GetProperty("track").GetProperty("title").GetString());
        using var full = await client.GetAsync($"{Prefix}/fixtures/{id}/stream");
        Assert.Equal(bytes, await full.Content.ReadAsByteArrayAsync());
        Assert.Equal("audio/mpeg", full.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", full.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, bytes)).StatusCode);
        using var list = JsonDocument.Parse(await client.GetStringAsync(Prefix + "/fixtures"));
        Assert.Equal(1, list.RootElement.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain(".mp3", await client.GetStringAsync(Prefix + "/fixtures"));
    }

    [Theory]
    [InlineData("bytes=0-9", 0, 10)]
    [InlineData("bytes=100-", 100, 1151)]
    [InlineData("bytes=-12", 1239, 12)]
    [InlineData("bytes=1200-9999", 1200, 51)]
    public async Task SingleRange_ReturnsExactSelectedBytes(string header, int start, int count)
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var bytes = Frames();
        var id = await AddFixtureAsync(client, bytes);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Prefix}/fixtures/{id}/stream");
        request.Headers.TryAddWithoutValidation("Range", header);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(bytes[start..(start + count)], await response.Content.ReadAsByteArrayAsync());
        Assert.Equal($"bytes {start}-{start + count - 1}/{bytes.Length}", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(count, response.Content.Headers.ContentLength);
    }

    [Theory]
    [InlineData("bytes=9000-", 416)]
    [InlineData("bytes=10-5", 416)]
    [InlineData("bytes=-0", 416)]
    [InlineData("bytes=abc-", 416)]
    [InlineData("bytes=0-1,3-4", 400)]
    public async Task InvalidRanges_HaveDefinedErrors(string range, int status)
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Prefix}/fixtures/{id}/stream");
        request.Headers.TryAddWithoutValidation("Range", range);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 416) Assert.Equal("bytes */1251", response.Content.Headers.ContentRange?.ToString());
    }

    [Fact]
    public async Task HeadAndIfRange_HaveCorrectRepresentationSemantics()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        var url = $"{Prefix}/fixtures/{id}/stream";
        using var head = await client.SendAsync(new(HttpMethod.Head, url));
        Assert.Equal(1251, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        Assert.Contains("bytes", head.Headers.AcceptRanges);
        foreach (var match in new[] { true, false })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 9);
            request.Headers.TryAddWithoutValidation("If-Range", match ? head.Headers.ETag!.ToString() : "\"different\"");
            using var response = await client.SendAsync(request);
            Assert.Equal(match ? HttpStatusCode.PartialContent : HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(match ? 10 : 1251, (await response.Content.ReadAsByteArrayAsync()).Length);
        }
    }

    [Theory]
    [InlineData("invalid", 400)]
    [InlineData("empty", 400)]
    [InlineData("short", 400)]
    [InlineData("oversize", 413)]
    public async Task FailedUpload_DoesNotLeaveFixtureOrFile(string kind, int status)
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var bytes = kind == "invalid" ? "not an mp3"u8.ToArray() : kind == "empty" ? [] : Frames();
        var declared = kind == "short" ? bytes.Length + 1L : kind == "oversize" ? 52428801L : bytes.Length;
        using var response = await UploadAsync(client, bytes, declared);
        Assert.Equal(status, (int)response.StatusCode);
        using var list = JsonDocument.Parse(await client.GetStringAsync(Prefix + "/fixtures"));
        Assert.Equal(0, list.RootElement.GetProperty("items").GetArrayLength());
        Assert.Empty(Directory.GetFiles(factory.Storage, "*.mp3", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AtFixtureCountCap_DuplicateRecoversExistingTrackAndNewBytesAreRejected()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var lastBytes = Frames();
        string? lastId = null;
        for (var index = 0; index < 100; index++)
        {
            lastBytes[^1] = (byte)index;
            lastId = await AddFixtureAsync(client, lastBytes);
        }
        using var duplicate = await UploadAsync(client, lastBytes);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        using var outcome = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.True(outcome.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal(lastId, outcome.RootElement.GetProperty("track").GetProperty("id").GetString());

        var newBytes = (byte[])lastBytes.Clone();
        newBytes[^1] = 100;
        using var rejected = await UploadAsync(client, newBytes);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var failure = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("quotaExceeded", failure.RootElement.GetProperty("code").GetString());
        using var list = JsonDocument.Parse(await client.GetStringAsync(Prefix + "/fixtures"));
        Assert.Equal(100, list.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(100, Directory.GetFiles(factory.Storage, "*.mp3", SearchOption.AllDirectories).Length);
        using var stream = await client.GetAsync($"{Prefix}/fixtures/{lastId}/stream");
        Assert.Equal(lastBytes, await stream.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task LocalQuota_RejectsNewFixture()
    {
        await using var factory = new DiagnosticFactory(byteLimit: 1000);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(client, Frames())).StatusCode);
    }

    [Fact]
    public async Task ExpiredSessionAndLogout_DenyLaterRanges()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Prefix}/fixtures/{id}/stream")).StatusCode);
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Prefix + "/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Prefix}/fixtures/{id}/stream")).StatusCode);
    }

    [Fact]
    public async Task MissingFixture_ReturnsNotFound()
    {
        await using var factory = new DiagnosticFactory();
        using var client = factory.CreateClient();
        await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Prefix}/fixtures/{Guid.NewGuid()}/stream")).StatusCode);
    }

    [Theory]
    [InlineData(StorageFault.BeforePut)]
    [InlineData(StorageFault.AfterPut)]
    public async Task ProviderUploadFailure_DoesNotPublishTrackAndRetryRemainsPossible(StorageFault fault)
    {
        await using var factory = new DiagnosticFactory(injectStorageFaults: true);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        // Resolve the catalog before setting its injected provider fault.
        await client.GetAsync(Prefix + "/fixtures");
        var store = factory.ObjectStore!;
        store.Fault = fault;
        using var response = await UploadAsync(client, Frames());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("storageUnavailable", error);
        Assert.DoesNotContain("Injected", error);
        Assert.DoesNotContain(factory.Storage, error);
        using var list = JsonDocument.Parse(await client.GetStringAsync(Prefix + "/fixtures"));
        Assert.Empty(list.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, store.DeleteAttempts);
        Assert.Empty(Directory.GetFiles(factory.Storage, "*.mp3", SearchOption.AllDirectories));
        store.Fault = StorageFault.None;
        var id = await AddFixtureAsync(client, Frames());
        Assert.Equal(Frames(), await client.GetByteArrayAsync($"{Prefix}/fixtures/{id}/stream"));
    }

    [Theory]
    [InlineData(StorageFault.Head)]
    [InlineData(StorageFault.Range)]
    [InlineData(StorageFault.WrongRange)]
    public async Task ProviderReadFailure_ReturnsSafeErrorBeforeCommittingMediaHeaders(StorageFault fault)
    {
        await using var factory = new DiagnosticFactory(injectStorageFaults: true);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        factory.ObjectStore!.Fault = fault;
        using var response = await client.GetAsync($"{Prefix}/fixtures/{id}/stream");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        Assert.Empty(response.Headers.AcceptRanges);
        var error = await response.Content.ReadAsStringAsync();
        Assert.Contains("storageUnavailable", error);
        Assert.DoesNotContain("Injected", error);
        Assert.DoesNotContain(factory.Storage, error);
        factory.ObjectStore.Fault = StorageFault.None;
        Assert.Equal(Frames(), await client.GetByteArrayAsync($"{Prefix}/fixtures/{id}/stream"));
    }

    [Fact]
    public async Task HeadInvalidRangesAndUnauthorizedRequests_DoNotOpenProviderPayload()
    {
        await using var factory = new DiagnosticFactory(injectStorageFaults: true);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        var store = factory.ObjectStore!;
        var url = $"{Prefix}/fixtures/{id}/stream";
        store.Fault = StorageFault.Range;
        using var head = await client.SendAsync(new(HttpMethod.Head, url));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(1251, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        Assert.Equal(1, store.HeadAttempts);
        Assert.Equal(0, store.RangeAttempts);
        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, url);
        invalidRequest.Headers.TryAddWithoutValidation("Range", "bytes=9999-");
        using var invalid = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, invalid.StatusCode);
        await client.PostAsync(Prefix + "/logout", null);
        using var denied = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(1, store.HeadAttempts);
        Assert.Equal(0, store.RangeAttempts);
    }

    [Theory]
    [InlineData(StorageFault.FirstRead, false, 503, "storageUnavailable")]
    [InlineData(StorageFault.FirstRead, true, 503, "storageUnavailable")]
    [InlineData(StorageFault.FirstReadMissing, false, 404, "trackUnavailable")]
    [InlineData(StorageFault.FirstReadMissing, true, 404, "trackUnavailable")]
    [InlineData(StorageFault.FirstReadDenied, false, 503, "storageUnavailable")]
    [InlineData(StorageFault.FirstReadDenied, true, 503, "storageUnavailable")]
    public async Task FirstReadFailure_ReturnsCompleteProblemWithoutMediaHeaders(StorageFault fault, bool partial, int status, string code)
    {
        await using var factory = new DiagnosticFactory(injectStorageFaults: true);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        var url = $"{Prefix}/fixtures/{id}/stream";
        factory.ObjectStore!.Fault = fault;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (partial) request.Headers.Range = new RangeHeaderValue(0, 9);
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Null(response.Headers.ETag);
        Assert.Empty(response.Headers.AcceptRanges);
        Assert.Null(response.Content.Headers.ContentRange);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(response.Content.Headers.ContentLength is null || response.Content.Headers.ContentLength == bytes.Length);
        using var problem = JsonDocument.Parse(bytes);
        Assert.Equal(status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        var error = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("Injected", error);
        Assert.DoesNotContain(factory.Storage, error);
        factory.ObjectStore.Fault = StorageFault.None;
        Assert.Equal(Frames(), await client.GetByteArrayAsync(url));
    }

    [Fact]
    public async Task MidResponseFailure_AbortsBodyInsteadOfReportingSuccessfulTruncatedMedia()
    {
        await using var factory = new DiagnosticFactory(injectStorageFaults: true);
        using var client = factory.CreateClient();
        await LoginAsync(client);
        var id = await AddFixtureAsync(client, Frames());
        factory.ObjectStore!.Fault = StorageFault.MidRead;
        using var response = await client.GetAsync($"{Prefix}/fixtures/{id}/stream", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1251, response.Content.Headers.ContentLength);
        await Assert.ThrowsAnyAsync<IOException>(async () =>
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var output = new MemoryStream();
            await stream.CopyToAsync(output);
        });
        factory.ObjectStore.Fault = StorageFault.None;
        Assert.Equal(Frames(), await client.GetByteArrayAsync($"{Prefix}/fixtures/{id}/stream"));
    }

    private static async Task BootstrapAsync(HttpClient client)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync(Prefix + "/bootstrap"));
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", doc.RootElement.GetProperty("csrfToken").GetString());
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", DiagnosticFactory.Origin);
    }
    private static async Task LoginAsync(HttpClient client)
    {
        await BootstrapAsync(client);
        using var response = await client.PostAsJsonAsync(Prefix + "/session", new { operatorKey = DiagnosticFactory.Key });
        response.EnsureSuccessStatusCode();
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }
    private static async Task<string> AddFixtureAsync(HttpClient client, byte[] bytes)
    {
        using var response = await UploadAsync(client, bytes);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("track").GetProperty("id").GetString()!;
    }
    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, long? declared = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Prefix + "/fixtures") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        request.Headers.Add("X-File-Name", "fixture.mp3");
        request.Headers.Add("X-File-Size", (declared ?? bytes.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
        return client.SendAsync(request);
    }
    private static byte[] Frames()
    {
        // Structural frames for HTTP byte tests, not proof of audible MP3 decoding.
        var bytes = Enumerable.Repeat((byte)55, 417 * 3).ToArray();
        for (var offset = 0; offset < bytes.Length; offset += 417)
        {
            bytes[offset] = 255; bytes[offset + 1] = 251; bytes[offset + 2] = 144; bytes[offset + 3] = 100;
        }
        return bytes;
    }
}
