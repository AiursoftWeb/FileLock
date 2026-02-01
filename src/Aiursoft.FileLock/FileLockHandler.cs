using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.FileLock;

public class FileLockHandler : NavigationCommandHandlerBuilder
{
    protected override string Name => "file-lock";
    protected override string Description => "A Zero-Trust file encryption tool by Aiursoft.";

    protected override CommandHandlerBuilder[] GetSubCommandHandlers()
    {
        return
        [
            new EncryptHandler(),
            new DecryptHandler()
        ];
    }
}
