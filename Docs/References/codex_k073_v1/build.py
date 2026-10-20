from pathlib import Path
from PIL import Image
import numpy as np
import json, hashlib, base64, io

P = Path(__file__).resolve().parent
ROOT = P.parents[2]
NN = Image.Resampling.NEAREST
ORDERS = {'static':['후면 대기'], 'run':['오른발 전진','교차·오른발 지지','왼발 전진','교차·왼발 지지'], 'fire':['준비','당기기','조준','발사','회복','다음 화살 준비'], 'fall':['피격','무릎 꺾임','옆으로 넘어짐','쓰러짐']}
NAMES = {'static':'후면 대기','run':'측면 달리기','fire':'후면 사격','fall':'쓰러지기'}

def components(mask):
    seen = np.zeros(mask.shape, bool)
    result=[]
    for y,x in zip(*np.where(mask)):
        if seen[y,x]: continue
        stack=[(int(y),int(x))]; seen[y,x]=True; coords=[]
        while stack:
            a,b=stack.pop(); coords.append((a,b))
            for da,db in ((0,1),(0,-1),(1,0),(-1,0),(1,1),(-1,-1),(1,-1),(-1,1)):
                c,d=a+da,b+db
                if 0<=c<mask.shape[0] and 0<=d<mask.shape[1] and mask[c,d] and not seen[c,d]:
                    seen[c,d]=True; stack.append((c,d))
        result.append(coords)
    return sorted(result,key=len,reverse=True)

def clean(im, minimum=3):
    a=np.array(im.convert('RGBA')); mask=a[:,:,3]>=128
    for c in components(mask):
        if len(c)<minimum:
            for y,x in c: mask[y,x]=False
    a[:,:,3]=mask.astype('uint8')*255; a[~mask,:3]=0
    return Image.fromarray(a)

def uri(im):
    b=io.BytesIO(); im.save(b,format='PNG'); return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()

def build():
    manifest={'card':'K-073','decision':'D-081','native_cell':[96,128],'game_cell':[288,384], 'native_foot_top_left':[48,120], 'game_foot_top_left':[144,360], 'unity_pivot_normalized_bottom_left':[0.5,0.0625], 'scale':3,'filter':'nearest','sheets':[]}
    validation={}; all_frames=[]; sections=[]
    for motion,order in ORDERS.items():
        frames=[]; raw_boxes=[]
        if motion=='static':
            frames=[Image.open(ROOT/'Docs/References/codex_k070_v1/archer_back_96x128.png').convert('RGBA')]
        else:
            raw=Image.open(P/f'archer_{motion}_generated.png').convert('RGBA')
            for i in range(len(order)):
                part=raw.crop((round(i*raw.width/len(order)),0,round((i+1)*raw.width/len(order)),raw.height))
                a=np.array(part); comps=components(a[:,:,3]>=128); main=comps[0]
                ys,xs=zip(*main); box=(min(xs),min(ys),max(xs)+1,max(ys)+1)
                # 생성 원안의 연결된 인물 영역만 추출해 투명 여백의 잡점을 제외한다.
                keep=np.zeros(a.shape[:2],bool)
                for c in comps:
                    if len(c)>100:
                        for y,x in c: keep[y,x]=True
                a[:,:,3]=keep.astype('uint8')*255; a[~keep,:3]=0
                part=Image.fromarray(a).crop(box); raw_boxes.append(box)
                frames.append(part)
            # 낙하 중에는 키가 줄어드는 비율을 유지한다. 프레임마다 키를 다시 늘리지 않는다.
            scale=73/frames[0].height
            if motion=='fire': scale=73/(raw_boxes[0][3]-raw_boxes[0][1])
            for i,part in enumerate(frames):
                part=clean(part.resize((round(part.width*scale),round(part.height*scale)),NN))
                rgb=part.convert('RGB').quantize(colors=32,method=Image.Quantize.MEDIANCUT).convert('RGBA'); rgb.putalpha(part.getchannel('A')); part=clean(rgb)
                box=part.getbbox(); part=part.crop(box)
                # 머리 중심을 몸의 기준으로 삼고 활이 뻗은 방향에 따라 몸통이 밀리지 않게 한다.
                mask=np.array(part)[:,:,3]>0
                if motion=='fire':
                    xx=np.where(mask[-12:])[1]; anchor=(int(xx.min())+int(xx.max())+1)/2
                elif motion=='fall' and i>=2: anchor=part.width/2
                else:
                    band=mask[:min(19,part.height)]
                    xx=np.where(band)[1]; anchor=(int(xx.min())+int(xx.max())+1)/2
                cell=Image.new('RGBA',(96,128)); x=round(48-anchor); y=120-part.height
                assert x>=0 and x+part.width<=96 and y>=0, (motion,i,part.size,x,y)
                cell.alpha_composite(part,(x,y)); frames[i]=cell
        sheet=Image.new('RGBA',(96*len(frames),128))
        checks=[]
        for i,im in enumerate(frames):
            im=clean(im); frames[i]=im; sheet.alpha_composite(im,(96*i,0))
            bbox=im.getbbox(); comps=components(np.array(im)[:,:,3]>0)
            assert bbox[3]==120 and set(im.getchannel('A').getdata())<={0,255}
            assert all(len(c)>=3 for c in comps)
            checks.append({'frame':i,'bbox':bbox,'bottom_exclusive':bbox[3],'connected_component_sizes':[len(c) for c in comps]})
        native=f'archer_{motion}_native.png'; game=f'archer_{motion}.png'; preview=f'archer_{motion}_4x.png'
        sheet.save(P/native); sheet.resize((sheet.width*3,384),NN).save(P/game); sheet.resize((sheet.width*4,512),NN).save(P/preview)
        assert Image.open(P/game).tobytes()==sheet.resize((sheet.width*3,384),NN).tobytes()
        old=Image.open(ROOT/f'Game/Assets/_Project/Art/Characters/{motion.title()}/archer_{motion}.png').convert('RGBA')
        assert old.size==(len(frames)*256,384)
        entry={'motion':motion,'native':native,'game':game,'preview_4x':preview,'frames':len(frames),'order':order,'source_game':f'Game/Assets/_Project/Art/Characters/{motion.title()}/archer_{motion}.png','source_cell':[256,384],'source_foot_top_left':[128,352],'raw_crop_boxes':raw_boxes}
        manifest['sheets'].append(entry); validation[motion]=checks
        sections.append({'motion':motion,'name':NAMES[motion],'order':order,'native':uri(sheet),'old':uri(old),'count':len(frames)})
        all_frames.extend(frames)
    board=Image.new('RGBA',(6*96,3*128))
    for i,frame in enumerate(all_frames): board.alpha_composite(frame,((i%6)*96,(i//6)*128))
    board.resize((board.width*4,board.height*4),NN).save(P/'contact_sheet_4x.png')
    (P/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    before=json.loads((P/'source_hashes_before.json').read_text()); changed=[p for p,h in before.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    assert not changed, changed
    (P/'validation.json').write_text(json.dumps({'frames':validation,'game_files_unchanged':len(before),'nearest_3x_verified':True,'binary_alpha':True,'foot_bottom_exclusive':120},indent=2),encoding='utf-8')
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-073 궁수 동작 검토</title><style>
    *{box-sizing:border-box}body{margin:0;background:#101613;color:#eee4cf;font:16px/1.7 system-ui,sans-serif}main{max-width:1200px;margin:auto;padding:28px}h1,h2{color:#d5b16b}button,select{font:inherit;padding:8px 16px;background:#26352d;color:#eee4cf;border:1px solid #d5b16b;border-radius:5px}header{border-bottom:1px solid #526052;padding-bottom:24px}.controls{display:flex;gap:10px;flex-wrap:wrap;position:sticky;top:0;background:#101613;padding:12px 0;z-index:2}section{padding:24px 0;border-bottom:1px solid #526052}.pair{display:flex;gap:24px;flex-wrap:wrap}.stage{position:relative;width:288px;height:384px;background:repeating-conic-gradient(#26352d 0% 25%,#1c2822 0% 50%) 0/24px 24px;overflow:hidden}.stage canvas{width:288px;height:384px;image-rendering:pixelated}.line{position:absolute;top:360px;width:100%;border-top:1px dashed #d5b16b;pointer-events:none}.label{margin:8px 0}.strip{max-width:100%;overflow:auto}.strip img{height:256px;image-rendering:pixelated}small{color:#b7c2b6}p{max-width:950px}@media(max-width:420px){main{padding:14px}.pair{gap:8px}}
    </style><main><header><small>Human Last Rampart · K-073 · 검토용 1판</small><h1>궁수 — 성벽 위의 움직임</h1><p>73픽셀 인물 · 원본 96×128 · 게임용 288×384. 갈색 머리와 남색 군복을 유지하고 후면 사격과 측면 달리기를 비교합니다. 노란 점선은 발 기준선입니다.</p><p>왼쪽은 새 원본의 3배 확대, 오른쪽은 현재 게임 시트입니다. 기존 칸은 256×384, 새 칸은 288×384입니다. 기존 그림은 발 (128,352)을 새 발 (144,360)에 맞춰 옮겨 표시했습니다.</p></header><div class="controls"><button id="pause">일시 정지</button><button id="next">한 칸씩</button><button id="flip">좌우 뒤집기</button><label>재생 속도 <select id="speed"><option value="8">초당 8칸</option><option value="4">초당 4칸</option><option value="12">초당 12칸</option></select></label></div><div id="sections"></div><p>발사 순서: 준비 → 당기기 → 조준 → 발사 → 회복 → 다음 화살 준비. 쓰러지기는 마지막 칸에서 잠시 멈춘 뒤 다시 재생합니다. 재생 속도는 검토용이며 게임의 재생 설정은 Claude가 연결합니다.</p><p>Unity 실행·게임 연결·실제 전투 캡처는 수행하지 않았습니다. 사용자 외형 검토 후 Claude가 칸 크기와 피벗을 적용합니다.</p></main><script>const data=DATA;let paused=false,flipped=false,tick=0;const records=[];let loaded=0;window.previewReady=false;for(const d of data){const s=document.createElement('section');s.innerHTML=`<h2>${d.name} · ${d.count}칸</h2><div class="pair"><div><p class="label">새 궁수 · 73픽셀</p><div class="stage"><canvas width="288" height="384"></canvas><div class="line"></div></div></div><div><p class="label">현재 게임 시트</p><div class="stage"><canvas width="288" height="384"></canvas><div class="line"></div></div></div></div><p class="frame"></p><details><summary>새 원본 전체 칸 보기 · 2배</summary><div class="strip"><img alt="${d.name} 전체 칸" src="${d.native}"></div></details>`;document.querySelector('#sections').append(s);const images=[new Image(),new Image()];images.forEach((im,i)=>{im.onload=()=>{loaded++;window.previewReady=loaded===data.length*2;draw()};im.src=i?d.old:d.native});records.push({d,s,images,canvases:[...s.querySelectorAll('canvas')]})}function draw(){for(const r of records){const n=r.d.count;const f=r.d.motion==='fall'?Math.min(tick%(n+5),n-1):tick%n;r.s.querySelector('.frame').textContent=`${f+1} / ${n} · ${r.d.order[f]}`;r.canvases.forEach((c,i)=>{const ctx=c.getContext('2d');ctx.clearRect(0,0,288,384);if(!r.images[i].complete)return;ctx.save();ctx.imageSmoothingEnabled=false;if(flipped){ctx.translate(288,0);ctx.scale(-1,1)}if(i)ctx.drawImage(r.images[i],f*256,0,256,384,16,8,256,384);else ctx.drawImage(r.images[i],f*96,0,96,128,0,0,288,384);ctx.restore()})}}let last=0;function loop(now){if(!paused&&now-last>=1000/Number(document.querySelector('#speed').value)){tick++;draw();last=now}requestAnimationFrame(loop)}requestAnimationFrame(loop);document.querySelector('#pause').onclick=()=>{paused=!paused;document.querySelector('#pause').textContent=paused?'재생':'일시 정지'};document.querySelector('#next').onclick=()=>{paused=true;tick++;document.querySelector('#pause').textContent='재생';draw()};document.querySelector('#flip').onclick=()=>{flipped=!flipped;draw()};</script>'''
    html=html.replace('DATA',json.dumps(sections,ensure_ascii=False))
    (ROOT/'_HTML/4_그림_캐릭터/codex_K-073_궁수동작_v1.html').write_text(html,encoding='utf-8')
    print('완료: 15칸, 4종 시트, 발선·알파·3배 확대·Game 원본 해시 검증')

if __name__=='__main__': build()
