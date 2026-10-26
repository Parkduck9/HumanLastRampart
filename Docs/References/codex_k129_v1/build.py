from pathlib import Path
from PIL import Image, ImageFilter, ImageDraw
import numpy as np
import json, hashlib, base64, io
from collections import deque

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
CELL = (256, 288)
GROUND = 280
CX = 128
NN = Image.Resampling.NEAREST

def components(mask):
    seen = np.zeros(mask.shape, bool)
    groups = []
    for y,x in zip(*np.where(mask)):
        if seen[y,x]: continue
        seen[y,x] = True
        q = [(int(y),int(x))]
        group = []
        while q:
            yy,xx=q.pop(); group.append((yy,xx))
            for dy,dx in ((-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)):
                ny,nx=yy+dy,xx+dx
                if 0<=ny<mask.shape[0] and 0<=nx<mask.shape[1] and mask[ny,nx] and not seen[ny,nx]:
                    seen[ny,nx]=True;q.append((ny,nx))
        groups.append(group)
    return sorted(groups,key=len,reverse=True)

# 선택된 시안의 색만 사용하고 반투명 테두리는 제거한다.
ref=np.array(Image.open(ROOT/'Docs/References/codex_k128_v1/B_climber_study.png').convert('RGBA'))
colors,counts=np.unique(ref[ref[:,:,3]>200,:3],axis=0,return_counts=True)
PALETTE=np.concatenate([colors[np.argsort(counts)[::-1]][:32],np.array([[35,64,91],[69,117,157]],dtype=np.uint8)])

def clean(im):
    a=np.array(im.convert('RGBA')); mask=a[:,:,3]>=200
    groups=components(mask)
    keep=np.zeros(mask.shape,bool)
    if groups:
        yy,xx=zip(*groups[0]);keep[yy,xx]=True
    a[~keep]=0;a[keep,3]=255
    im=Image.fromarray(a)
    bbox=im.getbbox()
    return im.crop(bbox) if bbox else im

def palette(im):
    a=np.array(im);m=a[:,:,3]>0
    if m.any():
        rgb=a[m,:3].astype(np.int32)
        d=((rgb[:,None,:]-PALETTE[None,:,:].astype(np.int32))**2).sum(2)
        a[m,:3]=PALETTE[d.argmin(1)]
    a[~m]=0
    return Image.fromarray(a)

def normal(im):
    a=np.array(im);m=a[:,:,3]>0
    # 같은 알파 외곽의 얕은 부조 노멀. 실제 3차원 베이크는 아니다.
    height=np.array(Image.fromarray((m*255).astype('uint8')).filter(ImageFilter.GaussianBlur(3)),dtype=float)/255
    lum=(a[:,:,:3].astype(float)*[.2126,.7152,.0722]).sum(2)/255
    lum=np.array(Image.fromarray((lum*255).astype('uint8')).filter(ImageFilter.GaussianBlur(1.2)),dtype=float)/255
    gy,gx=np.gradient(height*.65+lum*.12)
    nx=-gx*2.2;ny=gy*2.2;nz=np.ones_like(nx)
    norm=np.sqrt(nx*nx+ny*ny+nz*nz)
    rgb=np.stack([nx/norm,ny/norm,nz/norm],2)
    rgb=np.clip(np.rint((rgb+1)*127.5),0,255).astype('uint8')
    rgb[~m]=[128,128,255]
    return Image.fromarray(rgb,'RGB')

def uri(im):
    b=io.BytesIO();im.save(b,format='PNG')
    return 'data:image/png;base64,'+base64.b64encode(b.getvalue()).decode()

def main():
    sources=json.loads((OUT/'generation.json').read_text(encoding='utf-8'))
    raw={}; offsets={}; source_widths={}
    for key,src in sources.items():
        im=Image.open(OUT/src['file']).convert('RGBA');cols,rows=src['grid']
        a=np.array(im)
        groups=[g for g in components(a[:,:,3]>=200) if len(g)>100][:cols*rows]
        if len(groups)!=cols*rows:raise ValueError((key,'원안 인물 분리 수',len(groups),cols*rows))
        records=[]
        for g in groups:
            yy,xx=zip(*g);box=(min(xx),min(yy),max(xx)+1,max(yy)+1)
            cut=a[box[1]:box[3],box[0]:box[2]].copy();mask=np.zeros(cut.shape[:2],bool)
            mask[np.array(yy)-box[1],np.array(xx)-box[0]]=True
            cut[~mask]=0;cut[mask,3]=255
            records.append((box,Image.fromarray(cut)))
        records.sort(key=lambda v:(v[0][1]+v[0][3])/2)
        raw[key]=[];offsets[key]=[];source_widths[key]=im.width/cols
        for r in range(rows):
            row=sorted(records[r*cols:(r+1)*cols],key=lambda v:(v[0][0]+v[0][2])/2)
            raw[key].append([v[1] for v in row]);offsets[key].append([v[0][0]-round(c*im.width/cols) for c,v in enumerate(row)])
        src['extracted_bboxes']=[list(v[0]) for r in range(rows) for v in sorted(records[r*cols:(r+1)*cols],key=lambda v:(v[0][0]+v[0][2])/2)]
        if src.get('combine_rows'):
            raw[key]=[raw[key][0]+raw[key][1],raw[key][2]+raw[key][3]]
            offsets[key]=[offsets[key][0]+offsets[key][1],offsets[key][2]+offsets[key][3]]
    specs=[]
    def add(name,label,key,row,n,phases,mode='ground',fps=8,loop=False,indices=None,heights=None,mirror=False):
        specs.append(dict(name=name,label=label,key=key,row=row,n=n,phases=phases,mode=mode,fps=fps,loop=loop,indices=indices,heights=heights,mirror=mirror))
    walk=['왼발 접지','왼발 하강','왼발 통과','왼발 상승','오른발 접지','오른발 하강','오른발 통과','오른발 상승']
    for row,d,lab in [(0,'front','정면'),(1,'back','후면'),(2,'right','우측')]:
        add('wallhand_walk_'+d,lab+' 걷기','walk',row,8,walk,loop=True)
    add('wallhand_walk_left','좌측 걷기','walk',2,8,walk,loop=True,mirror=True)
    punch=['경계','주먹 당김','몸통 비틀기','정타','반동','회복']
    add('wallhand_punch_front','성벽 치기 · 정면','punch_fall',0,6,punch)
    add('wallhand_punch_side','성벽 치기 · 측면','punch_fall',1,6,punch)
    climb=['왼손 뻗기','왼손 걸기','왼팔 당기기','오른 무릎 지지','오른손 뻗기','오른손 걸기','오른팔 당기기','왼 무릎 지지']
    for row,d,lab in [(0,'back','후면'),(1,'front','정면')]:
        add('wallhand_climb_'+d,'등반 · '+lab,'action',row,8,climb,mode='grip',fps=6,loop=True)
    add('wallhand_lose_grip_fall','손 놓침 · 추락','punch_fall',2,6,['양손 지지','오른손 미끄러짐','왼손 놓침','균형 상실','뒤로 회전','등부터 추락'],mode='pelvis')
    add('wallhand_land_stagger','착지 · 비틀거림','punch_fall',3,6,['착지','충격 흡수','양손 지지','무릎 세움','휘청임','회복'])
    add('wallhand_trap_fall','함정 · 뒤로 쓰러짐','trap',0,4,['뒤로 흔들림','무릎 꺾임','뒤로 넘어짐','등 착지'],indices=[0,1,2,3])
    add('wallhand_trap_lie','함정 · 누워 버티기','trap',0,2,['누워 숨 들이쉬기','누워 숨 내쉬기'],indices=[4,5],fps=2,loop=True)
    add('wallhand_get_up','함정 · 일어나기','trap',1,6,['누움','팔꿈치 지지','앉기','한 무릎 세움','양손 밀기','기립'])
    smash=['경계','왼손 뻗기','흉벽 붙잡기','오른팔 당기기','주먹 들기','내려치기','충격 압축','회복']
    for row,d,lab in [(2,'front','정면'),(3,'back','후면')]:
        add('wallhand_parapet_smash_'+d,'통로 부수기 · '+lab,'action',row,8,smash)
    death=['치명타','상체 경직','무릎 풀림','기울기','균형 상실','쓰러짐','착지','잔여 움직임','몸 이완','누운 최종 자세']
    for row,d,lab in [(0,'front','정면'),(1,'side','측면')]:
        add('wallhand_death_'+d,'죽음 · '+lab,'death',row,10,death,fps=9)
    manifest={'card':'K-129','asset_status':'검토용 게임 시트 · Unity 연결 전','character':'성벽손 오우거','height_native_px':144,'height_m':4.5,'pixels_per_metre_native':32,'cell_native':list(CELL),'cell_game':[768,864],'scale_game':3,'pivot_native_top_left':[CX,GROUND],'pivot_game_top_left':[384,840],'unity_pivot_bottom_left_normalized':[.5,8/288],'baseline_native':280,'last_opaque_ground_row':279,'coordinate_system':'좌상단 원점, +x 오른쪽, +y 아래. 피벗은 화소 중심이 아닌 경계 좌표.','frame_order':'가로 한 줄, 왼쪽부터 0번','normals':{'format':'RGB tangent-space +Y (OpenGL / Unity), linear','background_rgb':[128,128,255],'alpha_source':'대응 색 시트의 알파','method':'동일 실루엣의 얕은 외곽 부조 + 약한 명도 굴곡; 3D 베이크 아님','mirror':'반전된 색 그림에서 노멀 재계산, X 방향도 반전됨'},'import_recommendation':{'filter':'Point','wrap':'Clamp','mipmaps':False,'compression':'None','max_texture_size':8192,'albedo_srgb':True,'normal_srgb':False,'normal_texture_type':'NormalMap','flip_green':False},'palette_rgb':PALETTE.tolist(),'motions':[]}
    all_frames={}
    for sp in specs:
        frames=[];metrics=[]
        # 원안 격자별 동일 축척. 걷기는 키144로 정렬한다.
        scales={'walk':144/raw['walk'][0][0].height,'action':.76 if sp['row']<2 else .90,'punch_fall':144/raw['punch_fall'][0][0].height,'trap':144/raw['trap'][1][-1].height,'death':144/raw['death'][0][0].height}
        for f in range(sp['n']):
            idx=sp['indices'][f] if sp['indices'] else f
            im=raw[sp['key']][sp['row']][idx]
            scale=scales[sp['key']]
            if sp['heights']: scale=sp['heights'][f]/im.height
            w,h=max(1,round(im.width*scale)),max(1,round(im.height*scale))
            if w>240 or h>264:
                raise ValueError((sp['name'],f,w,h,'칸 넘침'))
            im=palette(clean(im.resize((w,h),NN)))
            if sp['mirror']:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            x=CX-round((source_widths[sp['key']]/2-offsets[sp['key']][sp['row']][idx])*scale)
            if sp['mirror']:x=2*CX-x-im.width
            if sp['name']=='wallhand_trap_lie':x=CX-im.width//2
            y=GROUND-im.height
            if sp['mode']=='grip':y=16
            if sp['mode']=='pelvis':y=152-im.height//2
            canvas=Image.new('RGBA',CELL);canvas.paste(im,(x,y))
            if sp['name']=='wallhand_get_up' and f==0:canvas=all_frames['wallhand_trap_lie'][-1].copy()
            bbox=canvas.getbbox()
            anchor=[CX,GROUND] if sp['mode']=='ground' else [CX,16 if sp['mode']=='grip' else 152]
            metrics.append({'frame':f,'phase_ko':sp['phases'][f],'bbox_native':list(bbox),'anchor_native':anchor,'anchor_game':[v*3 for v in anchor],'ground_contact_y':GROUND if sp['mode']=='ground' else None,'connected_components':len(components(np.array(canvas)[:,:,3]>0)),'source_scale':scale,'source_bbox_size':list(raw[sp['key']][sp['row']][idx].size),'placement_native':[x,y]})
            frames.append(canvas)
        sheet=Image.new('RGBA',(CELL[0]*sp['n'],CELL[1]))
        nsheet=Image.new('RGB',sheet.size,(128,128,255))
        for f,im in enumerate(frames):sheet.paste(im,(f*CELL[0],0));nsheet.paste(normal(im),(f*CELL[0],0))
        for kind,image in [('native',sheet),('native_normal',nsheet),('game',sheet.resize((sheet.width*3,sheet.height*3),NN)),('game_normal',nsheet.resize((sheet.width*3,sheet.height*3),NN)),('preview_4x',sheet.resize((sheet.width*4,sheet.height*4),NN))]:
            folder=OUT/kind;folder.mkdir(exist_ok=True)
            suffix='_normal' if 'normal' in kind else ''
            image.save(folder/(sp['name']+suffix+'.png'))
        motion={k:sp[k] for k in ('name','label','n','fps','loop','mode','phases')}
        motion.update({'cell_native':list(CELL),'cell_game':[768,864],'sheet_native':list(sheet.size),'sheet_game':[sheet.width*3,sheet.height*3],'files':{'native':'native/'+sp['name']+'.png','game':'game/'+sp['name']+'.png','native_normal':'native_normal/'+sp['name']+'_normal.png','game_normal':'game_normal/'+sp['name']+'_normal.png','preview_4x':'preview_4x/'+sp['name']+'.png'},'frames':metrics})
        if sp['mirror']:motion['mirrored_from']='wallhand_walk_right'
        manifest['motions'].append(motion);all_frames[sp['name']]=frames
    manifest['ui']=[]
    for name,label,src,size,target in [('wallhand_map_icon','지도 아이콘',raw['map'][0][0],64,58),('wallhand_hud_portrait','체력 막대 초상',raw['ui'][0][1],96,90)]:
        factor=target/max(src.size)
        im=palette(clean(src.resize((round(src.width*factor),round(src.height*factor)),NN)))
        canvas=Image.new('RGBA',(size,size));canvas.paste(im,((size-im.width)//2,(size-im.height)//2))
        canvas.save(OUT/(name+'.png'));normal(canvas).save(OUT/(name+'_normal.png'))
        canvas.resize((size*4,size*4),NN).save(OUT/'preview_4x'/(name+'.png'))
        manifest['ui'].append({'name':name,'label':label,'size':[size,size],'file':name+'.png','normal':name+'_normal.png','pivot_normalized':[.5,.5],'view':'거의 수직 하향' if size==64 else '정면 상반신'})
    manifest['anchor_notes']={'ground':'지면 경계 y280, 서 있을 때 발바닥·누웠을 때 몸의 최하 접점. 공중에 뜨는 발을 지면에 억지로 내리지 않음.','grip':'양손 위쪽의 매달림 기준 (128,16). 한 손 접촉점은 프레임 내 그림에 따라 달라짐. 성벽 이동량은 시뮬레이션에서 적용.','pelvis':'추락 시 몸 중심 기준 (128,152). 실제 추락 경로·속도는 시뮬레이션에서 적용.','registration':'원안 각 칸의 중앙축을 고정. 주먹이 앞으로 뻗어도 불투명 바운드 중앙으로 재정렬하지 않음.'}
    (OUT/'generation.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    return manifest,all_frames

if __name__=='__main__':main()
