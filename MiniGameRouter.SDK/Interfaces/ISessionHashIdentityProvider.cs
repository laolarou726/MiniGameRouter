using System.Collections.Frozen;

namespace MiniGameRouter.SDK.Interfaces;

public interface ISessionHashIdentityProvider
{
    string GeneralSessionHash { get; }

    FrozenDictionary<string, string> ServiceHashes { get; }
}