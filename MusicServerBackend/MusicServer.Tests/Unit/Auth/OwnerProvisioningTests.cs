using MusicServer.Application.Auth;
using MusicServer.Domain.Entities;
using MusicServer.Infrastructure.Auth;
using Xunit;

namespace MusicServer.Tests.Unit.Auth;

public sealed class OwnerProvisioningTests
{
    internal static GoogleClientConfiguration Configuration() => new()
    {
        WebClientId = "test-web.apps.googleusercontent.com", AndroidClientId = "test-android.apps.googleusercontent.com",
        BrowserOrigin = "https://localhost:5173", AndroidCertificateSha1 = string.Join(':', Enumerable.Repeat("AB", 20))
    };

    [Fact]
    public async Task InvalidGoogleProof_NeverTouchesOwnerStore()
    {
        var store = new MemoryStore();
        var verifier = new FakeVerifier { Reject = true };
        var error = await Assert.ThrowsAsync<OwnerOperationException>(() =>
            new OwnerProvisioner(verifier, store, Configuration()).ProvisionAsync("proof", "nonce", default));
        Assert.Equal("googleProofInvalid", error.Code);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task MissingConfiguration_RejectsBeforeProofOrDatabaseWork()
    {
        var verifier = new FakeVerifier();
        var store = new MemoryStore();
        await Assert.ThrowsAsync<OwnerOperationException>(() =>
            new OwnerProvisioner(verifier, store, new()).ProvisionAsync("proof", "nonce", default));
        Assert.Equal(0, verifier.Calls);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ClientIdWithTrailingNewline_IsRejected()
    {
        var configuration = Configuration();
        configuration.WebClientId += "\n";
        Assert.False(configuration.IsRuntimeConfigured);
        Assert.Throws<OwnerOperationException>(configuration.ValidateForProvisioning);
    }

    [Fact]
    public async Task VerifiedOwner_IsIdempotent_AndAnotherIdentityCannotReplaceIt()
    {
        var verifier = new FakeVerifier();
        var store = new MemoryStore();
        var provisioner = new OwnerProvisioner(verifier, store, Configuration());
        Assert.Equal(OwnerProvisionResult.Created, await provisioner.ProvisionAsync("proof", "nonce", default));
        Assert.Equal(OwnerProvisionResult.AlreadyConfigured, await provisioner.ProvisionAsync("proof", "nonce", default));
        verifier.Subject = "another-owner";
        var error = await Assert.ThrowsAsync<OwnerOperationException>(() => provisioner.ProvisionAsync("proof", "nonce", default));
        Assert.Equal("ownerAlreadyConfigured", error.Code);
        Assert.Equal("test-owner", store.Owner!.GoogleSubject);
    }

    [Theory]
    [InlineData("test-owner ")]
    [InlineData("TEST-OWNER")]
    [InlineData("another-owner")]
    public void ExistingOwnerComparison_IsExact(string otherSubject)
    {
        var owner = new Owner { Id = 1, GoogleIssuer = "https://accounts.google.com", GoogleSubject = "test-owner", CreatedAt = DateTime.UtcNow };
        Assert.Throws<OwnerOperationException>(() => EfOwnerIdentityStore.Match(owner, new("https://accounts.google.com", otherSubject, null)));
        Assert.Equal("test-owner", owner.GoogleSubject);
    }

    [Fact]
    public async Task RuntimeStore_CannotProvisionOwner_EvenWithConfiguredConnection()
    {
        var store = new ConfiguredOwnerIdentityStore("Server=127.0.0.1;Database=offline;User ID=offline;Password=", TimeProvider.System);
        var error = await Assert.ThrowsAsync<OwnerOperationException>(() => store.ProvisionAsync(new("https://accounts.google.com", "test-owner", null), default));
        Assert.Equal("operatorOnly", error.Code);
    }

    [Fact]
    public async Task Readiness_RequiresConfiguredClientsAndExistingOwner_AndHandlesDatabaseOutage()
    {
        var store = new MemoryStore();
        Assert.False(await new OwnerReadiness(store, Configuration()).IsReadyAsync(default));
        await store.ProvisionAsync(new("https://accounts.google.com", "test-owner", null), default);
        Assert.True(await new OwnerReadiness(store, Configuration()).IsReadyAsync(default));
        Assert.False(await new OwnerReadiness(store, new()).IsReadyAsync(default));
        store.Unavailable = true;
        Assert.False(await new OwnerReadiness(store, Configuration()).IsReadyAsync(default));
    }

    [Theory]
    [InlineData("origin-path")]
    [InlineData("origin-http")]
    [InlineData("client-empty")]
    [InlineData("client-same")]
    [InlineData("certificate-empty")]
    [InlineData("package-wrong")]
    [InlineData("native-origin-wrong")]
    public void RuntimeConfiguration_RejectsIncompleteOrMismatchedClients(string scenario)
    {
        var configuration = Configuration();
        switch (scenario)
        {
            case "origin-path": configuration.BrowserOrigin += "/callback"; break;
            case "origin-http": configuration.BrowserOrigin = "http://localhost:5173"; break;
            case "client-empty": configuration.WebClientId = ""; break;
            case "client-same": configuration.AndroidClientId = configuration.WebClientId; break;
            case "certificate-empty": configuration.AndroidCertificateSha1 = ""; break;
            case "package-wrong": configuration.AndroidPackage = "com.other.app"; break;
            case "native-origin-wrong": configuration.NativeOrigin = "https://other.example"; break;
        }
        Assert.False(configuration.IsRuntimeConfigured);
    }

    internal sealed class MemoryStore : IOwnerIdentityStore
    {
        public Owner? Owner { get; private set; }
        public bool Unavailable { get; set; }
        public int Writes { get; private set; }
        public int Reads { get; private set; }
        public Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads++;
            if (Unavailable) throw new InvalidOperationException("test-only-private-database-detail");
            return Task.FromResult(Owner is not null);
        }
        public Task<OwnerProvisionResult> ProvisionAsync(VerifiedGoogleIdentity identity, CancellationToken cancellationToken)
        {
            Writes++;
            if (Owner is not null) return Task.FromResult(EfOwnerIdentityStore.Match(Owner, identity));
            Owner = new() { Id = 1, GoogleIssuer = identity.Issuer, GoogleSubject = identity.Subject, DisplayName = identity.DisplayName, CreatedAt = DateTime.UtcNow };
            return Task.FromResult(OwnerProvisionResult.Created);
        }
    }

    private sealed class FakeVerifier : IGoogleIdentityVerifier
    {
        public bool Reject { get; set; }
        public int Calls { get; private set; }
        public string Subject { get; set; } = "test-owner";
        public Task<VerifiedGoogleIdentity> VerifyAsync(string token, string clientId, string nonce, CancellationToken cancellationToken)
        {
            Calls++;
            if (Reject) throw new OwnerOperationException("googleProofInvalid");
            return Task.FromResult(new VerifiedGoogleIdentity("https://accounts.google.com", Subject, "Test owner"));
        }
    }
}
