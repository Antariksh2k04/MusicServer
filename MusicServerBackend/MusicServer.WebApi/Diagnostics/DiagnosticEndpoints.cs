using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using MusicServer.Streaming;

namespace MusicServer.Diagnostics;

public static class DiagnosticEndpoints
{
    public sealed record Login(string OperatorKey);

    public static void MapDiagnostics(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/diagnostics"))
            {
                context.Response.Headers.CacheControl = "no-store";
                if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip))
                {
                    await Problem(403, "localOnly").ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
        var diagnostic = app.MapGroup("/api/v1/diagnostics");
        diagnostic.MapNativeDiagnostics();
        diagnostic.MapGet("/bootstrap", (HttpContext context, IAntiforgery csrf) => Results.Ok(new
        {
            mode = "diagnostic", csrfToken = csrf.GetAndStoreTokens(context).RequestToken
        }));
        diagnostic.MapPost("/session", async (HttpContext context, Login login, DiagnosticSessions sessions,
            DiagnosticOptions options, IAntiforgery csrf) =>
        {
            if (!await SafeMutationAsync(context, options, csrf)) return Problem(403, "csrfRejected");
            if (login.OperatorKey is null || sessions.Create(login.OperatorKey) is not { } session)
                return Problem(401, "signInRequired");
            context.Response.Cookies.Append(DiagnosticSessions.CookieName, session.Token, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/api/v1/diagnostics",
                // This intentionally isolated HTTP experiment never uses the production cookie name.
                Secure = false, MaxAge = TimeSpan.FromSeconds(options.SessionSeconds)
            });
            return Results.Ok(new { mode = "diagnostic", session.ExpiresAt });
        });
        var owner = diagnostic.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var sessions = http.RequestServices.GetRequiredService<DiagnosticSessions>();
            if (http.Request.Headers.ContainsKey("Authorization")) return Problem(401, "signInRequired");
            if (sessions.Authenticate(http.Request.Cookies[DiagnosticSessions.CookieName]) is null)
                return Problem(401, "signInRequired");
            if (HttpMethods.IsPost(http.Request.Method) && !await SafeMutationAsync(http,
                http.RequestServices.GetRequiredService<DiagnosticOptions>(),
                http.RequestServices.GetRequiredService<IAntiforgery>())) return Problem(403, "csrfRejected");
            return await next(context);
        });
        owner.MapGet("/session", (HttpContext context, DiagnosticSessions sessions, IAntiforgery csrf) => Results.Ok(new
        {
            mode = "diagnostic", expiresAt = sessions.Authenticate(context.Request.Cookies[DiagnosticSessions.CookieName]),
            csrfToken = csrf.GetAndStoreTokens(context).RequestToken
        }));
        owner.MapPost("/logout", (HttpContext context, DiagnosticSessions sessions) =>
        {
            sessions.Revoke(context.Request.Cookies[DiagnosticSessions.CookieName]);
            context.Response.Cookies.Delete(DiagnosticSessions.CookieName, new CookieOptions { Path = "/api/v1/diagnostics" });
            return Results.NoContent();
        });
        owner.MapPost("/native/revoke-all", (DiagnosticNativeSessions sessions) =>
        {
            sessions.RevokeAll();
            return Results.NoContent();
        });
        owner.MapGet("/fixtures", (FixtureCatalog catalog) => Results.Ok(new { items = catalog.List() }));
        owner.MapGet("/fixtures/{id:guid}", (Guid id, FixtureCatalog catalog) =>
            catalog.Find(id) is { } track ? Results.Ok(track) : Problem(404, "trackUnavailable"));
        owner.MapPost("/fixtures", UploadAsync);
        owner.MapMethods("/fixtures/{id:guid}/stream", ["GET", "HEAD"], StreamAsync);
    }

    private static async Task<IResult> UploadAsync(HttpContext context, FixtureCatalog catalog)
    {
        if (!long.TryParse(context.Request.Headers["X-File-Size"], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
            return Problem(400, "transferIncomplete");
        if (!string.Equals(context.Request.ContentType, "audio/mpeg", StringComparison.OrdinalIgnoreCase))
            return Problem(400, "invalidMp3");
        try
        {
            var name = Uri.UnescapeDataString(context.Request.Headers["X-File-Name"].ToString());
            var result = await catalog.UploadAsync(context.Request.Body, name, size, context.RequestAborted);
            return Results.Json(new { track = result.Track, duplicate = result.Duplicate }, statusCode: result.Duplicate ? 200 : 201);
        }
        catch (FixtureFailure failure) { return Problem(failure.Status, failure.Code); }
        catch (BadHttpRequestException failure) { return Problem(failure.StatusCode, "transferIncomplete"); }
        catch (IOException) { return Problem(503, "storageUnavailable"); }
        catch (UnauthorizedAccessException) { return Problem(503, "storageUnavailable"); }
    }

    internal static async Task<IResult> StreamAsync(Guid id, HttpContext context, FixtureCatalog catalog)
    {
        var track = catalog.Find(id);
        if (track is null) return Problem(404, "trackUnavailable");
        var head = HttpMethods.IsHead(context.Request.Method);
        var range = ByteRange.Resolve(head ? null : context.Request.Headers.Range.ToString(),
            context.Request.Headers.IfRange.ToString(), track.Etag, track.SizeBytes);
        if (range.Kind == RangeKind.Multiple) return Problem(400, "multipleRangesUnsupported");
        if (range.Kind == RangeKind.Unsatisfiable)
        {
            context.Response.Headers.ContentRange = $"bytes */{track.SizeBytes}";
            return Problem(416, "rangeUnsatisfiable");
        }
        try
        {
            var metadata = await catalog.HeadAsync(id, context.RequestAborted);
            if (metadata is null) return Problem(404, "trackUnavailable");
            if (metadata.SizeBytes != track.SizeBytes || metadata.Etag != track.Etag) return Problem(503, "storageUnavailable");
            await using var source = head ? null : await catalog.OpenRangeAsync(id, range.Start, range.Length, context.RequestAborted);
            if (source is not null && (source.Start != range.Start || source.Length != range.Length
                || source.Metadata != metadata)) return Problem(503, "storageUnavailable");
            context.Response.StatusCode = range.Kind == RangeKind.Partial ? 206 : 200;
            context.Response.ContentType = "audio/mpeg";
            context.Response.ContentLength = range.Length;
            context.Response.Headers.AcceptRanges = "bytes";
            context.Response.Headers.ETag = track.Etag;
            if (range.Kind == RangeKind.Partial)
                context.Response.Headers.ContentRange = $"bytes {range.Start}-{range.Start + range.Length - 1}/{track.SizeBytes}";
            if (source is not null) await BoundedStreamCopy.CopyAsync(source.Content, context.Response.Body, range.Length, context.RequestAborted);
            return Results.Empty;
        }
        catch (FileNotFoundException) { return StreamProblem(context, 404, "trackUnavailable"); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return StreamProblem(context, 503, "storageUnavailable");
        }
    }

    private static IResult StreamProblem(HttpContext context, int status, string code)
    {
        if (context.Response.HasStarted) { context.Abort(); return Results.Empty; }
        // The provider can fail on its first read after media headers were assigned.
        // Preserve unrelated headers (including no-store) for the problem response.
        context.Response.ContentLength = null;
        context.Response.ContentType = null;
        context.Response.Headers.Remove("Accept-Ranges");
        context.Response.Headers.Remove("Content-Range");
        context.Response.Headers.Remove("ETag");
        return Problem(status, code);
    }

    private static async Task<bool> SafeMutationAsync(HttpContext context, DiagnosticOptions options, IAntiforgery csrf)
    {
        if (context.Request.Headers.Origin.ToString() != options.BrowserOrigin) return false;
        try { await csrf.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        title: "Local experiment request failed", extensions: new Dictionary<string, object?> { ["code"] = code });
}
