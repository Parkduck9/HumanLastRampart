from pathlib import Path
import base64, json, html

ROOT = Path(__file__).resolve().parents[4]
ART = ROOT / 'Docs/References/codex_k028_v1'
NAMES = ['성벽 수리 자리', '병영', '함정 공방', '성벽 계단']
IDS = ['repair', 'barracks', 'workshop', 'stairs']
UNITS = [('archer', '궁수'), ('gunner', '총병'), ('cannon', '대포병'), ('wizard', '마법사'), ('hero', '주인공')]

def save(path, text):
    p = ART / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding='utf-8')

def data(path):
    p = Path(path)
    mime = 'image/svg+xml' if p.suffix == '.svg' else 'image/png'
    return 'data:' + mime + ';base64,' + base64.b64encode(p.read_bytes()).decode()

def svg(body, w, h):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{body}</svg>'

def text(x, y, s, size=24, color='#eee4cf'):
    return f'<text x="{x}" y="{y}" fill="{color}" text-anchor="middle" font-family="Malgun Gothic,sans-serif" font-size="{size}">{html.escape(s)}</text>'

def wall(state):
    color = ['#829b83', '#d5b16b', '#c37e70'][state]
    body = ''
    for row in range(3):
        for col in range(10):
            x = 12 + col * 29 + (14 if row == 1 else 0)
            if state == 2 and col in [4, 5, 6]:
                continue
            body += f'<rect x="{x}" y="{12+row*26}" width="27" height="24" rx="2" fill="{color}" stroke="#26352d" stroke-width="2"/>'
    if state == 1:
        body += '<path d="M172 9l-19 24 15 13-25 18 8 28 M166 46l26 10" fill="none" stroke="#362c24" stroke-width="6"/>'
    if state == 2:
        body += '<path d="M130 72l22 7-9 15-23-5z M181 48l19 6-4 17-22-4z M170 82l21-5 10 15-24 7z" fill="#c37e70" stroke="#503d36" stroke-width="3"/>'
    return body

def castle(states):
    body = '<rect x="0" y="0" width="720" height="610" rx="22" fill="#18231e"/>'
    body += '<path d="M180 160H540V465H180Z" fill="#26352d" stroke="#60745d" stroke-width="2" stroke-dasharray="7 8"/>'
    body += '<path d="M290 245h140v135H290z" fill="#394b3e" stroke="#d5b16b" stroke-width="2"/>'
    body += text(360, 310, '성 안', 28)
    for i, transform in enumerate(['translate(200 84)', 'translate(624 150) rotate(90)', 'translate(520 520) rotate(180)', 'translate(96 470) rotate(-90)']):
        body += f'<g transform="{transform}">{wall(states[i])}</g>'
    for x,y,label in [(360,55,'북'), (675,317,'동'), (360,580,'남'), (45,317,'서')]:
        body += text(x,y,label,26)
    return svg(body,720,610)

for state, name in enumerate(['intact', 'cracked', 'broken']):
    for side in ['north', 'east', 'south', 'west']:
        # 각 면은 회전 전 같은 규격이며 면별 파일을 독립 교체한다.
        save(f'result/codex_wall_{side}_{name}.svg', svg(wall(state),320,110))
save('result/codex_castle_example.svg', castle([1,0,0,1]))
save('result/codex_castle_defeat.svg', castle([1,0,0,2]))
save('result/codex_castle_intact.svg', castle([0,0,0,0]))

save('ui/codex_interact_e.svg', svg('<path d="M15 10H145V78H91L80 95 69 78H15Z" fill="#26352d" stroke="#d5b16b" stroke-width="3"/><rect x="55" y="23" width="50" height="44" rx="6" fill="#eee4cf"/>'+text(80,56,'E',32,'#26352d'),160,100))
for ident, name in zip(IDS,NAMES):
    save(f'ui/codex_name_{ident}.svg',svg('<path d="M6 7H314L302 30 314 53H6L18 30Z" fill="#26352d" stroke="#d5b16b" stroke-width="2"/>'+text(160,38,name,23),320,60))
for state,label,color in [('victory','승리','#d5b16b'),('defeat','패배','#c37e70')]:
    b=f'<path d="M10 32H140V14H660V32H790L765 71 790 110H660V130H140V110H10L35 71Z" fill="#26352d" stroke="{color}" stroke-width="3"/>'
    b+=f'<path d="M155 29H645M155 114H645" stroke="{color}"/>'+text(400,88,label,48,color)
    save(f'ui/codex_ribbon_{state}.svg',svg(b,800,144))

assets = {}
for p in ART.rglob('*'):
    if p.suffix in ['.png','.svg'] and not any(s in p.parts for s in ['originals','qa']):
        assets[str(p.relative_to(ART)).replace('\\','/')] = data(p)

def img(key, **attrs):
    more=' '.join(f'{k}="{v}"' for k,v in attrs.items())
    return f'<img src="{assets[key]}" {more}>'

stations=''.join('<div class="station" style="left:'+str(13+i*24)+'%">'+img(f'stations/codex_{ident}.png',alt=name)+'<div class="sign" style="bottom:'+str((96+[640,704,544,800][i])/1024*100+3)+'%">'+img(f'ui/codex_name_{ident}.svg',alt=name)+'</div></div>' for i,(ident,name) in enumerate(zip(IDS,NAMES)))
hero = data(ROOT / 'Game/Assets/_Project/Art/Characters/Hero/hero_walk_run_grid.png')
portraits=''.join('<div class="portrait">'+img(f'result/codex_portrait_{ident}.png',alt=name)+f'<b>{name}</b><span>'+['−12','−8','−2','양호','부상'][i]+'</span></div>' for i,(ident,name) in enumerate(UNITS))
gallery=''.join('<figure>'+img(f'stations/codex_{ident}.png',alt=name)+f'<figcaption>{name} · 높이 {ratio}배</figcaption></figure>' for ident,name,ratio in zip(IDS,NAMES,[2,2.2,1.7,2.5]))
stateGallery=''.join('<figure>'+img(f'result/codex_wall_north_{ident}.svg',alt=name)+f'<figcaption>{name}</figcaption></figure>' for ident,name in zip(['intact','cracked','broken'],['멀쩡','금감','부서짐']))
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-028 정비 마당·결과 그림</title>
<style>
:root{color-scheme:dark;--gold:#d5b16b;--ink:#eee4cf;--panel:#26352d}*{box-sizing:border-box}body{margin:0;background:#101613;color:var(--ink);font:16px/1.65 'Malgun Gothic',sans-serif}main{max-width:1500px;margin:auto;padding:32px}h1,h2{font-family:Batang,serif;font-weight:500}h1{font-size:36px;margin:8px 0}h2{color:var(--gold);margin-top:48px}.eyebrow{color:var(--gold);letter-spacing:.16em;font-size:13px}.muted{color:#aab6a9}.notice{border-left:3px solid var(--gold);padding:10px 18px;background:#1b2820}button{font:inherit;min-height:46px;background:#26352d;color:var(--ink);border:1px solid var(--gold);padding:6px 20px;cursor:pointer;border-radius:4px;margin:5px}button[aria-pressed=true]{background:#d5b16b;color:#18231e}button:focus-visible{outline:3px solid #eee4cf;outline-offset:3px}.controls{display:flex;align-items:center;flex-wrap:wrap;gap:8px;margin:14px 0}.scroll{overflow:auto;border:1px solid #59654c}.yard{position:relative;aspect-ratio:32/9;min-width:900px;overflow:hidden;background-size:100% 100%}.station{position:absolute;bottom:13%;width:21%;transform:translateX(-50%)}.station>img{width:100%;height:100%;object-fit:contain;object-position:bottom}.sign{position:absolute;bottom:106%;left:50%;transform:translateX(-50%);width:78%}.sign img{width:100%}.hero{position:absolute;left:13%;bottom:13%;width:4%;height:28.4%;background-image:var(--hero);background-size:400% 400%;background-position:0 100%;transform:translateX(-50%)}.prompt{position:absolute;bottom:44%;left:13%;width:4.5%;transform:translateX(-50%)}.prompt img{width:100%}.result{border:1px solid #5f694e;padding:24px;display:grid;grid-template-columns:1fr 1fr;gap:24px;background:radial-gradient(ellipse at 20% 20%,#354a39,#18231e 75%)}.ribbon{grid-column:1/-1;width:44%;max-width:550px;margin:auto}.castle img{width:100%}.portraits{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;align-content:center}.portrait{text-align:center;background:#26352d;border-bottom:2px solid var(--gold);padding:10px}.portrait img{width:100%;aspect-ratio:1;object-fit:contain;display:block}.portrait b,.portrait span{display:block}.portrait span{color:#c6cdb7}.result footer{grid-column:1/-1;display:flex;justify-content:space-between;align-items:center}.gallery{display:grid;grid-template-columns:repeat(4,1fr);gap:16px}.gallery figure{margin:0;text-align:center;background:repeating-conic-gradient(#233028 0% 25%,#1a231e 0% 50%) 0/24px 24px;padding:14px}.gallery img{width:100%;height:240px;object-fit:contain}.states{grid-template-columns:repeat(3,1fr)}.states img{height:100px}details{margin:20px 0;padding:14px;border:1px solid #52634f}code{color:var(--gold)}a{color:var(--gold)}.bgstrip img{width:100%;display:block}#feedback{color:var(--gold);min-height:1.7em} @media(max-width:800px){main{padding:16px}.result{grid-template-columns:1fr}.gallery{grid-template-columns:repeat(2,1fr)}.ribbon{width:80%}.portraits{grid-template-columns:repeat(3,1fr)}h1{font-size:27px}}
</style><main>
<div class="eyebrow">K-028 · 1차 시안 · 2026.10.16</div><h1>성벽 안에서, 다음 전투를 준비한다</h1><p class="notice">사용자 방향 확정 전 그림 검토용입니다. 숫자와 상태는 배치 예시이며 실제 전투 결과가 아닙니다. 게임에는 연결하지 않았습니다.</p>
<h2>3번 · 정비 마당</h2><p>왼쪽부터 수리 자리 → 병영 → 함정 공방 → 성벽 계단. 같은 높이의 길을 따라 이동합니다.</p>
<div class="controls"><button id="day" aria-pressed="true">낮</button><button id="evening" aria-pressed="false">저녁</button><span class="muted">A / D 이동 · E 장소 확인 · 아래 장소 버튼으로도 확인</span></div>
<div class="scroll"><div class="yard" id="yard">@@STATIONS@@<div class="hero" id="hero" aria-label="기존 주인공 크기 비교"></div><div class="prompt" id="prompt">@@E@@</div></div></div>
<div class="controls" id="places"></div><p id="feedback" role="status">수리 자리 앞 · E로 확인</p>
<h2>4번 · 한 장으로 보는 전투 결과</h2><p>성벽 네 면의 손상과 병력 손실, 두 영웅의 상태를 한 화면에서 읽는 배치 시안입니다.</p>
<div class="controls"><button id="victory" aria-pressed="true">승리 예시</button><button id="defeat" aria-pressed="false">패배 예시</button></div>
<div class="result"><img class="ribbon" id="ribbon" src="@@VICTORY@@" alt="승리"><div class="castle">@@CASTLE@@</div><div class="portraits">@@PORTRAITS@@</div><footer><span>예시 · 북 금감 / 서 부서짐 · 병력 손실 22명</span><button id="confirm">확인</button></footer></div><p id="resultFeedback" role="status" class="muted">패배 결과로 진입하는 실제 흐름은 별도 확정이 필요합니다.</p>
<h2>분리 그림과 기준점</h2><p>장소 그림은 각각 1024×1024 투명 PNG, 공통 기준점은 왼쪽 위 기준 (512, 928)입니다. 검토 배치의 주인공 키를 320으로 잡으면 장소 실루엣 높이는 640 / 704 / 544 / 800입니다.</p><div class="gallery">@@GALLERY@@</div>
<h2>성벽 겉칠 · 윤곽으로 구분하는 세 단계</h2><div class="gallery states">@@STATES@@</div><p class="muted">4면 × 3상태의 개별 SVG. 면 색은 손상 단계의 보조 신호입니다. 수치 경계와 게임 연결 방식은 정하지 않았습니다.</p>
<details><summary>배경 원본 구도 확인 · 장소 없는 긴 띠</summary><div class="bgstrip">@@DAY@@<p>낮</p>@@EVENING@@<p>저녁</p></div></details>
<details><summary>검토할 항목과 남은 결정</summary><p>마당의 밝기·장소 비율·한 화면 노출 범위, 결과 화면에서 손상과 손실이 읽히는지 확인합니다. 배경의 좌우 연결부는 완전 무봉제 반복을 보증하지 않습니다. 실제 카메라·이동 거리·상호작용 범위·손상 단계 경계·패배 결과 진입 흐름·초상 부상/사망 표시 규칙은 확정 전입니다. 작은 초상의 도트 밀도와 캐릭터 동일성도 사용자 검토가 필요합니다.</p><p>원본·생성 프롬프트·편집 SVG·재출력 도구는 <code>Docs/References/codex_k028_v1/</code>에 있습니다. 이 페이지는 모든 그림을 내장하며 서버 없이 열립니다.</p></details>
</main><script>
const A=@@ASSETS@@;const names=['성벽 수리 자리','병영','함정 공방','성벽 계단'];let x=13,near=0,held={};const yard=document.getElementById('yard'),hero=document.getElementById('hero'),prompt=document.getElementById('prompt');
document.documentElement.style.setProperty('--hero','url("@@HERO@@")');
function time(t){yard.style.backgroundImage='url("'+A['backgrounds/codex_yard_'+t+'_3840x1080.png']+'")';['day','evening'].forEach(n=>document.getElementById(n).setAttribute('aria-pressed',n===t));}time('day');
document.getElementById('day').onclick=()=>time('day');document.getElementById('evening').onclick=()=>time('evening');
function move(){hero.style.left=x+'%';near=Math.round((x-13)/24);near=Math.max(0,Math.min(3,near));const close=Math.abs(x-(13+near*24))<7;prompt.hidden=!close;prompt.style.left=(13+near*24)+'%';document.getElementById('feedback').textContent=close?names[near]+' 앞 · E로 확인':'A / D로 장소 앞으로 이동';}
function interact(){if(!prompt.hidden)document.getElementById('feedback').textContent=names[near]+' · '+['성벽 수리','병력 모집·보충','함정 구매·설치','다음 전투'][near]+' 그림 확인 (게임 기능 미연결)';}
names.forEach((n,i)=>{let b=document.createElement('button');b.textContent=n;b.onclick=()=>{x=13+i*24;move();interact()};document.getElementById('places').appendChild(b)});
window.addEventListener('keydown',e=>{if(e.target.matches('button,input,textarea,select'))return;const k=e.key.toLowerCase();if(['a','d','e'].includes(k)){e.preventDefault();held[k]=true;if(k==='e'&&!e.repeat)interact();}});window.addEventListener('keyup',e=>held[e.key.toLowerCase()]=false);window.addEventListener('blur',()=>held={});let last=0;function tick(t){const dt=Math.min((t-last)/1000,.05);last=t;if(held.a||held.d){x=Math.max(5,Math.min(95,x+((held.d?1:0)-(held.a?1:0))*15*dt));move();}requestAnimationFrame(tick)}requestAnimationFrame(tick);
function result(s){document.getElementById('ribbon').src=A['ui/codex_ribbon_'+s+'.svg'];document.getElementById('ribbon').alt=s==='victory'?'승리':'패배';['victory','defeat'].forEach(n=>document.getElementById(n).setAttribute('aria-pressed',n===s));document.getElementById('resultFeedback').textContent=s==='defeat'?'패배 띠 비교용입니다. 패배 결과 진입 흐름은 미확정입니다.':'승리 결과 배치 예시입니다. 실제 결과 데이터와 연결하지 않았습니다.';}document.getElementById('victory').onclick=()=>result('victory');document.getElementById('defeat').onclick=()=>result('defeat');document.getElementById('confirm').onclick=()=>document.getElementById('resultFeedback').textContent='그림 확인 예시입니다. 화면 전환·저장은 실행하지 않습니다.';
</script></html>'''
page=page.replace('각각 1024×1024','각각 1536×1024').replace('(512, 928)','(768, 928)')
page=page.replace('bottom:13%;width:21%','bottom:4.074%;width:40%;height:94.815%').replace('width:78%','width:42%')
page=page.replace('bottom:13%;width:4%;height:28.4%','bottom:10.037%;width:6.667%;height:35.556%')
page=page.replace('북 금감 / 서 부서짐','북 금감 / 서 금감').replace('<footer><span>','<footer><span id="resultCaption">')
page=page.replace('function result(s){',"function result(s){document.getElementById('castle').src=A['result/codex_castle_'+(s==='defeat'?'defeat':'example')+'.svg'];document.getElementById('castle').alt=s==='defeat'?'북 금감, 동 남 멀쩡, 서 부서짐':'북 서 금감, 동 남 멀쩡';document.getElementById('resultCaption').textContent=s==='defeat'?'예시 · 북 금감 / 서 부서짐 · 병력 손실 22명':'예시 · 북 금감 / 서 금감 · 병력 손실 22명';")
replacements={'STATIONS':stations,'E':img('ui/codex_interact_e.svg',alt='E 상호작용'),'VICTORY':assets['ui/codex_ribbon_victory.svg'],'CASTLE':img('result/codex_castle_example.svg',id='castle',alt='북 서 금감, 동 남 멀쩡'),'PORTRAITS':portraits,'GALLERY':gallery,'STATES':stateGallery,'DAY':img('backgrounds/codex_yard_day_3840x1080.png',alt='낮 배경'),'EVENING':img('backgrounds/codex_yard_evening_3840x1080.png',alt='저녁 배경'),'ASSETS':json.dumps(assets,ensure_ascii=False),'HERO':hero}
save('source/codex_review.template.html',page)
for key,val in replacements.items():
    page=page.replace('@@'+key+'@@',val)
dest=ROOT/'_HTML/codex_K-028_정비마당_결과그림_v1.html'
dest.parent.mkdir(exist_ok=True)
dest.write_text(page,encoding='utf-8')
print(json.dumps({'검토페이지':str(dest),'내장그림':len(assets)},ensure_ascii=False))
