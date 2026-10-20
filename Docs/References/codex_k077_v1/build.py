from pathlib import Path
from PIL import Image
import numpy as np
import json, base64, hashlib, re

P = Path(__file__).resolve().parent
R = P.parents[2]
NN = Image.Resampling.NEAREST

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def components(im):
    a = np.array(im)[:, :, 3] > 0
    seen = set()
    groups = []
    for y, x in zip(*np.where(a)):
        if (x,y) in seen: continue
        todo = [(int(x),int(y))]; seen.add((x,y)); group = []
        while todo:
            x,y = todo.pop(); group.append((x,y))
            for dx,dy in [(-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,-1),(-1,1),(1,1)]:
                q=(x+dx,y+dy)
                if 0<=q[0]<im.width and 0<=q[1]<im.height and a[q[1],q[0]] and q not in seen:
                    seen.add(q); todo.append(q)
        groups.append(group)
    return sorted(groups,key=len,reverse=True)

def inline(im):
    import io
    out=io.BytesIO(); im.save(out,format='PNG')
    return 'data:image/png;base64,'+base64.b64encode(out.getvalue()).decode()

def main():
    cfg=json.loads((P/'generation.json').read_text(encoding='utf-8'))
    src=Image.open(P/'generated.png').convert('RGBA')
    pal=json.loads((R/'Docs/References/codex_k066_v1/palettes.json').read_text(encoding='utf-8'))['goblin']['colors']
    palette=np.array([tuple(bytes.fromhex(c[1:])) for c in pal],dtype=np.int32)
    frames=[]; checks=[]
    for i,c in enumerate(cfg['frames']):
        source=Image.open(P/c['source']).convert('RGBA') if 'source' in c else src
        im=source.crop(c['crop']); a=np.array(im)
        a[a[:,:,3]<128]=0; a[a[:,:,3]>=128,3]=255
        im=Image.fromarray(a)
        box=im.getbbox(); scale=c['scale']
        tile=im.crop(box).resize((round((box[2]-box[0])*scale),round((box[3]-box[1])*scale)),NN)
        a=np.array(tile)
        dist=((a[:,:,:3,None].astype(np.int32)-palette.T[None,None,:,:])**2).sum(2)
        a[:,:,:3]=palette[dist.argmin(2)]; a[a[:,:,3]==0]=0
        tile=Image.fromarray(a)
        groups=components(tile); removed=sum(len(g) for g in groups[1:])
        for g in groups[1:]:
            for x,y in g: a[y,x]=0
        tile=Image.fromarray(a)
        # 발선은 마지막 불투명 행 아래 경계이며 쓰러짐에서는 지면 접촉선이다.
        x=round(48-(c['axis']-box[0])*scale); y=120-tile.height
        assert x>=0 and y>=0 and x+tile.width<=96
        out=Image.new('RGBA',(96,128)); out.paste(tile,(x,y))
        assert out.getbbox()[3]==120
        assert len(components(out))==1
        frames.append(out)
        checks.append({'index':i,'bbox':out.getbbox(),'ground_bottom_exclusive':120,'axis_x':48,'components_8_connected':1,'removed_source_fragment_pixels':removed,'opaque_pixels':int((np.array(out)[:,:,3]>0).sum())})
    definitions=[('goblin_walk_attack4',0,8,2,['걷기: 왼발 지지','걷기: 통과','걷기: 오른발 지지','걷기: 반대 통과','공격: 준비','공격: 들어 올리기','공격: 내려치기','공격: 회복']),('goblin_fall4',8,4,1,['피격 반동','무릎 꺾임','옆으로 넘어짐','옆으로 누움']),('goblin_wallhit4',12,4,1,['토벽 타격: 준비','토벽 타격: 머리 위로 들기','토벽 타격: 내려치기','토벽 타격: 회복'])]
    manifest={'card':'K-077','size_decision':'D-081','native_cell':[96,128],'game_cell':[288,384],'native_pivot_pixels_top_left':[48,120],'game_pivot_pixels_top_left':[144,360],'unity_normalized_pivot':[0.5,0.0625],'alpha':'binary 0/255','scaling':'nearest-neighbor','figure_height_target':56,'sheets':[]}
    for name,start,count,rows,order in definitions:
        sheet=Image.new('RGBA',(384,128*rows))
        for n in range(count): sheet.paste(frames[start+n],((n%4)*96,(n//4)*128))
        sheet.save(P/(name+'_native.png'))
        game=sheet.resize((sheet.width*3,sheet.height*3),NN); game.save(P/(name+'.png'))
        sheet.resize((sheet.width*4,sheet.height*4),NN).save(P/(name+'_4x.png'))
        assert np.array_equal(np.array(Image.open(P/(name+'.png'))),np.repeat(np.repeat(np.array(sheet),3,0),3,1))
        assert np.array_equal(np.array(Image.open(P/(name+'_4x.png'))),np.repeat(np.repeat(np.array(sheet),4,0),4,1))
        assert Image.open(P/(name+'.png')).mode=='RGBA'
        manifest['sheets'].append({'file':name+'.png','native_file':name+'_native.png','preview_file':name+'_4x.png','grid':[4,rows],'frame_count':count,'order_top_left_row_major':order,'native_size':list(sheet.size),'game_size':list(game.size),'frames':checks[start:start+count]})
    (P/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    make_page(manifest)
    original=json.loads((P/'source_hashes_before.json').read_text())
    unchanged=all(digest(R/p)==h for p,h in original.items())
    assert unchanged
    result={'game_files_unchanged':len(original),'all_game_hashes_unchanged':unchanged,'frames':checks,'all_alpha_binary':all(set(np.unique(np.array(f)[:,:,3])).issubset({0,255}) for f in frames),'game_exact_nearest_3x':True,'preview_exact_nearest_4x':True}
    (P/'validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({'frames':len(frames),'unchanged_game_files':len(original),'sizes':[s['game_size'] for s in manifest['sheets']]}))

def make_page(manifest):
    oldwalk=Image.open(R/'Game/Assets/_Project/Art/Characters/Goblin/goblin_walk_attack4.png').convert('RGBA')
    oldfall=Image.open(R/'Game/Assets/_Project/Art/Characters/Fall/goblin_fall4.png').convert('RGBA')
    sections=[]
    for title,name,row,old,oldrow,note in [('걷기','goblin_walk_attack4',0,oldwalk,0,'왼발·오른발 교대와 곤봉의 작은 흔들림'),('곤봉 공격','goblin_walk_attack4',1,oldwalk,1,'준비 → 들어 올리기 → 내려치기 → 회복'),('쓰러짐','goblin_fall4',0,oldfall,0,'피격 → 무릎 꺾임 → 옆으로 넘어짐 → 누움 · 마지막 자세 유지'),('토벽 타격','goblin_wallhit4',0,oldwalk,0,'현재 전용 시트 없음: 기존 걷기를 대조용으로 표시 · 토벽은 위치 설명용 목업')]:
        new=Image.open(P/(name+'_native.png'))
        newrow=new.crop((0,row*128,384,(row+1)*128))
        ow=old.width//4; oh=old.height//(2 if old is oldwalk else 1)
        oldstrip=old.crop((0,oldrow*oh,old.width,(oldrow+1)*oh))
        sections.append(f'<section><h2>{title}</h2><p>{note}</p><div class="pair">'+stage(newrow,96,128,120,'새 그림 · 원본 4배',title=='토벽 타격',title=='쓰러짐')+stage(oldstrip,ow,oh,352,'현재 게임 그림 · 화면 키를 맞춘 비교',False,title=='쓰러짐',True)+'</div><details><summary>새 시트 전체 보기</summary><img alt="'+title+' 새 시트" src="'+inline(new)+'"></details></section>')
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>고블린 동작 1판</title><style>
    *{box-sizing:border-box}body{margin:0;background:#171b22;color:#e7e8e6;font:16px/1.6 system-ui,sans-serif}main{max-width:1050px;padding:28px;margin:auto}h1{font-size:30px}h2{font-size:23px}p{color:#bdc8c4}button{background:#343e46;border:1px solid #71817c;color:white;padding:9px 18px;margin:4px;cursor:pointer}section{border-top:1px solid #46514e;margin-top:30px;padding-top:16px}.pair{display:flex;gap:18px;flex-wrap:wrap}.stage{width:384px;height:512px;position:relative;overflow:hidden;background:repeating-conic-gradient(#252d31 0 25%,#293236 0 50%) 0/24px 24px;max-width:100%}.sprite{position:absolute;left:0;top:0;width:384px;height:512px;background-repeat:no-repeat;image-rendering:pixelated}.line{position:absolute;top:480px;left:0;right:0;border-top:1px dashed #9cab81}.wall{position:absolute;left:6px;top:365px;width:90px;height:115px;background:#735637;border:4px solid #443323;box-shadow:inset 9px 0 #9b784a;opacity:.75}.caption{font-size:14px}figure{margin:0}details img{max-width:100%;image-rendering:pixelated;width:768px}footer{padding:30px 0;color:#b3c0ba}@media(max-width:440px){main{padding:12px}.stage{width:336px;height:448px}.sprite{transform:scale(.875);transform-origin:top left}.line{top:420px}.wall{transform:scale(.875);transform-origin:top left;top:319px}}
    </style><main><h1>고블린 동작 · 1판</h1><p>확정 크기 D-081: 고블린 약 56점 · 원본 칸 96×128 · 게임 칸 288×384.<br>기존 4×2 걷기·공격 / 쓰러짐 4장을 유지하고 토벽 타격 4장을 추가했습니다. 게임에 연결하기 전 검토 자료입니다.</p><button id="play">일시 정지</button><button id="step">한 장씩</button><button id="reset">처음부터</button><span id="counter"></span>'''+''.join(sections)+'''<footer>새 그림은 원본 4배 최근접 표시. 기존 그림은 인물 키가 비슷하게 보이도록 최근접 환산하여 같은 지면에 표시했습니다. 실제 게임 화면 캡처가 아닙니다. 토벽 목업은 시트에 포함되지 않습니다. 쓰러짐은 마지막 프레임에서 멈춘 뒤 다시 시작합니다. Unity 실행·게임 연결은 수행하지 않았습니다.</footer></main><script>
    let frame=0,tick=0,playing=true;const sprites=[...document.querySelectorAll('.sprite')];function draw(){for(const s of sprites){const f=s.dataset.fall==='1'?Math.min(frame%7,3):frame%4;s.style.backgroundPosition=`${-384*f}px 0px`;}document.querySelector('#counter').textContent=`프레임 ${frame%4+1} / 4`;}function advance(){frame=(frame+1)%28;draw()}document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'일시 정지':'재생'};document.querySelector('#step').onclick=()=>{playing=false;document.querySelector('#play').textContent='재생';frame=(frame%4+1)%4;draw()};document.querySelector('#reset').onclick=()=>{frame=0;draw()};setInterval(()=>{if(playing)advance()},180);draw();
    </script></html>'''
    page=R/'_HTML/4_그림_캐릭터/codex_K-077_고블린동작_v1.html'
    page.write_text(html,encoding='utf-8')

def stage(im,w,h,foot,label,wall=False,fall=False,old=False):
    if old:
        # 기존 인물 높이를 약 56점으로 맞춰 원본 칸 안에서 비교한다.
        strip=Image.new('RGBA',(384,128))
        for i in range(4):
            f=im.crop((i*w,0,(i+1)*w,h)); scale=56/240
            f=f.resize((round(w*scale),round(h*scale)),NN)
            strip.paste(f,(i*96+48-round(w*scale/2),120-round(foot*scale)))
        im=strip
    return '<figure><div class="stage">'+('<div class="wall"></div>' if wall else '')+f'<div class="sprite" data-fall="{int(fall)}" style="background-image:url({inline(im)});background-size:1536px 512px"></div><div class="line"></div></div><figcaption class="caption">{label}</figcaption></figure>'

if __name__=='__main__': main()
