using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;

public sealed class OneDriveStorage : ICloudStorage
{
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
        var folderId = await EnsureFolderAsync(folderName, cancellationToken);

        await using var stream = File.OpenRead(localFilePath);

        var uploaded = await _client.Drives[_driveId]
            .Items[folderId]
            .ItemWithPath(fileName)
            .Content
            .PutAsync(stream, cancellationToken: cancellationToken);

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

    public async Task DownloadAsync(
        string remoteFileId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        await using var downloadStream = await _client
            .Drives[_driveId]
            .Items[remoteFileId]
            .Content
            .GetAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "Graph returned no download stream.");

        await using var outputStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await downloadStream.CopyToAsync(outputStream, cancellationToken);

        Console.WriteLine("Downloaded file to local disk.");
    }
}