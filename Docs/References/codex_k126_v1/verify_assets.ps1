$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;using System.Drawing;using System.IO;using System.Collections.Generic;
public static class Check126 {
public static string[] Run(string d){var log=new List<string>();foreach(string f in Directory.GetFiles(Path.Combine(d,"native"),"*.png")){string id=Path.GetFileName(f);using(var b=new Bitmap(f))using(var g=new Bitmap(Path.Combine(d,"game",id)))using(var p=new Bitmap(Path.Combine(d,"previews",id))){if(g.Width!=b.Width*3||g.Height!=b.Height*3||p.Width!=b.Width*4||p.Height!=b.Height*4)throw new Exception(id+" size");int frames=id.Contains("6f")?6:id=="merlon_states.png"?4:1,cw=b.Width/frames;var bottoms=new List<int>();int islands=0;for(int frame=0;frame<frames;frame++){int bottom=-1;for(int y=0;y<b.Height;y++)for(int x=0;x<cw;x++){Color c=b.GetPixel(frame*cw+x,y);if(c.A!=0&&c.A!=255)throw new Exception(id+" alpha");if(c.A>0){bottom=y;int n=0;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=x+dx,yy=y+dy;if((dx!=0||dy!=0)&&xx>=0&&xx<cw&&yy>=0&&yy<b.Height&&b.GetPixel(frame*cw+xx,yy).A>0)n++;}if(n==0)islands++;}for(int yy=0;yy<3;yy++)for(int xx=0;xx<3;xx++)if(g.GetPixel((frame*cw+x)*3+xx,y*3+yy).ToArgb()!=c.ToArgb())throw new Exception(id+" nearest3");for(int yy=0;yy<4;yy++)for(int xx=0;xx<4;xx++)if(p.GetPixel((frame*cw+x)*4+xx,y*4+yy).ToArgb()!=c.ToArgb())throw new Exception(id+" nearest4");}if(cw==96&&!id.StartsWith("dust")&&bottom!=119)throw new Exception(id+" contact line");if(cw==96&&bottom>119)throw new Exception(id+" baseline overflow");bottoms.Add(bottom);}if(id=="wall_stage1_cracks.png"||id=="wall_stage2_gouges.png")for(int y=0;y<128;y++)if(b.GetPixel(0,y).A!=0||b.GetPixel(127,y).A!=0)throw new Exception(id+" seam");if(islands!=0)throw new Exception(id+" isolated pixels");log.Add(id+" | "+b.Width+"x"+b.Height+" | bottom="+string.Join(",",bottoms)+" | isolated_pixels="+islands);}}
return log.ToArray();}}
'@
$rows=[Check126]::Run($PSScriptRoot)
$rows | Set-Content -Encoding UTF8 "$PSScriptRoot/verification.txt"
$rows
