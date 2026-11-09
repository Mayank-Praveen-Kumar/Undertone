using Undertone.Services;
using Xunit;

namespace Undertone.Tests;

public class UpdateInstallSourceTests
{
    [Theory]
    // Running directory matches the Inno-recorded install location → Installed
    // (also how winget/Chocolatey install — they wrap the same Inno setup).
    [InlineData(@"C:\Program Files\Undertone", @"C:\Program Files\Undertone", InstallSource.Installed)]
    // Trailing-slash / case differences must still match.
    [InlineData(@"C:\Program Files\Undertone\", @"c:\program files\undertone", InstallSource.Installed)]
    [InlineData(@"C:\Users\me\AppData\Local\Programs\Undertone",
                @"C:\Users\me\AppData\Local\Programs\Undertone\", InstallSource.Installed)]
    // No Inno entry, running from a Scoop app dir → Scoop.
    [InlineData(@"C:\Users\me\scoop\apps\undertone\1.1.14", null, InstallSource.Scoop)]
    // An unrelated Inno install exists elsewhere, but we run from Scoop → Scoop.
    [InlineData(@"C:\Users\me\scoop\apps\undertone\1.1.14",
                @"C:\Program Files\Undertone", InstallSource.Scoop)]
    // No Inno entry, not under scoop → Portable (manually-extracted zip).
    [InlineData(@"C:\Users\me\Downloads\Undertone", null, InstallSource.Portable)]
    // Inno entry points somewhere else and we're not scoop → Portable.
    [InlineData(@"D:\Apps\Undertone-portable",
                @"C:\Program Files\Undertone", InstallSource.Portable)]
    public void ClassifyInstall_categorizesRunningCopy(
        string appDir, string? innoInstallLocation, InstallSource expected)
    {
        Assert.Equal(expected, UpdateService.ClassifyInstall(appDir, innoInstallLocation));
    }
}
