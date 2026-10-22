$ErrorActionPreference='Stop'
$compile=[IO.File]::ReadAllText("$PSScriptRoot/pack_assets.ps1").Split([string[]]@('$root=Split-Path'),[StringSplitOptions]::None)[0]
Invoke-Expression $compile
$out=$PSScriptRoot
$frames=@();$checks=@();$sources=@()
$configs=@(@('hero_walk_run_grid',4,4),@('hero_back_idle',1,1),@('hero_command',3,1))
foreach($conf in $configs){
 $name=$conf[0];$cols=$conf[1];$rows=$conf[2]
 $native=[K109Pack]::Load("$out/${name}_native.png");$game=[K109Pack]::Load("$out/$name.png")
 $checks+=@{check="$name sizes";pass=($native.Width -eq $cols*96 -and $native.Height -eq $rows*128 -and $game.Width -eq $cols*288 -and $game.Height -eq $rows*384)}
 foreach($kind in @('color','normal','spec')){
  $suffix=if($kind -eq 'color'){''}else{"_$kind"}
  $n=[K109Pack]::Load("$out/${name}${suffix}_native.png");$g=[K109Pack]::Load("$out/${name}${suffix}.png")
  $ok=$true
  for($y=0;$y -lt $g.Height;$y++){for($x=0;$x -lt $g.Width;$x++){if($g.GetPixel($x,$y).ToArgb() -ne $n.GetPixel([int][Math]::Floor($x/3),[int][Math]::Floor($y/3)).ToArgb()){$ok=$false;break}}if(!$ok){break}}
  $checks+=@{check="$name $kind exact nearest 3x";pass=$ok};$n.Dispose();$g.Dispose()
 }
 for($row=0;$row -lt $rows;$row++){for($col=0;$col -lt $cols;$col++){
  $cell=[K109Pack]::Crop($native,$col*96,$row*128,96,128);$bb=[K109Pack]::Box($cell);$components=[K109Pack]::Components($cell);$binary=$true;$zeroRgb=$true;$colors=@{}
  for($y=0;$y -lt 128;$y++){for($x=0;$x -lt 96;$x++){$c=$cell.GetPixel($x,$y);if($c.A -ne 0 -and $c.A -ne 255){$binary=$false};if($c.A -eq 255){$colors[$c.ToArgb()]=$true}elseif($c.R -ne 0 -or $c.G -ne 0 -or $c.B -ne 0){$zeroRgb=$false}}}
  $axis=[K109Pack]::Axis($cell);$pass=($bb.Bottom -eq 120 -and $bb.Height -eq 73 -and [Math]::Abs($axis-48) -le 0.51 -and $binary -and $components.Count -eq 1 -and $bb.Left -gt 0 -and $bb.Right -lt 96 -and $zeroRgb)
  $frames+=@{sheet=$name;row=$row;column=$col;bbox=@($bb.X,$bb.Y,$bb.Right,$bb.Bottom);height=$bb.Height;torso_axis_x=$axis;last_opaque_row=$bb.Bottom-1;binary_alpha=$binary;transparent_rgb_zero=$zeroRgb;palette_colors=$colors.Count;component_sizes=@($components);pass=$pass}
  $cell.Dispose()
 }}
 $native.Dispose();$game.Dispose()
}
foreach($e in @('neutral','determined','surprised','tired')){$b=[K109Pack]::Load("$out/portrait_hero_$e.png");$checks+=@{check="portrait $e size";pass=($b.Width -eq 256 -and $b.Height -eq 384)};$b.Dispose()}
$b=[K109Pack]::Load("$out/officer_expressions.png");$checks+=@{check='tutorial strip layout';pass=($b.Width -eq 576 -and $b.Height -eq 144)};$b.Dispose()
foreach($name in @('portrait_hero_neutral','portrait_hero_determined','portrait_hero_surprised','portrait_hero_tired','officer_expressions')){
 $b=[K109Pack]::Load("$out/$name.png");$ok=$true
 for($y=0;$y -lt $b.Height;$y++){for($x=0;$x -lt $b.Width;$x++){$a=$b.GetPixel($x,$y).A;if($a -ne 0 -and $a -ne 255){$ok=$false}}}
 $checks+=@{check="$name binary alpha";pass=$ok};$b.Dispose()
}
$decoded=@();foreach($file in Get-ChildItem $out -Filter *.png){$b=[K109Pack]::Load($file.FullName);$decoded+=@{file=$file.Name;width=$b.Width;height=$b.Height};$b.Dispose()}
$repo=(Resolve-Path "$out/../../..").Path
$oldManifest=Get-Content -Raw -Encoding UTF8 "$repo/Docs/References/codex_k107_v1/manifest.json"|ConvertFrom-Json
foreach($s in $oldManifest.source_art){if($s.path -like 'Game/*'){$hash=(Get-FileHash -Algorithm SHA256 "$repo/$($s.path)").Hash.ToLower();$sources+=@{path=$s.path;sha256=$hash;matches_k107_readonly_source=($hash -eq $s.sha256)}}}
$result=@{pass=(@($frames|Where-Object {!$_.pass}).Count -eq 0 -and @($checks|Where-Object {!$_.pass}).Count -eq 0 -and @($sources|Where-Object {!$_.matches_k107_readonly_source}).Count -eq 0);frames=$frames;checks=$checks;decoded_pngs=$decoded;readonly_source_checks=$sources;browser_render_verified=$false;unity_executed=$false;notes='Browser inventory returned no available apps or browsers. PNG decoding and HTML runtime checks are recorded separately.'}
$result|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 "$out/validation.json"
$result.frames|ForEach-Object {[pscustomobject]$_}|Select-Object sheet,row,column,height,torso_axis_x,pass|Format-Table
Write-Output "검증 통과: $($result.pass)"
