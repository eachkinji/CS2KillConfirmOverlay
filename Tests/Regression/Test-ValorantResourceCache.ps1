#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot '..\Common\SourceView.ps1')
$source = Get-Content -Raw (Resolve-SourcePath $root 'Widget/Services/Runtime/SharedResources.cs')
$method = [regex]::Match($source, '(?ms)^        private static async Task EnsureCurrentFileAsync\([^\r\n]+\).*?^        \}').Value
if (!$method) { throw 'Production shared resource revision method was not found.' }
$shim = @'
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
public static class ValorantResourceCacheRegression
{
    private static readonly SemaphoreSlim RevisionGate = new SemaphoreSlim(1,1);
    private static int Downloads;
    private static Task DownloadAsync(string source, bool asset, string destination)
    {
        Downloads++;
        File.Copy(source,destination,true);
        return Task.CompletedTask;
    }
    public static void Run(string root) => Verify(root).GetAwaiter().GetResult();
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    private static async Task Verify(string root)
    {
        Directory.CreateDirectory(root);
        string source=Path.Combine(root,"remote-native_animation.json");
        string target=Path.Combine(root,"native_animation.json");
        string unrelated=Path.Combine(root,"another-pack.png");
        File.WriteAllText(target,"old unversioned cache");
        File.WriteAllText(source,"native metadata version 1");
        File.WriteAllText(unrelated,"preserve");
        await EnsureCurrentFileAsync(source,target,"revision1");
        Check(File.ReadAllText(target)=="native metadata version 1","Legacy cache was reused.");
        await EnsureCurrentFileAsync(source,target,"revision1");
        Check(Downloads==1,"Current file was downloaded twice.");
        File.WriteAllText(source,"native metadata version 2 with new texture names");
        await EnsureCurrentFileAsync(source,target,"revision2");
        Check(File.ReadAllText(target)==File.ReadAllText(source),"Same-ID new metadata bytes were stale.");
        Check(File.ReadAllText(target+".valorant-revision")=="revision2","Revision marker was not advanced.");
        File.Delete(target);
        await EnsureCurrentFileAsync(source,target,"revision2");
        Check(Downloads==3,"A pruned file was not restored.");
        Check(File.ReadAllText(unrelated)=="preserve","Another pack was changed.");
        Check(Directory.GetFiles(root).Length==4,"Reimport accumulated versioned resource copies.");
    }
'@
Add-Type -TypeDefinition ($shim + "`n" + $method + "`n}")
$output = Join-Path $root 'Output/Regression-ValorantResourceCache43'
[ValorantResourceCacheRegression]::Run($output)
Write-Output 'PASS: legacy cached metadata refresh, same-ID new bytes, current revision reuse, pruned cache restore, unrelated-pack isolation, bounded copies'
