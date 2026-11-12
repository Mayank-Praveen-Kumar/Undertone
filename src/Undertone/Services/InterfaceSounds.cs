using Avalonia;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Undertone.Services;

/// <summary>One cached 30 ms wave; no player processes or idle timers.</summary>
internal static class InterfaceSounds
{
    public static bool Enabled { get; set; } = true;
    private static bool installed;
    private static byte[]? wave;
    private static GCHandle pinned;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        Button.ClickEvent.AddClassHandler<Button>(static (button, args) =>
        {
            if (ReferenceEquals(args.Source, button) && button.IsEffectivelyEnabled) PlayClick();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        MenuItem.ClickEvent.AddClassHandler<MenuItem>(static (item, args) =>
        {
            if (ReferenceEquals(args.Source, item) && item.IsEffectivelyEnabled) PlayClick();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    internal static void PlayClick()
    {
        if (!Enabled || !OperatingSystem.IsWindows()) return;
        try
        {
            if (wave is null)
            {
                using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Undertone.InterfaceClick.wav");
                if (source is null) return;
                using var bytes = new MemoryStream(); source.CopyTo(bytes);
                wave = bytes.ToArray(); pinned = GCHandle.Alloc(wave, GCHandleType.Pinned);
            }
            PlaySound(pinned.AddrOfPinnedObject(), IntPtr.Zero, 0x0001 | 0x0002 | 0x0004 | 0x0010);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or IOException) { }
    }

    [DllImport("winmm.dll", EntryPoint="PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);
}
