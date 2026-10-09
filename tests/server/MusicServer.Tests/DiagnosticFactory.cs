using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicServer.Storage;

namespace MusicServer.Tests;

public sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}

public sealed class DiagnosticFactory(string environment = "Development", bool enabled = true, bool remote = false,
    long byteLimit = 250 * 1024 * 1024, bool injectStorageFaults = false) : WebApplicationFactory<Program>
{
    public const string Key = "test-only-operator-key-at-least-32-characters";
    public const string Origin = "http://127.0.0.1:5173";
    public TestClock Clock { get; } = new();
    public FaultingObjectStore? ObjectStore { get; private set; }
    public string Storage { get; } = Path.Combine(RepositoryRoot(), ".local", "tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Diagnostics:Enabled", enabled.ToString());
        builder.UseSetting("Diagnostics:OperatorKey", Key);
        builder.UseSetting("Diagnostics:BrowserOrigin", Origin);
        builder.UseSetting("Diagnostics:StorageDirectory", Storage);
        builder.UseSetting("Diagnostics:StorageByteLimit", byteLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureServices(services =>
        {
            if (injectStorageFaults)
            {
                var registration = services.Single(descriptor => descriptor.ServiceType == typeof(IObjectStore));
                services.Remove(registration);
                services.AddSingleton<IObjectStore>(provider => ObjectStore = new FaultingObjectStore(
                    (IObjectStore)registration.ImplementationFactory!(provider)));
            }
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IStartupFilter>(new AddressFilter(remote));
        });
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(current.FullName, "global.json")))
            current = current.Parent ?? throw new InvalidOperationException("Repository root not found.");
        return current.FullName;
    }

    private sealed class AddressFilter(bool remote) : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, proceed) =>
                {
                    context.Connection.RemoteIpAddress = remote ? IPAddress.Parse("198.51.100.1") : IPAddress.Loopback;
                    await proceed(context);
                });
                next(app);
            };
    }
}
