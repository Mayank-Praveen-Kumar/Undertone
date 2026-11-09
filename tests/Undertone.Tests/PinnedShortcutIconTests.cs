using System.IO;
using Undertone.Services;
using Xunit;

namespace Undertone.Tests;

/// <summary>
/// App Icon on a pinned taskbar button (11-03): only the pinned shortcut of THIS exe is
/// repointed; classic resets it to the exe's own icon.
/// </summary>
public class PinnedShortcutIconTests
{
    [Theory]
    [InlineData(@"G:\Undertone\Undertone.exe", @"G:\Undertone\Undertone.exe", true)]
    [InlineData(@"g:\undertone\UNDERTONE.EXE", @"G:\Undertone\Undertone.exe", true)]
    [InlineData(@"G:\Undertone\.\Undertone.exe", @"G:\Undertone\Undertone.exe", true)]
    [InlineData(@"C:\dev\bin\Debug\Undertone.exe", @"G:\Undertone\Undertone.exe", false)]
    [InlineData("", @"G:\Undertone\Undertone.exe", false)]
    [InlineData(null, @"G:\Undertone\Undertone.exe", false)]
    public void Only_this_exe_matches(string? target, string exe, bool expected) =>
        Assert.Equal(expected, PinnedShortcutIcon.IsSameExe(target, exe));

    [Fact]
    public void Classic_resets_to_exe_icon_others_point_at_copied_ico()
    {
        Assert.Equal("", PinnedShortcutIcon.DesiredIconPath(AppIconService.DefaultId));
        var dark = PinnedShortcutIcon.DesiredIconPath("dark");
        Assert.Equal("dark.ico", Path.GetFileName(dark));
        Assert.Equal("app-icons", Path.GetFileName(Path.GetDirectoryName(dark)));
    }
}
