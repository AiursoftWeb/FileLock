using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CSTools.Tools;

namespace Aiursoft.FileLock.Tests;

[TestClass]
public class CorruptedFileTests
{
    private readonly NestedCommandApp _program = new NestedCommandApp()
        .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
        .WithFeature(new FileLockHandler());

    [TestMethod]
    public async Task TestCorruptedFile()
    {
        var testRunId = Guid.NewGuid().ToString();
        var tempSource = Path.Combine(Path.GetTempPath(), $"FileLock-Src-{testRunId}");
        var tempVault = Path.Combine(Path.GetTempPath(), $"FileLock-Vault-{testRunId}");
        var tempRestored = Path.Combine(Path.GetTempPath(), $"FileLock-Restored-{testRunId}");
        var password = "password";

        try
        {
            // 1. Setup valid vault with 2 files
            Directory.CreateDirectory(tempSource);
            File.WriteAllText(Path.Combine(tempSource, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(tempSource, "file2.txt"), "content2");

            await _program.TestRunAsync([
                "file-lock", "encrypt",
                "-i", tempSource,
                "-o", tempVault,
                "-p", password
            ]);

            // 2. Corrupt one file
            var encFiles = Directory.GetFiles(tempVault, "*.enc");
            // Pick the first one and corrupt it
            var fileToCorrupt = encFiles[0];

            // Corrupt by truncating to invalid size (e.g. 10 bytes, which is less than Header size)
            using (var fs = new FileStream(fileToCorrupt, FileMode.Open, FileAccess.Write))
            {
                fs.SetLength(10);
            }

            // 3. Decrypt
            var result = await _program.TestRunAsync([
                "file-lock", "decrypt",
                "-i", tempVault,
                "-o", tempRestored,
                "-p", password
            ]);

            // Check that we got a warning in StdOut (since we changed it to write to Out)
            Assert.Contains("Header Error", result.StdErr, "Should report failure for corrupted file in StdOut");

            // Check that the OTHER file was not restored successfully
            var restoredFiles = Directory.Exists(tempRestored) ? Directory.GetFiles(tempRestored) : [];
            Assert.HasCount(0, restoredFiles, "Should not have restored the non-corrupted file");
        }
        finally
        {
            FolderDeleter.DeleteByForce(tempSource);
            FolderDeleter.DeleteByForce(tempVault);
            FolderDeleter.DeleteByForce(tempRestored);
        }
    }

    [TestMethod]
    public async Task TestMissingSource()
    {
        var testRunId = Guid.NewGuid().ToString();
        var missingPath = Path.Combine(Path.GetTempPath(), $"FileLock-Missing-{testRunId}");
        var tempOut = Path.Combine(Path.GetTempPath(), $"FileLock-Out-{testRunId}");

        // This should NOT throw, but return non-zero exit code
        var result = await _program.TestRunAsync([
            "file-lock", "encrypt",
            "-i", missingPath,
            "-o", tempOut,
            "-p", "pass"
        ]);

        Assert.AreNotEqual(0, result.ProgramReturn, "Should return non-zero for missing source");
        Assert.IsTrue(result.StdOut.Contains("Source not found") || result.StdErr.Contains("Source not found"), "Should report error");
    }
}
