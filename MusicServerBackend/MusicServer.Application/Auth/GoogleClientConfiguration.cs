using System.Text.RegularExpressions;

namespace MusicServer.Application.Auth;

public sealed class GoogleClientConfiguration
{
    public string WebClientId { get; set; } = "";
    public string AndroidClientId { get; set; } = "";
    public string BrowserOrigin { get; set; } = "";
    public string AndroidPackage { get; set; } = "com.musicserver.shell";
    public string AndroidCertificateSha1 { get; set; } = "";
    public string NativeOrigin { get; set; } = "https://localhost";

    public static bool IsClientId(string? value) => value is { Length: > 0 and <= 255 } && Regex.IsMatch(value,
        @"\A[A-Za-z0-9-]+\.apps\.googleusercontent\.com\z", RegexOptions.CultureInvariant);

    public void ValidateForProvisioning()
    {
        if (!IsClientId(WebClientId) || !IsHttpsOrigin(BrowserOrigin))
            throw new OwnerOperationException("googleConfigurationMissing");
    }

    public bool IsRuntimeConfigured => IsClientId(WebClientId) && IsClientId(AndroidClientId)
        && WebClientId != AndroidClientId && IsHttpsOrigin(BrowserOrigin)
        && AndroidPackage == "com.musicserver.shell" && NativeOrigin == "https://localhost"
        && AndroidCertificateSha1 is { Length: 59 }
        && Regex.IsMatch(AndroidCertificateSha1, @"^(?:[0-9A-Fa-f]{2}:){19}[0-9A-Fa-f]{2}$", RegexOptions.CultureInvariant);

    private static bool IsHttpsOrigin(string? value) => value is { Length: > 0 and <= 2048 }
        && Uri.TryCreate(value, UriKind.Absolute, out var origin) && origin.Scheme == "https"
        && origin.UserInfo.Length == 0 && origin.AbsolutePath == "/" && origin.Query.Length == 0
        && origin.Fragment.Length == 0 && value == origin.GetLeftPart(UriPartial.Authority);
}
