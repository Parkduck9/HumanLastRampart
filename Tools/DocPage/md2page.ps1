# 설계 문서(마크다운)를 사용자용 웹 문서 한 장으로 바꾼다 (_HTML/2_설계 모양과 같은 꾸밈).
# 쓰는 법: powershell -NoProfile -ExecutionPolicy Bypass -File Tools\DocPage\md2page.ps1 -In <md> -Out <html> [-Title <제목>]
# 지원: # 제목, 목록(-, 1.), 표(|), 코드 묶음(```), **굵게**, `코드`, 빈 줄 문단. 그 밖은 문단 글로 둔다.
param([Parameter(Mandatory)][string]$In, [Parameter(Mandatory)][string]$Out, [string]$Title)

$lines = [System.IO.File]::ReadAllLines((Resolve-Path $In), [System.Text.Encoding]::UTF8)

function Esc([string]$s) { return $s.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;') }
function Inline([string]$s) {
    $s = Esc $s
    $s = [regex]::Replace($s, '`([^`]+)`', '<code>$1</code>')
    $s = [regex]::Replace($s, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
    return $s
}

$body = New-Object System.Text.StringBuilder
$nav = New-Object System.Text.StringBuilder
$list = $null; $inCode = $false; $table = $false; $h2 = 0; $first = $null

function CloseList { if ($script:list) { [void]$script:body.Append("</$($script:list)>`n"); $script:list = $null } }
function CloseTable { if ($script:table) { [void]$script:body.Append("</table></div>`n"); $script:table = $false } }

foreach ($raw in $lines) {
    $line = $raw.TrimEnd()
    if ($line.StartsWith('```')) {
        CloseList; CloseTable
        if ($inCode) { [void]$body.Append("</code></pre>`n"); $inCode = $false } else { [void]$body.Append('<pre><code>'); $inCode = $true }
        continue
    }
    if ($inCode) { [void]$body.Append((Esc $line) + "`n"); continue }
    if ($line -match '^\|') {
        CloseList
        $cells = $line.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        if ($cells -join '' -match '^[-: ]+$') { continue }
        if (-not $table) {
            [void]$body.Append('<div class="tw"><table><tr>' + (($cells | ForEach-Object { '<th>' + (Inline $_) + '</th>' }) -join '') + "</tr>`n"); $table = $true
        } else {
            [void]$body.Append('<tr>' + (($cells | ForEach-Object { '<td>' + (Inline $_) + '</td>' }) -join '') + "</tr>`n")
        }
        continue
    }
    CloseTable
    if ($line -match '^(#{1,4}) (.+)$') {
        CloseList
        $level = $matches[1].Length; $text = $matches[2]
        if ($level -eq 1) { if (-not $first) { $first = $text }; [void]$body.Append("<h1>$(Inline $text)</h1>`n"); continue }
        if ($level -eq 2) { $h2++; [void]$nav.Append("<a href=`"#s$h2`">$(Esc $text)</a>"); [void]$body.Append("<h2 id=`"s$h2`">$(Inline $text)</h2>`n"); continue }
        [void]$body.Append("<h$level>$(Inline $text)</h$level>`n"); continue
    }
    if ($line -match '^(\s*)([-*]|\d+\.) (.+)$') {
        $indent = $matches[1].Length; $mark = $matches[2]; $itemText = $matches[3]
        $kind = if ($mark -match '\d') { 'ol' } else { 'ul' }   # 여기서 $matches가 덮이므로 먼저 받아 둔다
        if (-not $list) { [void]$body.Append("<$kind>`n"); $list = $kind }
        $style = if ($indent -ge 2) { ' style="margin-left:22px"' } else { '' }
        [void]$body.Append("<li$style>$(Inline $itemText)</li>`n"); continue
    }
    CloseList
    if ($line -eq '') { continue }
    [void]$body.Append("<p>$(Inline $line)</p>`n")
}
CloseList; CloseTable

if (-not $Title) { $Title = if ($first) { $first } else { [System.IO.Path]::GetFileNameWithoutExtension($In) } }
$html = $body.ToString()
# 첫 h1 바로 뒤에 목차를 넣는다
$idx = $html.IndexOf('</h1>')
if ($idx -ge 0 -and $nav.Length -gt 0) { $html = $html.Insert($idx + 5, "`n<nav>" + $nav.ToString() + "</nav>`n") }

$page = @"
<!doctype html>
<html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$(Esc $Title)</title>
<style>
:root { --bg:#f6f4ef; --surface:#fff; --surface-2:#efece4; --text:#22201c; --muted:#6b665c; --line:#ddd8cc; --accent:#8a5a2b; --warn-soft:#f6dfd9; }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) { --bg:#171614; --surface:#211f1c; --surface-2:#2a2824; --text:#ece8df; --muted:#a39d91; --line:#3a3732; --accent:#d9a066; --warn-soft:#3d2420; } }
:root[data-theme="dark"] { --bg:#171614; --surface:#211f1c; --surface-2:#2a2824; --text:#ece8df; --muted:#a39d91; --line:#3a3732; --accent:#d9a066; --warn-soft:#3d2420; }
body { margin:0; background:var(--bg); color:var(--text); font:15px/1.65 "Malgun Gothic","Apple SD Gothic Neo",sans-serif; }
main { max-width:980px; margin:0 auto; padding:24px 16px 64px; }
h1 { font-size:26px; margin:8px 0 12px; }
h2 { font-size:20px; margin:36px 0 10px; padding-top:12px; border-top:2px solid var(--line); color:var(--accent); }
h3 { font-size:16px; margin:20px 0 6px; }
nav { display:flex; flex-wrap:wrap; gap:6px; margin:12px 0 8px; }
nav a { background:var(--surface); border:1px solid var(--line); border-radius:999px; padding:3px 11px; color:var(--text); text-decoration:none; font-size:13px; }
code { background:var(--surface-2); padding:1px 5px; border-radius:4px; font-size:13px; word-break:break-all; }
pre { background:var(--surface); border:1px solid var(--line); border-radius:8px; padding:10px 12px; overflow-x:auto; }
pre code { background:none; padding:0; }
.tw { overflow-x:auto; margin:8px 0; }
table { border-collapse:collapse; width:100%; background:var(--surface); font-size:14px; }
th, td { border:1px solid var(--line); padding:6px 9px; text-align:left; vertical-align:top; }
th { background:var(--surface-2); }
ul, ol { padding-left:22px; margin:6px 0; }
</style></head>
<body><main>
$html
</main></body></html>
"@
[System.IO.File]::WriteAllText($Out, $page, (New-Object System.Text.UTF8Encoding $false))
Write-Output "만듦: $Out"
