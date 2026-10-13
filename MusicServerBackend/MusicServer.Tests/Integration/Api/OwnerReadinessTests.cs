using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicServer.Application.Auth;
using MusicServer.Tests.Unit.Auth;
using Xunit;

namespace MusicServer.Tests.Integration.Api;

public sealed class OwnerReadinessTests
{
    [Fact]
    public async Task ConfiguredClients_WithoutOwner_FailReadinessWithoutPrivateDetails()
    {
        var store = new OwnerProvisioningTests.MemoryStore();
        var settings = OwnerProvisioningTests.Configuration();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<GoogleClientConfiguration>();
                services.AddSingleton(settings);
                services.RemoveAll<IOwnerIdentityStore>();
                services.AddSingleton<IOwnerIdentityStore>(store);
            });
        });
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, store.Reads);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(settings.WebClientId, body);
        Assert.DoesNotContain("test-owner", body);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/health/live")).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/owner/provision")]
    [InlineData("/owner/provision")]
    public async Task NormalApi_DoesNotExposeOwnerRegistration(string path)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(path, new StringContent("{}"))).StatusCode);
    }
}
