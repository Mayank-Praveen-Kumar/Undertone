using Undertone.Helpers;
using Xunit;

namespace Undertone.Tests;

/// <summary>
/// 10-27: on Windows the single-instance mutex/pipe was keyed by user name alone, so the dev
/// build (UNDERTONE_DATA_DIR = Undertone-Dev) could not start while the installed Undertone ran: it
/// handed itself to that window and quit. A custom data root is now its own instance, as on
/// Unix; the default root keeps the original names.
/// </summary>
public class SingleInstanceGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DefaultRoot_KeepsTheOriginalNames(string? dataDir) =>
        Assert.Equal("", SingleInstanceGuard.RootSuffix(dataDir));

    [Fact]
    public void CustomRoot_GetsItsOwnInstance_StableAcrossSpellings()
    {
        var dev = Path.Combine(Path.GetTempPath(), "Undertone-Dev");
        var suffix = SingleInstanceGuard.RootSuffix(dev);
        Assert.Matches("^-[0-9A-F]{8}$", suffix);
        Assert.Equal(suffix, SingleInstanceGuard.RootSuffix(dev.ToLowerInvariant() + "\\"));
        Assert.Equal(suffix, SingleInstanceGuard.RootSuffix(dev.Replace('\\', '/')));
        Assert.NotEqual(suffix, SingleInstanceGuard.RootSuffix(Path.Combine(Path.GetTempPath(), "Undertone-Other")));
    }

    /// <summary>10-28: a Debug exe started without UNDERTONE_DATA_DIR runs on Undertone-dev, yet was
    /// keyed like the installed app, so it handed itself to the running official Undertone and
    /// quit. The key now follows the root the app really uses.</summary>
    [Fact]
    public void DevRootWithoutTheVariable_IsItsOwnInstance_AndTheDefaultRootKeepsTheOriginalNames()
    {
        var appData = Path.Combine(Path.GetTempPath(), "AppData", "Roaming");
        var installed = Path.Combine(appData, "Undertone");
        var dev = Path.Combine(appData, "Undertone-dev");
        Assert.Equal("", SingleInstanceGuard.SuffixFor(installed, installed));
        Assert.Equal("", SingleInstanceGuard.SuffixFor(installed.ToUpperInvariant() + "\\", installed));
        Assert.Equal(SingleInstanceGuard.RootSuffix(dev), SingleInstanceGuard.SuffixFor(dev, installed));
        Assert.NotEqual("", SingleInstanceGuard.SuffixFor(dev, installed));
    }
}
