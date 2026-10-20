$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
$utf8=New-Object Text.UTF8Encoding($true)
function Embed($p) { 'data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($p)) }
$screens=@(
 @{name='main';after='after_main';title='메인';note='설계 5장 메인 · 크림 판을 없애고 왼쪽 어둠에 한글 제목과 출정 단추 하나를 놓았습니다. K-083 배경을 쓰고 이어하기·설정·종료는 판 없이 한 열로 정리했습니다.'},
 @{name='story';after='after_story_paper';title='스토리 · 낡은 종이';note='설계 4.3·4.4 및 5장 스토리 · 33px 본문, 약 51px 줄 간격, 45px 이름표. K-071 궁수 초상은 원본 64×96을 정확히 3배로 표시했습니다. 고정 모서리·테두리 안에 24점 무늬를 반복하여 가로 늘어짐을 피했습니다.'},
 @{name='story';after='after_story_wood';title='스토리 · 어두운 나무';note='설계 4.2의 기본 판 재질을 비교하는 두 번째 안입니다. 인물·대사·위치·크기는 종이 안과 같고 재질과 글자색만 바꿨습니다.'},
 @{name='result';after='after_result';title='전투 결과';note='설계 4.6 및 5장 결과 · 보고서 점유율 61.9%. 승리 봉랍은 지름 192px로 기존 약 90px의 두 배 수준입니다. 잃은 병사 −1을 옆에 66px로 배치하고, 다음 읽기로 성벽 네 면의 방향·수치·색, 병종별 아이콘과 막대, 주인공·마법사 초상을 놓았습니다.'}
)
$sections='';$assets=@()
foreach($s in $screens){
 $before=Join-Path $root ('Docs/References/ui_redesign_v2/now_'+$s.name+'.png')
 $after=Join-Path $PSScriptRoot ($s.after+'.png')
 $a=[Drawing.Bitmap]::FromFile($before);$b=[Drawing.Bitmap]::FromFile($after)
 $comparison=New-Object Drawing.Bitmap(3840,1134);$g=[Drawing.Graphics]::FromImage($comparison)
 $g.Clear([Drawing.ColorTranslator]::FromHtml('#171813'));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor;$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
 $g.DrawImage($a,[Drawing.Rectangle]::new(0,54,1920,1080));$g.DrawImageUnscaled($b,1920,54)
 $f=[Drawing.Font]::new('Malgun Gothic',27,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
 $g.DrawString('현재 화면',$f,[Drawing.Brushes]::White,24,5);$g.DrawString('기대 화면 · '+$s.title,$f,[Drawing.Brushes]::White,1944,5)
 $compPath=Join-Path $PSScriptRoot ('compare_'+$s.after+'.png');$comparison.Save($compPath,[Drawing.Imaging.ImageFormat]::Png)
 $g.Dispose();$f.Dispose();$comparison.Dispose();$a.Dispose();$b.Dispose()
 $sections+='<section><h2>'+$s.title+'</h2><p>'+$s.note+'</p><a class="image" href="'+(Embed $compPath)+'" download="compare_'+$s.after+'.png"><img src="'+(Embed $compPath)+'" alt="'+$s.title+' 현재 화면과 기대 화면 나란히 비교"></a><details><summary>기대 화면 크게 보기</summary><img class="after" src="'+(Embed $after)+'" alt="'+$s.title+' 기대 화면 1920×1080"></details></section>'
}
$paper=Embed (Join-Path $PSScriptRoot 'after_story_paper.png');$wood=Embed (Join-Path $PSScriptRoot 'after_story_wood.png')
$html=@"
<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-084 · 화면 다시 설계 2판 기대 사진</title>
<style>*{box-sizing:border-box}body{margin:0;background:#141813;color:#faebc9;font:17px/1.7 'Malgun Gothic',sans-serif}main{max-width:1920px;margin:auto;padding:36px 24px 80px}h1{font-size:34px;line-height:1.3}h2{font-size:26px;margin:0 0 12px;color:#e4c481}h3{font-size:20px}p{max-width:1100px}a{color:#e4c481}section{margin-top:56px}img{display:block;width:100%;height:auto;image-rendering:pixelated}.image{display:block}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}figure{margin:0}figcaption{padding:8px 0}.note{border-left:4px solid #c6a251;padding:8px 18px}.after{max-width:1920px}details{margin-top:12px}summary{cursor:pointer;color:#e4c481;padding:8px 0}button{font:inherit;background:#35251c;color:#faebc9;border:2px solid #b1945c;padding:10px 18px;cursor:pointer}button[aria-pressed=true]{background:#c6a251;color:#261910}body.actual main{max-width:none}body.actual .after{width:1920px;max-width:none}body.actual details{overflow:auto}li{margin:8px 0}.status{color:#d2b570} @media(max-width:700px){main{padding:24px 12px}h1{font-size:27px}.pair{grid-template-columns:1fr}}
</style><main><p class="status">K-084 · 게임 미연결 · 구현 전 배치와 재질 검토</p><h1>화면 다시 설계 2판<br>메인 · 스토리 · 전투 결과</h1><p>왼쪽은 현재 캡처, 오른쪽은 기대 화면입니다. 비교 그림을 누르면 원본 비교 PNG를 저장합니다. 각 화면 아래에서 기대 화면만 펼쳐 볼 수 있습니다.</p><p class="note">스토리는 <strong>낡은 종이 / 어두운 나무</strong> 중 선택이 필요합니다. 글꼴은 오프라인 대체인 <strong>맑은 고딕 Bold</strong>입니다. 최종 도트 글꼴과 제목 글씨는 별도 확정 대상입니다.</p><button id="actual" aria-pressed="false">기대 화면을 원래 크기로 보기</button>
$sections
<section><h2>스토리 재질을 같은 크기로 비교</h2><div class="pair"><figure><img src="$paper" alt="종이 안"><figcaption>가안 · 낡은 종이와 짙은 먹 글</figcaption></figure><figure><img src="$wood" alt="나무 안"><figcaption>나안 · 어두운 나무와 밝은 글</figcaption></figure></div><p>두 안 모두 초상·배경·글자가 화면상 3px 격자를 따릅니다. 초상이 작아진 것은 서로 다른 확대 배율을 없앴기 때문입니다.</p></section>
<section><h2>검사한 내용</h2><p>기대 화면 4장 모두 1920×1080 PNG, 640×360 원본의 정확한 최근접 3배입니다. 전체 화면에 부분 투명도 없이 불투명하게 합성했습니다. 배경은 640×360으로 최근접 축소 후 중앙값 분할 방식으로 256색 이하로 줄였습니다. 글·테두리·아이콘·초상도 이 격자에서 합성했습니다.</p><p>실제 글자 위치의 바탕과 측정한 최소 대비: 종이 본문·결과 수치 <strong>6.41:1</strong>, 나무 위 글 <strong>10.32:1</strong>, 황동 출정 <strong>7.07:1</strong>. 제목 음영·외곽선은 글자 바탕으로 분리하여 측정했습니다. 전체 측정값은 제작 폴더의 contrast.tsv에 있습니다.</p><p>이 카드는 정지 화면 목업이므로 모션 프레임·발선·피벗과 애니메이션은 해당하지 않습니다. Unity를 열거나 실행하지 않았습니다.</p></section>
<section><h2>아쉬운 점</h2><ul><li>K-083 로고는 영문이므로 한글 화면 지시에 맞춰 그대로 넣지 않았습니다. 한글 제목은 황동 양각 조판이며 최종 전용 손글씨 그림보다 개성이 부족합니다.</li><li>맑은 고딕 Bold를 원본 격자에서 이진 렌더링했습니다. 실제 도트 글꼴이 아니므로 글꼴 확정 후 다시 비교해야 합니다.</li><li>배경은 한 격자로 맞췄지만 생성 그림 안의 큰 덩어리와 세부의 묘사 밀도까지 수작업 도트처럼 통일하지는 못했습니다.</li><li>성벽 최대 체력 1000은 북쪽 피해 −50을 설명하기 위한 예시입니다. 원본 캡처에는 최대 체력이 없으며 실제 전투 데이터가 아닙니다.</li><li>대사창과 보고서의 종이·나무는 절차적으로 만든 검토용 재질입니다. 9칸 분할용 게임 에셋이나 구현 코드는 아닙니다.</li><li>승리 상태 한 장만 비교했습니다. 패배 문구·전환 움직임·실제 입력과 해상도 대응은 게임 연결 후 검증해야 합니다.</li></ul></section></main><script>document.querySelector('#actual').addEventListener('click',function(){const on=document.body.classList.toggle('actual');this.setAttribute('aria-pressed',String(on));this.textContent=on?'화면 너비에 맞춰 보기':'기대 화면을 원래 크기로 보기';});</script></html>
"@
$htmlPath=Join-Path $root '_HTML/6_그림_화면/codex_K-084_UI2판_기대사진1_v1.html'
[IO.File]::WriteAllText($htmlPath,$html,$utf8)
$manifest=[ordered]@{card='K-084';type='정지 화면 목업 · 게임 에셋 아님';native_size=@(640,360);screen_size=@(1920,1080);scale=3;filter='nearest-neighbor';font='Malgun Gothic Bold';font_sizes_native=@(22,15,11);font_sizes_screen=@(66,45,33);story_line_height=51;panel_center_tile_native=@(24,24);fixed_border_native=3;report_rect_screen=@(252,87,1416,906);report_coverage_percent=61.87;seal_diameter_screen=192;frame_count=1;frame_order=@('static');pivot=$null;foot_baseline=$null;background_quantization='640×360 nearest-neighbor + median-cut 256 colors, then exact 3x';portraits='K-071 native 64×96 at 3x; result busts cropped at native scale';title='한글 양각 조판, K-083 영문 로고 제외';sources=@('Docs/GDD/UI_Redesign_v2.md','Docs/GDD/UI_NoSlop.md','Docs/References/ui_redesign_v2/now_main.png','Docs/References/ui_redesign_v2/now_story.png','Docs/References/ui_redesign_v2/now_result.png','Docs/References/codex_k083_v1/main_menu_keyart.png','Docs/References/codex_k071_v1/','Docs/References/codex_k067_v1/','Game/Assets/_Project/Art/UI/Resources/ScreenArt/main.png','Game/Assets/_Project/Art/UI/Resources/ScreenArt/victory.png');files=@()}
Get-ChildItem $PSScriptRoot -Filter '*.png' | ForEach-Object {$im=[Drawing.Bitmap]::FromFile($_.FullName);$manifest.files+=@{file=$_.Name;size=@($im.Width,$im.Height)};$im.Dispose()}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'manifest.json'),($manifest|ConvertTo-Json -Depth 8),$utf8)
Write-Output $htmlPath
