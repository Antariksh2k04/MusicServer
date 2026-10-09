using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MusicServer.Diagnostics;

public sealed record DiagnosticSession(string Token, DateTimeOffset ExpiresAt);

public sealed class DiagnosticSessions(DiagnosticOptions options, TimeProvider clock)
{
    public const string CookieName = "music_diagnostic_session";
    private readonly ConcurrentDictionary<string, DateTimeOffset> sessions = new();
    private readonly byte[] keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.OperatorKey));
    private readonly object gate = new();

    public DiagnosticSession? Create(string key)
    {
        if (key.Length > 256 || !CryptographicOperations.FixedTimeEquals(
            keyHash, SHA256.HashData(Encoding.UTF8.GetBytes(key)))) return null;
        lock (gate)
        {
            foreach (var entry in sessions.Where(x => x.Value <= clock.GetUtcNow())) sessions.TryRemove(entry.Key, out _);
            if (sessions.Count >= 32) return null;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var expires = clock.GetUtcNow().AddSeconds(options.SessionSeconds);
            sessions[Digest(token)] = expires;
            return new(token, expires);
        }
    }

    public DateTimeOffset? Authenticate(string? token)
    {
        if (token is null || token.Length != 64) return null;
        var hash = Digest(token);
        if (!sessions.TryGetValue(hash, out var expires)) return null;
        if (expires > clock.GetUtcNow()) return expires;
        sessions.TryRemove(hash, out _);
        return null;
    }

    public void Revoke(string? token)
    {
        if (token is not null) sessions.TryRemove(Digest(token), out _);
    }
    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
