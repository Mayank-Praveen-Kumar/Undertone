using Undertone.Models;
using Xunit;
namespace Undertone.Tests;
public class TrackSnoozeTests
{
    [Fact]
    public void IsSnoozed_TrueForFutureUntil()
    { Assert.True(new Track { SnoozedUntil = DateTime.UtcNow.AddDays(1) }.IsSnoozed); }
    [Fact]
    public void IsSnoozed_FalseForPastOrNull()
    {
        Assert.False(new Track { SnoozedUntil = DateTime.UtcNow.AddDays(-1) }.IsSnoozed);
        Assert.False(new Track { SnoozedUntil = null }.IsSnoozed);
    }
}
