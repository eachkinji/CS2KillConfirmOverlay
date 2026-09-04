param([string]$ReferenceEnginePath = '')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$motion = Get-Content -Raw (Join-Path $root 'Widget/Controls/Animations/Core/CrossfireStyle2Motion.cs')
$probe = @'
public static class Style2Probe {
 public static double[] Sample(double time, bool flame) {
  var s=KillConfirmGameBar.Controls.CrossfireStyle2Motion.Sample(time,flame);
  return new[]{s.Scale,s.Alpha,s.X,s.Y};
 }
 public static void Verify() {
  var m=KillConfirmGameBar.Controls.CrossfireStyle2Motion.Sample(0);
  if(m.Scale!=0||m.Alpha!=1)throw new System.Exception("Entry state");
  if(Sample(1275,false)[1]!=0||Sample(345,true)[1]!=0)throw new System.Exception("Independent expiry");
  if(Sample(275,false)[1]!=1||System.Math.Abs(Sample(295,false)[1]-.98)>1e-10)throw new System.Exception("Main fade steps");
  if(Sample(205,true)[1]!=1||System.Math.Abs(Sample(225,true)[1]-.85)>1e-10)throw new System.Exception("Flame fade steps");
  foreach(double t in new[]{0,15,60,75,105,200,275,345,750,1275}) {
   var a=Sample(t,false); var b=Sample(t,true);
   if(a[0]<0||a[0]>1||b[0]<0||b[0]>1)throw new System.Exception("Unnormalized scale");
  }
  var knife=KillConfirmGameBar.Controls.CrossfireStyle2Motion.MainLayout("knife");
  var c4=KillConfirmGameBar.Controls.CrossfireStyle2Motion.MainLayout("c4defuse");
  var banner=KillConfirmGameBar.Controls.CrossfireStyle2Motion.MainLayout("firstkill");
  if(knife.Width!=116||knife.Height!=170||knife.Y!=-7||c4.Width!=130||c4.Height!=140||c4.Y!=-15||banner.Width!=241||banner.Y!=-100)
   throw new System.Exception("Event anchors/dimensions");
 }
}
'@
Add-Type -TypeDefinition ($motion + $probe)
[Style2Probe]::Verify()
$compared = 0
if ($ReferenceEnginePath) {
    $js = @'
require(process.argv[1]);
const e=global.CFKillmarkEngine, times=[];
for(let t=0;t<=1400;t+=5)times.push(t);
times.push(74.999,104.999,204.999,274.999,344.999,1274.999);
const result=[];
for(const t of times)for(const flame of [false,true]) {
 const s=e.computeBadgeAnim(t,flame?e.MEB_PARAMS:e.MARK_PARAMS);
 result.push({t,flame,values:[s.scale/s.scaleLimit,s.alpha,s.ox,s.oy]});
}
process.stdout.write(JSON.stringify(result));
'@
    $samples = & node -e $js $ReferenceEnginePath | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Reference engine failed' }
    foreach ($sample in $samples) {
        $actual = [Style2Probe]::Sample($sample.t,$sample.flame)
        for ($i=0;$i -lt 4;$i++) {
            if ([math]::Abs($actual[$i] - $sample.values[$i]) -gt 1e-9) {
                throw "Reference mismatch: t=$($sample.t), flame=$($sample.flame), component=$i"
            }
        }
        $compared++
    }
}
Write-Host "PASS: style 2 timeline, normalized scale, independent expiry and event geometry; $compared samples compared with reference JavaScript."
