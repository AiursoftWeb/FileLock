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
        Description = "Source folder path to encrypt.",
        Required = true
    };
    private static readonly Option<string> OutputOption = new(
        name: "--output",
        aliases: ["-o"])
    {
        Description = "Destination folder for the encrypted vault.",
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
            Console.WriteLine("\n[Access Denied] Invalid password!");
            throw;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Error] {ex.Message}");
            throw;
        }
        finally
        {
            Console.ResetColor();
        }
    }
}