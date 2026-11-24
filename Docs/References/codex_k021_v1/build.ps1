# 기존 그림은 읽기만 하며 이 카드의 시안 자료만 다시 만듭니다.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Texture($name) {
    $path = Join-Path $root "Game/Assets/_Project/Art/Battle/Resources/BattleArt/World/$name.png"
    return 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))
}
$grass = Texture 'field_grass_albedo'
$stone = Texture 'walkway_stone_albedo'
$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="810" viewBox="0 0 1440 810" role="img" aria-labelledby="mapTitle mapDesc">
<title id="mapTitle">북쪽을 위로 둔 전투 준비 지도 배치 예시</title><desc id="mapDesc">성 가운데 마당을 두고 네 성벽에 병력 표식이 있습니다. 지형과 거리 표현은 축척이 정해지지 않은 설명용 그림입니다.</desc>
<defs><pattern id="grass" width="256" height="256" patternUnits="userSpaceOnUse"><image href="__GRASS__" width="256" height="256"/></pattern><pattern id="stone" width="128" height="128" patternUnits="userSpaceOnUse"><image href="__STONE__" width="128" height="128"/></pattern><radialGradient id="light"><stop stop-color="#8b9b6266"/><stop offset="1" stop-color="#172b2b99"/></radialGradient><pattern id="grid" width="44" height="44" patternUnits="userSpaceOnUse"><path d="M44 0H0V44" fill="none" stroke="#c4cda0" stroke-opacity=".055"/></pattern><filter id="shadow"><feDropShadow dx="6" dy="10" stdDeviation="6" flood-color="#0a160d" flood-opacity=".7"/></filter><g id="flag"><path d="M0 19V-8L19-5 17 6 0 4" stroke="#c9ddbe" fill="#76a69a" stroke-width="1.5"/><path d="M-4 20H5" stroke="#dbd5b8" stroke-width="2"/></g><g id="hill"><ellipse rx="85" ry="35" fill="#374c37" stroke="#84925c" stroke-opacity=".3"/><ellipse cy="-5" rx="63" ry="25" fill="#445b3d"/><path d="M-58 4Q-8-30 48-1M-40 12Q4-13 40 8" fill="none" stroke="#9aab6d" stroke-opacity=".25"/></g><g id="tower"><rect x="-22" y="-22" width="44" height="44" fill="#566056" stroke="#a5aa8b" stroke-width="3"/><rect x="-15" y="-15" width="30" height="30" fill="#39483d" stroke="#7d876c"/><path d="M-20-19H20M-19 19H20M-19-19V19M19-19V19" stroke="#c1bda0" stroke-dasharray="6 5" stroke-width="5"/></g></defs>
<rect width="1440" height="810" fill="url(#grass)"/><rect width="1440" height="810" fill="#243b2d" opacity=".5"/><rect width="1440" height="810" fill="url(#light)"/><rect width="1440" height="810" fill="url(#grid)"/>
<path d="M710 810L708 471M720 337L735 0" stroke="#b0a778" stroke-width="40" opacity=".12"/>
<g opacity=".8"><use href="#hill" x="430" y="233"/><use href="#hill" x="1010" y="505"/><use href="#hill" x="1000" y="220"/><use href="#hill" x="380" y="628"/></g>
<path d="M655 158L720 195 785 158" fill="none" stroke="#b8846b" stroke-width="3" opacity=".6"/>
<g fill="none" stroke="#d5c49a" opacity=".22"><circle cx="720" cy="402" r="240" stroke-dasharray="3 9"/><circle cx="720" cy="402" r="307" stroke-dasharray="2 11"/></g>
<g filter="url(#shadow)"><rect x="611" y="304" width="218" height="218" fill="#2d4431" stroke="#a4aa8c" stroke-width="3"/><path d="M611 304H829V522H611Z" fill="none" stroke="url(#stone)" stroke-width="27"/><path d="M611 300H829" stroke="#e4c98b" stroke-width="5"/><path d="M610 326H830M610 500H830M633 304V522M807 304V522" stroke="#2b3b2c" stroke-width="2"/><path d="M611 290H829M611 536H829M597 304V522M843 304V522" stroke="#a0a18a" stroke-width="9" stroke-dasharray="11 8"/><use href="#tower" x="604" y="297"/><use href="#tower" x="836" y="297"/><use href="#tower" x="604" y="529"/><use href="#tower" x="836" y="529"/>
<rect x="685" y="374" width="70" height="60" fill="#53634c" stroke="#8e9578"/><path d="M678 374L720 344 762 374Z" fill="#3b4a3c" stroke="#9d9f7d" stroke-width="2"/><rect x="713" y="409" width="14" height="25" fill="#223629"/><circle cx="720" cy="463" r="7" fill="#b398cb" stroke="#e0c6f6"/><path d="M695 468L700 456 705 468" fill="#d5b16b" stroke="#f6dc9e"/>
</g>
<g font-family="Malgun Gothic,sans-serif" text-anchor="middle" fill="#eddaac" font-size="25"><text x="720" y="254">북</text><text x="891" y="421">동</text><text x="720" y="573">남</text><text x="548" y="421">서</text></g>
<g font-family="Malgun Gothic,sans-serif" font-size="12" fill="#e8e5ce"><g transform="translate(653 327)"><use href="#flag"/><text x="25" y="8">궁수 10</text></g><g transform="translate(745 327)"><use href="#flag"/><text x="25" y="8">총병 10</text></g><g transform="translate(642 489)"><use href="#flag"/><text x="25" y="8">궁수 10</text></g><g transform="translate(741 489)"><use href="#flag"/><text x="25" y="8">총병 10</text></g><g transform="translate(617 391)"><use href="#flag"/></g><g transform="translate(806 391)"><use href="#flag"/></g></g>
<g fill="#c5ba8c" stroke="#273b2d" stroke-width="2"><path d="M652 304v-18h9v18zM696 304v-18h9v18zM740 304v-18h9v18zM784 304v-18h9v18z"/></g>
<g font-family="Malgun Gothic,sans-serif" text-anchor="middle" fill="#c9d1af" font-size="11"><text x="720" y="451">성 안 마당</text><text x="933" y="562">언덕 · 배치 예시</text><text x="720" y="611">거리·축척은 설명용</text></g>
</svg>
'@
$svg = $svg.Replace('__GRASS__', $grass).Replace('__STONE__', $stone)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'map_layout.source.svg'), $svg, $utf8)
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'preview.source.html'))
$html = $source.Replace('__MAP__', $svg)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'preview.html'), $html, $utf8)
[IO.File]::WriteAllText((Join-Path $root '_HTML/codex_K-021_지도준비창_v1.html'), $html, $utf8)
Write-Output '지도 SVG·독립 HTML 2개 생성 완료'
