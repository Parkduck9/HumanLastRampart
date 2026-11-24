from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import json, hashlib, base64, math

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
PAGE = ROOT/'_HTML/5_그림_전장/codex_K-092_반사마스크_v1.html'
MATERIALS = {
    'matte': {'label':'천·피부·머리·흙·나무', 'rg':[13,26]},
    'leather': {'label':'가죽 장화·장갑·허리띠', 'rg':[77,64]},
    'iron': {'label':'거친 철·포신·바퀴 테', 'rg':[242,140]},
    'polished': {'label':'총열·금속 고리·밝은 철', 'rg':[255,191]},
    'gold': {'label':'금색 금속 장식·버클', 'rg':[255,217]},
    'effect': {'label':'섬광·연기·발광 궤적', 'rg':[0,0]},
}
KEYS=list(MATERIALS)

def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def uri(p): return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()

def line_region(shape,a,b,radius):
    y,x=np.indices(shape); a=np.array(a); b=np.array(b); v=b-a
    t=np.clip(((x-a[0])*v[0]+(y-a[1])*v[1])/max(1,float(v@v)),0,1)
    return (x-a[0]-t*v[0])**2+(y-a[1]-t*v[1])**2<=radius**2

BARRELS={
    'gunner_static': [((196,185),(177,240),4.5)],
    'gunner_run': [((246,229),(185,249),5),((253,231),(193,250),5),((258,231),(198,252),5),((254,232),(194,251),5)],
    'gunner_fall': [((242,229),(171,266),5),((237,244),(166,278),5),((240,294),(173,317),5),((269,350),(204,347),5)],
    'gunner_fire': [((231,158),(182,202),4.5),((258,177),(205,196),4.5),((237,181),(193,201),4.5),((234,155),(188,195),4.5),((250,293),(202,264),4.5),((219,148),(179,195),4.5)],
}

def classify(a,name,frame,scale=1):
    r,g,b=[a[:,:,i].astype(float) for i in range(3)]
    y,x=np.indices(r.shape)
    visible=a[:,:,3]>0
    solid=a[:,:,3]>=128
    bright=np.maximum.reduce([r,g,b]); dark=bright<38
    warm=(r>g*1.42)&(g>b*1.12)&(r>55)&(r<220)
    m=np.zeros(r.shape,dtype=np.uint8)
    ys,xs=np.where(solid)
    if not len(ys): return m
    top,bottom=int(ys.min()),int(ys.max())
    height=max(1,bottom-top)
    # 색이 같은 머리와 장화를 높이로 구분한다. 외곽선에는 금속을 덧칠하지 않는다.
    lower=y>top+height*.61
    torso=(y>top+height*.39)&(y<top+height*.80)
    if name in ('muzzle_smoke','bullet_trace'):
        m[visible]=KEYS.index('effect'); return m
    if name.startswith('haul_cannon') or name=='cannon_recoil':
        grey=(np.abs(r-g)<23)&(np.abs(g-b)<24)&(b>=g*.86)&(r<=g*1.24)&(bright>38)
        m[grey]=KEYS.index('iron')
        m[grey&(bright>112)]=KEYS.index('polished')
        # 나무 살과 갈색 포가는 낮은 반사값을 유지한다.
    elif name=='cannonball':
        m[~dark]=KEYS.index('iron')
    elif name=='arrow':
        m[(x>r.shape[1]*.70)&(~dark)]=KEYS.index('iron')
    elif name in ('ladder_front','ladder_side'):
        m[(np.maximum.reduce([r,g,b])-np.minimum.reduce([r,g,b])<35)&(~dark)]=KEYS.index('iron')
    elif name.startswith('hero') or name.startswith('wizard'):
        m[warm&(y>top+height*.80)]=KEYS.index('leather')
        gold=(r>72)&(g>r*.49)&(g<r*.92)&(b<g*.45)&(y>top+height*(.48 if name.startswith('wizard') else .37))
        m[gold]=KEYS.index('gold')
    elif name.startswith('ogre'):
        m[warm&lower]=KEYS.index('leather')
        grey=(np.abs(r-g)<16)&(np.abs(g-b)<15)&(b>g*.91)&(bright>60)&torso
        m[grey]=KEYS.index('iron')
        # 장갑과 가죽띠는 갈색, 회색의 버클·징만 철로 지정한다.
    elif 'goblin' in name:
        skin=(g>r*.95)&(g>b*1.2)&(r>60)&solid
        sy,sx=np.where(skin)
        center=float(np.median(sx)) if len(sx) else r.shape[1]*.5
        if 'ladder' in name:
            right=float(sx.max()+2) if len(sx) else r.shape[1]*.56
            m[warm&lower&(x<right)]=KEYS.index('leather')
        else:
            straps=torso&(y<top+height*.73)&(np.abs(x-center)<r.shape[1]*.14)
            m[warm&straps]=KEYS.index('leather')
        gold=(r>185)&(g>r*.55)&(g<r*.92)&(b<g*.52)&torso
        m[gold]=KEYS.index('gold')
        if 'ladder' in name:
            grey=(b>=r*.98)&(np.abs(r-g)<18)&(bright>110)
            m[grey]=KEYS.index('iron')
    else:
        m[warm&lower]=KEYS.index('leather')
        # 피부색에 가까운 흰 옷깃과 견장은 천으로 취급한다.
        if name in BARRELS:
            aa,bb,rr=BARRELS[name][frame]
            region=line_region(r.shape,np.array(aa)/scale,np.array(bb)/scale,rr/scale)
            m[region&(~dark)]=KEYS.index('polished')
        if name.startswith('cannon_crew') or name.startswith('crew_'):
            gold=(r>145)&(g>r*.50)&(g<r*.82)&(b<g*.55)&torso
            m[gold]=KEYS.index('gold')
    m[dark]=0
    m[~visible]=0
    return m

def native_factor(a,cw,ch):
    # 원본이 정확한 정수 최근접 격자일 때만 축소본을 만든다.
    for s in (3,2):
        h,w=a.shape[:2]
        if cw%s or ch%s or h%s or w%s: continue
        small=a[::s,::s]
        if np.array_equal(a,np.repeat(np.repeat(small,s,axis=0),s,axis=1)): return s
    return 1

def build():
    inventory=json.loads((OUT/'source_inventory.json').read_text(encoding='utf8'))
    sheets=[]; checks=[]; contacts=[]
    for item in inventory:
        src=ROOT/item['path']; name=src.stem
        a=np.array(Image.open(src).convert('RGBA')); h,w=a.shape[:2]
        cw,ch=item['cell']; cols=w//cw; rows=h//ch
        factor=native_factor(a,cw,ch)
        assert w%cw==0 and h%ch==0
        folder=Path('Cannon') if '/Battle/' in item['path'] else Path('Characters')/src.parent.name
        target=OUT/folder/(name+'_spec.png'); target.parent.mkdir(parents=True,exist_ok=True)
        output=np.zeros_like(a); output[:,:,3]=a[:,:,3]
        ids=np.zeros((h,w),dtype=np.uint8); frames=[]
        for i in range(cols*rows):
            xx=(i%cols)*cw; yy=(i//cols)*ch
            part=a[yy:yy+ch,xx:xx+cw]
            mat=classify(part[::factor,::factor],name,i,factor)
            mat=np.repeat(np.repeat(mat,factor,axis=0),factor,axis=1)
            ids[yy:yy+ch,xx:xx+cw]=mat
            ys,xs=np.where(part[:,:,3]>0); sy,sx=np.where(part[:,:,3]>=128)
            frames.append({'index':i,'row':i//cols,'column':i%cols,'alpha_bounds': None if not len(ys) else [int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)],'cutout_bottom_y':None if not len(sy) else int(sy.max()+1)})
        for idx,key in enumerate(KEYS):
            sel=(ids==idx)&(a[:,:,3]>0)
            output[sel,0]=MATERIALS[key]['rg'][0]; output[sel,1]=MATERIALS[key]['rg'][1]
        Image.fromarray(output).save(target)
        colors=np.array([[84,92,111],[163,103,57],[79,164,215],[168,225,255],[255,213,57],[118,83,138]],dtype=np.uint8)
        contact=np.zeros_like(a); contact[:,:,:3]=colors[ids]; contact[:,:,3]=a[:,:,3]
        contacts.append(Image.fromarray(contact).crop((0,0,cw,ch)))
        native=OUT/'native'/folder/(name+'_spec.png'); native.parent.mkdir(parents=True,exist_ok=True)
        # 구형 시트에 없는 96×128 원본을 임의로 만들어 픽셀을 이동시키지 않는다.
        Image.fromarray(output[::factor,::factor]).save(native)
        preview=OUT/'previews'/folder/(name+'_spec_4x.png'); preview.parent.mkdir(parents=True,exist_ok=True)
        small=Image.fromarray(output[::factor,::factor])
        small.resize((small.width*4,small.height*4),Image.Resampling.NEAREST).save(preview)
        normal=src.with_name(name+'_normal.png')
        if not normal.exists() and name=='ladder_goblin_carry_run4': normal=src.with_name('ladder_goblin_carry_normal.png')
        usable=normal.exists() and Image.open(normal).size==(w,h)
        normal_note='일치하는 기존 노멀맵 사용' if usable else ('기존 노멀맵 크기 불일치: 평면 법선' if normal.exists() else '노멀맵 없음: 평면 법선')
        if ch==384 and cw==288: pivot=[144,360]
        elif name.startswith('haul_cannon') and name!='haul_cannon_wheel': pivot=[336,352]
        elif name=='haul_cannon_wheel': pivot=[50.5,50.5]
        elif name in ('ladder_front','ladder_side'): pivot=[128,1472]
        elif ch==384: pivot=[cw/2,352]
        elif name in ('cannon_recoil','muzzle_smoke'): pivot=[128,240]
        else: pivot=[cw/2,ch/2]
        counts={k:int(np.count_nonzero((ids==i)&(a[:,:,3]>0))) for i,k in enumerate(KEYS)}
        sheet={'name':name,'source':item['path'],'source_sha256':digest(src),'mask':target.relative_to(OUT).as_posix(),'mask_sha256':digest(target),'size':[w,h],'cell':[cw,ch],'grid':[cols,rows],'frame_count':cols*rows,'frame_order':'왼쪽 위부터 행 우선, 각 행 왼쪽→오른쪽','pivot_top_left':pivot,'alpha_cutoff':0.5,'frames':frames,'native_mask':native.relative_to(OUT).as_posix(),'native_cell':[cw//factor,ch//factor],'native_scale_to_game':factor,'native_note':'정수 최근접 격자 복원' if factor>1 else '현재 원본 해상도 보존; 손실 없는 축소 원본 없음','preview_4x':preview.relative_to(OUT).as_posix(),'preview_frames':list(range(cols*rows)),'normal_source':normal.relative_to(ROOT).as_posix() if normal.exists() else None,'normal_used_in_review':usable,'normal_note':normal_note,'material_pixels':counts}
        sheets.append(sheet)
        loaded=np.array(Image.open(target)); n=np.array(Image.open(native))
        frame_checks=[]
        for f in frames:
            xx=f['column']*cw; yy=f['row']*ch
            patch=loaded[yy:yy+ch,xx:xx+cw,3]
            by,bx=np.where(patch>0); gy,gx=np.where(patch>=128)
            bounds=None if not len(by) else [int(bx.min()),int(by.min()),int(bx.max()+1),int(by.max()+1)]
            bottom=None if not len(gy) else int(gy.max()+1)
            frame_checks.append(bounds==f['alpha_bounds'] and bottom==f['cutout_bottom_y'])
        checks.append({'name':name,'size_match':loaded.shape==a.shape,'alpha_exact':bool(np.array_equal(loaded[:,:,3],a[:,:,3])),'blue_zero':bool((loaded[:,:,2]==0).all()),'transparent_rgb_zero':bool((loaded[a[:,:,3]==0,:3]==0).all()),'native_roundtrip_exact':bool(np.array_equal(loaded,np.repeat(np.repeat(n,factor,axis=0),factor,axis=1))),'preview_size_4x':Image.open(preview).size==(n.shape[1]*4,n.shape[0]*4),'source_unchanged':digest(src)==sheet['source_sha256'],'frame_bounds_and_foot_line_exact':all(frame_checks),'frames_checked':len(frame_checks),'new_alpha_pixels':int(np.count_nonzero((loaded[:,:,3]>0)&(a[:,:,3]==0)))})
    board=Image.new('RGB',(1200,100+math.ceil(len(sheets)/6)*260),'#303640'); draw=ImageDraw.Draw(board)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',14)
    draw.text((14,8),'재질 구분 검토 · 각 시트 첫 프레임 (원본과 겹치는 픽셀만 표시)',font=font,fill='white')
    for i,key in enumerate(KEYS):
        col=tuple(int(v) for v in colors[i]); px=14+i*196
        draw.rectangle((px,45,px+14,59),fill=col)
        draw.text((px+20,41),['무광','가죽','거친 철','밝은 철·총열','금장','발광 효과'][i],font=font,fill='white')
    for i,(s,im) in enumerate(zip(sheets,contacts)):
        im.thumbnail((190,218),Image.Resampling.NEAREST); px=(i%6)*200; py=90+(i//6)*260
        board.paste(im,(px+(200-im.width)//2,py+26),im)
        draw.text((px+3,py+3),s['name'],fill='white',font_size=10)
    board.save(OUT/'material_contact.png')
    manifest={'card':'K-092','channels':{'R':'반사 강도 (선형 데이터)','G':'매끄러움 (선형 데이터)','B':'미사용, 0','A':'원본 알파 바이트 그대로'},'import':{'sRGB':False,'filter':'Point','mipmaps':False,'compression':'None','wrap':'Clamp','texture_type':'Default; Normal map으로 가져오지 않음'},'materials':MATERIALS,'source_contract':'원본 크기·격자·프레임·피벗 보존. D-081 외 규격은 개별 항목 참조.','sheets':sheets}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    passed=all(all(v for k,v in c.items() if isinstance(v,bool)) and c['new_alpha_pixels']==0 for c in checks)
    (OUT/'validation.json').write_text(json.dumps({'sheet_count':len(sheets),'all_pass':passed,'checks':checks},ensure_ascii=False,indent=2),encoding='utf8')
    assert passed, '픽셀 검증 실패: validation.json 확인'
    embedded=[]
    for s in sheets:
        obj={k:s[k] for k in ['name','size','cell','grid','frame_count','normal_note','normal_used_in_review','mask','material_pixels']}
        obj.update({'sprite':uri(ROOT/s['source']),'spec':uri(OUT/s['mask']),'normal':uri(ROOT/s['normal_source']) if s['normal_used_in_review'] else None})
        embedded.append(obj)
    template=(OUT/'review_template.html').read_text(encoding='utf8')
    PAGE.parent.mkdir(parents=True,exist_ok=True)
    PAGE.write_text(template.replace('/*__DATA__*/',json.dumps(embedded,ensure_ascii=False,separators=(',',':'))),encoding='utf8')
    print(json.dumps({'sheets':len(sheets),'frames':sum(s['frame_count'] for s in sheets),'normal_usable':sum(s['normal_used_in_review'] for s in sheets),'native_factors':{str(i):sum(s['native_scale_to_game']==i for s in sheets) for i in (1,2,3)},'checks_passed':passed,'html_bytes':PAGE.stat().st_size}))

if __name__=='__main__': build()
