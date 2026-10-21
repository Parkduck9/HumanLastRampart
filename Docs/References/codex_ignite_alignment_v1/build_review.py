"""실제 Unity 촬영 PNG를 변경하지 않고 자체 내장 검토 HTML을 만든다."""
from pathlib import Path
import base64
import hashlib
import json
import struct
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CAP = HERE / 'qa/attempt3/captures'
before = HERE.parent / 'codex_ignite_camera_20261021/qa/attempt3/captures/04_ignite_contact_frame_4.png'
frames = [CAP / f'{78+i:02}_ignite_contact_frame_{i}.png' for i in range(1, 7)]
presets = [frames[3], CAP / '85_ignite_contact_preset_1.png', CAP / '86_ignite_contact_preset_2.png']

def metadata(path):
    data = path.read_bytes()
    return dict(path=str(path.relative_to(ROOT)).replace('\\', '/'),
                sha256=hashlib.sha256(data).hexdigest(),
                width=struct.unpack('>I', data[16:20])[0], height=struct.unpack('>I', data[20:24])[0])

def embed(path):
    return 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode('ascii')

checks = {}
for mode in ('edit', 'play'):
    run = ET.parse(HERE / f'qa/attempt3/{mode}.xml').getroot()
    checks[mode] = {k: int(run.attrib[k]) for k in ('total', 'passed', 'failed', 'inconclusive')}

game = [ROOT / 'Game/Assets/_Project/Art/Characters/Fire/cannon_crew_ignite6.png',
        ROOT / 'Game/Assets/_Project/Art/Battle/Resources/BattleArt/Cannon/cannon_recoil.png']
record = dict(version=1, date='2026-10-21', before=metadata(before), frames=[metadata(p) for p in frames],
              presets=[metadata(p) for p in presets], game_assets=[metadata(p) for p in game], tests=checks,
              placement=dict(installed_forward_m=0.25, clearance_m=0.02, shooter_forward_m=0.23),
              limitations=['정적 접점과 화면 투영 범위 확인. 3→4 자루 길이의 동작 연속성 최종 선택은 별도.',
                           '새 이미지 생성 없음. 원본 PNG를 HTML 제작 과정에서 변경하지 않음.',
                           'FrameCost 정원 초과로 측정 전 실패. FrameTiming 초반만 측정; 성능 향상·FPS 보장 아님.'])
(HERE / 'review.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>K086 점화 배치 보정 — 실제 Unity 촬영</title><style>
*{box-sizing:border-box}body{margin:0;background:#121824;color:#e6eafa;font:16px/1.65 system-ui,sans-serif}main{max-width:1450px;margin:auto;padding:26px}h1{font-size:29px}h2{font-size:21px;margin:26px 0 10px}p{margin:8px 0}.note{color:#b4c1d9}.box{padding:18px;background:#1c2636;border:1px solid #3c4a61;border-radius:10px;margin:18px 0}.grid{display:grid;grid-template-columns:1fr 1fr;gap:18px}figure{margin:0}figcaption{margin:8px 0}img,canvas{width:100%;height:auto;display:block;background:#0b101a;image-rendering:pixelated}button{font:inherit;color:inherit;background:#30445e;border:1px solid #7285a2;border-radius:5px;padding:8px 13px;cursor:pointer}button:focus{outline:3px solid #f4c96c}.controls{display:flex;flex-wrap:wrap;gap:8px;margin:14px 0}.good{color:#b9e9be}.warn{color:#f2ce8a}table{border-collapse:collapse;width:100%}td,th{border:1px solid #49566b;padding:8px;text-align:left}@media(max-width:750px){.grid{grid-template-columns:1fr}main{padding:16px}}</style>
<main><h1>K086 점화 배치 보정</h1><p>실제 Unity A·B·C 사진기의 전후 비교입니다. 새 그림을 생성하지 않고, 승인된 점화 그림과 도화선 그림을 게임에 연결했습니다.</p>
<div class="box"><strong>앞뒤 배치: 0.25m → 0.02m 여유 → 최종 0.23m</strong><p>대포 그림은 논리 자리에서 바깥쪽으로 0.25m 이동해 그려집니다. 사격병도 같은 깊이에 두면 대포 판이 불꽃을 가려, 사격병은 거기서 0.02m 뒤로 뺐습니다. 확장 점화 시트의 사격병만 적용하며 보조병·구형 시트의 자리는 유지합니다.</p><p class="note">대포의 논리 위치·사거리·인물 키·좌우 간격은 유지했습니다. 양쪽 PNG의 기존 GUID도 유지했습니다. Unity가 .meta 플랫폼 설정을 정규화했습니다.</p></div>
<h2>4번 접촉칸 전후</h2><div class="grid"><figure><figcaption>이전 — 불꽃이 도화선보다 아래</figcaption><img id="before" alt="보정 전 실제 카메라 4번 칸"><canvas id="beforeCrop" width="600" height="450" aria-label="보정 전 대포 주변 확대"></canvas></figure><figure><figcaption>이후 — 불꽃이 보이는 접촉 배치</figcaption><img id="after" alt="보정 후 실제 카메라 4번 칸"><canvas id="afterCrop" width="600" height="450" aria-label="보정 후 대포 주변 확대"></canvas></figure></div>
<h2>실제 촬영 여섯 칸·세 시점</h2><p class="note">재생은 촬영된 여섯 정지 사진을 순환합니다. 게임 속도나 전체 애니메이션 자연스러움의 최종 승인과 구분합니다.</p><div class="controls"><button id="play">재생</button><button id="prev">이전</button><button id="next">다음</button><button id="four">4번 접촉칸</button><button data-preset="0">A 기본</button><button data-preset="1">B 이동</button><button data-preset="2">C 높게</button></div><p id="state" aria-live="polite"></p><img id="viewer" alt="선택한 점화 실제 카메라 촬영"><canvas id="detail" width="800" height="600" aria-label="선택한 촬영의 대포 주변 확대"></canvas>
<h2>검증 결과와 실제 한계</h2><table><tr><th>항목</th><th>결과</th></tr><tr><td>최종 자동 시험</td><td class="good">Edit 115 통과 + 기존 1 유보 / 실패 0. Play 47/47 통과.</td></tr><tr><td>시도 1</td><td>Unity NUnit에 없는 NonParallelizable 특성으로 컴파일 실패. 특성을 제거하고 공유 정의 복원은 유지.</td></tr><tr><td>시도 2</td><td>Play 46/47. TrapFlow가 확률 자동 폭발을 부적절하게 실패 처리. 점화 불꽃은 동일 깊이에서 대포에 가려짐.</td></tr><tr><td>시도 3</td><td>불꽃 가림 여유 0.02m와 함정 검사 수정 후 Play 47/47. A·B·C에서 불꽃 표시 및 접점 투영 범위 확인.</td></tr><tr><td>TrapFlow 개선</td><td>자동 폭발 확률을 끄지 않고 실제 소모 ID 전체가 제거되는지 확인. 비소모 함정의 표시·처음 칸·지형 보존과 전체 개수 일치 확인. 다른 지형의 대조 함정이 반드시 남아 보존 검사가 빈 검사가 되지 않게 함.</td></tr><tr><td>동작 판단</td><td class="warn">정적 접촉 확인. 3→4 자루 길이 연속성과 동작 자연스러움의 최종 선택은 별도.</td></tr><tr><td>성능 측정 한계</td><td>별도 SimProbe.FrameCost 1건은 정원 초과로 측정 전 중단. FrameTiming은 초반 평균 7.74ms이나 GPU 원시 계수 0. 전투가 22.4초에 끝나 75초 중반 조건 미도달. 성능 향상이나 FPS를 보장하지 않음.</td></tr></table><p class="note">원본 PNG·이전 촬영·실패 기록을 보존했습니다. 이 HTML에는 사진을 모두 내장해 서버 없이 열 수 있습니다. Git 작업은 하지 않았습니다.</p></main>
<script>const frames=__FRAMES__,presets=__PRESETS__,before=__BEFORE__;let index=3,timer=null;const $=id=>document.getElementById(id);function cropped(img,canvas){const c=canvas.getContext('2d');c.imageSmoothingEnabled=false;c.clearRect(0,0,canvas.width,canvas.height);c.drawImage(img,780,470,400,300,0,0,canvas.width,canvas.height)}function setImage(id,url,crop){const img=$(id);img.onload=()=>cropped(img,$(crop));img.src=url}setImage('before',before,'beforeCrop');setImage('after',frames[3],'afterCrop');function stop(){if(timer)clearInterval(timer);timer=null;$('play').textContent='재생'}function show(url,label){setImage('viewer',url,'detail');$('state').textContent=label}function frame(){show(frames[index],`A 기본 · ${index+1}/6번 칸`)}$('play').onclick=()=>{if(timer){stop();return}$('play').textContent='정지';timer=setInterval(()=>{index=(index+1)%6;frame()},220)};$('prev').onclick=()=>{stop();index=(index+5)%6;frame()};$('next').onclick=()=>{stop();index=(index+1)%6;frame()};$('four').onclick=()=>{stop();index=3;frame()};document.querySelectorAll('[data-preset]').forEach(b=>b.onclick=()=>{stop();index=3;const n=Number(b.dataset.preset);show(presets[n],['A 기본','B 이동','C 높게'][n]+' · 4번 접촉칸')});frame();</script></html>'''
html = html.replace('__FRAMES__', json.dumps([embed(p) for p in frames])).replace('__PRESETS__', json.dumps([embed(p) for p in presets])).replace('__BEFORE__', json.dumps(embed(before)))
(HERE / 'preview.html').write_text(html, encoding='utf-8')
(ROOT / '_HTML/codex_K086_점화배치보정_v1.html').write_text(html, encoding='utf-8')
print(json.dumps(dict(images=10, html_bytes=len(html.encode()), tests=checks), ensure_ascii=False))
