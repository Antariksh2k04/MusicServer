using Google.Apis.Auth;
using MusicServer.Application.Auth;
using MusicServer.Infrastructure.Auth;
using Xunit;

namespace MusicServer.Tests.Unit.Auth;

public sealed class GoogleIdentityVerifierTests
{
    private const string Audience = "test-web.apps.googleusercontent.com";
    private const string Nonce = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Token = "test-header.test-payload.test-signature";
    private static GoogleJsonWebSignature.Payload Payload() => new()
    {
        Issuer = "https://accounts.google.com", Subject = "test-owner", Audience = Audience, Nonce = Nonce,
        IssuedAtTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ExpirationTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(), Name = "Test owner"
    };

    [Theory]
    [InlineData("accounts.google.com")]
    [InlineData("https://accounts.google.com")]
    public async Task ValidatedGooglePayload_UsesCanonicalImmutableIdentity(string issuer)
    {
        var payload = Payload(); payload.Issuer = issuer;
        var verifier = new GoogleIdentityVerifier(TimeProvider.System, (_, settings) =>
        {
            Assert.Equal([Audience], settings.Audience);
            Assert.Equal(TimeSpan.Zero, settings.ExpirationTimeClockTolerance);
            return Task.FromResult(payload);
        });
        var identity = await verifier.VerifyAsync(Token, Audience, Nonce, default);
        Assert.Equal("https://accounts.google.com", identity.Issuer);
        Assert.Equal("test-owner", identity.Subject);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    [InlineData("future-issued")]
    [InlineData("future-not-before")]
    [InlineData("subject")]
    public async Task InvalidClaims_CannotBecomeVerifiedIdentity(string scenario)
    {
        var payload = Payload();
        switch (scenario)
        {
            case "nonce": payload.Nonce = new string('B', 43); break;
            case "audience": payload.Audience = "other.apps.googleusercontent.com"; break;
            case "issuer": payload.Issuer = "https://other.example"; break;
            case "expired": payload.ExpirationTimeSeconds = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds(); break;
            case "future-issued": payload.IssuedAtTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(); break;
            case "future-not-before": payload.NotBeforeTimeSeconds = DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(); break;
            case "subject": payload.Subject = ""; break;
        }
        var verifier = new GoogleIdentityVerifier(TimeProvider.System, (_, _) => Task.FromResult(payload));
        var error = await Assert.ThrowsAsync<OwnerOperationException>(() => verifier.VerifyAsync(Token, Audience, Nonce, default));
        Assert.Equal("googleProofInvalid", error.Code);
    }

    [Fact]
    public async Task LibrarySignatureFailure_IsRedactedAndRejected()
    {
        var verifier = new GoogleIdentityVerifier(TimeProvider.System, (_, _) => throw new InvalidJwtException("test-only-private-token-detail"));
        var error = await Assert.ThrowsAsync<OwnerOperationException>(() => verifier.VerifyAsync(Token, Audience, Nonce, default));
        Assert.Equal("googleProofInvalid", error.Code);
        Assert.DoesNotContain("private-token-detail", error.Message);
    }

    [Fact]
    public async Task ProviderOutage_IsDistinguishedFromInvalidProof()
    {
        var verifier = new GoogleIdentityVerifier(TimeProvider.System, (_, _) => throw new HttpRequestException("test-only-private-provider-detail"));
        var error = await Assert.ThrowsAsync<OwnerOperationException>(() => verifier.VerifyAsync(Token, Audience, Nonce, default));
        Assert.Equal("googleProviderUnavailable", error.Code);
        Assert.DoesNotContain("private-provider-detail", error.Message);
    }

    [Fact]
    public async Task CancelledOrMalformedInput_DoesNotCallProvider()
    {
        var calls = 0;
        var verifier = new GoogleIdentityVerifier(TimeProvider.System, (_, _) => { calls++; return Task.FromResult(Payload()); });
        await Assert.ThrowsAsync<OwnerOperationException>(() => verifier.VerifyAsync("not-a-token", Audience, Nonce, default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifier.VerifyAsync(Token, Audience, Nonce, new CancellationToken(true)));
        Assert.Equal(0, calls);
    }
}
