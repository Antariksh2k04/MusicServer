using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Google.Apis.Util;
using MusicServer.Application.Auth;

namespace MusicServer.Infrastructure.Auth;

public sealed class GoogleIdentityVerifier : IGoogleIdentityVerifier
{
    private readonly TimeProvider clock;
    private readonly Func<string, GoogleJsonWebSignature.ValidationSettings, Task<GoogleJsonWebSignature.Payload>> validate;

    public GoogleIdentityVerifier(TimeProvider clock) : this(clock, GoogleJsonWebSignature.ValidateAsync) { }
    internal GoogleIdentityVerifier(TimeProvider clock,
        Func<string, GoogleJsonWebSignature.ValidationSettings, Task<GoogleJsonWebSignature.Payload>> validate)
    {
        this.clock = clock;
        this.validate = validate;
    }

    public async Task<VerifiedGoogleIdentity> VerifyAsync(string idToken, string clientId, string expectedNonce, CancellationToken cancellationToken)
    {
        if (!GoogleClientConfiguration.IsClientId(clientId)) throw new OwnerOperationException("googleConfigurationMissing");
        if (idToken.Length is < 10 or > 16384 || idToken.Count(character => character == '.') != 2
            || expectedNonce.Length != 43 || !expectedNonce.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            throw new OwnerOperationException("googleProofInvalid");
        cancellationToken.ThrowIfCancellationRequested();
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await validate(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId], Clock = new ProviderClock(clock),
                IssuedAtClockTolerance = TimeSpan.FromSeconds(30), ExpirationTimeClockTolerance = TimeSpan.Zero
            }).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        catch (InvalidJwtException) { throw new OwnerOperationException("googleProofInvalid"); }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException)
        { throw new OwnerOperationException("googleProviderUnavailable"); }
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (payload.Issuer is not ("accounts.google.com" or "https://accounts.google.com")
            || string.IsNullOrWhiteSpace(payload.Subject) || payload.Subject.Length > 255
            || payload.AudienceAsList is null || payload.AudienceAsList.Count() != 1 || payload.AudienceAsList.Single() != clientId
            || payload.ExpirationTimeSeconds is null || payload.ExpirationTimeSeconds <= now
            || payload.IssuedAtTimeSeconds is null || payload.IssuedAtTimeSeconds > now + 30
            || (payload.NotBeforeTimeSeconds is not null && payload.NotBeforeTimeSeconds > now)
            || payload.Nonce is null || payload.Nonce.Length != expectedNonce.Length
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(payload.Nonce), Encoding.UTF8.GetBytes(expectedNonce)))
            throw new OwnerOperationException("googleProofInvalid");
        return new("https://accounts.google.com", payload.Subject, payload.Name?.Length <= 1024 ? payload.Name : null);
    }

    private sealed class ProviderClock(TimeProvider clock) : IClock
    {
        public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;
        public DateTime Now => clock.GetLocalNow().DateTime;
    }
}
