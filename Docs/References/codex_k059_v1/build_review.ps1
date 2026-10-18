$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$spec = @'
panel_parchment|양피지 판|6,6,6,6|반복|UI
panel_wood|나무 명령판|6,6,6,6|반복|UI
panel_band|황동 계기 띠|6,6,6,6|반복|UI
plate|나무 간판|5,5,5,5|늘이기|UI
slip|전령 쪽지|4,4,6,6|늘이기|UI
slip_danger|위급 쪽지|6,4,6,6|늘이기|UI
bubble|잉크 말풍선|8,8,8,8|늘이기|UI
bubble_tail|말풍선 꼬리|||UI
button_wood|나무 단추|4,4,4,4|늘이기|UI
button_wood_down|눌린 나무 단추|4,4,4,4|늘이기|UI
button_brass|황동 단추|4,4,4,4|늘이기|UI
button_brass_down|눌린 황동 단추|4,4,4,4|늘이기|UI
frame_glow|고름 빛 테두리|4,4,4,4|늘이기|UI
keycap|키 표시|3,4,3,3|늘이기|UI
bar_track|체력 막대 홈|3,3,3,3|늘이기|UI
icon_close|닫기|||UI
icon_up|접기|||UI
icon_down|펴기|||UI
icon_play|다음|||UI
icon_pause|멈춤|||UI
icon_speed1|보통 빠르기|||UI
icon_speed2|두 배 빠르기|||UI
icon_speed4|네 배 빠르기|||UI
icon_hero|주인공 장교|||UI
icon_north|북쪽|||UI
icon_check|확정|||UI
icon_flag|조준 깃발|||UI
icon_auto|자동 조준|||UI
icon_idle|대기|||UI
icon_special|특수 적|||UI
icon_minus|인원 줄이기|||UI
icon_plus|인원 늘리기|||UI
icon_right|보내기|||UI
icon_warn|위험|||UI
seal|보고서 도장|||UI
vignette|가장자리 빛|||UI
story_box|대사 상자|8,8,8,8|늘이기|스토리 부품
story_nameplate|말하는 사람 이름표|6,6,6,6|늘이기|스토리 부품
story_next_0|넘김 첫 칸|||스토리 부품
story_next_1|넘김 둘째 칸|||스토리 부품
story_next_2|넘김 셋째 칸|||스토리 부품
story_next_3|넘김 넷째 칸|||스토리 부품
story_button|스토리 단추|4,4,4,4|늘이기|스토리 부품
story_button_hover|올린 스토리 단추|4,4,4,4|늘이기|스토리 부품
story_button_on|켜진 스토리 단추|4,4,4,4|늘이기|스토리 부품
story_fade_edge|잉크 암전 가장자리|||스토리 부품
story_portrait_hero|주인공 네 표정|||초상
story_portrait_wizard|마법사 네 표정|||초상
story_portrait_archer|궁수 네 표정|||초상
story_bg_rampart_dusk|해 질 녘 성벽|||배경
story_bg_rampart_night|밤 성벽|||배경
'@
$records = @()
$embedded = @()
foreach ($row in ($spec.Trim() -split "`n")) {
 $s = $row.Trim() -split '\|'
 $path = Join-Path $PSScriptRoot ($s[0] + '.png')
 $b = [System.Drawing.Bitmap]::new($path)
 $border = if ($s[2]) { @($s[2] -split ',' | ForEach-Object { [int]$_ }) } else { $null }
 $record = [ordered]@{ name=$s[0]; title=$s[1]; w=$b.Width; h=$b.Height; border=$border; mode=$s[3]; kind=$s[4]; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower() }
 $b.Dispose()
 $records += $record
 $copy = [ordered]@{}
 foreach ($k in $record.Keys) { $copy[$k] = $record[$k] }
 $copy.data = 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
 $embedded += $copy
}
$utf8 = [System.Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'manifest.json'), (ConvertTo-Json -Depth 6 -InputObject $records), $utf8)
$json = ConvertTo-Json -Depth 6 -Compress -InputObject $embedded
$html = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'review_template.html'), [Text.Encoding]::UTF8).Replace('__DATA__', $json)
$root = (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName
$dest = Join-Path $root '_HTML/6_그림_화면/codex_K-059_UI키트_스토리_v1.html'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest)) | Out-Null
[IO.File]::WriteAllText($dest, $html, $utf8)
Write-Output ('PNG ' + $records.Count + ' / ' + $dest)
