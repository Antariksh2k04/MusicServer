using Microsoft.EntityFrameworkCore;
using MusicServer.Application.Auth;
using MusicServer.Domain.Entities;
using MusicServer.Infrastructure.Persistence;
using MySql.Data.MySqlClient;

namespace MusicServer.Infrastructure.Auth;

public sealed class EfOwnerIdentityStore(MusicServerDbContext context, TimeProvider clock) : IOwnerIdentityStore
{
    public Task<bool> ExistsAsync(CancellationToken cancellationToken) => context.Owners.AsNoTracking()
        .AnyAsync(owner => owner.Id == 1 && owner.GoogleIssuer == "https://accounts.google.com" && owner.GoogleSubject != "", cancellationToken);

    public async Task<OwnerProvisionResult> ProvisionAsync(VerifiedGoogleIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.Issuer != "https://accounts.google.com" || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255)
            throw new OwnerOperationException("googleProofInvalid");
        var existing = await context.Owners.AsNoTracking().SingleOrDefaultAsync(owner => owner.Id == 1, cancellationToken);
        if (existing is not null) return Match(existing, identity);
        var owner = new Owner { Id = 1, GoogleIssuer = identity.Issuer, GoogleSubject = identity.Subject,
            DisplayName = identity.DisplayName, CreatedAt = clock.GetUtcNow().UtcDateTime };
        context.Owners.Add(owner);
        try { await context.SaveChangesAsync(cancellationToken); return OwnerProvisionResult.Created; }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            // The singleton primary key arbitrates concurrent operator invocations. Never replace an owner.
            context.Entry(owner).State = EntityState.Detached;
            existing = await context.Owners.AsNoTracking().SingleAsync(record => record.Id == 1, cancellationToken);
            return Match(existing, identity);
        }
    }

    internal static OwnerProvisionResult Match(Owner existing, VerifiedGoogleIdentity identity)
    {
        if (existing.GoogleIssuer != identity.Issuer || existing.GoogleSubject != identity.Subject)
            throw new OwnerOperationException("ownerAlreadyConfigured");
        return OwnerProvisionResult.AlreadyConfigured;
    }
}
