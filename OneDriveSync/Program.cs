
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

const string folderName = "OneDriveSyncTest";
const string fileName = "test.txt";
const long maxSimpleUploadBytes = 4L * 1024 * 1024;

var filePath = Path.Combine(
    AppContext.BaseDirectory,
    "samples",
    fileName);

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var clientId = configuration["Graph:ClientId"];

if (string.IsNullOrWhiteSpace(clientId))
{
    throw new InvalidOperationException(
        "Missing Graph:ClientId. Set it using dotnet user-secrets.");
}

var credential = new InteractiveBrowserCredential(
    new InteractiveBrowserCredentialOptions
    {
        ClientId = clientId,
        TenantId = "consumers",
        RedirectUri = new Uri("http://localhost")
    });

var client = new GraphServiceClient(
    credential,
    new[] { "Files.ReadWrite" });

var drive = await client.Me.Drive.GetAsync();

if (string.IsNullOrWhiteSpace(drive?.Id))
{
    throw new InvalidOperationException(
        "Could not retrieve the OneDrive drive ID.");
}

var driveId = drive.Id;

Console.WriteLine($"Drive ID: {driveId}");
Console.WriteLine(
    $"Owner: {drive.Owner?.User?.DisplayName ?? "Unknown"}");

DriveItem? folder = null;

try
{
    folder = await client.Drives[driveId]
        .Root
        .ItemWithPath(folderName)
        .GetAsync();

    if (folder?.Folder == null)
    {
        throw new InvalidOperationException(
            $"'{folderName}' exists but is not a folder.");
    }

    Console.WriteLine($"Folder already exists: {folderName}");
}
catch (ApiException ex) when (ex.ResponseStatusCode == 404)
{
    folder = await client.Drives[driveId]
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
        });

    Console.WriteLine($"Folder created: {folderName}");
}

if (string.IsNullOrWhiteSpace(folder?.Id))
{
    throw new InvalidOperationException(
        "Could not retrieve the destination folder ID.");
}

var folderId = folder.Id;

if (!File.Exists(filePath))
{
    throw new FileNotFoundException(
        $"Upload file not found: {filePath}", filePath);
}

var fileInfo = new FileInfo(filePath);

if (fileInfo.Length > maxSimpleUploadBytes)
{
    throw new InvalidOperationException(
        $"File is {fileInfo.Length:N0} bytes. " +
        "This application is configured for files up to 4 MB.");
}

string originalHash;

await using (var originalStream = File.OpenRead(filePath))
{
    var hashBytes = await SHA256.HashDataAsync(originalStream);
    originalHash = Convert.ToHexString(hashBytes);
}

Console.WriteLine($"Original SHA-256: {originalHash}");

DriveItem? uploaded;

await using (var stream = File.OpenRead(filePath))
{
    uploaded = await client.Drives[driveId]
        .Items[folderId]
        .ItemWithPath(fileName)
        .Content
        .PutAsync(stream);
}

var uploadedId = uploaded?.Id;

if (string.IsNullOrWhiteSpace(uploadedId))
{
    throw new InvalidOperationException(
        "Upload did not return a file ID.");
}

Console.WriteLine(
    $"Uploaded {fileName}, local {fileInfo.Length:N0} bytes, " +
    $"remote {uploaded!.Size?.ToString("N0") ?? "Unknown"} bytes, " +
    $"item ID {uploadedId}");

File.Delete(filePath);

Console.WriteLine($"Deleted local file: {filePath}");

await using (var downloadStream = await client
    .Drives[driveId]
    .Items[uploadedId]
    .Content
    .GetAsync()
    ?? throw new InvalidOperationException(
        "Graph returned no download stream."))
{
    await using var outputStream = new FileStream(
        filePath,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None);

    await downloadStream.CopyToAsync(outputStream);
}

Console.WriteLine("Downloaded file to local disk.");

string downloadedHash;

await using (var downloadedStream = File.OpenRead(filePath))
{
    var hashBytes = await SHA256.HashDataAsync(downloadedStream);
    downloadedHash = Convert.ToHexString(hashBytes);
}

Console.WriteLine($"Original SHA-256:   {originalHash}");
Console.WriteLine($"Downloaded SHA-256: {downloadedHash}");

if (string.Equals(
    originalHash,
    downloadedHash,
    StringComparison.Ordinal))
{
    Console.WriteLine("MATCH: File contents are identical.");
}
else
{
    Console.WriteLine("MISMATCH: File contents differ.");
    Environment.ExitCode = 1;
}

public partial class Program { }
