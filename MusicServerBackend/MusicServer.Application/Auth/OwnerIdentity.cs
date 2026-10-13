namespace MusicServer.Application.Auth;

public sealed record VerifiedGoogleIdentity(string Issuer, string Subject, string? DisplayName);
public enum OwnerProvisionResult { Created, AlreadyConfigured }

public interface IGoogleIdentityVerifier
{
    Task<VerifiedGoogleIdentity> VerifyAsync(string idToken, string clientId, string expectedNonce, CancellationToken cancellationToken);
}

public interface IOwnerIdentityStore
{
    Task<bool> ExistsAsync(CancellationToken cancellationToken);
    Task<OwnerProvisionResult> ProvisionAsync(VerifiedGoogleIdentity identity, CancellationToken cancellationToken);
}

public sealed class OwnerOperationException(string code) : InvalidOperationException("Owner operation failed: " + code)
{
    public string Code { get; } = code;
}

public sealed class OwnerProvisioner(IGoogleIdentityVerifier verifier, IOwnerIdentityStore store, GoogleClientConfiguration configuration)
{
    public async Task<OwnerProvisionResult> ProvisionAsync(string idToken, string nonce, CancellationToken cancellationToken)
    {
        configuration.ValidateForProvisioning();
        var identity = await verifier.VerifyAsync(idToken, configuration.WebClientId, nonce, cancellationToken);
        if (identity.Issuer != "https://accounts.google.com" || string.IsNullOrWhiteSpace(identity.Subject)
            || identity.Subject.Length > 255)
            throw new OwnerOperationException("googleProofInvalid");
        return await store.ProvisionAsync(identity, cancellationToken);
    }
}

public sealed class OwnerReadiness(IOwnerIdentityStore store, GoogleClientConfiguration configuration)
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!configuration.IsRuntimeConfigured) return false;
        try { return await store.ExistsAsync(cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return false; }
    }
}
