$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path "$PSScriptRoot/PixelTools.cs"
$base=(Get-Location).Path
$cannon=@('Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png','Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png')|ForEach-Object{Join-Path $base $_}
$crew=@('Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png','Game/Assets/_Project/Art/Characters/Run/cannon_crew_run.png','Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png')|ForEach-Object{Join-Path $base $_}
$cp=[K052]::Palette($cannon,@('#0C0A10','#2B2A35','#3F3F4B','#595660','#8C8580','#49372E','#70513C','#926641','#B68B5B'))
$sp=[K052]::Palette($crew,@('#09070A','#161B2E','#242D45','#35415A','#36251B','#513221','#59402F','#7C5133','#B88155','#D9C8A0','#F3DFB3','#E7B27E','#B47C56','#3A3038','#50464C'))
$report=@{cannon=$cp;crew=$sp;method='알파250 이상 픽셀 RGB 빈도(알파 통합). 재질별 목표색에 가장 가까운 실제 원본 RGB(출현8회 이상)만 선택. 전체 상위64색은 palette.json.';outlines=@{}}
foreach($p in @($cannon)+@($crew)){$b=[Drawing.Bitmap]::new($p);$report.outlines[(Split-Path $p -Leaf)]=[K052]::Outline($b);$b.Dispose()}
$report|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 "$PSScriptRoot/selected_palette.json"


