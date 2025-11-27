using Aiursoft.CommandFramework;
using Aiursoft.CommandFramework.Models;
using Aiursoft.FileLock;

return await new NestedCommandApp()
    .WithGlobalOptions(CommonOptionsProvider.VerboseOption) // 支持 --verbose
    .WithGlobalOptions(CommonOptionsProvider.DryRunOption)  // 支持 --dry-run (预留)
    .WithFeature(new EncryptHandler())
    .WithFeature(new DecryptHandler())
    .RunAsync(args);
