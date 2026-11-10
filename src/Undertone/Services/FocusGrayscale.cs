using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Undertone.Services;

/// <summary>A GPU saturation blend covers the client surface, including animated artwork.</summary>
public static class FocusGrayscale
{
    private static readonly Dictionary<Window, Image> Windows = new();
    private static readonly HashSet<Window> OpenWindows = new();
    private static Bitmap? _neutral;
    public static bool Enabled { get; private set; }
    private static bool _attached;
    public static void Attach()
    {
        if (_attached) return;
        _attached = true;
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => { OpenWindows.Add(window); if (Enabled) AttachWindow(window); });
        Window.WindowClosedEvent.AddClassHandler<Window>((window, _) => { Windows.Remove(window); OpenWindows.Remove(window); });
    }
    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (enabled) foreach (var window in OpenWindows.ToArray()) AttachWindow(window);
        foreach (var overlay in Windows.Values) overlay.IsVisible = enabled;
    }
    public static void AttachWindow(Window window)
    {
        if (Windows.ContainsKey(window) || window.Content is not Control content) return;
        if (_neutral is null)
        {
            var bitmap = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var pixels = bitmap.Lock()) System.Runtime.InteropServices.Marshal.WriteInt32(pixels.Address, unchecked((int)0xFF808080));
            _neutral = bitmap;
        }
        var overlay = new Image { Source = _neutral, Stretch = Stretch.Fill, IsHitTestVisible = false, IsVisible = Enabled };
        RenderOptions.SetBitmapBlendingMode(overlay, BitmapBlendingMode.Saturation);
        var panel = new Panel();
        // Keep inherited bindings alive while the content is briefly reparented.
        var hadLocalContext = content.IsSet(StyledElement.DataContextProperty);
        if (!hadLocalContext) content.DataContext = content.DataContext;
        window.Content = null;
        panel.Children.Add(content); panel.Children.Add(overlay);
        window.Content = panel;
        if (!hadLocalContext) content.ClearValue(StyledElement.DataContextProperty);
        Windows[window] = overlay;
    }
}
