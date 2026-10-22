from pathlib import Path
import base64
import hashlib
import json
import runpy
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
PAGE = ROOT / '_HTML/6_그림_화면/codex_K-101_반사가면재제작_v1.html'
# K-092의 분류 함수와 선형 재질값을 재사용하되, 현재 시트에 맞춰 보완한다.
OLD = runpy.run_path(str(ROOT / 'Docs/References/codex_k092_v1/build_masks.py'))
MATERIALS = dict(OLD['MATERIALS'])
MATERIALS['gem'] = {'label': '지팡이 보석', 'rg': [255, 230]}
KEYS = list(MATERIALS)
COLORS = np.array([[72,77,91], [162,104,62], [80,160,210],
                   [171,225,255], [255,211,54], [150,83,163], [64,235,226]], dtype=np.uint8)
SOURCES = [
    ('Hero/hero_back_idle', (288,384), (1,1), ['후면 대기']),
    ('Hero/hero_command', (864,384), (3,1), ['지휘']),
    ('Hero/hero_walk_run_grid', (1152,1536), (4,4), ['후면 걷기','왼쪽 걷기','왼쪽 달리기','정면 걷기']),
    ('Static/wizard_back_static', (288,384), (1,1), ['후면 대기']),
    ('Static/wizard_static', (288,384), (1,1), ['정면 대기']),
    ('Static/wizard_cast', (1152,384), (4,1), ['마법 시전']),
    ('Fire/cannon_crew_ignite6', (2592,384), (6,1), ['점화']),
]
# 주변 발광 효과와 보석 본체가 같은 색이므로 시전 프레임은 본체 영역을 지정한다.
GEM_POLYGONS = [
    [(68,49),(72,51),(71,56),(69,59),(66,59),(64,54),(65,51)],
    [(66,39),(70,41),(71,46),(69,50),(66,51),(63,47),(62,44),(64,41)],
    [(64,32),(68,34),(70,38),(69,42),(66,44),(63,43),(61,38),(62,35)],
    [(71,64),(75,66),(76,70),(74,73),(71,73),(68,69),(69,66)],
]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def uri(path):
    return 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode()

def classify(a, name, frame):
    m = OLD['classify'](a, name, frame, 3)
    r,g,b = [a[:,:,i].astype(float) for i in range(3)]
    y,x = np.indices(r.shape)
    visible = a[:,:,3] > 0
    if name.startswith('hero'):
        m[:]=0
        leather=np.zeros(r.shape,bool)
        for rgb in [(53,32,24),(104,53,31),(153,82,51),(193,123,76)]:
            leather |= np.all(a[:,:,:3]==rgb,axis=2)
        m[leather&(y>=94)]=KEYS.index('leather')
        # 원본 금장 팔레트는 머리·피부·가죽 색과 분리되어 있다.
        gold = np.zeros(r.shape, bool)
        for rgb in [(126,75,18),(188,128,21),(238,188,54),(255,228,129)]:
            gold |= np.all(a[:,:,:3] == rgb, axis=2)
        m[gold] = KEYS.index('gold')
    elif name.startswith('wizard'):
        m[:]=0
        leather=(r>g*1.42)&(g>b*1.12)&(r>55)&(r<220)&(y>=100)
        m[leather]=KEYS.index('leather')
        gold = np.all(a[:,:,:3] == (233,187,72), axis=2) | np.all(a[:,:,:3] == (196,141,53), axis=2)
        m[gold] = KEYS.index('gold')
        blue = (b>r*1.15)&(g>r*1.05)&(b>80)
        pale = (b>230)&(g>230)&(r>220)
        if name == 'wizard_cast':
            region = Image.new('1',(a.shape[1],a.shape[0]))
            ImageDraw.Draw(region).polygon(GEM_POLYGONS[frame], fill=1)
            gem_region = np.array(region, dtype=bool)
            m[blue] = KEYS.index('effect')
            m[(blue|pale)&gem_region] = KEYS.index('gem')
        else:
            # 정면의 파란 눈을 보석에 포함하지 않는다.
            staff_side=(x<35) if name=='wizard_static' else (x>55)
            gy,gx = np.where(blue & visible & staff_side)
            gem_region = np.zeros(r.shape,bool)
            gem_region[max(0,gy.min()-1):gy.max()+2,max(0,gx.min()-1):gx.max()+2] = True
            m[(blue|pale)&gem_region&staff_side] = KEYS.index('gem')
    elif name == 'cannon_crew_ignite6':
        m[m==KEYS.index('gold')]=0
        # 흰 소매·금빛 천 테두리는 천으로 유지한다. 각 프레임의 불꽃 위치만 따로 제외한다.
        flame = IGNITE['frames_data'][frame]['flame']['bounds']
        x0,y0,x1,y1 = flame
        region = (x>=x0-4)&(x<=x1+4)&(y>=y0-5)&(y<=y1+4)
        m[region] = KEYS.index('effect')
        # 밝은 견장 단추만 금속으로 지정하고 흰 소매·옷단과 갈색 장갑은 제외한다.
        buttons=[[(86,77),(105,80)],[(85,79),(104,81)],[(82,81),(100,80)],
                 [(64,77),(85,73)],[(80,82),(100,82)],[(86,77),(105,80)]]
        button_region=np.zeros(r.shape,bool)
        for bx,by in buttons[frame]:
            button_region |= (x-bx)**2+(y-by)**2<=4**2
        gold=button_region&(r>180)&(g>145)&(b>70)&(b<g*1.05)&(~region)
        m[gold]=KEYS.index('gold')
    m[~visible] = 0
    return m

IGNITE = json.loads((ROOT/'Docs/References/codex_k086_v5/manifest.json').read_text(encoding='utf8'))

def bounds(alpha, cutoff):
    ys,xs = np.where(alpha>=cutoff)
    return None if len(ys)==0 else [int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]

def build():
    sheets=[]
    contacts=[]
    for relative, size, grid, motions in SOURCES:
        src=ROOT/'Game/Assets/_Project/Art/Characters'/ (relative+'.png')
        name=src.stem
        a=np.array(Image.open(src).convert('RGBA'))
        assert (a.shape[1],a.shape[0]) == size
        cw,ch=size[0]//grid[0],size[1]//grid[1]
        assert (cw,ch)==((432,384) if name=='cannon_crew_ignite6' else (288,384))
        small=a[::3,::3]
        assert np.array_equal(a,np.repeat(np.repeat(small,3,0),3,1))
        h,w=small.shape[:2]; ncw,nch=cw//3,ch//3
        ids=np.zeros((h,w),np.uint8)
        frames=[]
        for i in range(grid[0]*grid[1]):
            xx=(i%grid[0])*ncw; yy=(i//grid[0])*nch
            ids[yy:yy+nch,xx:xx+ncw]=classify(small[yy:yy+nch,xx:xx+ncw],name,i)
            patch=a[(i//grid[0])*ch:(i//grid[0]+1)*ch,(i%grid[0])*cw:(i%grid[0]+1)*cw,3]
            hard=bounds(patch,128)
            frames.append({'index':i,'row':i//grid[0],'column':i%grid[0],
                'motion':motions[i//grid[0]],'alpha_bounds':bounds(patch,1),
                'cutout_bounds':hard,'cutout_bottom_y_exclusive':hard[3] if hard else None})
        native=np.zeros_like(small); native[:,:,3]=small[:,:,3]
        for i,key in enumerate(KEYS):
            sel=(ids==i)&(small[:,:,3]>0)
            native[sel,0:2]=MATERIALS[key]['rg']
        game=np.repeat(np.repeat(native,3,0),3,1)
        target=OUT/(name+'_spec.png'); npth=OUT/'native'/(name+'_spec.png')
        pp=OUT/'previews'/(name+'_spec_4x.png'); mp=OUT/'materials'/(name+'_materials.png')
        for p in (npth,pp,mp): p.parent.mkdir(exist_ok=True,parents=True)
        Image.fromarray(game).save(target); Image.fromarray(native).save(npth)
        Image.fromarray(native).resize((w*4,h*4),Image.Resampling.NEAREST).save(pp)
        mat=np.zeros_like(small); mat[:,:,:3]=COLORS[ids]; mat[:,:,3]=small[:,:,3]
        mat[small[:,:,3]==0,:3]=0
        Image.fromarray(mat).resize((w*3,h*3),Image.Resampling.NEAREST).save(mp)
        loaded=np.array(Image.open(target)); checks={
            'size_exact':Image.open(target).size==size,
            'alpha_exact':bool(np.array_equal(loaded[:,:,3],a[:,:,3])),
            'blue_zero':bool((loaded[:,:,2]==0).all()),
            'transparent_rgb_zero':bool((loaded[a[:,:,3]==0,:3]==0).all()),
            'native_3x_roundtrip_exact':bool(np.array_equal(loaded,np.repeat(np.repeat(np.array(Image.open(npth)),3,0),3,1))),
            'preview_4x_size_exact':Image.open(pp).size==(w*4,h*4),
            'no_new_alpha_pixels':not bool(((loaded[:,:,3]>0)&(a[:,:,3]==0)).any()),
            'frame_alpha_bounds_and_foot_line_exact':True,
        }
        for f in frames:
            xx=f['column']*cw; yy=f['row']*ch
            patch=loaded[yy:yy+ch,xx:xx+cw,3]
            checks['frame_alpha_bounds_and_foot_line_exact'] &= bounds(patch,1)==f['alpha_bounds'] and bounds(patch,128)==f['cutout_bounds']
        pivot=[288,360] if name=='cannon_crew_ignite6' else [144,360]
        sheet={'name':name,'source':src.relative_to(ROOT).as_posix(),'source_sha256':digest(src),
            'mask':target.name,'mask_sha256':digest(target),'size':list(size),'cell':[cw,ch],
            'grid':list(grid),'frame_count':len(frames),'frame_order':'행 우선, 각 행 왼쪽에서 오른쪽',
            'motions':motions,'pivot_top_left':pivot,'pivot_normalized_bottom_left':[pivot[0]/cw,1-pivot[1]/ch],
            'native_mask':npth.relative_to(OUT).as_posix(),'native_size':[w,h],'native_cell':[ncw,nch],
            'native_pivot_top_left':[p//3 for p in pivot],'scale_to_game':3,
            'preview_4x':pp.relative_to(OUT).as_posix(),'material_preview':mp.relative_to(OUT).as_posix(),
            'material_pixels_native':{key:int(((ids==i)&(small[:,:,3]>0)).sum()) for i,key in enumerate(KEYS)},
            'frames':frames,'verification':checks}
        assert all(checks.values()), name
        sheets.append(sheet)
        contacts.append((name,Image.fromarray(small).crop((0,0,ncw,nch)),Image.fromarray(mat).crop((0,0,ncw,nch))))
    for s in sheets:
        assert digest(ROOT/s['source'])==s['source_sha256']
        s['verification']['source_unchanged']=True
    manifest={'card':'K-101','channels':{'R':'반사 강도, 선형 0~255','G':'매끄러움, 선형 0~255','B':'항상 0','A':'현재 게임 시트 알파 바이트 그대로'},
        'import':{'sRGB':False,'filter':'Point','mipmaps':False,'compression':'None','wrap':'Clamp','texture_type':'Default'},
        'materials':MATERIALS,'materials_note':'기본값은 K-092와 동일. 보석 본체는 별도 높은 반사값. 발광 효과는 반사 제외.',
        'baseline_note':'기준 피벗 y=360. 원본 프레임의 실제 알파 경계는 이동 없이 그대로 보존. 점화 시트의 잔여 알파도 보존.',
        'ignite_contract':'K-086 v5 원본과 동일: 432×384칸, 6칸, 피벗 (288,360). 288폭 9칸으로 해석하지 않음.',
        'all_pass':True,'sheet_count':7,'frames_verified':sum(s['frame_count'] for s in sheets),'sheets':sheets}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    embedded=[]
    for s in sheets:
        e={k:s[k] for k in ('name','size','cell','grid','frame_count','motions','pivot_top_left')}
        e.update(sprite=uri(ROOT/s['source']),spec=uri(OUT/s['mask']),material=uri(OUT/s['material_preview']))
        embedded.append(e)
    template=(OUT/'review_template.html').read_text(encoding='utf8')
    PAGE.parent.mkdir(exist_ok=True,parents=True)
    PAGE.write_text(template.replace('/*__DATA__*/',json.dumps(embedded,ensure_ascii=False,separators=(',',':'))),encoding='utf8')
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    board=Image.new('RGB',(1008,7*280+55),'#202632'); d=ImageDraw.Draw(board)
    d.text((15,12),'현재 원본 / 재질 구분 · 금장 노랑 · 보석 청록 · 발광 보라 · 무광 회색',font=font,fill='white')
    for i,(name,src,mat) in enumerate(contacts):
        y=55+i*280
        d.text((15,y+5),name,font=font,fill='white')
        for j,im in enumerate((src,mat)):
            im=im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST)
            board.paste(im,(340+j*310,y),im)
    board.save(OUT/'material_contact.png')
    print(json.dumps({'sheets':len(sheets),'frames':manifest['frames_verified'],'all_pass':True,'page_bytes':PAGE.stat().st_size}))

if __name__=='__main__':
    build()
