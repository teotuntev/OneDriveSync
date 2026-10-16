
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Extensions.Configuration;

const string folderName = "OneDriveSyncTest";
const string fileName = "test.txt";

var filePath = Path.Combine(
    AppContext.BaseDirectory,
    "samples",
    "test.txt");

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

if (!File.Exists(filePath))
{
    throw new FileNotFoundException(
        $"Upload file not found: {filePath}", filePath);
}

var fileInfo = new FileInfo(filePath);

const long maxSimpleUploadBytes = 4 * 1024 * 1024;

if (fileInfo.Length > maxSimpleUploadBytes)
{
    throw new InvalidOperationException(
        $"File is {fileInfo.Length:N0} bytes. Simple upload supports up to 4 MB.");
}

DriveItem? uploaded;

await using (var stream = File.OpenRead(filePath))
{
    uploaded = await client.Drives[driveId]
        .Items[folder.Id]
        .ItemWithPath(fileName)
        .Content
        .PutAsync(stream);
}

if (string.IsNullOrWhiteSpace(uploaded?.Id))
{
    throw new InvalidOperationException("Upload did not return an item.");
}

Console.WriteLine(
    $"Uploaded {fileName}, local {fileInfo.Length:N0} bytes, " +
    $"remote {uploaded.Size:N0} bytes, item ID {uploaded.Id}");

public partial class Program { }
