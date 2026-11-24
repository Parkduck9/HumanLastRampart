$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$root=(Get-Location).Path
$src=@{
 cannon=@('Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png','Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png')
 crew=@('Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png','Game/Assets/_Project/Art/Characters/Run/cannon_crew_run.png','Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png')
}
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;using System.Drawing;using System.Collections.Generic;using System.Linq;
public class Scan052 {public static object[] Top(string[] files){var d=new Dictionary<int,int>();foreach(var f in files)using(var b=new Bitmap(f)){for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,y);if(c.A<250)continue;int k=c.ToArgb() | unchecked((int)0xff000000);if(!d.ContainsKey(k))d[k]=0;d[k]++;}}return d.OrderByDescending(p=>p.Value).Take(64).Select(p=>(object)new {hex="#"+(p.Key&0xffffff).ToString("X6"),count=p.Value}).ToArray();}}
"@
$result=@{}
foreach($key in $src.Keys){$files=@($src[$key]|ForEach-Object{Join-Path $root $_});$result[$key]=[Scan052]::Top($files)}
$result|ConvertTo-Json -Depth 6|Set-Content -Encoding UTF8 Docs/References/codex_k052_v1/palette.json
$result|ConvertTo-Json -Depth 6


