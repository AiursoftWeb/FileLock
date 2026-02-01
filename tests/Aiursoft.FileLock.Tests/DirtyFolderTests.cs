namespace Aiursoft.FileLock.Tests;

[TestClass]
public class DirtyFolderTests
{
    private string _workspace = null!;
    private string _inputPath = null!;
    private string _outputPath = null!;
    private const string Password = "password";

    [TestInitialize]
    public void Init()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "Aiursoft.FileLock.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_workspace);
        _inputPath = Path.Combine(_workspace, "input");
        _outputPath = Path.Combine(_workspace, "output");
        Directory.CreateDirectory(_inputPath);
        Directory.CreateDirectory(_outputPath);
        File.WriteAllText(Path.Combine(_inputPath, "file.txt"), "content");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_workspace))
            Directory.Delete(_workspace, true);
    }

    [TestMethod]
    public async Task EncryptToDirtyFolderShouldThrow()
    {
        // Arrange
        // Make output dirty
        File.WriteAllText(Path.Combine(_outputPath, "dirty.txt"), "dirty");
        var vault = new ZeroTrustVault();

        // Act & Assert
        try
        {
            await vault.Encrypt(_inputPath, _outputPath, Password);
            Assert.Fail("Should have thrown InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task DecryptToDirtyFolderShouldThrow()
    {
        // Arrange
        // First encrypt properly
        var cleanVaultPath = Path.Combine(_workspace, "clean-vault");
        var vault = new ZeroTrustVault();
        await vault.Encrypt(_inputPath, cleanVaultPath, Password);

        // Make output dirty
        File.WriteAllText(Path.Combine(_outputPath, "dirty.txt"), "dirty");

        // Act & Assert
        try
        {
            await vault.Decrypt(cleanVaultPath, _outputPath, Password);
            Assert.Fail("Should have thrown InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }
}
