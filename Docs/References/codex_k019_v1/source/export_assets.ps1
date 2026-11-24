# 생성 원안을 최근접 화소로 추출하고, 늘이기 경계와 번호를 정리한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$base = Split-Path $PSScriptRoot -Parent
$atlas = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'imagegen_atlas.png'))
function Extract($name, $x, $y, $w, $h, $dw, $dh) {
    $b = New-Object System.Drawing.Bitmap($dw, $dh)
    for ($py=0; $py -lt $dh; $py++) {
        for ($px=0; $px -lt $dw; $px++) {
            $sx = $x + [Math]::Min($w-1,[Math]::Floor(($px+0.5)*$w/$dw))
            $sy = $y + [Math]::Min($h-1,[Math]::Floor(($py+0.5)*$h/$dh))
            $b.SetPixel($px,$py,$atlas.GetPixel($sx,$sy))
        }
    }
    return $b
}
function Save($b,$name) { $b.Save((Join-Path $base "png/$name.png"),[System.Drawing.Imaging.ImageFormat]::Png) }
# 9칸 늘이기: 양끝은 붓 결을 보존하고, 중앙과 맞닿는 세로 열을 동일하게 만든다.
foreach($kind in @('normal','hover')) {
    $y=105; if($kind -eq 'hover'){$y=375}
    $b=Extract $kind 68 $y 1116 170 380 48
    for($py=0;$py -lt 48;$py++) {
        $edge=$b.GetPixel(190,$py)
        for($px=64;$px -lt 316;$px++){$b.SetPixel($px,$py,$edge)}
    }
    Save $b "command_band_$kind"; $b.Dispose()
}
$b=Extract '밑줄' 105 620 1048 65 320 8
Save $b 'command_underline'; $b.Dispose()
$ring=Extract '표식' 116 800 215 220 34 34
Save $ring 'command_circle'
# 3×5 숫자 원형을 2배 화소로 찍어 글꼴 설치와 무관한 번호를 제공한다.
$digits=@('010111010010111','110001010100111','110001010001110','101101111001001','111100110001110','011100110101010')
for($n=1;$n -le 6;$n++) {
    $b=$ring.Clone()
    for($dy=0;$dy -lt 5;$dy++){for($dx=0;$dx -lt 3;$dx++){
        if($digits[$n-1][$dy*3+$dx] -eq '1'){
            for($oy=0;$oy -lt 2;$oy++){for($ox=0;$ox -lt 2;$ox++){
                $b.SetPixel(14+$dx*2+$ox,12+$dy*2+$oy,[System.Drawing.Color]::FromArgb(255,238,228,207))
            }}
        }
    }}
    Save $b "command_number_$n"; $b.Dispose()
}
$ring.Dispose()
$b=Extract '세로선' 1110 700 58 500 3 360
# 양끝의 알파를 감쇠해 다른 높이에서도 갑자기 끊기지 않게 한다.
for($py=0;$py -lt 360;$py++) { for($px=0;$px -lt 3;$px++) {
    $c=$b.GetPixel($px,$py)
    $fade=[Math]::Min(1,[Math]::Min($py/55.0,(359-$py)/55.0))
    $b.SetPixel($px,$py,[System.Drawing.Color]::FromArgb([int]($c.A*$fade),$c.R,$c.G,$c.B))
}}
Save $b 'command_line'; $b.Dispose(); $atlas.Dispose()
Write-Output '투명 PNG 11개 추출 완료'
