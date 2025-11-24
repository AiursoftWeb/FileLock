using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.CSTools.Tools;

[assembly: DoNotParallelize]

namespace Aiursoft.FileLock.Tests;

[TestClass]
public class FileLockIntegrationTests
{
    // 因为是一个嵌套命令APP (NestedCommandApp)，我们需要模拟 Main 函数中的构建方式
    private readonly NestedCommandApp _program = new NestedCommandApp()
        .WithGlobalOptions(CommonOptionsProvider.VerboseOption)
        .WithFeature(new FileLockHandler());

    // 定位 assets 目录
    private readonly string _testVideo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "test_video.mp4");
    private readonly string _testPhotoFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "big-photos");

    [TestMethod]
    public async Task InvokeHelp()
    {
        // 测试 root help
        var result = await _program.TestRunAsync(["--help"]);
        Assert.AreEqual(0, result.ProgramReturn);

        // 测试 subcommand help
        var resultEnc = await _program.TestRunAsync(["file-lock", "encrypt", "--help"]);
        Assert.AreEqual(0, resultEnc.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeUnknown()
    {
        var result = await _program.TestRunAsync(["--wtf"]);
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task InvokeWithoutArg()
    {
        // 嵌套命令如果不带子命令，默认应该打印帮助并返回非0 (取决于框架实现，通常是 1)
        var result = await _program.TestRunAsync([]);
        Assert.AreEqual(1, result.ProgramReturn);
    }

    [TestMethod]
    public async Task TestFullEncryptionCycle()
    {
        // 1. Prepare Environment
        var testRunId = Guid.NewGuid().ToString();
        var tempSource = Path.Combine(Path.GetTempPath(), $"FileLock-Src-{testRunId}");
        var tempVault = Path.Combine(Path.GetTempPath(), $"FileLock-Vault-{testRunId}");
        var tempRestored = Path.Combine(Path.GetTempPath(), $"FileLock-Restored-{testRunId}");
        var password = "SuperSecretPassword123!";

        try
        {
            // 创建模拟源数据：包含一个视频和一个照片文件夹
            Directory.CreateDirectory(tempSource);
            File.Copy(_testVideo, Path.Combine(tempSource, "video.mp4"));
            
            // 模拟你的 assets 结构复制过去
            var photoDest = Path.Combine(tempSource, "photos");
            Directory.CreateDirectory(photoDest);
            foreach (var file in Directory.GetFiles(_testPhotoFolder))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(photoDest, fileName));
            }

            // 2. Run Encrypt Command
            // 注意命令结构: [RootCommand] [SubCommand] [Options...]
            // 但在 TestRunAsync 中，如果我们是用 NestedCommandApp 启动的，
            // 并且 FileLockHandler 是 Feature，那么命令参数应该是 ["file-lock", "encrypt", ...]
            var encryptResult = await _program.TestRunAsync([
                "file-lock", 
                "encrypt",
                "-i", tempSource,
                "-o", tempVault,
                "-p", password // 使用非交互参数
            ]);

            if (encryptResult.ProgramReturn != 0)
            {
                Console.WriteLine("Encrypt Stderr: " + encryptResult.StdErr);
                Console.WriteLine("Encrypt Stdout: " + encryptResult.StdOut);
            }
            Assert.AreEqual(0, encryptResult.ProgramReturn, "Encryption failed.");

            // 3. Assert Encryption Result
            Assert.IsTrue(Directory.Exists(tempVault));
            Assert.IsTrue(File.Exists(Path.Combine(tempVault, "vault.config")), "Vault config missing.");
            // 确保没有原始文件名泄露 (源文件叫 video.mp4, photos/...)
            Assert.IsFalse(File.Exists(Path.Combine(tempVault, "video.mp4")));
            Assert.IsFalse(Directory.Exists(Path.Combine(tempVault, "photos")));
            // 确保生成了 .enc 文件
            var encFiles = Directory.GetFiles(tempVault, "*.enc", SearchOption.AllDirectories);
            Assert.IsGreaterThanOrEqualTo(2, encFiles.Length, "Should have at least 2 encrypted files.");

            // 4. Run Decrypt Command
            var decryptResult = await _program.TestRunAsync([
                "file-lock",
                "decrypt",
                "-i", tempVault,
                "-o", tempRestored,
                "-p", password
            ]);

            if (decryptResult.ProgramReturn != 0)
            {
                Console.WriteLine("Decrypt Stderr: " + decryptResult.StdErr);
                Console.WriteLine("Decrypt Stdout: " + decryptResult.StdOut);
            }
            Assert.AreEqual(0, decryptResult.ProgramReturn, "Decryption failed.");

            // 5. Assert Decryption Result (Data Integrity)
            Assert.IsTrue(File.Exists(Path.Combine(tempRestored, "video.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(tempRestored, "photos", "p1.jpg")));

            // 验证文件大小/内容 (简单验证)
            var originalSize = new FileInfo(Path.Combine(tempSource, "video.mp4")).Length;
            var restoredSize = new FileInfo(Path.Combine(tempRestored, "video.mp4")).Length;
            Assert.AreEqual(originalSize, restoredSize, "Restored file size mismatch.");
        }
        finally
        {
            // 6. Cleanup
            FolderDeleter.DeleteByForce(tempSource);
            FolderDeleter.DeleteByForce(tempVault);
            FolderDeleter.DeleteByForce(tempRestored);
        }
    }

    [TestMethod]
    public async Task TestWrongPassword()
    {
        // Prepare
        var testRunId = Guid.NewGuid().ToString();
        var tempSource = Path.Combine(Path.GetTempPath(), $"FileLock-Src-{testRunId}");
        var tempVault = Path.Combine(Path.GetTempPath(), $"FileLock-Vault-{testRunId}");
        var tempRestored = Path.Combine(Path.GetTempPath(), $"FileLock-Fail-{testRunId}");
        
        try 
        {
            Directory.CreateDirectory(tempSource);
            File.WriteAllText(Path.Combine(tempSource, "secret.txt"), "content");

            // Encrypt with Password A
            await _program.TestRunAsync([
                "file-lock", "encrypt",
                "-i", tempSource,
                "-o", tempVault,
                "-p", "CorrectPassword"
            ]);

            // Decrypt with Password B
            var result = await _program.TestRunAsync([
                "file-lock", "decrypt",
                "-i", tempVault,
                "-o", tempRestored,
                "-p", "WrongPassword"
            ]);

            // Assert Failure
            // 我们的程序在捕获 UnauthorizedAccessException 后会打印错误但不一定会抛出异常导致 crash，
            // 但通常设计良好的 CLI 在错误时应该返回非 0。
            // 检查之前的代码，我们 catch 了异常并打印 Console.Error，但没有显式返回 exit code 1。
            // 如果 CommandFramework 默认处理 void/Task 返回值为 0，这里可能需要 Assert Output 包含 "Invalid password"
            
            Console.WriteLine(result.StdOut);
            
            // 更加严格的测试：检查输出是否包含错误提示
            Assert.IsTrue(
                result.StdErr.Contains("Invalid password") || 
                result.StdErr.Contains("Access Denied") || 
                result.StdErr.Contains("Unauthorized"));
            
            // 确保没有文件被解密
            Assert.IsFalse(Directory.Exists(tempRestored) && Directory.GetFiles(tempRestored).Length > 0);
        }
        finally
        {
            FolderDeleter.DeleteByForce(tempSource);
            FolderDeleter.DeleteByForce(tempVault);
            FolderDeleter.DeleteByForce(tempRestored);
        }
    }
}