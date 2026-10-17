
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using DriveUpload = Microsoft.Graph.Drives.Item.Items.Item.CreateUploadSession;

public sealed class OneDriveStorage : ICloudStorage
{
    private const long MaxSimpleUploadBytes = 4L * 1024 * 1024;
    private const int MaxSliceSize = 320 * 1024 * 4;

    private readonly GraphServiceClient _client;
    private readonly string _driveId;

    public OneDriveStorage(GraphServiceClient client, string driveId)
    {
        _client = client;
        _driveId = driveId;
    }

    private async Task<string> EnsureFolderAsync(
        string folderName,
        CancellationToken cancellationToken)
    {
        DriveItem? folder;

        try
        {
            folder = await _client.Drives[_driveId]
                .Root
                .ItemWithPath(folderName)
                .GetAsync(cancellationToken: cancellationToken);

            if (folder?.Folder == null)
            {
                throw new InvalidOperationException(
                    $"'{folderName}' exists but is not a folder.");
            }

            Console.WriteLine($"Folder already exists: {folderName}");
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            folder = await _client.Drives[_driveId]
                .Items["root"]
                .Children
                .PostAsync(new DriveItem
                {
                    Name = folderName,
                    Folder = new Folder(),
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["@microsoft.graph.conflictBehavior"] = "fail"
                    }
                }, cancellationToken: cancellationToken);

            Console.WriteLine($"Folder created: {folderName}");
        }

        if (string.IsNullOrWhiteSpace(folder?.Id))
        {
            throw new InvalidOperationException(
                "Could not retrieve the destination folder ID.");
        }

        return folder.Id;
    }

    public async Task<RemoteFile> UploadAsync(
        string folderName,
        string fileName,
        string localFilePath,
        CancellationToken cancellationToken = default)

    {

        var fileInfo = new FileInfo(localFilePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                $"Upload file not found: {localFilePath}",
                localFilePath);
        }

        var folderId = await EnsureFolderAsync(folderName, cancellationToken);

        if (fileInfo.Length <= MaxSimpleUploadBytes)
        {
            await using var stream = File.OpenRead(localFilePath);

            var uploaded = await _client.Drives[_driveId]
                .Items[folderId]
                .ItemWithPath(fileName)
                .Content
                .PutAsync(
                    stream,
                    cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(uploaded?.Id))
            {
                throw new InvalidOperationException(
                    "Upload did not return a file ID.");
            }

            return new RemoteFile(
                uploaded.Id,
                uploaded.Name ?? fileName,
                uploaded.Size);
        }

        return await UploadLargeAsync(
            folderId,
            fileName,
            localFilePath,
            cancellationToken);
    }

    private async Task<RemoteFile> UploadLargeAsync(
        string folderId,
        string fileName,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        var requestBody = new DriveUpload.CreateUploadSessionPostRequestBody
        {
            Item = new DriveItemUploadableProperties
            {
                AdditionalData = new Dictionary<string, object>
                {
                    ["@microsoft.graph.conflictBehavior"] = "replace"
                }
            }
        };

        var uploadSession = await _client.Drives[_driveId]
            .Items[folderId]
            .ItemWithPath(fileName)
            .CreateUploadSession
            .PostAsync(
                requestBody,
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "Could not create the upload session.");

        await using var stream = File.OpenRead(localFilePath);

        var uploadTask = new LargeFileUploadTask<DriveItem>(
            uploadSession,
            stream,
            MaxSliceSize,
            _client.RequestAdapter);

        var totalLength = stream.Length;

        IProgress<long> progress = new Progress<long>(bytesUploaded =>
        {
            Console.WriteLine(
                $"Uploaded {bytesUploaded:N0} of {totalLength:N0} bytes");
        });

        var result = await uploadTask.UploadAsync(
            progress,
            cancellationToken: cancellationToken);

        if (!result.UploadSucceeded ||
            string.IsNullOrWhiteSpace(result.ItemResponse?.Id))
        {
            throw new InvalidOperationException(
                "Large file upload did not complete successfully.");
        }

        var uploaded = result.ItemResponse;

        return new RemoteFile(
            uploaded.Id!,
            uploaded.Name ?? fileName,
            uploaded.Size);
    }

    public async Task DownloadAsync(
        string remoteFileId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var tempPath = destinationPath + ".download";

        try
        {
            await using (var downloadStream = await _client
                .Drives[_driveId]
                .Items[remoteFileId]
                .Content
                .GetAsync(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException(
                    "Graph returned no download stream."))
            await using (var outputStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                await downloadStream.CopyToAsync(
                    outputStream,
                    cancellationToken);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        Console.WriteLine("Downloaded file to local disk.");
    }
}
