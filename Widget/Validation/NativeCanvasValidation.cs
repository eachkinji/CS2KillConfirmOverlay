#if NATIVE_CANVAS_VALIDATION
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KillConfirmGameBar.Services;
using KillConfirmCompatibility.Contracts;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace KillConfirmGameBar.Controls {
public sealed partial class KillConfirmAnimation {
internal static async Task ValidateNativeCanvasAsync() {
 string output=Path.Combine(ApplicationData.Current.LocalFolder.Path,"NativeCanvas-test");Directory.CreateDirectory(output);
 try {
 GameStyleService.Current=GameStyleMode.Sanguosha;
 // Construct the real page and exercise its actual XAML SelectionChanged wiring.
 // No navigation: no game/service startup or modification of the installed app profile.
 var page=new KillConfirmWidgetPage(); Window.Current.Content=page;Window.Current.Activate();
 var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 var pack=typeof(KillConfirmWidgetPage).GetField("PackTestSectionView",flags).GetValue(page);
 var selector=(ComboBox)pack.GetType().GetField("RasterQualitySelector",flags).GetValue(pack);
 var animation=(KillConfirmAnimation)typeof(KillConfirmWidgetPage).GetField("LowerFeedbackAnimation",flags).GetValue(page);
 animation.ApplyViewportSize(1600,900);animation.Visibility=Visibility.Visible;
 int draws=0; double dpi=0;
 animation.SpriteCanvas.Draw+=(s,e)=>{draws++;dpi=e.DrawingSession.Dpi;};
 var report=new JsonArray();
 foreach(int edge in new[]{1280,3200,1600,1920,2560,1280}) {
 int before=draws;
 selector.SelectedItem=selector.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag==edge.ToString());
 animation.SpriteCanvas.Invalidate();await Task.Delay(250);
 if(AnimationRasterPolicy.ReadForGame(ApplicationData.Current.LocalSettings.Values,"sanguosha")!=edge)throw new Exception("Selector failed to save");
 if(AnimationRasterPolicy.MaxEdge!=edge || animation.SpriteCanvas.Width!=edge)throw new Exception("Selector failed to resize canvas");
 if(draws<=before)throw new Exception("Canvas did not redraw");
 if(Math.Abs(dpi-96)>.01)throw new Exception("Unexpected canvas DPI");
 report.Add(new JsonObject { ["selected"]=JsonValue.CreateNumberValue(edge),["pixelWidth"]=JsonValue.CreateNumberValue(animation.SpriteCanvas.ActualWidth*dpi/96),["pixelHeight"]=JsonValue.CreateNumberValue(animation.SpriteCanvas.ActualHeight*dpi/96),["dpi"]=JsonValue.CreateNumberValue(dpi),["draws"]=JsonValue.CreateNumberValue(draws-before)});
 }
 File.WriteAllText(Path.Combine(output,"result.json"),report.Stringify());
 } catch(Exception ex){File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());}
 finally {File.WriteAllText(Path.Combine(output,"done"),"done");}
}
}}
#endif
