using System.ComponentModel;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Undertone.ViewModels;

namespace Undertone.Views;

/// <summary>An anchored lyrics surface in the player's own window.</summary>
internal sealed class LyricsBubble
{
    private readonly MainWindow _owner;
    private readonly Control _anchor;
    private readonly Panel _root;
    private readonly Control _app;
    private readonly IEffect? _previousEffect;
    private readonly BlurEffect _blur = new() { Radius = 5 };
    private readonly Canvas _layer = new() { Background = Brushes.Transparent, ZIndex = 110 };
    private readonly Border _card;
    private readonly Polygon _tail;
    private readonly Button _anchorButton;
    private readonly LyricsViewModel _lyrics;
    private readonly TextBlock _currentLine = new() { FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
    private readonly TextBlock _nextLine = new() { FontSize = 16, Foreground = new SolidColorBrush(Color.Parse("#A6FFFFFF")), MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
    private readonly Action _closed;
    private bool _isClosed;

    public LyricsBubble(MainWindow owner, Control anchor, LyricsViewModel vm, Action clickAnchor, Action closed)
    {
        _owner = owner;
        _anchor = anchor;
        _root = (Panel)owner.Content!;
        _app = owner.FindControl<Panel>("AppContentLayer")!;
        _closed = closed;
        _lyrics = vm;
        var text = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_currentLine);
        text.Children.Add(_nextLine);
        var open = new Button
        {
            Content = text, Padding = new Thickness(28, 18),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(52),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            [AutomationProperties.NameProperty] = "Open full lyrics",
            [ToolTip.TipProperty] = "Open full lyrics"
        };
        open.Click += (_, _) =>
        {
            Close();
            if (owner.DataContext is MainWindowViewModel main && !main.IsLyricsViewActive)
                main.ToggleLyricsCommand.Execute(null);
        };
        var fill = new SolidColorBrush(Color.Parse("#191723"));
        _card = new Border
        {
            Background = fill, CornerRadius = new CornerRadius(52), ClipToBounds = true,
            BorderBrush = new SolidColorBrush(Color.Parse("#40FFFFFF")), BorderThickness = new Thickness(1),
            Child = open
        };
        _tail = new Polygon { Fill = fill, IsHitTestVisible = false };
        _anchorButton = new Button
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Width = anchor.Bounds.Width, Height = anchor.Bounds.Height,
            Content = new PathIcon { Data = Geometry.Parse("M 3,3 L 21,3 L 21,17 L 12,17 L 7,22 L 7,17 L 3,17 Z"), Width = 18, Height = 18 },
            [ToolTip.TipProperty] = "Click to close · Double-click for the lyrics page"
        };
        _anchorButton.Click += (_, _) => clickAnchor();
        _layer.Children.Add(_tail);
        _layer.Children.Add(_card);
        _layer.Children.Add(_anchorButton);
        _layer.PointerPressed += (_, e) => { if (ReferenceEquals(e.Source, _layer)) { Close(); e.Handled = true; } };
        _layer.Background = new SolidColorBrush(Color.Parse("#50000000"));
        _previousEffect = _app.Effect;
        _app.Effect = _blur;
        _root.Children.Add(_layer);
        _root.SizeChanged += OnSizeChanged;
        owner.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Position();
        _lyrics.PropertyChanged += OnLyricsChanged;
        _lyrics.SetLyricsSurfaceVisible(true, animateWords: false);
        UpdateLines();
    }

    private void OnLyricsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LyricsViewModel.ActiveLineIndex) or nameof(LyricsViewModel.ActiveLyricLines)
            or nameof(LyricsViewModel.IsSearching) or nameof(LyricsViewModel.ShowSearchButton) or null)
            UpdateLines();
    }

    private void UpdateLines()
    {
        var lines = _lyrics.ActiveLyricLines as IReadOnlyList<Undertone.Models.LyricLine>
            ?? _lyrics.ActiveLyricLines.ToArray();
        if (lines.Count == 0)
        {
            _currentLine.Text = _lyrics.IsSearching ? "Finding lyrics…" : "No lyrics available";
            _nextLine.Text = "Click to open full lyrics";
            return;
        }
        var index = Math.Clamp(_lyrics.ActiveLineIndex, 0, lines.Count - 1);
        _currentLine.Text = lines[index].IsIntroPlaceholder ? "♪" : lines[index].Text.Replace('\n', ' ').Replace('\r', ' ');
        _nextLine.Text = lines.Skip(index + 1).FirstOrDefault(line => !line.IsIntroPlaceholder && !string.IsNullOrWhiteSpace(line.Text))?.Text.Replace('\n', ' ').Replace('\r', ' ') ?? "";
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => Position();

    private void Position()
    {
        if (_anchor.TranslatePoint(default, _root) is not { } point) { Close(); return; }
        var width = Math.Min(440, Math.Max(220, _root.Bounds.Width - 32));
        var height = 104.0;
        var center = point.X + _anchor.Bounds.Width / 2;
        var left = Math.Clamp(center - width / 2, 16, Math.Max(16, _root.Bounds.Width - width - 16));
        var top = Math.Max(16, point.Y - height - 12);
        _card.Width = width; _card.Height = height;
        Canvas.SetLeft(_card, left); Canvas.SetTop(_card, top);
        _tail.Points = new Points { new(center - 11, top + height - 1), new(center + 11, top + height - 1), new(center, point.Y + 2) };
        Canvas.SetLeft(_anchorButton, point.X); Canvas.SetTop(_anchorButton, point.Y);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Close(); e.Handled = true;
    }

    public void Close()
    {
        if (_isClosed) return;
        _isClosed = true;
        _lyrics.PropertyChanged -= OnLyricsChanged;
        _lyrics.SetLyricsSurfaceVisible(false, animateWords: false);
        _root.SizeChanged -= OnSizeChanged;
        _owner.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _root.Children.Remove(_layer);
        if (ReferenceEquals(_app.Effect, _blur)) _app.Effect = _previousEffect;
        _closed();
    }
}
