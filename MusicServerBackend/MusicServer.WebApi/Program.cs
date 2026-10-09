using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using MusicServer.Diagnostics;
using MusicServer.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, builder.Configuration.GetValue("Local:Port", 5080));
    options.Limits.MaxRequestBodySize = FixtureCatalog.FileLimit;
});
var diagnostics = builder.Configuration.GetSection("Diagnostics").Get<DiagnosticOptions>() ?? new();
var diagnosticMode = builder.Environment.IsDevelopment() && diagnostics.Enabled;
if (diagnosticMode)
{
    if (diagnostics.OperatorKey.Length is < 32 or > 256 || diagnostics.SessionSeconds is < 1 or > 3600
        || diagnostics.NativeAccessSeconds is < 1 or > 600
        || diagnostics.StorageByteLimit < 1 || diagnostics.StorageOperationLimit < 1
        || !Uri.TryCreate(diagnostics.BrowserOrigin, UriKind.Absolute, out var origin)
        || !origin.IsLoopback || origin.Scheme != "http")
        throw new InvalidOperationException("Local diagnostics require a configured operator key and loopback HTTP origin.");
    if (string.IsNullOrWhiteSpace(diagnostics.StorageDirectory))
        diagnostics.StorageDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.local/media"));
    builder.Services.AddSingleton(diagnostics);
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<DiagnosticSessions>();
    builder.Services.AddSingleton<DiagnosticNativeSessions>();
    builder.Services.AddSingleton<IObjectStore>(services => new LocalFileObjectStore(
        services.GetRequiredService<IHostEnvironment>(), new LocalObjectStoreOptions
        {
            Enabled = true,
            Directory = Path.Combine(diagnostics.StorageDirectory, "objects-session-" + Guid.NewGuid().ToString("N")),
            ByteLimit = diagnostics.StorageByteLimit,
            OperationLimit = diagnostics.StorageOperationLimit
        }));
    builder.Services.AddSingleton<FixtureCatalog>();
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    builder.Services.Configure<KeyManagementOptions>(options =>
    {
        options.XmlRepository = new TransientKeyRepository();
        options.XmlEncryptor = null;
    });
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-Token";
        options.Cookie.Name = "music_diagnostic_csrf";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
    });
}
var app = builder.Build();
app.MapGet("/api/v1/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/api/v1/health/ready", () => Results.Json(
    new { status = "unavailable", code = "dependenciesNotConfigured" }, statusCode: 503));
if (diagnosticMode) app.MapDiagnostics();
app.Run();

public partial class Program;
