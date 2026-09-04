using System.Runtime.CompilerServices;

namespace Blaizio.Cli.Tests;

/// <summary>
/// Keeps the community-directory fallback off the network for the whole test run. Without this,
/// any test that names an unrecorded <c>@namespace</c> fetches the published directory from
/// blaiz.io - and on a runner where that host accepts the connection and never answers, the
/// courtesy lookup used to become a 30 s wait and exit 130 instead of the unknown-registry exit 2.
/// Tests that exercise the directory point <c>BLAIZIO_DIRECTORY</c> at their own file and restore
/// <see cref="Sentinel"/> afterwards.
/// </summary>
internal static class OfflineDirectory
{
    /// <summary>A path that does not exist: the lookup fails fast and reports "not listed".</summary>
    public static readonly string Sentinel = Path.Combine(Path.GetTempPath(), "blaizio-tests-no-directory.json");

    [ModuleInitializer]
    internal static void Apply() => Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", Sentinel);

    /// <summary>Back to the offline default after a test that overrode the location.</summary>
    public static void Reset() => Environment.SetEnvironmentVariable("BLAIZIO_DIRECTORY", Sentinel);
}
