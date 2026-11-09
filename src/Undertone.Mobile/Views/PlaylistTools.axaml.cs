using Undertone.Controls;

namespace Undertone.Mobile.Views;

public partial class PlaylistTools : GlassPanel
{
    /// <summary>Styled as the GlassPanel it is (MobileStyles' header-glass).</summary>
    protected override Type StyleKeyOverride => typeof(GlassPanel);

    public PlaylistTools()
    {
        InitializeComponent();
    }
}
