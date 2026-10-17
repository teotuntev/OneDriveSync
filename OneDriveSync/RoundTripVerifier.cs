public sealed class RoundTripVerifier
{
    private readonly ICloudStorage _storage;

    public RoundTripVerifier(ICloudStorage storage)
    {
        _storage = storage;
    }

    public async Task<VerificationResult> RunAsync(
        string folderName,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(localFilePath);

        var originalHash = await FileHasher.ComputeSha256Async(
            localFilePath, cancellationToken);

        var remote = await _storage.UploadAsync(
            folderName, fileName, localFilePath, cancellationToken);

        // Delete the local copy so the comparison can't pass on the original
        File.Delete(localFilePath);

        await _storage.DownloadAsync(
            remote.Id, localFilePath, cancellationToken);

        var downloadedHash = await FileHasher.ComputeSha256Async(
            localFilePath, cancellationToken);

        return new VerificationResult(remote, originalHash, downloadedHash);
    }
}