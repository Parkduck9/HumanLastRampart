from pathlib import Path
import base64
import hashlib
import json
import struct

folder = Path(__file__).resolve().parent
repo = folder.parents[2]
images = []
checks = []
for frame in range(1, 7):
    path = folder / 'qa' / 'attempt3' / 'captures' / f'{frame:02}_ignite_contact_frame_{frame}.png'
    data = path.read_bytes()
    size = struct.unpack('>II', data[16:24])
    assert size == (1920, 1080), (path, size)
    encoded = base64.b64encode(data).decode('ascii')
    assert base64.b64decode(encoded) == data
    images.append('data:image/png;base64,' + encoded)
    checks.append({'frame': frame, 'source': str(path.relative_to(folder)), 'width': size[0], 'height': size[1],
                   'sha256': hashlib.sha256(data).hexdigest(), 'embedded_bytes_identical': True})

page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>K-086 실제 카메라 점화 v5</title>
<style>body{margin:0;background:#111821;color:#e9edf2;font:17px/1.7 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:25px}h1{font-size:27px;line-height:1.3}.notice{background:#263241;padding:16px;border-left:4px solid #f1bc57}.warn{color:#ffd17c}button{background:#33485e;border:1px solid #7e97af;border-radius:7px;color:white;padding:11px 17px;font:inherit;cursor:pointer}button:focus-visible{outline:3px solid #ffd17c}nav{display:flex;flex-wrap:wrap;gap:9px;align-items:center;margin:15px 0}#frameLabel{min-width:125px;font-weight:bold}.stage{overflow:auto;border:1px solid #415267;background:#080b10}#shot{display:block;width:100%;height:auto;image-rendering:pixelated}.stage.full #shot{width:1920px;max-width:none}.small{font-size:14px;color:#c1cbd7}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:15px}.card{background:#202b38;padding:16px}a{color:#b4d4ff}</style>
<main><h1>K-086 v5 · 실제 카메라 점화 6칸</h1><p class="notice"><strong>기술 시험 1/1 통과 · 실제 접촉은 미완료</strong><br>4번 사진에서 불꽃이 도화선 고리보다 아래에 보입니다. 아래 원본 사진을 직접 비교해 주세요. 접촉 성공으로 처리하지 않았습니다.</p>
<nav aria-label="사진 재생"><button id="play" aria-pressed="false">재생</button><button id="previous">이전</button><button id="next">다음</button><button id="fourth">4번 칸 보기</button><button id="zoom" aria-pressed="false">1:1 크기 확대</button><span id="frameLabel" aria-live="polite"></span></nav>
<div class="stage" id="stage"><img id="shot" alt="실제 전투 카메라 점화 4번 칸"></div>
<p class="small">실제 1920×1080 PNG 여섯 장을 바꾸지 않고 내장했습니다. 재생은 정지 촬영 사진의 슬라이드이며 시뮬레이션 연속 동영상이 아닙니다. 방향키 ← →로도 이동합니다. 서버·외부 파일이 필요 없습니다.</p>
<div class="grid"><section class="card"><strong>촬영 조건</strong><p>실제 BattleCameraRig A: 거리 3m · 높이 2.1m · 내려보기 24도 · 화각 40도. 첫 대포로 주인공의 가로 위치만 옮겼습니다. 확대 촬영용 새 카메라는 쓰지 않았습니다.</p></section><section class="card"><strong>통과한 기술 검사</strong><p>2592×384 시트 · 432×384 여섯 칸 · 발 288·360 · 실제 그리기 버퍼 80~85 · 전투 시간 0 유지 · 사진 6장 저장.</p></section><section class="card"><strong class="warn">남은 시각 검토</strong><p>4번 도화선 접촉, 3→4 횃불 길이와 자세의 연속성. 깊이 차이가 원인일 가능성이 있으나 투영 검사를 하기 전에는 보정량을 확정하지 않습니다.</p></section></div>
<p class="small">부모 실행: 2026-10-21 · qa/attempt3/play.xml 1/1 통과. 처음 두 시도는 컴파일·준비 상태 실패로 기록했습니다. 그림은 시험 메모리에서만 교체했고 Game PNG·정의 에셋에는 적용하지 않았습니다. 전투 시간이 멈춰도 연기 등 실시간 표시 효과는 달라질 수 있습니다.</p></main>
<script>const frames=__FRAMES__;let index=3,timer=null;const $=id=>document.getElementById(id);function show(){ $('shot').src=frames[index];$('shot').alt=`실제 전투 카메라 점화 ${index+1}번 칸`;$('frameLabel').textContent=`${index+1} / 6번 칸`; }function stop(){if(timer!==null)clearInterval(timer);timer=null;$('play').textContent='재생';$('play').setAttribute('aria-pressed','false');}function move(amount){stop();index=(index+amount+6)%6;show();}$('play').onclick=()=>{if(timer!==null){stop();return;}$('play').textContent='정지';$('play').setAttribute('aria-pressed','true');timer=setInterval(()=>{index=(index+1)%6;show();},450);};$('previous').onclick=()=>move(-1);$('next').onclick=()=>move(1);$('fourth').onclick=()=>{stop();index=3;show();};$('zoom').onclick=()=>{const full=$('stage').classList.toggle('full');$('zoom').setAttribute('aria-pressed',String(full));$('zoom').textContent=full?'화면에 맞추기':'1:1 크기 확대';};document.addEventListener('keydown',e=>{if(e.key==='ArrowLeft'){e.preventDefault();move(-1);}if(e.key==='ArrowRight'){e.preventDefault();move(1);}});show();</script></html>'''.replace('__FRAMES__', json.dumps(images))
for path in (folder / 'preview.html', repo / '_HTML' / 'codex_K086_실제카메라_v5.html'):
    path.write_text(page, encoding='utf-8')
result = {'source': 'Unity actual battle camera screenshots, attempt3', 'unity_test': {'passed': 1, 'failed': 0},
          'images': checks, 'external_resources': False, 'browser_interaction_checked': False,
          'visual_contact': 'incomplete: flame appears below fuse; manual observation only'}
(folder / 'preview_validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print('6 original PNGs embedded unchanged; self-contained HTML written twice.')
