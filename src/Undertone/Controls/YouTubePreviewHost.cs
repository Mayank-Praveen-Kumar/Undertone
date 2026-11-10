using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
#if WINDOWS
using Microsoft.Web.WebView2.Core;
#endif

namespace Undertone.Controls;

/// <summary>A single on-demand browser. Released completely when the preview closes.</summary>
public sealed class YouTubePreviewHost : NativeControlHost
{
    public string VideoId { get; set; } = "";
    public event Action<string>? StatusChanged;
    public event Action? Finished;
    public event Action? Failed;
    private bool _closed;
    public YouTubePreviewHost() => LayoutUpdated += (_, _) =>
    {
#if WINDOWS
        ResizeController();
#endif
    };
    public void Stop()
    {
        _closed = true;
#if WINDOWS
        _controller?.Close(); _controller = null;
#endif
    }
#if WINDOWS
    private CoreWebView2Controller? _controller;
    private IntPtr _handle;
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _closed = false;
        _handle = CreateWindowEx(0, "STATIC", "", 0x50000000, 0, 0, 640, 360, parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_handle == IntPtr.Zero) throw new InvalidOperationException("The preview window could not be created.");
        _ = InitializeAsync(_handle);
        return new PlatformHandle(_handle, "HWND");
    }
    private async Task InitializeAsync(IntPtr handle)
    {
        CoreWebView2Controller? pending = null;
        try
        {
            var root = Path.Combine(Helpers.AppPaths.DataRoot, "cache", "preview-player");
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "preview.html"), Html);
            // This browser exists only after the user explicitly requests a preview.
            var options = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required" };
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "browser"), options);
            if (_closed || handle != _handle) return;
            pending = await environment.CreateCoreWebView2ControllerAsync(handle);
            if (_closed || handle != _handle) { pending.Close(); return; }
            _controller = pending;
            var web = pending.CoreWebView2;
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            web.Settings.AreDefaultContextMenusEnabled = true;
            web.SetVirtualHostNameToFolderMapping("undertone.local", root, CoreWebView2HostResourceAccessKind.DenyCors);
            web.NewWindowRequested += (_, e) => e.Handled = true;
            web.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.NavigationStarting += (_, e) =>
            {
                if (!e.Uri.StartsWith("https://undertone.local/", StringComparison.Ordinal)) e.Cancel = true;
            };
            web.WebMessageReceived += (_, e) =>
            {
                if (!e.Source.StartsWith("https://undertone.local/", StringComparison.Ordinal)) return;
                var message = e.TryGetWebMessageAsString();
                if (message == "ended") Finished?.Invoke();
                else if (message == "playing") StatusChanged?.Invoke("Playing a 30-second excerpt · YouTube");
                else if (message == "error") { StatusChanged?.Invoke("This video cannot play here. Try another YouTube link."); Failed?.Invoke(); }
            };
            web.ProcessFailed += (_, _) => { StatusChanged?.Invoke("The preview player stopped. Close and retry the preview."); Finished?.Invoke(); };
            ResizeController();
            web.Navigate("https://undertone.local/preview.html?id=" + Uri.EscapeDataString(VideoId));
            StatusChanged?.Invoke("Press Play in the YouTube player if playback does not start.");
        }
        catch (Exception)
        {
            pending?.Close(); _controller = null;
            if (!_closed) { StatusChanged?.Invoke("YouTube previews need Microsoft Edge WebView2 Runtime. Install it from Microsoft's website, then retry."); Failed?.Invoke(); }
        }
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty) ResizeController();
    }
    private void ResizeController()
    {
        if (_controller is null || _handle == IntPtr.Zero) return;
        if (GetClientRect(_handle, out var rect)) _controller.Bounds = new System.Drawing.Rectangle(0, 0, rect.Right, rect.Bottom);
        _controller.IsVisible = true;
    }
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _closed = true;
        _controller?.Close(); _controller = null;
        DestroyWindow(control.Handle); _handle = IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(int exStyle, string className, string name, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
#else
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke("Embedded previews are available in Undertone for Windows."));
        return base.CreateNativeControlCore(parent);
    }
#endif
    private const string Html = """
        <!doctype html><html><head><meta name="viewport" content="width=device-width, initial-scale=1">
        <style>html,body,#player{margin:0;width:100%;height:100%;background:#0d0a17;overflow:hidden}</style></head><body>
        <div id="player"></div><script>
        const id=new URLSearchParams(location.search).get('id'); let player;
        function message(s){window.chrome.webview.postMessage(s);}
        function onYouTubeIframeAPIReady(){
          if(!/^[A-Za-z0-9_-]{11}$/.test(id)){message('error');return;}
          player=new YT.Player('player',{videoId:id,playerVars:{autoplay:1,controls:1,playsinline:1,start:0,end:30,origin:location.origin},events:{
            onReady:e=>e.target.playVideo(),
            onStateChange:e=>{if(e.data===1)message('playing');if(e.data===0)message('ended');},
            onError:e=>message('error')
          }});
        }
        </script><script src="https://www.youtube.com/iframe_api"></script></body></html>
        """;
}
