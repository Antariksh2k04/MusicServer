using System.Security.Cryptography;
using System.Text;

namespace MusicServer.Diagnostics;

public sealed record NativeDiagnosticGrant(string AccessToken, DateTimeOffset AccessExpiresAt,
    string RefreshToken, DateTimeOffset SessionExpiresAt);

// Short-lived, in-memory experiment authority, registered only in explicit Development mode.
public sealed class DiagnosticNativeSessions(DiagnosticOptions options, TimeProvider clock)
{
    private sealed class Family
    {
        public required string AccessHash { get; set; }
        public required string RefreshHash { get; set; }
        public required DateTimeOffset AccessExpires { get; set; }
        public required DateTimeOffset Expires { get; init; }
        public HashSet<string> Consumed { get; } = [];
    }

    private readonly object gate = new();
    private readonly List<Family> families = [];
    private readonly byte[] keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.OperatorKey));

    public NativeDiagnosticGrant? Create(string? key)
    {
        if (key is null || key.Length > 256 || !CryptographicOperations.FixedTimeEquals(keyHash,
            SHA256.HashData(Encoding.UTF8.GetBytes(key)))) return null;
        lock (gate)
        {
            Prune();
            if (families.Count >= 32) return null;
            var family = new Family { AccessHash = "", RefreshHash = "", AccessExpires = default,
                Expires = clock.GetUtcNow().AddSeconds(options.SessionSeconds) };
            families.Add(family);
            return Issue(family);
        }
    }

    public NativeDiagnosticGrant? Refresh(string? token)
    {
        if (!ValidToken(token)) return null;
        lock (gate)
        {
            Prune();
            var hash = Digest(token!);
            var replay = families.Find(x => x.Consumed.Contains(hash));
            if (replay is not null) { families.Remove(replay); return null; }
            var family = families.Find(x => x.RefreshHash == hash);
            // Bound retained lineage even with maliciously rapid rotations.
            if (family is null) return null;
            if (family.Consumed.Count >= 128) { families.Remove(family); return null; }
            family.Consumed.Add(hash);
            return Issue(family);
        }
    }

    public bool Authenticate(string? token)
    {
        if (!ValidToken(token)) return false;
        lock (gate)
        {
            Prune();
            return families.Any(x => x.AccessHash == Digest(token!) && x.AccessExpires > clock.GetUtcNow());
        }
    }

    public void Revoke(string? refresh)
    {
        if (!ValidToken(refresh)) return;
        lock (gate)
        {
            var hash = Digest(refresh!);
            families.RemoveAll(x => x.RefreshHash == hash || x.Consumed.Contains(hash));
        }
    }

    public void RevokeAll() { lock (gate) families.Clear(); }

    private NativeDiagnosticGrant Issue(Family family)
    {
        var access = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var refresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        family.AccessHash = Digest(access);
        family.RefreshHash = Digest(refresh);
        family.AccessExpires = DateTimeOffset.Compare(clock.GetUtcNow().AddSeconds(options.NativeAccessSeconds),
            family.Expires) < 0 ? clock.GetUtcNow().AddSeconds(options.NativeAccessSeconds) : family.Expires;
        return new(access, family.AccessExpires, refresh, family.Expires);
    }

    private void Prune() => families.RemoveAll(x => x.Expires <= clock.GetUtcNow());
    private static bool ValidToken(string? token) => token is { Length: 64 } && token.All(Uri.IsHexDigit);
    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
