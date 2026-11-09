using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Undertone.Android;

/// <summary>Android application object: owns the Avalonia app across activity restarts.</summary>
[Application]
public class UndertoneApplication : AvaloniaAndroidApplication<AndroidApp>
{
    protected UndertoneApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder).WithInterFont();
}
