
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var clientId = configuration["Graph:ClientId"];

if (string.IsNullOrWhiteSpace(clientId))
{
    throw new InvalidOperationException(
        "Missing ClientID");
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

Console.WriteLine($"Drive ID: {drive?.Id}");
Console.WriteLine($"Owner: {drive?.Owner?.User?.DisplayName}");

public partial class Program { }
