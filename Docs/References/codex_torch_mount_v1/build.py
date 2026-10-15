from pathlib import Path
import base64, hashlib, json, shutil, subprocess, re, sys, zipfile
from PIL import Image

CARD=Path('C:/Unity/1/HLR-cards/K-018')
HERE=CARD/'Docs/References/codex_torch_mount_v1'
HERE.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source=Path(sys.argv[1])
prompt_record=Path(sys.argv[2])
if source.resolve()!=(HERE/'torch_mount_source.png').resolve():shutil.copy2(source,HERE/'torch_mount_source.png')
im=Image.open(source).convert('RGBA')
box=im.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
assert box
crop=im.crop(box)
# Technical placement only: same generated pixels, nearest-neighbour, no repaint/alpha removal.
scale=min(64/crop.width,192/crop.height)
size=(max(1,round(crop.width*scale)),max(1,round(crop.height*scale)))
small=crop.resize((max(1,round(size[0]/4)),max(1,round(size[1]/4))),Image.Resampling.NEAREST)
body=small.resize((small.width*4,small.height*4),Image.Resampling.NEAREST)
sprite=Image.new('RGBA',(128,256))
position=(64-body.width//2,224-body.height)
sprite.paste(body,position)
sprite.save(HERE/'torch_mount_off.png')
flame_src=CARD/'Docs/References/codex_effects_v1/torch_flame_sheet.png'
shutil.copy2(flame_src,HERE/'torch_flame_existing.png')
headbox=body.crop((0,0,body.width,max(1,body.height//4))).getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
tip=[position[0]+(headbox[0]+headbox[2])//2,position[1]+4]
metadata={
 'card':'K-018','task':'T-CX-059','status':'사용자 외형 검토 대기 / Game 미연결',
 'body':{'file':'torch_mount_off.png','size':[128,256],'pivotTopLeft':[64,224],
         'visibleHeightMetersProposal':0.3,'alphaBBox':sprite.getbbox(),
         'sourceCrop':box,'nearestLogicalSize':list(small.size),'sha256':sha(HERE/'torch_mount_off.png')},
 'litState':{'method':'동일 body PNG + 기존 torch_flame 별도 레이어','bodySharedExactly':True,
             'flameFile':'torch_flame_existing.png','flameColumns':6,'flameCell':[256,256],
             'flameBaseTopLeft':tip,'previewFlameCellSize':[36,36],'fps':10,
             'sha256':sha(HERE/'torch_flame_existing.png')},
 'gameReferenceOnly':{'spacingMeters':3,'insetMeters':0.5,'flameHeightAboveWalkwayMeters':0.24},
 'placementNote':'0.3m는 받침 전체 높이 제안. 게임의 0.24m는 기존 불꽃 위치이므로 몸체 피벗/불꽃 접점을 실제 난간에 맞춰 Claude가 정한다. 기존 높이를 이 카드에서 바꾸지 않음.',
 'normalMap':None,'unityTested':False,'newGameCode':False,
 'source':{'file':'torch_mount_source.png','sha256':sha(HERE/'torch_mount_source.png'),'size':list(im.size)},
 'tool':'built-in imagegen','promptRecord':json.loads(prompt_record.read_text(encoding='utf-8-sig'))}
(HERE/'manifest.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
readme='''# K-018 낮·밤 횃불대 시안

작성: Codex / T-CX-059 / M-CL-075·076. 사용자 외형 검토 대기, Game/와 기존 Art를 수정하지 않았다.

낮에도 남는 갈색 나무 횃대·검은 철 받침·까맣게 탄 천 머리. 기존 낡은 회색 석재와 캐릭터 도트에 맞춘 제한색 시안이다. 생성 원안과 프롬프트는 함께 보관한다. 첫 원안의 흐린 가장자리는 재생성으로 보완하고, 최종은 동일 영역 자르기·최근접 축소·격자 배치만 한다. 픽셀 덧칠·배경색 제거는 하지 않았다.

## 낮과 밤의 받침은 완전히 같은 그림

torch_mount_off.png 한 장을 두 상태가 공유한다. 밤은 새로 그린 받침으로 바꾸지 않고 기존 K-012 torch_flame 그림을 별도 레이어로 더한다. 이 때문에 점화할 때 횃대 길이·철띠·벽 부착점이 달라지지 않는다. HTML의 낮/밤 참조도 같은 body PNG를 읽는다. 기존 불꽃은 6칸·10fps이며 이번 카드에서 새로 바꾸지 않았다.

## 규격안

- 받침 칸 128×256, 아래쪽 부착 기준점 (64,224), 실제 그림 높이 약 0.3m 제안.
- 불꽃: 기존 1536×256/6칸, 각256×256, 아래 가운데. HTML에서는 36×36칸으로 축소해 몸체 머리에 둔다. 정확 접점은 manifest의 flameBaseTopLeft.
- 기존 BattleLights 공개 값: 성벽 바깥 난간 안쪽0.5m·간격3m·불꽃은 통로 위0.24m. **0.3m 몸체 높이와 0.24m 불꽃 높이는 같은 뜻이 아니다.** 실제3D 부착 때 피벗/바닥 아래 들어갈 부분·난간 가림을 Claude가 확인한다. 새 배치 규칙 확정 아님.
- 낮에는 몸체만, 밤에는 같은 몸체+기존 불꽃. 받침은 밤의 불꽃 켜기/끄기와 분리해서 항상 그리는 연결안. 실제 게임의 명암·점광원·날씨 반응은 HTML에서 검증하지 않았다.

## 확인할 것

사용자는 철제받침 모양·갈색/회색 대비·작은 크기의 실루엣을 고른다. 확정 뒤 Claude가 Game으로 복사/.meta 및 임포트 설정/실제3D 크기·가림·0.24m불꽃 접점을 확인한다. HTML은 레이어 정렬과 재생을 확인하는 도구이며 Unity 화면 캡처가 아니다. 원본 이미지는 비트맵이고 UI 비교판의 배경/기준선만 CSS다.
'''
(HERE/'README.md').write_text(readme,encoding='utf-8')
data=lambda p:'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
template='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-018 낮·밤 횃불대</title>
<style>*{box-sizing:border-box}body{margin:0;background:#171d22;color:#ede5d4;font:16px/1.7 'Malgun Gothic',sans-serif}main{max-width:1100px;margin:auto;padding:30px 22px}h1{font-size:30px}.note{border-left:3px solid #c6a763;background:#293036;padding:14px}button,select,a.dl{font:inherit;color:inherit;background:#303b46;border:1px solid #847750;padding:8px 12px;border-radius:4px;cursor:pointer}nav{display:flex;flex-wrap:wrap;gap:9px;margin:20px 0}.compare{display:grid;grid-template-columns:1fr 1fr;gap:18px}.panel{background:#27313b;border:1px solid #4b5864;border-radius:5px;padding:18px;overflow:hidden}.stage{height:310px;display:grid;place-items:center;background:repeating-conic-gradient(#42474b 0% 25%,#363c41 0% 50%) 0/24px 24px}.art{position:relative;width:128px;height:256px;transform-origin:50% 75%;image-rendering:pixelated}.body{position:absolute;width:128px;height:256px;inset:0}.flame{position:absolute;width:36px;height:36px;background-size:216px 36px;image-rendering:pixelated;pointer-events:none}.off .flame{display:none}.stage.day{background:repeating-conic-gradient(#d1d0bc 0% 25%,#bbbdaa 0% 50%) 0/24px 24px}.compact .art{transform:scale(.25)}.compact .stage{height:120px}.small{font-size:14px;color:#cec4b0}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:14px/1.7 'Malgun Gothic',sans-serif}a{color:#e9cf89}@media(max-width:650px){.compare{grid-template-columns:1fr}}</style>
<main><h1>K-018 · 낮에도 보이는 횃불대</h1><p class="note">사용자 외형 검토용 시안입니다. 낮과 밤은 <b>같은 받침 그림</b>을 쓰고, 밤에만 기존 6칸 불꽃을 얹습니다. Game 연결·실제3D 조명은 포함하지 않았습니다.</p>
<nav><button id="play">정지</button><button id="prev">이전 칸</button><button id="next">다음 칸</button><label>속도 <select id="fps" aria-label="재생 속도"><option>8</option><option selected>10</option><option>12</option></select></label><button id="size">작게 보기</button><button id="back">배경 전환</button></nav>
<section class="compare" id="compare"><article class="panel"><h2>낮 · 꺼짐</h2><div class="stage day"><div class="art off"><img class="body" alt="낮의 꺼진 나무 횃대와 철 받침"></div></div><p>받침은 낮에도 남습니다.</p></article><article class="panel"><h2>밤 · 켜짐</h2><div class="stage"><div class="art"><img class="body" alt="밤에도 동일한 횃대와 철 받침"><div class="flame" role="img" aria-label="기존 불꽃 6칸 애니메이션"></div></div></div><p id="status" role="status"></p></article></section>
<p class="small">받침 128×256 · 부착 기준점(64,224) · 약0.3m 제안. 기존 불꽃 위치0.24m와 몸체 높이는 다르며 실제 난간 접점은 연결 후 확인합니다.</p><nav><a class="dl" id="bodyDL">받침 PNG 저장</a><a class="dl" id="flameDL">기존 불꽃 PNG 저장</a></nav><details><summary>제작 문서·규격·남은 검토</summary><pre id="readme"></pre></details></main>
<script id="data" type="application/json">__DATA__</script><script>
const D=JSON.parse(document.getElementById('data').textContent),$=id=>document.getElementById(id);let frame=0,playing=true,fps=10,last=null,elapsed=0;
document.querySelectorAll('.body').forEach(i=>i.src=D.body);document.querySelectorAll('.flame').forEach(i=>{i.style.backgroundImage='url('+D.flame+')';i.style.left=(D.tip[0]-18)+'px';i.style.top=(D.tip[1]-36)+'px'});
$('bodyDL').href=D.body;$('bodyDL').download='torch_mount_off.png';$('flameDL').href=D.flame;$('flameDL').download='torch_flame_existing.png';$('readme').textContent=D.readme;
function paint(){document.querySelectorAll('.flame').forEach(e=>e.style.backgroundPosition=(-36*frame)+'px 0');$('status').textContent='불꽃 '+(frame+1)+'/6 · '+fps+'fps · '+(playing?'재생 중':'정지');$('play').textContent=playing?'정지':'재생'}
function step(n){playing=false;frame=(frame+n+6)%6;elapsed=0;paint()}
$('play').onclick=()=>{playing=!playing;elapsed=0;last=null;paint()};$('prev').onclick=()=>step(-1);$('next').onclick=()=>step(1);$('fps').onchange=()=>{fps=Number($('fps').value);elapsed=0;paint()};$('size').onclick=()=>{const small=$('compare').classList.toggle('compact');$('size').textContent=small?'크게 보기':'작게 보기'};$('back').onclick=()=>document.querySelectorAll('.stage').forEach(e=>e.classList.toggle('day'));
function tick(t){if(last!==null&&playing){elapsed+=Math.min(t-last,500);while(elapsed>=1000/fps){elapsed-=1000/fps;frame=(frame+1)%6}paint()}last=t;requestAnimationFrame(tick)}paint();requestAnimationFrame(tick);
</script></html>'''
html=template.replace('__DATA__',json.dumps({'body':data(HERE/'torch_mount_off.png'),'flame':data(HERE/'torch_flame_existing.png'),'tip':tip,'readme':readme},ensure_ascii=False).replace('</','<\\/'))
(HERE/'preview.html').write_text(html,encoding='utf-8')
(CARD/'_HTML/codex_K018_횃불대_v1.html').write_text(html,encoding='utf-8')
checks=[]
def ck(name,v):
 checks.append({'check':name,'passed':bool(v)})
 assert v,name
ck('128×256 body',sprite.size==(128,256))
ck('transparent margin',sprite.getpixel((0,0))[3]==0 and sprite.getpixel((127,255))[3]==0)
ck('body inside cell',sprite.getbbox()[0]>0 and sprite.getbbox()[2]<128 and sprite.getbbox()[1]>=0 and sprite.getbbox()[3]<=224)
ck('existing flame byte identical',sha(flame_src)==sha(HERE/'torch_flame_existing.png'))
ck('flame 6×256',Image.open(HERE/'torch_flame_existing.png').size==(1536,256))
ck('same body shared',html.count('class="body"')==2 and "querySelectorAll('.body')" in html)
ck('html copy equal',sha(HERE/'preview.html')==sha(CARD/'_HTML/codex_K018_횃불대_v1.html'))
ck('readme embedded',json.loads(re.search(r'<script id="data" type="application/json">(.*?)</script>',html,re.S).group(1))['readme']==readme)
ck('no remote resource',not re.search(r'<(?:img|script)[^>]+src=["\']https?://',html))
node='C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
js='\n'.join(re.findall(r'<script>(.*?)</script>',html,re.S))
ck('JS syntax',subprocess.run([node,'--check','--input-type=commonjs'],input=js,text=True,capture_output=True).returncode==0)
ck('source preserved',sha(source)==sha(HERE/'torch_mount_source.png'))
(HERE/'qa.json').write_text(json.dumps({'checks':checks,'passed':len(checks),'unityTested':False,'browserTested':False,'artApproved':False},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
if prompt_record.resolve()!=(HERE/'generation.json').resolve():shutil.copy2(prompt_record,HERE/'generation.json')
if Path(__file__).resolve()!=(HERE/'build.py').resolve():shutil.copy2(Path(__file__),HERE/'build.py')
with zipfile.ZipFile(HERE/'torch_mount_review.zip','w',zipfile.ZIP_DEFLATED) as z:
 for name in ['torch_mount_source.png','torch_mount_off.png','torch_flame_existing.png','README.md','manifest.json','generation.json','qa.json','preview.html']:
  z.write(HERE/name,name)
(CARD/'CARD_RESULT.md').write_text('# K-018 結果\n\n'.replace('結果','결과')+readme+'\n자동 규격/해시/JS 검사 '+str(len(checks))+' 통과. 브라우저 확인은 부모가 별도 기록. CARD_RESULT는 커밋하지 않는다.\n',encoding='utf-8')
print(json.dumps({'passed':len(checks),'bbox':sprite.getbbox(),'tip':tip,'bodySize':body.size,'folder':str(HERE)},ensure_ascii=False))
