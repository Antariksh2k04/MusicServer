namespace MusicServer.Diagnostics;

public sealed class DiagnosticOptions
{
    public bool Enabled { get; set; }
    public string OperatorKey { get; set; } = "";
    public string BrowserOrigin { get; set; } = "http://127.0.0.1:5173";
    public string StorageDirectory { get; set; } = "";
    public int SessionSeconds { get; set; } = 1800;
    public long StorageByteLimit { get; set; } = 250 * 1024 * 1024;
    public int StorageOperationLimit { get; set; } = 10_000;
}
