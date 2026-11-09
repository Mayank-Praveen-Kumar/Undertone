using Android.Content;
using Undertone.Mobile.Services;
using Undertone.Services;
using Uri = Android.Net.Uri;

namespace Undertone.Android.Services;

/// <summary>Opens a web page in the user's browser. Never throws into the UI.</summary>
public sealed class AndroidUrlLauncher : IUrlLauncher
{
    private readonly Context _context;

    public AndroidUrlLauncher(Context context) => _context = context;

    public bool Open(string url)
    {
        try
        {
            var intent = new Intent(Intent.ActionView, Uri.Parse(url));
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.Write("Android", $"Open link failed: {ex.Message}");
            return false;
        }
    }
}
