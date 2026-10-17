public sealed class FakeStorage : ICloudStorage
{
    private readonly Func<byte[], byte[]> _onDownload;
    private byte[] _stored = Array.Empty<byte>();

    public bool LocalFileExistedAtDownload { get; private set; }
    public string? UploadedName { get; private set; }

    public FakeStorage(Func<byte[], byte[]>? onDownload = null)
    {
        _onDownload = onDownload ?? (bytes => bytes);
    }

    public async Task<RemoteFile> UploadAsync(
        string folderName, string fileName, string localFilePath,
        CancellationToken cancellationToken = default)
    {
        _stored = await File.ReadAllBytesAsync(localFilePath, cancellationToken);
        UploadedName = fileName;
        return new RemoteFile("fake-id", fileName, _stored.Length);
    }

    public async Task DownloadAsync(
        string remoteFileId, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        LocalFileExistedAtDownload = File.Exists(destinationPath);
        await File.WriteAllBytesAsync(
            destinationPath, _onDownload(_stored), cancellationToken);
    }
}