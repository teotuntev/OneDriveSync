public class FileHasherTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public FileHasherTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ComputeSha256Async_ReturnsKnownHash()
    {
        var path = Path.Combine(_dir, "a.txt");
        await File.WriteAllTextAsync(path, "hello");

        var hash = await FileHasher.ComputeSha256Async(path);

        Assert.Equal(
            "2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824",
            hash);
    }

    [Fact]
    public async Task ComputeSha256Async_DifferentContent_GivesDifferentHash()
    {
        var a = Path.Combine(_dir, "a.txt");
        var b = Path.Combine(_dir, "b.txt");
        await File.WriteAllTextAsync(a, "hello");
        await File.WriteAllTextAsync(b, "hello!");

        Assert.NotEqual(
            await FileHasher.ComputeSha256Async(a),
            await FileHasher.ComputeSha256Async(b));
    }

    [Fact]
    public async Task ComputeSha256Async_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            FileHasher.ComputeSha256Async(Path.Combine(_dir, "missing.txt")));
    }
}