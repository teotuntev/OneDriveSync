public sealed record VerificationResult(
    RemoteFile Remote,
    string OriginalHash,
    string DownloadedHash)
{
    public bool IsMatch => string.Equals(
        OriginalHash,
        DownloadedHash,
        StringComparison.Ordinal);
}