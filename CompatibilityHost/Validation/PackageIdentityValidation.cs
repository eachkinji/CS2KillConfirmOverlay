using KillConfirmCompatibility.Desktop.Runtime;
using Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation
{
    internal static class PackageIdentityValidation
    {
        internal static async Task RunAsync(string mode, string output)
        {
            var package = Windows.ApplicationModel.Package.Current;
            if (package.Id.Name != "KillConfirmCompatibility.Validation") throw new Exception("Identity validation requires its isolated test package.");
            Directory.CreateDirectory(output);
            var choices = DesktopStorage.Current.LocalSettings.Values;
            if (mode == "--package-child")
            {
                var file = await DesktopStorage.AssetFileAsync(new Uri("ms-appx:///Assets/KillConfirmCode/Csol4/3kill.png"));
                using var bitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), file.Path);
                if (bitmap.SizeInPixels.Width == 0) throw new Exception("Packaged resource did not load.");
                using var current = Process.GetCurrentProcess();
                foreach (ProcessModule module in current.Modules)
                    if (module.ModuleName.Equals("Microsoft.Graphics.Canvas.dll", StringComparison.OrdinalIgnoreCase)
                        && !module.FileName.Equals(Path.Combine(AppContext.BaseDirectory, "Microsoft.Graphics.Canvas.dll"), StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Desktop renderer loaded the legacy UWP runtime.");
                choices["CompatibilityDisplay.ValidationCounter"] = 2;
                File.WriteAllText(Path.Combine(output, "child-identity.txt"), package.Id.FamilyName);
                return;
            }
            choices["CompatibilityDisplay.ValidationCounter"] = 1;
            var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--package-child"); start.ArgumentList.Add(output);
            using var child = Process.Start(start);
            await child.WaitForExitAsync();
            if (child.ExitCode != 0 || File.ReadAllText(Path.Combine(output, "child-identity.txt")) != package.Id.FamilyName) throw new Exception("Child process lost package identity.");
            if (!(choices["CompatibilityDisplay.ValidationCounter"] is int counter) || counter != 2) throw new Exception("LocalSettings changes are not visible across processes.");
            choices.Remove("CompatibilityDisplay.ValidationCounter");
            File.WriteAllText(Path.Combine(output, "package.txt"), "PASS: packaged full-trust startup, inherited child identity, ms-appx Win2D assets, live cross-process LocalSettings; no Game Bar dependency");
        }
    }
}
