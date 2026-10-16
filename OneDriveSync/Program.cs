using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Extensions.Configuration;

const string folderName = "OneDriveSyncTest";
const string fileName = "test.txt";
const long maxSimpleUploadBytes = 4L * 1024 * 1024;
const string scope = "Files.ReadWrite";

try
{
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

    var client = new GraphServiceClient(credential, new[] { scope });

    using var authTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

    var drive = await client.Me.Drive.GetAsync(
        requestConfiguration: null,
        cancellationToken: authTimeout.Token);

    if (string.IsNullOrWhiteSpace(drive?.Id))
    {
        throw new InvalidOperationException(
            "Could not retrieve the OneDrive drive ID.");
    }

    var driveId = drive.Id;

    Console.WriteLine($"Drive ID: {driveId}");
    Console.WriteLine(
        $"Owner: {drive.Owner?.User?.DisplayName ?? "Unknown"}");

    ICloudStorage storage = new OneDriveStorage(client, driveId);

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

    var originalHash = await FileHasher.ComputeSha256Async(filePath);

    Console.WriteLine($"Original SHA-256: {originalHash}");

    var uploaded = await storage.UploadAsync(folderName, fileName, filePath);

    Console.WriteLine(
        $"Uploaded {fileName}, local {fileInfo.Length:N0} bytes, " +
        $"remote {uploaded.Size?.ToString("N0") ?? "Unknown"} bytes, " +
        $"item ID {uploaded.Id}");

    File.Delete(filePath);

    Console.WriteLine($"Deleted local file: {filePath}");

    await storage.DownloadAsync(uploaded.Id, filePath);

    var downloadedHash = await FileHasher.ComputeSha256Async(filePath);

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
        Console.Error.WriteLine("MISMATCH: File contents differ.");
        Environment.ExitCode = 1;
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine(
        "Sign-in timed out or was cancelled. " +
        "Run the app again and complete the sign-in in the browser.");
    Environment.ExitCode = 1;
}
catch (AuthenticationFailedException ex)
{
    Console.Error.WriteLine(
        $"Authentication failed or was cancelled: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (ApiException ex)
{
    Console.Error.WriteLine(
        $"Microsoft Graph error (HTTP {ex.ResponseStatusCode}): {ex.Message}");
    Environment.ExitCode = 1;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine(
        $"Network error. Check your internet connection: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"File error: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Operation failed: {ex.Message}");
    Environment.ExitCode = 1;
}

public partial class Program { }