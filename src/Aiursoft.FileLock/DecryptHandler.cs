using System.CommandLine;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.FileLock;

public class DecryptHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "decrypt";
    protected override string Description => "Decrypt a secure vault to a folder.";

    private static readonly Option<string> InputOption = new(
        name: "--input",
        aliases: ["-i"])
    {
        Description = "Encrypted vault path.", // 修正了描述
        Required = true
    };
    private static readonly Option<string> OutputOption = new(
        name: "--output",
        aliases: ["-o"])
    {
        Description = "Destination folder for restored files.", // 修正了描述
        Required = true
    };

    private static readonly Option<string> PasswordOption = new(
        name: "--password",
        aliases: ["-p"])
    {
        Description = "The master password (optional). If not set, will ask interactively."
    };

    protected override Option[] GetCommandOptions() => [
        InputOption,
        OutputOption,
        PasswordOption
    ];

    protected override async Task Execute(ParseResult parseResult)
    {
        var inputPath = parseResult.GetValue(InputOption)!;
        var outputPath = parseResult.GetValue(OutputOption)!;
        var password = parseResult.GetValue(PasswordOption);

        // 修正逻辑：解密只需要输入一次密码
        if (string.IsNullOrWhiteSpace(password))
        {
            // 提示语也改得更准确了
            password = PasswordHelper.ReadPassword($"Enter password to unlock vault at '{inputPath}': ");
        }

        Console.WriteLine("\nUnlocking vault...");

        var vault = new ZeroTrustVault();
        try
        {
            await vault.Decrypt(inputPath, outputPath, password);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[Success] Files restored to: {outputPath}");
        }
        catch (UnauthorizedAccessException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            await Console.Error.WriteLineAsync("\n[Access Denied] Invalid password!");
            throw new InvalidOperationException("Invalid password.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            await Console.Error.WriteLineAsync($"\n[Error] {ex.Message}");
            throw;
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
