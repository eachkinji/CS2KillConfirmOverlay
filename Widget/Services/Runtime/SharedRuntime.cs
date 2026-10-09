using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.Web.Http;
namespace KillConfirmGameBar.Services
{
    internal static class SharedRuntime
    {
        private static bool Applying,Busy;
        private static string Catalog,Display,Settings;
        public static long SettingsRevision { get; private set; }
        private static readonly Dictionary<string,JsonValue> Pending=new Dictionary<string,JsonValue>();
        private static HashSet<string> PrimaryKeys=new HashSet<string>();
        private static DispatcherTimer Timer;
        public static async Task InitializeAsync()
        {
            await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(ServiceLauncher.ResolveGroupId(LocalServiceEndpoints.Port,DeveloperModeSettingsStore.IsEnabled));
            using (var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                try
                {
                    var startupClock = Stopwatch.StartNew();
                    var nextLaunch = TimeSpan.FromSeconds(2);
                    while (true)
                    {
                        try
                        {
                            LocalServiceAuth.InvalidateCachedToken();
                            string portPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "widget_port.txt");
                            if (File.Exists(portPath) && int.TryParse(File.ReadAllText(portPath).Trim(), out int port)) PortSettingsStore.SavePort(port);
                            startupTimeout.Token.ThrowIfCancellationRequested();
                            await PollAsync(startupTimeout.Token);
                            if (!await ServiceLauncher.RegisterCurrentProcessAsync(startupTimeout.Token))
                                throw new IOException("The companion exited before widget registration completed.");
                            break;
                        }
                        catch
                        {
                            startupTimeout.Token.ThrowIfCancellationRequested();
                            // A previous companion can still own the port while shutting down.
                            // Relaunch after failed connections so its successful no-op launch
                            // cannot leave this widget waiting for a process that has exited.
                            if (startupClock.Elapsed >= nextLaunch)
                            {
                                App.LogCrash("Retrying companion launch after startup connection failure.");
                                await KillConfirmWidgetPage.TryLaunchFullTrustHelperAsync(ServiceLauncher.ResolveGroupId(LocalServiceEndpoints.Port,DeveloperModeSettingsStore.IsEnabled));
                                nextLaunch = startupClock.Elapsed + TimeSpan.FromSeconds(2);
                            }
                            await Task.Delay(250, startupTimeout.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("后台连接超时，请关闭小组件后重试。 / Companion connection timed out. Close the widget and try again.");
                }
            }
            App.LogCrash("Widget shared state connected.");
            await SharedResources.InitializeAsync();
            ApplicationData.Current.LocalSettings.Values.MapChanged += OnSettingChanged;
            Timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
            Timer.Tick += async (s,e) => { try { await PollAsync(); } catch(Exception error) { App.Log("Shared runtime: "+error.Message); } };
            Timer.Start();
        }
        private static void OnSettingChanged(IObservableMap<string,object> map,IMapChangedEventArgs<string> args)
        {
            // Only the ordinary panel selects the display mode. A delayed
            // widget settings event must never overwrite its mode selection.
            if(Applying || args.Key == "CompatibilityDisplay.Enabled") return;
            if(!map.TryGetValue(args.Key,out object value)) { Pending[args.Key]=null; return; }
            string kind=value is bool ? "bool" : value is int ? "int" : value is long ? "long" : value is float ? "float" : value is double ? "double" : value is string ? "string" : null;
            if(kind==null) return;
            var scalar=new JsonObject { ["Kind"]=JsonValue.CreateStringValue(kind),["Text"]=JsonValue.CreateStringValue(Convert.ToString(value,CultureInfo.InvariantCulture)) };
            Pending[args.Key]=JsonValue.Parse(scalar.Stringify());
        }
        private static object Scalar(JsonObject value)
        {
            string text=value["Text"].GetString();
            switch(value["Kind"].GetString()) {case "bool":return bool.Parse(text);case "int":return int.Parse(text,CultureInfo.InvariantCulture);case "long":return long.Parse(text,CultureInfo.InvariantCulture);case "float":return float.Parse(text,CultureInfo.InvariantCulture);case "double":return double.Parse(text,CultureInfo.InvariantCulture);default:return text;}
        }
        private static async Task PostAsync(HttpClient client,string path,JsonObject json)
        {
            using(var content=new HttpStringContent(json.Stringify(),UnicodeEncoding.Utf8,"application/json"))
            using(var response=await client.PostAsync(LocalServiceEndpoints.Build(path),content)) response.EnsureSuccessStatusCode();
        }
        private static async Task PollAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if(Busy) return; Busy=true;
            try
            {
                using(var client=await LocalServiceAuth.CreateHttpClientAsync())
                {
                    foreach(string key in new List<string>(Pending.Keys))
                    {
                        var saved=Pending[key];
                        await PostAsync(client,"/shared/setting",new JsonObject { ["key"]=JsonValue.CreateStringValue(key),["value"]=saved ?? JsonValue.CreateNullValue() });
                        if(Pending.TryGetValue(key,out var now) && ReferenceEquals(saved,now)) Pending.Remove(key);
                    }
                    using(var response=await client.GetAsync(LocalServiceEndpoints.Build("/shared/state")).AsTask(cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        var snapshot=JsonObject.Parse(await response.Content.ReadAsStringAsync().AsTask(cancellationToken));
                        string settings=snapshot["settings"].Stringify();
                        if(settings!=Settings) { Settings=settings; SettingsRevision++; }
                        SharedResources.RemoteDataRoot=snapshot["dataRoot"].GetString();
                        Applying=true;
                        try
                        {
                            var currentKeys=new HashSet<string>();
                            if(snapshot["settings"].ValueType==JsonValueType.Object) foreach(var pair in snapshot["settings"].GetObject()) {
                                currentKeys.Add(pair.Key);
                                if(!Pending.ContainsKey(pair.Key)) {
                                    object value=Scalar(pair.Value.GetObject());
                                    if(!ApplicationData.Current.LocalSettings.Values.TryGetValue(pair.Key,out object previous) || !Equals(previous,value)) ApplicationData.Current.LocalSettings.Values[pair.Key]=value;
                                }
                            }
                            foreach(string key in PrimaryKeys) if(!currentKeys.Contains(key) && !Pending.ContainsKey(key)) ApplicationData.Current.LocalSettings.Values.Remove(key);
                            PrimaryKeys=currentKeys;
                            string catalog=snapshot["catalog"].Stringify();
                            if(catalog!="null" && catalog!=Catalog) {File.WriteAllText(Path.Combine(ApplicationData.Current.LocalFolder.Path,"pack-catalog.json"),catalog); Catalog=catalog; PackCatalogService.ResetSharedCache(); SharedResources.Invalidate();}
                            string display=snapshot["display"].Stringify();
                            if(display!="null" && display!=Display) {string folder=Path.Combine(ApplicationData.Current.LocalFolder.Path,"CompatibilityDisplay");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"display.json"),display); Display=display;SharedResources.Invalidate();SettingsRevision++;}
                        }
                        finally {Applying=false;}
                    }
                    string status=Path.Combine(ApplicationData.Current.LocalFolder.Path,"CompatibilityDisplay","gamebar-runtime.json");
                    string guard=Path.Combine(ApplicationData.Current.LocalFolder.Path,"CompatibilityDisplay","gamebar-status.json");
                    if(File.Exists(status)) await PostAsync(client,"/shared/gamebar-status",new JsonObject {
                        ["runtime"]=JsonValue.Parse(File.ReadAllText(status)),
                        ["display"]=File.Exists(guard) ? JsonValue.Parse(File.ReadAllText(guard)) : JsonValue.CreateNullValue()
                    });
                }
            }
            finally {Busy=false;}
        }
    }
}
