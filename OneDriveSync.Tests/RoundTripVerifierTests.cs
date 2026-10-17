public class RoundTripVerifierTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public RoundTripVerifierTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateFile(string content = "hello")
    {
        var path = Path.Combine(_dir, "test.txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task RunAsync_IdenticalDownload_IsMatch()
    {
        var verifier = new RoundTripVerifier(new FakeStorage());

        var result = await verifier.RunAsync("folder", CreateFile());

        Assert.True(result.IsMatch);
        Assert.Equal(result.OriginalHash, result.DownloadedHash);
    }

    [Fact]
    public async Task RunAsync_CorruptedDownload_IsNotMatch()
    {
        var storage = new FakeStorage(bytes => bytes.Append((byte)'x').ToArray());
        var verifier = new RoundTripVerifier(storage);

        var result = await verifier.RunAsync("folder", CreateFile());

        Assert.False(result.IsMatch);
        Assert.NotEqual(result.OriginalHash, result.DownloadedHash);
    }

    [Fact]
    public async Task RunAsync_DeletesLocalCopyBeforeDownload()
    {
        var storage = new FakeStorage();
        var verifier = new RoundTripVerifier(storage);

        await verifier.RunAsync("folder", CreateFile());

        Assert.False(storage.LocalFileExistedAtDownload);
    }

    [Fact]
    public async Task RunAsync_UploadsUnderTheLocalFileName()
    {
        var storage = new FakeStorage();
        var verifier = new RoundTripVerifier(storage);

        var result = await verifier.RunAsync("folder", CreateFile());

        Assert.Equal("test.txt", storage.UploadedName);
        Assert.Equal("test.txt", result.Remote.Name);
    }

    [Fact]
    public async Task RunAsync_RestoresFileAtOriginalPath()
    {
        var path = CreateFile("some content");

        await new RoundTripVerifier(new FakeStorage()).RunAsync("folder", path);

        Assert.Equal("some content", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task RunAsync_UploadFails_KeepsLocalFileAndPropagates()
    {
        var path = CreateFile("keep me");
        var verifier = new RoundTripVerifier(new ThrowingStorage());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            verifier.RunAsync("folder", path));

        Assert.True(File.Exists(path));
        Assert.Equal("keep me", await File.ReadAllTextAsync(path));
    }
}