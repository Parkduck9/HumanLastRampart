
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$repo=(Resolve-Path (Join-Path $root '../../..')).Path
$enc=New-Object Text.UTF8Encoding($false)
$m=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'manifest.json')|ConvertFrom-Json
$images=[ordered]@{}
foreach($a in $m.assets){$images[$a.file]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root "png/$($a.file)")))}
$wall=Join-Path $repo 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/wall_stone_albedo.png'
$images['wall']='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($wall))
$images['reference']='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $root 'reference_map.png')))
$html=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'review_template.html'))
$html=$html.Replace('__IMAGES__',($images|ConvertTo-Json -Compress)).Replace('__MANIFEST__',($m|ConvertTo-Json -Depth 8 -Compress))
[IO.File]::WriteAllText((Join-Path $repo '_HTML/codex_K-020_map_markers_v1.html'),$html,$enc)

# 견본의 배경과 글자는 검토용이며 표식 소재에는 포함하지 않는다.
function Brush($hex){New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml($hex))}
function Place($file,$x,$y,$scale=1){$im=[Drawing.Image]::FromFile((Join-Path $root "png/$file"));$g.DrawImage($im,[Drawing.Rectangle]::new($x,$y,($im.Width*$scale),($im.Height*$scale)),0,0,$im.Width,$im.Height,[Drawing.GraphicsUnit]::Pixel);$im.Dispose()}
function BeginBoard($w,$h,$color){$script:board=New-Object Drawing.Bitmap($w,$h);$script:g=[Drawing.Graphics]::FromImage($board);$g.Clear([Drawing.ColorTranslator]::FromHtml($color));$g.InterpolationMode='NearestNeighbor';$g.PixelOffsetMode='Half'}
function SaveBoard($file){$board.Save((Join-Path $root $file),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$board.Dispose()}
$font=New-Object Drawing.Font('Malgun Gothic',12)
$small=New-Object Drawing.Font('Malgun Gothic',10)
$cream=Brush '#eee4cf';$field=Brush '#63764f';$gold=Brush '#edcb83'
BeginBoard 1000 660 '#101613'
$g.DrawString('K-020 지도 표식 · 사용자 확정 전 시안 · 원래 크기 / 4배',$font,$cream,20,15)
for($i=0;$i -lt $m.assets.Count;$i++){
    $a=$m.assets[$i];$x=20+($i%5)*196;$y=60+[Math]::Floor($i/5)*290
    $g.FillRectangle($field,$x,$y,182,220)
    Place $a.file ($x+18) ($y+12)
    $factor=if($a.width -eq 64){2}else{4}
    Place $a.file ($x+18) ($y+60) $factor
    $g.DrawString($a.label,$small,$cream,$x,($y+226));$g.DrawString("$($a.width) × $($a.height)",$small,$cream,$x,($y+249))
}
SaveBoard 'contact_sheet.png'

# 지도 그림은 직교 배치 시안이며 실제 게임 캡처가 아니다.
BeginBoard 960 600 '#63764f'
$stone=[Drawing.Image]::FromFile($wall);$tile=New-Object Drawing.TextureBrush($stone);$inside=Brush '#666a65'
$g.FillRectangle($inside,292,140,376,320)
foreach($r in @(@(270,118,420,22),@(270,460,420,22),@(270,118,22,364),@(668,118,22,364),@(258,106,44,44),@(658,106,44,44),@(258,450,44,44),@(658,450,44,44))){$g.FillRectangle($tile,$r[0],$r[1],$r[2],$r[3])}
foreach($c in @(@('북',480,200),@('동',580,300),@('남',480,400),@('서',380,300))){Place 'compass_back.png' ($c[1]-32) ($c[2]-32);$g.DrawString($c[0],$font,$gold,($c[1]-9),($c[2]-12))}
foreach($c in @(@('archer',342,80),@('gunner',444,80),@('archer',546,80),@('gunner',720,242),@('archer',720,344),@('archer',342,504),@('gunner',444,504),@('gunner',546,504),@('archer',208,242),@('gunner',208,344))){Place "flag_$($c[0]).png" $c[1] $c[2]}
Place 'group_selected.png' 440 76;Place 'move_east.png' 480 84;Place 'cannon_target_active.png' 764 140
$g.DrawString('지도 표식 배치 시안 · 실제 게임 캡처 아님',$font,$gold,280,20)
$g.DrawString('대포 1',$font,$gold,753,180)
$g.DrawString('화면 화소 기준 · 궁수 청록 / 총병 자주 / 방위와 이동 황동',$font,$gold,230,562)
SaveBoard 'map_preview.png'
$stone.Dispose();$tile.Dispose();$inside.Dispose();$font.Dispose();$small.Dispose();$cream.Dispose();$field.Dispose();$gold.Dispose()
Write-Output '검토 HTML·견본 PNG·지도 배치 PNG 저장 완료'
