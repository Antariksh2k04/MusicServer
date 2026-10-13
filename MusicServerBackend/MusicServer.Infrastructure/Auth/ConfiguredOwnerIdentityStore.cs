using Microsoft.EntityFrameworkCore;
using MusicServer.Application.Auth;
using MusicServer.Infrastructure.Persistence;

namespace MusicServer.Infrastructure.Auth;

// Runtime identity uses its own DML connection. No startup migration or fallback to operator credentials.
public sealed class ConfiguredOwnerIdentityStore(string? connectionString, TimeProvider clock) : IOwnerIdentityStore
{
    private MusicServerDbContext OpenContext()
    {
        if (string.IsNullOrWhiteSpace(connectionString)) throw new OwnerOperationException("ownerDatabaseNotConfigured");
        var connection = DatabaseMigrator.RequireConnection(connectionString);
        return new(new DbContextOptionsBuilder<MusicServerDbContext>().UseMySQL(connection).Options);
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken)
    {
        await using var context = OpenContext();
        return await new EfOwnerIdentityStore(context, clock).ExistsAsync(cancellationToken);
    }

    public Task<OwnerProvisionResult> ProvisionAsync(VerifiedGoogleIdentity identity, CancellationToken cancellationToken)
        => throw new OwnerOperationException("operatorOnly");
}
