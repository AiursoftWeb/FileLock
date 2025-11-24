using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;

namespace Aiursoft.FileLock;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 构建并运行嵌套命令应用
        return await new NestedCommandApp()
            .WithGlobalOptions(CommonOptionsProvider.VerboseOption) // 支持 --verbose
            .WithGlobalOptions(CommonOptionsProvider.DryRunOption)  // 支持 --dry-run (预留)
            .WithFeature(new FileLockHandler()) // 注册主入口
            .RunAsync(args);
    }
}

// ================== 命令路由层 ==================

// 主命令：file-lock

// 子命令：encrypt

// 子命令：decrypt

// ================== 辅助工具层 ==================

// ================== 核心业务层 (ZeroTrustVault) ==================
// 包含了之前设计的：PBKDF2防爆破 + HKDF密钥派生 + AES-GCM加密 + 文件名混淆