from pathlib import Path
from PIL import Image
import json, base64, hashlib, shutil

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
NN = Image.Resampling.NEAREST
sources = json.loads((OUT / 'generation.json').read_text(encoding='utf-8'))['sources']
names = {'menu':'main_menu_dark_1920x1080', 'city':'story_fog_tide_1920x1080', 'dead':'story_dead_beacon_1920x1080', 'lit':'story_relit_beacon_1920x1080'}
assets = []
(OUT/'sources').mkdir(exist_ok=True)
(OUT/'previews').mkdir(exist_ok=True)

def save(im, filename, role, **extra):
    im.save(OUT/filename)
    alpha = im.convert('RGBA').getchannel('A')
    assets.append(dict(file=filename, size=list(im.size), mode=im.mode, alpha_extrema=list(alpha.getextrema()), role=role, frames=1, frame_order=[0], pivot_normalized=[0.5,0.5], **extra))

for key, src in sources.items():
    shutil.copyfile(src, OUT/'sources'/f'{key}_generated.png')
for key, name in names.items():
    im = Image.open(OUT/'sources'/f'{key}_generated.png').convert('RGBA').resize((1920,1080), NN)
    save(im, name+'.png', 'menu_background' if key=='menu' else 'story_background', safe_dialogue_rect_px=[0,720,1920,1080] if key!='menu' else None)
    save(im.resize((7680,4320),NN), 'previews/'+name+'_4x.png', 'preview_4x')

logo = Image.open(OUT/'sources/logo_generated.png').convert('RGBA')
for w,h in [(1600,560),(800,280)]:
    ratio = min((w-40)/logo.width,(h-40)/logo.height)
    sized=logo.resize((round(logo.width*ratio),round(logo.height*ratio)),NN)
    canvas=Image.new('RGBA',(w,h))
    canvas.paste(sized,((w-sized.width)//2,(h-sized.height)//2))
    save(canvas,f'title_logo_{w}x{h}.png','title_logo', text='빛을 잇는 자들')
save(Image.open(OUT/'title_logo_800x280.png').resize((3200,1120),NN),'previews/title_logo_4x.png','preview_4x')

current=ROOT/'Game/Assets/_Project/Art/UI/Resources/ScreenArt/main.png'
def uri(p):
    return 'data:image/png;base64,'+base64.b64encode(Path(p).read_bytes()).decode('ascii')
def img(p,alt,cls=''):
    return f'<img class="{cls}" src="{uri(p)}" alt="{alt}">'
def stage(key,dialogue=False):
    content=img(OUT/(names[key]+'.png'),{'menu':'등불과 검은 안개','city':'도시를 삼키는 안개','dead':'꺼진 등대 요새','lit':'되살아난 등대 요새'}[key])
    if key=='menu':
        content+=f'<div class="menu overlay">{img(OUT/"title_logo_800x280.png","빛을 잇는 자들")}<div class="choices">시작<br>설정<br>종료</div></div>'
    if dialogue:
        content+='<div class="safe overlay">하단 1/3 · 대사 영역</div><div class="portrait left overlay">초상 자리</div><div class="portrait right overlay">초상 자리</div><div class="dialogue overlay"><b>이야기 배치 예시</b><p>꺼진 빛을 다시 잇기 위해, 우리는 안개 속으로 향했다.</p></div>'
    return '<div class="stage">'+content+'</div>'
old=img(current,'현재 게임에서 사용하는 성벽 배경')
sections=''
for key,title,desc in [('city','01 · 검은 안개 해일','도시의 불빛이 꺼지던 날. 아래쪽 안개는 낮은 대비로 비워 두었습니다.'),('dead','02 · 폐허 요새 도착','원정대는 먼 실루엣으로만 표현했습니다. 주인공의 얼굴과 복장은 정하지 않습니다.'),('lit','03 · 등불 재점화','같은 요새의 구도를 유지하고 빛기둥과 갈라지는 안개로 점화를 표현했습니다.')]:
    sections+=f'<section><h2>{title}</h2><p>{desc}</p><div class="pair"><article><h3>새 이야기 배경</h3>{stage(key,True)}</article><article><h3>현재 이야기 배경 · 읽기 전용 비교</h3><div class="stage">{old}</div></article></div></section>'
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>빛을 잇는 자들 · 제목과 이야기 배경</title><style>
*{box-sizing:border-box;min-width:0}body{overflow-wrap:anywhere}body{margin:0;background:#11151c;color:#e8e0cf;font:16px/1.65 "Malgun Gothic",sans-serif}main{max-width:1600px;padding:36px 28px;margin:auto}h1{font-size:32px;margin:10px 0}h2{font-size:23px}h3{font-size:14px;color:#c9b78e}p{color:#b6bec9}section{margin:36px 0 60px}.tag{color:#d8b568;letter-spacing:2px;font-size:13px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:20px}.stage{position:relative;aspect-ratio:16/9;overflow:hidden;border:1px solid #454341;background:#080b10;container-type:inline-size}.stage>img{width:100%;height:100%;object-fit:cover;display:block;image-rendering:pixelated}.menu{position:absolute;left:5%;top:17%;width:45%;text-align:center}.menu img{width:100%;image-rendering:pixelated}.choices{font-size:3cqw;line-height:2.2;color:#e8d9b8;text-shadow:0 2px 2px #000}.safe{position:absolute;inset:66.666% 0 0;border-top:1px dashed #dbb86c;color:#c6b381;font-size:1.8cqw;padding:0 2%;background:#0002}.portrait{position:absolute;top:43%;bottom:27.4%;width:15%;border:1px dashed #b8af9d80;color:#aaa39480;text-align:center;font-size:1.6cqw;padding-top:9%}.left{left:17.5%}.right{right:17.5%}.dialogue{position:absolute;left:6%;right:6%;bottom:3%;height:24.45%;background:#e6d6b9ed;color:#29282e;border:2px solid #6a5c43;padding:1.4% 3%;font-size:1.7cqw}.dialogue p{color:#29282e;margin:2% 0}.hidden .overlay{display:none}.toolbar{position:sticky;top:0;background:#11151cf5;padding:12px 0;z-index:3;display:flex;gap:10px;flex-wrap:wrap}button{background:#333542;border:1px solid #84724f;color:#f2e6c9;border-radius:4px;padding:9px 15px;font:inherit;cursor:pointer}button[aria-pressed=true]{background:#645330}.logos{display:grid;grid-template-columns:1fr 1fr;gap:20px}.logo-sample{padding:20px;background:repeating-conic-gradient(#262b34 0 25%,#343b46 0 50%) 0/24px 24px}.light{background:repeating-conic-gradient(#c8c3b9 0 25%,#e6e0d5 0 50%) 0/24px 24px}.logo-sample img{width:100%;image-rendering:pixelated}small{color:#9ca5b2}.flip{max-width:1000px}.flip img{display:none}.flip img.active{display:block}footer{border-top:1px solid #454341;padding-top:20px;color:#9ca5b2}@media(max-width:760px){main{padding:22px 14px}.pair,.logos{grid-template-columns:1fr}h1{font-size:25px}}
</style><main><div class="tag">K-105 · 그림 검토 · 가제</div><h1>빛을 잇는 자들</h1><p>검은 안개에 잠긴 대륙, 꺼진 등대 요새를 잇는 원정.<br>제목 2종 · 메인 배경 1장 · 이야기 배경 3장 / 새 그림에 식별 가능한 주인공 없음.</p><div class="toolbar"><button id="overlay" aria-pressed="true">배치 예시 끄기</button><button id="play" aria-pressed="false">점화 전후 자동 비교</button></div><small>배치 예시는 검토용입니다. 실제 게임 캡처가 아니며, 초상 자리의 점선은 그림에 포함되지 않습니다.</small><section><h2>메인 화면 · 검은 안개 위의 불빛</h2><div class="pair"><article><h3>새 메인 배경 + 분리된 제목 로고</h3>'''+stage('menu')+'''</article><article><h3>현재 게임 배경 · 읽기 전용 비교</h3><div class="stage">'''+old+'''</div></article></div></section><section><h2>제목 로고 · 투명 배경</h2><p>빛을 잇는 자들 · 1600×560 / 800×280 · 아래 격자는 투명 영역을 확인하기 위한 바탕입니다.</p><div class="logos"><div class="logo-sample">'''+img(OUT/'title_logo_1600x560.png','어두운 바탕 위 투명 제목 로고')+'''</div><div class="logo-sample light">'''+img(OUT/'title_logo_800x280.png','밝은 바탕 위 투명 제목 로고')+'''</div></div></section>'''+sections+'''<section><h2>같은 요새 · 점화 전후</h2><p>아래 버튼으로 정지 장면을 전환하거나, 위의 자동 비교로 2초마다 확인할 수 있습니다. 게임용 동작 시트가 아닌 정지 배경 비교입니다.</p><button id="step">점화 후 보기</button><div class="stage flip">'''+img(OUT/(names['dead']+'.png'),'점화 전 요새','active')+img(OUT/(names['lit']+'.png'),'점화 후 요새')+'''</div></section><footer>모든 새 배경은 1920×1080 PNG입니다. 원안·4배 최근접 확대·프롬프트·크기 정보는 Docs/References/codex_k105_v1/에 보관했습니다.<br>현재 비교 그림: Game/Assets/_Project/Art/UI/Resources/ScreenArt/main.png (현재 인트로 장면도 같은 그림 참조). 기존 초상·병사·적·성벽은 수정하지 않았습니다. Unity 적용과 Play 검증은 별도입니다.</footer></main><script>
let lit=false,timer=null;const step=document.querySelector('#step');function flip(){lit=!lit;document.querySelectorAll('.flip img').forEach((x,i)=>x.classList.toggle('active',i===(lit?1:0)));step.textContent=lit?'점화 전 보기':'점화 후 보기'}step.onclick=flip;document.querySelector('#overlay').onclick=function(){let off=document.body.classList.toggle('hidden');this.textContent=off?'배치 예시 켜기':'배치 예시 끄기';this.setAttribute('aria-pressed',String(!off))};document.querySelector('#play').onclick=function(){if(timer){clearInterval(timer);timer=null;this.textContent='점화 전후 자동 비교'}else{flip();timer=setInterval(flip,2000);this.textContent='자동 비교 멈추기'}this.setAttribute('aria-pressed',String(!!timer))};
</script></html>'''
review=ROOT/'_HTML/6_그림_화면/codex_K-105_제목과이야기배경_v1.html'
review.write_text(html,encoding='utf-8')
manifest=dict(card='K-105',title='빛을 잇는 자들',generator='built-in image_gen',assets=assets,scale_method='nearest_neighbor',static_backgrounds=True,sprite_sheet_contract_applicable=False,foot_baseline=None,notes_ko=['정지 배경과 제목 로고만 제작. 캐릭터 시트의 발 기준선·셀 규칙은 적용 대상 아님.','원안은 sources/, 완성 배경은 1920×1080. 확대 미리보기는 완성본의 4배 최근접 확대.','게임 적용은 Claude 담당. 초상·병사·적·성벽 수정 없음.'],current_comparison=dict(file=str(current.relative_to(ROOT)).replace('\\','/'),sha256=hashlib.sha256(current.read_bytes()).hexdigest()),review=str(review.relative_to(ROOT)).replace('\\','/'))
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'assets':len(assets),'review_bytes':review.stat().st_size}))
