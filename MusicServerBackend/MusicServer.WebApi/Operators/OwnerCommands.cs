using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MusicServer.Application.Auth;
using MusicServer.Infrastructure.Auth;
using MusicServer.Infrastructure.Persistence;

namespace MusicServer.Operators;

public static class OwnerCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || args[0] is not ("nonce" or "configuration" or "status" or "provision"))
        {
            Console.Error.WriteLine("Usage: owner nonce|configuration|status|provision");
            return 2;
        }
        try
        {
            if (args[0] == "nonce")
            {
                Console.WriteLine(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
                return 0;
            }
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build()
                .GetSection("Auth:Google").Get<GoogleClientConfiguration>() ?? new();
            if (args[0] == "configuration")
            {
                configuration.ValidateForProvisioning();
                Console.WriteLine(configuration.IsRuntimeConfigured
                    ? "Google client configuration is structurally complete; actual Google registrations remain operator-verified."
                    : "Google provisioning configuration is present; Android/runtime configuration remains incomplete.");
                return 0;
            }
            if (args[0] == "provision") configuration.ValidateForProvisioning();
            var connection = DatabaseMigrator.RequireConnection(Environment.GetEnvironmentVariable("ConnectionStrings__MusicServerMigration"));
            await using var context = new MusicServerDbContext(new DbContextOptionsBuilder<MusicServerDbContext>().UseMySQL(connection).Options);
            var store = new EfOwnerIdentityStore(context, TimeProvider.System);
            if (args[0] == "status")
            {
                Console.WriteLine(await store.ExistsAsync(CancellationToken.None) ? "Owner is configured." : "Owner is not configured.");
                return 0;
            }
            var token = Environment.GetEnvironmentVariable("MUSICSERVER_OWNER_ID_TOKEN") ?? "";
            var nonce = Environment.GetEnvironmentVariable("MUSICSERVER_OWNER_NONCE") ?? "";
            var result = await new OwnerProvisioner(new GoogleIdentityVerifier(TimeProvider.System), store, configuration)
                .ProvisionAsync(token, nonce, CancellationToken.None);
            Console.WriteLine(result == OwnerProvisionResult.Created
                ? "Verified Google owner provisioned. No app session was issued."
                : "Verified identity matches the existing owner. No changes made.");
            return 0;
        }
        catch (OwnerOperationException exception)
        {
            Console.Error.WriteLine("Owner operation failed: " + exception.Code + ". See docs/owner-provisioning.md.");
            return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Owner operation failed. Check local configuration, Google availability, and database permissions. Credentials and identity details were not logged.");
            return 1;
        }
    }
}
