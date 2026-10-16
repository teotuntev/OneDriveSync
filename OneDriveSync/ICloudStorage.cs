public interface ICloudStorage
{
    Task<RemoteFile> UploadAsync(
        string folderName,
        string fileName,
        string localFilePath,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string remoteFileId,
        string destinationPath,
        CancellationToken cancellationToken = default);
}