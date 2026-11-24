$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$entries = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'manifest.json') | ConvertFrom-Json
function Embed([string]$path) { return 'data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) }
function Tile([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $source=[Drawing.Bitmap]::FromFile($path)
    $tile=$source.Clone((New-Object Drawing.Rectangle(8,8,16,16)),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stream=New-Object IO.MemoryStream
    try { $tile.Save($stream,[Drawing.Imaging.ImageFormat]::Png); return 'data:image/png;base64,'+[Convert]::ToBase64String($stream.ToArray()) }
    finally { $stream.Dispose(); $tile.Dispose(); $source.Dispose() }
}
function Escape([string]$s) { return [System.Net.WebUtility]::HtmlEncode($s) }
$images = @{}
foreach ($e in $entries) {
    $images[$e.id] = @{}
    foreach ($scale in 1,2,4) {
        $path = if ($scale -eq 1) { Join-Path $PSScriptRoot "$($e.id).png" } else { Join-Path $PSScriptRoot "preview_${scale}x/$($e.id).png" }
        $images[$e.id][$scale] = Embed $path
    }
}
function Picture([string]$id,[int]$scale,[string]$label) {
    $size = 32*$scale
    return "<img src='$($images[$id][$scale])' width='$size' height='$size' alt='$(Escape $label)' loading='eager'>"
}
$cards = New-Object Text.StringBuilder
foreach ($e in $entries) {
    $null=$cards.Append("<article class='card' data-group='$($e.group)'><header><span>$($e.group)</span><h3>$(Escape $e.name)</h3></header><div class='twins'>")
    foreach ($surface in @(@('paper','양피지'),@('wood','짙은 나무'))) {
        $null=$cards.Append("<section class='surface $($surface[0])' aria-label='$($e.name) · $($surface[1])'><p>$($surface[1])</p><div class='samples'>")
        foreach ($scale in 1,2,4) { $null=$cards.Append("<figure>$(Picture $e.id $scale ($e.name+' · '+$scale+'배'))<figcaption>${scale}배</figcaption></figure>") }
        $null=$cards.Append('</div></section>')
    }
    $detail=$e.description
    if ($e.group -eq '훈련') { $detail='계급점 '+$e.id.Substring($e.id.Length-1)+'개 · '+$detail }
    if ($e.group -eq '용병') { $detail="계약 동전 · 1명 고용 $($e.contractCost)골드 · 전투마다 급료 $($e.wage)골드" }
    $null=$cards.Append("</div><p class='description'>$(Escape $detail)</p></article>")
}
$rows = New-Object Text.StringBuilder
foreach ($surface in @(@('paper','양피지 장부'),@('wood','나무 장부'))) {
    $null=$rows.Append("<section class='ledger surface $($surface[0])'><h3>$($surface[1])</h3>")
    foreach ($id in 'item_telescope_lens','item_fire_arrows','item_shield_rail','training_archer_2','merc_gunner') {
        $e=$entries | Where-Object id -eq $id
        $sub=if ($e.group -eq '용병') { "1명 기준 · 전투마다 급료 $($e.wage)골드" } else { $e.description }
        $price=if ($e.group -eq '용병') { $e.contractCost } else { $e.price }
        $badge=if ($e.consumable -eq '1') { Picture 'badge_one_battle' 1 '한 전투만' } elseif ($e.perWall -eq '1') { Picture 'badge_per_wall' 1 '성벽별' } else { '' }
        $null=$rows.Append("<div class='shop-row'>$(Picture $id 1 $e.name)<div class='row-name'><strong>$(Escape $e.name)</strong><small>$(Escape $sub)</small></div><span class='badge'>$badge</span><span class='price'>$(Picture 'mark_gold_coin' 1 '골드')<b>$([int]$price | ForEach-Object { $_.ToString('N0') })</b></span></div>")
    }
    $null=$rows.Append('</section>')
}
$paper = Embed (Join-Path $root 'Docs/References/codex_k059_v1/panel_parchment.png')
$wood = Embed (Join-Path $root 'Docs/References/codex_k059_v1/panel_wood.png')
$paperTile = Tile (Join-Path $root 'Docs/References/codex_k059_v1/panel_parchment.png')
$woodTile = Tile (Join-Path $root 'Docs/References/codex_k059_v1/panel_wood.png')
$sheet = Embed (Join-Path $PSScriptRoot 'contact_sheet.png')
$html = @'
<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>강화·훈련·용병 아이콘 1판 | K-067</title>
<style>
:root{color-scheme:dark;--paper:#e8d9b5;--ink:#3a2a1e;--gold:#d5b16b;--wood:#3b2a1f}*{box-sizing:border-box}body{margin:0;background:#23170f;color:var(--paper);font:16px/1.7 'Malgun Gothic',sans-serif}main{max-width:1460px;margin:auto;padding:40px 28px 80px}h1,h2,h3,p,figure{margin:0}h1,h2{font-family:Batang,serif}h1{font-size:36px;line-height:1.4}h2{font-size:26px;margin-bottom:12px}h3{font-size:18px}.intro{max-width:900px;margin:12px 0 20px;color:#c8bba0}.eyebrow{color:var(--gold);letter-spacing:.12em;font-size:13px}.facts{display:flex;flex-wrap:wrap;gap:8px;margin:16px 0 30px}.facts span{border:1px solid #8a6a32;padding:4px 12px;font-size:14px}nav{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:16px}button,a{font:inherit}button{background:var(--wood);color:var(--paper);border:1px solid #8a6a32;padding:7px 20px;cursor:pointer}button[aria-pressed=true]{background:var(--gold);color:var(--ink)}button:focus-visible,a:focus-visible{outline:3px solid #f5d99c;outline-offset:4px}.count{color:#c8bba0;margin-bottom:16px}.catalogue{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}.card{border:1px solid #5e4430;background:#2b1e15;padding:16px}.card header{display:flex;align-items:center;gap:12px;margin-bottom:12px}.card header span{font-size:13px;color:var(--gold)}.twins{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.surface{border:8px solid transparent;image-rendering:pixelated}.paper{color:var(--ink);background:var(--paper);border-image:url('__PAPER__') 6 stretch;background-image:url('__PAPERTILE__');background-size:48px 48px}.wood{color:#eee4cf;background:var(--wood);border-image:url('__WOOD__') 6 stretch;background-image:url('__WOODTILE__');background-size:48px 48px}.surface>p{font-size:13px;text-align:center}.samples{display:flex;align-items:end;justify-content:center;gap:8px;padding:14px 0 4px}.samples figure{display:flex;align-items:center;flex-direction:column;flex:none}.samples img{display:block}.samples figcaption{font-size:12px;line-height:1.5;margin-top:8px}.description{color:#c8bba0;font-size:14px;margin-top:12px;min-height:24px}img{image-rendering:pixelated;object-fit:contain}section.context{margin:44px 0}.ledgers{display:grid;grid-template-columns:1fr 1fr;gap:20px}.ledger{padding:16px}.ledger h3{margin-bottom:12px}.shop-row{display:flex;align-items:center;gap:12px;padding:12px 0;border-top:1px solid #8a6a32;min-height:80px}.shop-row>img{flex:none}.row-name{flex:1;min-width:0}.row-name strong{display:block;font-size:17px}.row-name small{display:block;font-size:12px;word-break:keep-all}.price{display:flex;align-items:center;gap:4px;min-width:94px;justify-content:end;font-size:19px;white-space:nowrap}.price img,.badge img{display:block}.badge{width:32px}.note{font-size:14px;color:#c8bba0;margin-top:12px}.sheet{margin-top:40px}.sheet summary{cursor:pointer;color:var(--gold);padding:12px 0}.sheet img{display:block;max-width:100%;height:auto;margin-top:12px}.footer{border-top:1px solid #5e4430;margin-top:36px;padding-top:16px;color:#c8bba0;font-size:13px}.anchor{color:var(--gold)}[hidden]{display:none!important}@media(max-width:1190px){.catalogue{grid-template-columns:1fr}.ledgers{grid-template-columns:1fr}}@media(max-width:640px){main{padding:24px 12px}h1{font-size:28px}.twins{grid-template-columns:1fr}.card{padding:12px}.ledger{padding:8px}.shop-row{gap:8px}.price{min-width:74px}.row-name strong{font-size:15px}.badge{width:24px}.badge img{width:24px;height:24px}}
</style></head><body><main>
<p class="note">v1.1 총병 문양 수정</p><p class="eyebrow">정비 마당 · 상점과 병영</p><h1>강화·훈련·용병 아이콘 <span style="color:#d5b16b">1판</span></h1>
<p class="intro">황동과 양피지 색으로 그린 작은 장비와 병종 문양입니다. 각 그림을 양피지와 짙은 나무 위에 나란히 놓았습니다. 훈련 단계는 아래의 계급점 개수로, 용병은 오른쪽 아래 계약 동전으로 구분합니다.</p>
<div class="facts"><span>장비 11종</span><span>훈련 9종</span><span>용병 2종</span><span>공용 표시 4종</span><span>원본 32×32 · 투명 배경</span><span>1배 · 2배 · 4배</span></div>
<section class="context" aria-labelledby="context-title"><h2 id="context-title">장부 한 줄에 놓으면</h2><p class="intro">실제 품목 이름과 가격을 사용한 배치 예시입니다. 아이콘은 32px 원본 크기이며, 용병 고용비와 급료는 1명 기준입니다.</p><div class="ledgers">__ROWS__</div><p class="note">소모품에는 ‘한 전투만’, 방패 난간에는 ‘성벽별’ 표시를 함께 놓았습니다. 게임 연결 전 검토용 목업입니다.</p></section>
<h2>전체 그림 비교</h2><nav aria-label="그림 종류"><button type="button" aria-pressed="true" data-filter="전체">전체</button><button type="button" aria-pressed="false" data-filter="상점">상점</button><button type="button" aria-pressed="false" data-filter="훈련">훈련</button><button type="button" aria-pressed="false" data-filter="용병">용병</button><button type="button" aria-pressed="false" data-filter="공용 표시">공용 표시</button></nav><p class="count" role="status" aria-live="polite">26종 표시 중</p><div class="catalogue">__CARDS__</div>
<details class="sheet"><summary>전체 비교표 한 장으로 보기 · 4배 확대</summary><a class="anchor" href="__SHEET__" download="강화아이콘_전체비교.png">비교표 PNG 저장</a><img src="__SHEET__" width="1600" height="1416" alt="장비·훈련·용병·공용 표시 26종 전체 비교표"></details>
<p class="footer">각 원본의 가장자리 1px은 비워 두었습니다. 확대본은 최근접 이웃 방식으로 원본 점을 그대로 늘렸습니다. 기존 UI 키트의 양피지·나무 판과 색상 기준을 사용했습니다. 이 페이지는 인터넷이나 서버 없이 직접 열 수 있습니다.</p>
</main><script>
const buttons=[...document.querySelectorAll('[data-filter]')];const cards=[...document.querySelectorAll('.card')];buttons.forEach(button=>button.addEventListener('click',()=>{const group=button.dataset.filter;buttons.forEach(b=>b.setAttribute('aria-pressed',String(b===button)));let count=0;cards.forEach(card=>{card.hidden=group!=='전체'&&card.dataset.group!==group;if(!card.hidden)count++});document.querySelector('.count').textContent=count+'종 표시 중'}));
</script></body></html>
'@
$html=$html.Replace('__PAPER__',$paper).Replace('__WOOD__',$wood).Replace('__CARDS__',$cards.ToString()).Replace('__ROWS__',$rows.ToString()).Replace('__SHEET__',$sheet).Replace('__PAPERTILE__',$paperTile).Replace('__WOODTILE__',$woodTile)
$target=Join-Path $root '_HTML/6_그림_화면/codex_K-067_강화아이콘_v1.html'
[IO.File]::WriteAllText($target,$html,(New-Object Text.UTF8Encoding($false)))
Write-Output '서버 없이 여는 한국어 검토 페이지 제작 완료'
