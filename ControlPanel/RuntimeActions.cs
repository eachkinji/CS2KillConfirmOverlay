namespace KillConfirmGameBar;
// Same commands the original panel used to route through the package manifest.
internal static class KillConfirmWidgetPage
{
    public const string OpenGameBarParameterGroupId="OpenGameBar";
    public const string OpenUninstallerParameterGroupId="OpenUninstaller";
    public const string OpenRuntimeLogsParameterGroupId="OpenRuntimeLogs";
    public static Task<bool> TryLaunchFullTrustHelperAsync(string group)
    {
        try
        {
            string root=KillConfirmCompatibility.Contracts.RuntimePaths.InstallRoot;
            var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(root,"KillConfirmService","cskillconfirm.exe")) { UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=Path.Combine(root,"KillConfirmService") };
            start.Environment["KILLCONFIRM_DATA_ROOT"]=KillConfirmCompatibility.Contracts.RuntimePaths.DataRoot;
            start.Environment["KILLCONFIRM_INSTALL_ROOT"]=root;
            var commands=new Dictionary<string,string> {
                ["OpenGameBar"]="--open-game-bar", ["OpenUninstaller"]="--open-uninstaller", ["OpenRuntimeLogs"]="--open-logs",
                ["CompatibilityDisplay"]="--open-compatibility-display", ["StopCompatibilityDisplay"]="--close-compatibility-display", ["ExitAll"]="--exit-all",
                ["OpenSettingsWindow"]="--open-settings-launcher", ["OpenSettingsWindowDeveloper"]="--open-settings-launcher",
                ["OpenQuarkUpdate"]="--open-quark-update", ["OpenAuthorGitHub"]="--open-author-github", ["OpenAuthorBilibili"]="--open-author-bilibili"
            };
            if(commands.TryGetValue(group,out var command)) start.ArgumentList.Add(command);
            else if(group=="FreeServicePort") { start.ArgumentList.Add("--free-port"); start.ArgumentList.Add(Services.LocalServiceEndpoints.Port.ToString()); }
            else if(group.StartsWith("ServicePort",StringComparison.Ordinal) || group=="CrossfirePreset" || group=="CrossfirePresetDeveloper")
            {
                start.ArgumentList.Add("--port"); start.ArgumentList.Add(Services.LocalServiceEndpoints.Port.ToString());
                start.ArgumentList.Add("--exit-with-ui");
                start.ArgumentList.Add("--preset"); start.ArgumentList.Add("valorant_00000_base");
                if(group.EndsWith("Developer",StringComparison.Ordinal)) start.ArgumentList.Add("--developer-mode");
            }
            else throw new ArgumentException("Unknown runtime command: "+group);
            using var process=System.Diagnostics.Process.Start(start);
            return Task.FromResult(process!=null);
        }
        catch(Exception error) { App.LogCrash("Ordinary runtime launch",error); return Task.FromResult(false); }
    }
}
