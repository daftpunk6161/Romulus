using System.Runtime.CompilerServices;

namespace RomCleanup.Infrastructure.Orchestration;

/// <summary>
/// Module initializer that registers the rules.json-based pattern factory with
/// <see cref="RomCleanup.Core.GameKeys.GameKeyNormalizer"/> so that the convenience
/// Normalize(string) overload resolves patterns lazily from rules.json.
/// </summary>
#pragma warning disable CA2255 // Intentional library-level module initialization for Core pattern registration.
internal static class GameKeyNormalizationModuleInit
{
    [ModuleInitializer]
    internal static void Init() => GameKeyNormalizationProfile.EnsureRegistered();
}
#pragma warning restore CA2255
