using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Undertone.Controls;
using Undertone.Views;
using Undertone.Models;
using Undertone.ViewModels;
using Xunit;

namespace Undertone.Tests;

public class LocalApiSettingsTests
{
    private sealed class MemoryPersistence : TestPersistenceService
    {
        public AppSettings Stored { get; set; } = new() { LocalApiPort = 0 };
        public override Task<AppSettings> LoadSettingsAsync() => Task.FromResult(Clone(Stored));
        public override Task SaveSettingsAsync(AppSettings settings) { Stored = Clone(settings); return Task.CompletedTask; }
        private static AppSettings Clone(AppSettings settings) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
    }

    [AvaloniaFact]
    public async Task EnabledSetting_RestartsTheApi_AndDisableIsPersisted()
    {
        using var persistence = new MemoryPersistence();
        persistence.Stored.LocalApiEnabled = true;
        var library = new FakeLibraryService();
        var vm = new SettingsViewModel(persistence, library, new FakeHistoryLog());
        var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        try
        {
            vm.SetPlayer(player);
            await vm.LoadAsync();
            Assert.True(vm.LocalApiEnabled);
            Assert.True(vm.LocalApiRunning);
            using var discovery = JsonDocument.Parse(File.ReadAllText(Path.Combine(persistence.DataDirectory, "local-api.json")));
            Assert.True(discovery.RootElement.GetProperty("running").GetBoolean());
            Assert.Equal(64, discovery.RootElement.GetProperty("token").GetString()!.Length);
            await vm.SaveAsync();
            Assert.True(persistence.Stored.LocalApiEnabled);
            vm.LocalApiEnabled = false;
            await vm.SaveAsync();
            Assert.False(vm.LocalApiRunning);
            Assert.False(persistence.Stored.LocalApiEnabled);
            using var stopped = JsonDocument.Parse(File.ReadAllText(Path.Combine(persistence.DataDirectory, "local-api.json")));
            Assert.False(stopped.RootElement.GetProperty("running").GetBoolean());
        }
        finally { vm.StopLocalApi(); }
    }

    [AvaloniaFact]
    public async Task PlayerAttachedAfterLoad_StartsSavedApi_AndDefaultsStayOff()
    {
        using var persistence = new MemoryPersistence();
        var library = new FakeLibraryService();
        var vm = new SettingsViewModel(persistence, library, new FakeHistoryLog());
        await vm.LoadAsync();
        Assert.False(vm.LocalApiEnabled);
        Assert.False(vm.LocalApiRunning);
        Assert.False(File.Exists(Path.Combine(persistence.DataDirectory, "local-api.json")));
        vm.LocalApiEnabled = true;
        await vm.SaveAsync();
        Assert.False(vm.LocalApiRunning);
        var player = new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService());
        try
        {
            vm.SetPlayer(player);
            Assert.True(vm.LocalApiRunning);
        }
        finally { vm.StopLocalApi(); }
    }

    [AvaloniaFact]
    public async Task IntegrationsCard_IsVisible_AndToggleControlsTheApi()
    {
        var app = Application.Current!;
        if (!app.Resources.TryGetResource("HeartFillIcon", null, out _))
        {
            app.Resources["InterSemiBold"] = FontFamily.Default;
            app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Undertone/")) { Source = new Uri("avares://Undertone.UI/Assets/Icons.axaml") });
            app.Styles.Add(new StyleInclude(new Uri("avares://Undertone/")) { Source = new Uri("avares://Undertone.UI/Assets/Styles.axaml") });
        }
        using var persistence = new MemoryPersistence();
        var library = new FakeLibraryService();
        var vm = new SettingsViewModel(persistence, library, new FakeHistoryLog());
        vm.SetPlayer(new PlayerViewModel(new FakeAudioPlayer(), library, persistence, new FakeAnimatedCoverService()));
        await vm.LoadAsync();
        vm.SelectedSettingsTab = SettingsViewModel.TabIntegrations;
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1080, Height = 780 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var card = view.FindControl<Border>("LocalApiCard")!;
            var toggle = view.FindControl<LottieToggle>("LocalApiToggle")!;
            Assert.True(card.IsEffectivelyVisible);
            Assert.False(toggle.IsChecked);
            toggle.SetCurrentValue(LottieToggle.IsCheckedProperty, true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(vm.LocalApiEnabled);
            Assert.True(vm.LocalApiRunning);
            Assert.False(vm.LocalApiTokenRevealed);
            if (HeadlessTestApp.RealRendering && Environment.GetEnvironmentVariable("UNDERTONE_API_SHOT") is { Length: > 0 } screenshot)
            {
                for (var i = 0; i < 100; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Thread.Sleep(5);
                }
                window.CaptureRenderedFrame()!.Save(screenshot);
            }
            toggle.SetCurrentValue(LottieToggle.IsCheckedProperty, false);
            Dispatcher.UIThread.RunJobs();
            Assert.False(vm.LocalApiRunning);
        }
        finally { vm.StopLocalApi(); window.Close(); }
    }
}
