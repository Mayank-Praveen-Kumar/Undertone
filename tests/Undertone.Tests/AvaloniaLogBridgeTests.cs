using Undertone.Services;
using Xunit;

namespace Undertone.Tests;

/// <summary>
/// The Avalonia warning mirror keeps the session log useful for bug reports; two
/// structural binding-noise classes (transient view-model casts during sub-view
/// attach, null-DataContext chains in recycled containers) are excluded so they
/// stop filling the log — and eating the 80-warning cap — at every startup.
/// These pin exactly what is (and is not) considered benign.
/// </summary>
public class AvaloniaLogBridgeTests
{
    [Theory]
    // Hosted sub-view briefly inherits MainWindowViewModel before its own DataContext lands.
    [InlineData("An error occurred binding IsVisible to IsBackButtonVisible at IsBackButtonVisible: " +
                "Unable to cast object of type 'Undertone.ViewModels.MainWindowViewModel' to type 'Undertone.ViewModels.TopBarViewModel'.")]
    [InlineData("An error occurred binding Command to PlayPauseCommand at PlayPauseCommand: " +
                "Unable to cast object of type 'Undertone.ViewModels.MainWindowViewModel' to type 'Undertone.ViewModels.PlayerViewModel'.")]
    [InlineData("An error occurred binding Text to WrittenByText at WrittenByText: " +
                "Unable to cast object of type 'Undertone.ViewModels.MainWindowViewModel' to type 'Undertone.ViewModels.LyricsViewModel'.")]
    // Recycled/detached container evaluating a $parent chain with no DataContext.
    [InlineData("An error occurred binding Height to $parent[UserControl].DataContext.TileRowHeight at DataContext: Value is null.")]
    [InlineData("An error occurred binding HighlightText to $parent[UserControl].DataContext.SearchText at DataContext: Value is null.")]
    public void KnownTransients_AreClassifiedBenign(string message)
        => Assert.True(AvaloniaLogBridge.IsKnownBenignBindingTransient(message));

    [Theory]
    // A genuine model-type mismatch is a real authoring bug — must keep logging.
    [InlineData("An error occurred binding Title at Title: " +
                "Unable to cast object of type 'Undertone.Models.Track' to type 'Undertone.Models.Album'.")]
    // A chain failing at a real property (not the DataContext hop) is a real trail.
    [InlineData("An error occurred binding Text to CurrentTrack.Title at CurrentTrack: Value is null.")]
    // Non-binding warnings are untouched.
    [InlineData("Layout cycle detected on measure pass.")]
    [InlineData("PlatformImpl is null, couldn't handle input.")]
    public void RealWarnings_AreNotFiltered(string message)
        => Assert.False(AvaloniaLogBridge.IsKnownBenignBindingTransient(message));
}
