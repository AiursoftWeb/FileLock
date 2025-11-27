using System.CommandLine;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.FileLock;

public class EncryptHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "encrypt";
    protected override string Description => "Encrypt a folder into a secure vault.";

    // 定义参数 -i 和 -o
    private static readonly Option<string> InputOption = new(
        name: "--input",
        aliases: "-i")
    {
        Description = "Source folder path to encrypt.",
        Required = true
    };
    private static readonly Option<string> OutputOption = new(
        name: "--output",
        aliases: "-o")
    {
        Description = "Destination folder for the encrypted vault.",
        Required = true
    };

    private static readonly Option<string> PasswordOption = new(
        name: "--password",
        aliases: "-p")
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

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("Setting up encryption.");
            password = PasswordHelper.ReadPassword("Please set a master password: ");
            var confirm = PasswordHelper.ReadPassword("Confirm password: ");
            if (password != confirm)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Passwords do not match!");
                Console.ResetColor();
                return;
            }
        }

        Console.WriteLine("\nStarting encryption...");
        var vault = new ZeroTrustVault();
        try
        {
            await vault.Encrypt(inputPath, outputPath, password);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[Success] Encrypted vault created at: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] {ex.Message}");
            throw;
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
