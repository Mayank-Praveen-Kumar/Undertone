using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Undertone.Views;
using Undertone.ViewModels;
using Xunit;
namespace Undertone.Tests;
public class BrandingCleanupTests
{
    [AvaloniaFact]
    public void AudioPageOnlyContainsOutputDeviceAndNoAboutPage()
    {
        var app=Application.Current!;
        if(!app.Resources.TryGetResource("HeartFillIcon",null,out _))
        {
            app.Resources["InterSemiBold"]=FontFamily.Default;
            app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Undertone/")){Source=new Uri("avares://Undertone.UI/Assets/Icons.axaml")});
            app.Styles.Add(new StyleInclude(new Uri("avares://Undertone/")){Source=new Uri("avares://Undertone.UI/Assets/Styles.axaml")});
        }
        var view=new SettingsView();
        Assert.NotNull(view.FindControl<ComboBox>("OutputDeviceCombo"));
        Assert.Null(view.FindControl<StackPanel>("AboutTabPanel"));
        Assert.Null(view.FindControl<StackPanel>("StatisticsTabPanel"));
        Assert.Null(view.FindControl<StackPanel>("AccountSyncTabPanel"));
        Assert.NotNull(view.FindControl<StackPanel>("BuiltInFeaturesSection"));
        Assert.Null(view.FindControl<TextBox>("PluginSearchBox"));
        Assert.NotNull(view.FindControl<TextBox>("ListenBrainzTokenBox"));
        Assert.Null(view.FindControl<Slider>("PreampSlider"));
        Assert.Null(view.FindControl<Slider>("AudioBalanceSlider"));
        Assert.Null(view.FindControl<Button>("EqSavePresetButton"));
    }
    [Fact]
    public void AboutIsAbsentFromSettingsNavigation()
    {
        var vm=new SettingsViewModel(new TestPersistenceService(),new FakeLibraryService(),new FakeHistoryLog());
        Assert.DoesNotContain(vm.Sections,s=>s.Key==SettingsViewModel.TabAbout);
        Assert.DoesNotContain(vm.Sections,s=>s.Key==SettingsViewModel.TabStatistics);
        Assert.DoesNotContain(vm.Sections,s=>s.Key==SettingsViewModel.TabAccountDevices);
        Assert.False(vm.EqualizerEnabled);
    }
}
