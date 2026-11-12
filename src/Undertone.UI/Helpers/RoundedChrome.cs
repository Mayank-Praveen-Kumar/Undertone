using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace Undertone.Helpers;

/// <summary>Keeps shared chrome rounded even when an older view or template sets its own radius.</summary>
public static class RoundedChrome
{
    public static readonly AttachedProperty<CornerRadius?> RadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, CornerRadius?>("Radius", typeof(RoundedChrome));
    public static void SetRadius(Control control, CornerRadius? value) => control.SetValue(RadiusProperty, value);
    public static CornerRadius? GetRadius(Control control) => control.GetValue(RadiusProperty);

    static RoundedChrome()
    {
        RadiusProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.PropertyChanged -= KeepRounded;
            if (change.GetNewValue<CornerRadius?>() is not null)
            {
                control.PropertyChanged += KeepRounded;
                Apply(control);
            }
        });
    }

    private static StyledProperty<CornerRadius>? Target(Control control) => control switch
    {
        Border => Border.CornerRadiusProperty,
        ContentPresenter => ContentPresenter.CornerRadiusProperty,
        TemplatedControl => TemplatedControl.CornerRadiusProperty,
        _ => null,
    };

    private static void KeepRounded(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (sender is Control control && change.Property == Target(control)) Apply(control);
    }

    private static void Apply(Control control)
    {
        if (GetRadius(control) is not { } radius || Target(control) is not { } property) return;
        // Multi-line editors retain a rounded rectangular writing area.
        if (control is TextBox { AcceptsReturn: true } || control.TemplatedParent is TextBox { AcceptsReturn: true }) radius = new CornerRadius(18);
        if (control.GetValue(property) != radius) control.SetCurrentValue(property, radius);
    }
}
