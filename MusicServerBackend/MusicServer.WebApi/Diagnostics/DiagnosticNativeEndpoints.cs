namespace MusicServer.Diagnostics;

public static class DiagnosticNativeEndpoints
{
    public sealed record RefreshRequest(string RefreshToken);

    public static void MapNativeDiagnostics(this RouteGroupBuilder diagnostic)
    {
        var native = diagnostic.MapGroup("/native").AddEndpointFilter(async (context, next) =>
        {
            // Native HTTP has no browser Origin or cookies. Reject ambiguous authority and
            // cross-origin credential provisioning, including requests from a WebView.
            if (context.HttpContext.Request.Headers.ContainsKey("Origin")
                || context.HttpContext.Request.Headers.ContainsKey("Cookie")) return Problem(403, "nativeOnly");
            return await next(context);
        });
        native.MapPost("/session", (DiagnosticEndpoints.Login login, DiagnosticNativeSessions sessions) =>
            sessions.Create(login.OperatorKey) is { } grant ? Results.Ok(grant) : Problem(401, "signInRequired"));
        native.MapPost("/refresh", (RefreshRequest request, DiagnosticNativeSessions sessions) =>
            sessions.Refresh(request.RefreshToken) is { } grant ? Results.Ok(grant) : Problem(401, "signInRequired"));
        // Idempotent revocation by refresh authority works even after access expiry.
        native.MapPost("/logout", (RefreshRequest request, DiagnosticNativeSessions sessions) =>
        {
            sessions.Revoke(request.RefreshToken);
            return Results.NoContent();
        });
        var owner = native.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            var header = context.HttpContext.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : null;
            if (!context.HttpContext.RequestServices.GetRequiredService<DiagnosticNativeSessions>().Authenticate(token))
                return Problem(401, "signInRequired");
            return await next(context);
        });
        owner.MapGet("/fixtures", (FixtureCatalog catalog) => Results.Ok(new { items = catalog.List() }));
        owner.MapGet("/fixtures/{id:guid}", (Guid id, FixtureCatalog catalog) =>
            catalog.Find(id) is { } track ? Results.Ok(track) : Problem(404, "trackUnavailable"));
        owner.MapMethods("/fixtures/{id:guid}/stream", ["GET", "HEAD"], DiagnosticEndpoints.StreamAsync);
    }

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        title: "Local native experiment request failed", extensions: new Dictionary<string, object?> { ["code"] = code });
}
