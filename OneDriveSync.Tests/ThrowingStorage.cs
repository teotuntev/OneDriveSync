public sealed class ThrowingStorage : ICloudStorage
{
    public Task<RemoteFile> UploadAsync(
        string folderName, string fileName, string localFilePath,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("upload failed");

    public Task DownloadAsync(
        string remoteFileId, string destinationPath,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("download failed");
}