using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
namespace Undertone.Compatibility;
public static class Cleanup
{
    private static object? Get(object? value, string name) => value?.GetType().GetProperty(name)?.GetValue(value);
    private static void Set(object? value, string name, object? item) { var p=value?.GetType().GetProperty(name); if(p?.CanWrite==true)p.SetValue(value,item); }
    public static void Settings(object value)
    {
        if(Get(value,"SectionGroups") is IEnumerable groups)
            foreach(var group in groups)
                if(Get(group,"Sections") is IList sections)
                    for(var i=sections.Count-1;i>=0;i--)
                        if(Get(sections[i],"Key") is string key && new[]{"About","Statistics","Account & Devices"}.Contains(key))sections.RemoveAt(i);
    }
    public static void View(object value)
    {
        if(value is not Control control)return;
        if(control.FindControl<StackPanel>("AudioTabPanel") is {} audio && control.FindControl<ComboBox>("OutputDeviceCombo") is {} combo)
        {
            Control row=combo;
            while(row.Parent is Control parent && row is not Border)row=parent;
            if(row.Parent is Panel panel)panel.Children.Remove(row);
            else if(row.Parent is Border border)border.Child=null;
            audio.Children.Clear();audio.Children.Add(row);
        }
        if(control.FindControl<Control>("AboutTabPanel") is {} about)
        {
            if(about.Parent is Panel panel)panel.Children.Remove(about);
            about.IsVisible=false;
        }
        foreach(var name in new[]{"StatisticsTabPanel","AccountSyncTabPanel"})
            if(control.FindControl<Control>(name) is {} page)
            {if(page.Parent is Panel parent)parent.Children.Remove(page);page.IsVisible=false;}
        if(control.FindControl<StackPanel>("IntegrationsTabPanel") is {} integrations)
        {
            var cards=integrations.GetLogicalDescendants().OfType<Border>()
                .Where(b=>b.Classes.Contains("setting-card") && b.GetLogicalDescendants().OfType<TextBlock>()
                    .Any(t=>(t.Text??"").Contains("Last.fm",StringComparison.OrdinalIgnoreCase)||(t.Text??"").Contains("ListenBrainz",StringComparison.OrdinalIgnoreCase))).ToArray();
            foreach(var card in cards)if(card.Parent is Panel parent)parent.Children.Remove(card);
            integrations.Children.Clear();foreach(var card in cards)integrations.Children.Add(card);
        }
        if(control.FindControl<StackPanel>("PluginsTabPanel") is {} plugins && control.FindControl<StackPanel>("BuiltInFeaturesSection") is {} builtIn)
        {
            if(builtIn.Parent is Panel parent)parent.Children.Remove(builtIn);
            plugins.Children.Clear();builtIn.IsVisible=true;plugins.Children.Add(builtIn);
        }
        control.AttachedToVisualTree+=(_,_)=>Dispatcher.UIThread.Post(()=>
        {
            var settings=Get(control.DataContext,"Settings")??control.DataContext;
            var command=Get(settings,"ToggleEqualizerCommand");
            foreach(var button in control.GetVisualDescendants().OfType<Button>().ToArray())
                if(command!=null && ReferenceEquals(button.Command,command))button.IsVisible=false;
        },DispatcherPriority.Background);
    }
    public static void Player(object value)
    {
        if(value is not INotifyPropertyChanged changed || Get(value,"UpNext") is not INotifyCollectionChanged queue)return;
        var scheduled=false;
        void Schedule()
        {
            if(scheduled)return;scheduled=true;
            Dispatcher.UIThread.Post(()=>
            {
                scheduled=false;
                try{Refill(value);}catch{ /* Existing playback continues if a legacy model differs. */ }
            },DispatcherPriority.Background);
        }
        queue.CollectionChanged+=(_,_)=>Schedule();
        changed.PropertyChanged+=(_,e)=>{if(e.PropertyName is "CurrentTrack" or "State")Schedule();};
        Schedule();
    }
    private static void Refill(object player)
    {
        var current=Get(player,"CurrentTrack");
        if(current==null || Get(player,"UpNext") is not IList queue || queue.Count!=0
            || Get(player,"StopAfterCurrentTrack") is true || Get(player,"IsRadioActive") is true
            || Get(player,"RepeatMode")?.ToString()=="One")return;
        var library=player.GetType().GetField("_library",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(player);
        if(Get(library,"Tracks") is not IEnumerable tracks)return;
        var currentId=Get(current,"Id");
        var blocked=player.GetType().GetMethod("IsBlockedExplicit",BindingFlags.Instance|BindingFlags.NonPublic);
        var pool=tracks.Cast<object>().Where(t=>!Equals(Get(t,"Id"),currentId)&&Get(t,"SkipWhenShuffling") is not true
            && blocked?.Invoke(player,new[]{t}) is not true).DistinctBy(t=>Get(t,"Id")).OrderBy(_=>Random.Shared.Next()).ToArray();
        if(pool.Length==0)return;
        var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(current.GetType()))!;
        foreach(var track in pool)list.Add(track);
        Set(player,"IsShuffleEnabled",true);
        var cycle=(IList)Activator.CreateInstance(list.GetType())!;cycle.Add(current);foreach(var track in pool)cycle.Add(track);
        foreach(var field in new[]{"_originalQueue","_repeatCycleTracks"})player.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(player,cycle);
        foreach(var track in pool)queue.Add(track);
    }
}
