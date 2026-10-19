from pathlib import Path
from PIL import Image, ImageOps, ImageStat, ImageFilter, ImageChops, ImageDraw, ImageFont
import json, math, base64, io, random, hashlib

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
NEAR = Image.Resampling.NEAREST
S = math.sin(math.radians(25))

def uri(im):
    if isinstance(im, Path):
        data = im.read_bytes()
    else:
        b = io.BytesIO(); im.save(b, format='PNG'); data = b.getvalue()
    return 'data:image/png;base64,' + base64.b64encode(data).decode()

def read(p):
    return Image.open(p).convert('RGBA')

def main():
    specs = json.loads((OUT/'asset_specs.json').read_text(encoding='utf-8'))
    provenance = json.loads((OUT/'generation.json').read_text(encoding='utf-8-sig'))
    (OUT/'sprites').mkdir(exist_ok=True)
    (OUT/'review').mkdir(exist_ok=True)
    sprites = {}; entries = []
    for d in specs:
        name, ko, kind, w, h, wm, dm, hm, prompt = d
        source = read(OUT/provenance[name]['source'])
        alpha = source.getchannel('A')
        bbox = alpha.point(lambda a: 255 if a > 24 else 0).getbbox()
        if not bbox:
            raise ValueError(name + ': 투명 영역만 있음')
        cut = source.crop(bbox)
        canvas = Image.new('RGBA', (w,h))
        if name.startswith('path_'):
            canvas = source.resize((w,h), NEAR)
        elif kind == 'decal':
            # 생성된 낮은 시점 이미지를 지면 텍스처 좌표의 비율로 복원한다.
            canvas.alpha_composite(cut.resize((int(w*.88),int(h*.82)),NEAR),(int(w*.06),int(h*.09)))
        else:
            target_h = h-8
            if kind == 'terrain':
                target_h = {'earthwall_intact':90,'earthwall_cracked':90,'earthwall_collapsed':57,'earthwall_rubble':34,'fallen_tree':52}[name]
            scale = min((w-12)/cut.width, target_h/cut.height)
            nw, nh = max(1,round(cut.width*scale)),max(1,round(cut.height*scale))
            # 발밑의 투명 여백을 없애 밑변 중앙을 공통 기준점으로 쓴다.
            canvas.alpha_composite(cut.resize((nw,nh),NEAR),((w-nw)//2,h-nh))
        sprites[name] = canvas
        e = {'id':name,'name_ko':ko,'file':'sprites/'+name+'.png','pixel_rect':[0,0,w,h],
             'pivot':[0.5,0.5] if kind=='decal' else [0.5,0.0],
             'kind':'ground_decal' if kind=='decal' else 'upright', 'category':kind,
             'size_m':{'width':wm,'depth':dm,'height':hm},
             'orientation':'horizontal_XZ' if kind=='decal' else 'camera_facing_billboard',
             'passable_visual':kind!='terrain' or name in ['earthwall_rubble','fallen_tree','earthwall_collapsed'],
             'recommended_opacity':0.62 if kind=='decal' else 0.85}
        if kind!='decal':
            if kind=='terrain':
                e['billboard_size_m'] = {'width':5.6,'height':2.8}
            else:
                visible=canvas.getchannel('A').point(lambda a:255 if a>24 else 0).getbbox()
                e['billboard_size_m']={'width':round(wm*w/(visible[2]-visible[0]),4),'height':round(hm*h/(visible[3]-visible[1]),4)}
        entries.append(e)

    # 같은 생성 질감의 연결 띠를 공유하여 회전 없는 북쪽·동쪽 끝을 맞춘다.
    straight=sprites['path_straight']; curve=sprites['path_curve']
    edge=straight.crop((0,56,128,60))
    edge.paste((0,0,0,0),(0,0,4,4));edge.paste((0,0,0,0),(124,0,128,4))
    straight.paste(edge,(0,0)); straight.paste(edge,(0,124))
    curve.paste(edge,(0,0)); curve.paste(edge.transpose(Image.Transpose.ROTATE_270),(124,0))
    for name,im in sprites.items(): im.save(OUT/'sprites'/f'{name}.png')
    contact=Image.new('RGB',(1200,math.ceil(len(specs)/4)*190),(88,111,79))
    pen=ImageDraw.Draw(contact);font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    for i,d in enumerate(specs):
        im=sprites[d[0]];im.thumbnail((276,138),NEAR);x=(i%4)*300;y=(i//4)*190
        contact.paste(im,(x+(300-im.width)//2,y+28),im);pen.text((x+12,y+4),d[1],font=font,fill=(236,230,209))
    contact.save(OUT/'review'/'asset_contact.png')
    groups=[('decal',(512,512),(128,128)),('upright',(512,512),(128,128)),('terrain',(1024,256),(256,128))]
    for group,size,cell in groups:
        sheet=Image.new('RGBA',size); n=0
        for e in entries:
            if e['category']!=group: continue
            im=sprites[e['id']]; x=(n%(size[0]//cell[0]))*cell[0]; y=(n//(size[0]//cell[0]))*cell[1]
            sheet.paste(im,(x,y)); e['sheet']=f'{group}_sheet.png'
            e['sheet_rect']=[x,size[1]-y-im.height,im.width,im.height];n+=1
        sheet.save(OUT/f'{group}_sheet.png')
    manifest={'card':'K-068','version':1,'pixel_rect_origin':'bottom_left','pivot_units':'normalized_bottom_left',
        'size_axes':'width=X, depth=Z, height=Y; size_m is intended placement extent in meters (ground quad includes transparent margin; path is a full 5m tile). billboard_size_m is the full transparent quad; small props use the declared visible width and screen-facing height.',
        'ground_projection':'UV textures are normalized to ground plane. Apply on XZ once; review projects depth by sin(25 degrees). Do not flatten PNG again.',
        'cell_size_m':5,'camera_pitch_deg':25,'sprites':entries,
        'import_recommendations':{'filter':'Point','compression':'None','wrap':'Clamp','alpha':'straight; preserve generated alpha','mipmaps':'evaluate in engine at distance; not tested'},
        'path_connections':{'tile_size_m':[5,5],'straight':['north','south'],'curve':['north','east'],'port_pixels':128,'shared_edge_band_px':4,'rotation':'rotate quad about Y in 90 degree increments'},
        'terrain_damage_registration':'All earthwall variants use same 256x128 canvas, pivot and 5.6x2.8m billboard. Keep transform unchanged when swapping.',
        'placement_notes_ko':['데칼을 먼저 그리고 적을 그 위에 표시한다. 지면과 약간 떨어뜨려 겹침 깜빡임을 피한다.','풀·꽃·잔돌을 넓게 흩뿌리고 같은 곳에 중첩하지 않는다. 키 큰 풀과 덤불은 주요 접근로 바깥에 둔다.','그을음과 폭발 구덩이는 사건 발생 위치에만 생성한다. 토벽의 이동·엄폐·충돌 판정은 기존 지형 규칙을 따른다.','통과 가능한 나무와 토벽 잔해는 신규 장애물로 등록하지 않는다.']}
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    byid={e['id']:e for e in entries}
    rng=random.Random(68); placements=[]
    # 벽 앞쪽의 빈 지면에만 적용한 비교용 배치다.
    for _ in range(155):
        name=rng.choices(['grass_01','grass_02','grass_03','flowers_01','flowers_02','dirt_01','dirt_02','dirt_03','pebbles_01','pebbles_02'],[15,15,13,3,3,5,4,4,5,5])[0]
        px=rng.randint(20,1900);py=rng.randint(250,558)
        placements.append((name,px,py,30+(py-250)*.065))
    for name,x,y in [('bush_01',95,466),('bush_02',1760,500),('bush_03',1530,385),('tallgrass_01',270,530),('tallgrass_02',1845,420),('weapon_01',600,430),('weapon_02',1130,515),('weapon_03',1580,520),('shield',890,465),('signpost',80,370),('earthwall_intact',340,410),('earthwall_cracked',1570,440),('earthwall_collapsed',1320,510),('earthwall_rubble',1450,528),('fallen_tree',660,510)]:
        placements.append((name,x,y,38))
    # 길 연결은 별도 증거 그림에서도 확대한 채 확인한다.
    for j in range(4): placements.insert(0,('path_straight',1010,305+j*66,31.2))
    def draw_asset(im,name,x,y,ppm,opacity=None):
        e=byid[name]; art=sprites[name]
        if e['kind']=='ground_decal':
            dw=round(e['size_m']['width']*ppm); dh=round(e['size_m']['depth']*ppm*S)
            py=0.5
        else:
            dw=round(e['billboard_size_m']['width']*ppm);dh=round(e['billboard_size_m']['height']*ppm);py=1
        patch=art.resize((max(1,dw),max(1,dh)),NEAR)
        strength=e['recommended_opacity'] if opacity is None else opacity
        patch.putalpha(patch.getchannel('A').point(lambda a:round(a*strength)))
        im.alpha_composite(patch,(round(x-dw*.5),round(y-dh*py)))
    comparisons=[]
    for stem,title in [('01_pf_day_clean','평상시'),('03_pf_day_blast_clean','폭발 장면')]:
        original=read(OUT/'input'/f'{stem}.png'); after=original.copy()
        for p in placements: draw_asset(after,*p)
        if 'blast' in stem:
            for p in [('scorch_01',820,389,23),('scorch_02',1420,330,20),('crater_01',1150,450,23),('crater_02',430,470,23)]:draw_asset(after,*p)
        after.save(OUT/'review'/f'{stem}_after.png'); comparisons.append((original,after,title))
    # 실제 고블린 시트의 첫 프레임을 동일 크기·위치로 모든 배경에 사용한다.
    goblin_source=read(ROOT/'Game/Assets/_Project/Art/Characters/Goblin/goblin_walk_attack4.png')
    goblin=goblin_source.crop((0,0,256,384));bb=goblin.getchannel('A').point(lambda a:255 if a>64 else 0).getbbox()
    if not bb: raise ValueError('고블린 프레임 없음')
    goblin=goblin.crop(bb)
    tests=[];readability=[]
    for name in ['none','grass_02','dirt_01','path_straight','pebbles_02','scorch_02','crater_02','bush_02','tallgrass_02']:
        bg=read(OUT/'input'/'01_pf_day_clean.png').crop((700,420,940,540))
        if name!='none':draw_asset(bg,name,120,85,28)
        before=bg.copy();g=goblin.resize((max(1,round(goblin.width*28/goblin.height)),28),NEAR)
        gx=120-g.width//2;gy=85-g.height
        ga=g.getchannel('A');boundary=ImageChops.subtract(ga,ga.filter(ImageFilter.MinFilter(3)))
        backdrop=before.crop((gx,gy,gx+g.width,gy+g.height)).convert('L');fg=g.convert('L')
        contrasts=[abs(a-b)/255 for a,b,m in zip(backdrop.get_flattened_data(),fg.get_flattened_data(),boundary.get_flattened_data()) if m>127]
        readability.append({'background':name,'goblin_height_px':28,'mean_contour_luma_difference':round(sum(contrasts)/len(contrasts),4),'contour_fraction_above_0_10':round(sum(v>.10 for v in contrasts)/len(contrasts),4)})
        bg.alpha_composite(g,(gx,gy))
        label='기존 지면' if name=='none' else byid[name]['name_ko']
        tests.append((name,label,bg))
    (OUT/'readability.json').write_text(json.dumps({'source':'Game/Assets/_Project/Art/Characters/Goblin/goblin_walk_attack4.png','frame_rect_top_left':[0,0,256,384],'method':'Same actual goblin frame, 28px tall. Compare luminance at inner alpha contour to underlying scene. Enemy composited last. Static composite diagnostic only; not gameplay proof.','tests':readability},indent=2),encoding='utf-8')
    mosaic=Image.new('RGBA',(240*3,120*3))
    for i,(_,_,im) in enumerate(tests):mosaic.alpha_composite(im,((i%3)*240,(i//3)*120))
    mosaic.save(OUT/'review'/'goblin_readability.png')
    joins=Image.new('RGBA',(384,256),(85,108,80,255))
    joins.alpha_composite(straight,(128,0));joins.alpha_composite(curve,(128,128));joins.alpha_composite(straight.transpose(Image.Transpose.ROTATE_90),(256,128))
    joins.save(OUT/'review'/'path_connections.png')
    def picture(im,extra=''):return '<img '+extra+' src="'+uri(im)+'">'
    html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>전장 들판 꾸미기 · 1판</title><style>
    *{box-sizing:border-box}body{margin:0;background:#171e1b;color:#e1e5db;font:16px/1.65 system-ui,'Malgun Gothic',sans-serif}main{max-width:1480px;margin:auto;padding:38px 28px}h1{font-size:34px;margin:8px 0}h2{margin-top:42px;color:#d6c49a}p{max-width:1000px}small,.muted{color:#aeb9aa}button,input{accent-color:#b6c893}button{padding:8px 16px;color:#e1e5db;background:#354739;border:1px solid #819275;cursor:pointer}img{max-width:100%;image-rendering:pixelated}.view{position:relative;line-height:0;background:#405240}.view>img{display:block;width:100%}.over{position:absolute;inset:0;width:100%;height:100%;object-fit:contain;clip-path:inset(0 0 0 50%)}.toolbar{display:flex;align-items:center;gap:12px;margin:14px 0;flex-wrap:wrap}.toolbar input{flex:1}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:18px}.tile{background:#253029;padding:16px;overflow:auto}.tile h3{margin:0 0 8px}.swatch{background-color:#607857;background-image:linear-gradient(45deg,#6b8062 25%,transparent 25%),linear-gradient(-45deg,#6b8062 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#6b8062 75%),linear-gradient(-45deg,transparent 75%,#6b8062 75%);background-size:16px 16px;background-position:0 0,0 8px,8px -8px,-8px 0;padding:12px;display:block;width:max-content;max-width:none;margin:10px 0}.swatch img{max-width:none;vertical-align:bottom}details{border-top:1px solid #4d6150;padding:8px 0}code{color:#c9d2b8}a{color:#c6d8ac}.tag{font-size:13px;color:#c8c9ab}figure{margin:0}figcaption{font-size:14px;margin-top:8px}
    </style><main><div class="tag">전장 배경 검토 · 카드 068 · 2026년 10월 20일</div><h1>비어 있는 들판에 전투의 흔적을</h1><p>풀과 꽃은 작게, 흙과 발자국은 넓게, 잔해는 드물게 배치했습니다. 기존 캡처 위에 새 자산을 합성한 정지 목업입니다. 게임에 연결한 결과는 아니며 실제 조명·가림·이동 중 식별은 연결 뒤 확인해야 합니다.</p><p class="muted">빛은 왼쪽 위에서, 색은 탁한 올리브·흙갈색으로 맞췄습니다. 풀 지면 텍스처와 노멀 맵은 기존 쌍을 유지합니다. 모든 그림은 이 페이지 안에 들어 있어 서버와 인터넷 없이 열 수 있습니다.</p>'''
    for i,(before,after,title) in enumerate(comparisons):
        html+=f'<h2>{title} · 전후 비교</h2><p>왼쪽은 적용 전, 오른쪽은 적용 후입니다. 손잡이를 움직이거나 전체 화면을 골라 비교하세요.</p><div class="toolbar"><button onclick="set({i},100)">적용 전 전체</button><input aria-label="{title} 비교 위치" type="range" min="0" max="100" value="50" oninput="set({i},this.value)"><button onclick="set({i},0)">적용 후 전체</button></div><div class="view">'+picture(before)+picture(after,f'class="over" id="after{i}"')+'</div>'
    html+='<h2>고블린 식별 · 동일한 실루엣 비교</h2><p>기존 고블린의 실제 시트 첫 프레임을 높이 28화소로 맞췄습니다. 각 배경에 같은 위치·크기로 겹쳐 비교합니다. 어두운 외곽선은 풀·흙·재 위에서 유지됩니다. 덤불·키 큰 풀은 몸통과 색이 겹치므로 주요 이동 경로 밖으로 제한하고, 적보다 뒤에 그려야 합니다.</p><div class="grid">'
    for _,label,im in tests:html+='<figure class="tile">'+picture(im,'style="width:480px"')+'<figcaption>'+label+' · 2배 확대</figcaption></figure>'
    html+='</div><h2>길 조각 연결</h2><p>5m × 5m 직선·곡선 조각입니다. 위쪽에서 들어온 길이 오른쪽으로 이어집니다. 연결부 4화소 띠를 공유합니다. 아래는 지면에 눕히기 전 텍스처 확인입니다.</p>'+picture(joins,'style="width:768px"')
    html+='<h2>전체 자산 · 원본 크기와 4배 확대</h2><p>바닥 데칼은 수평 지면에 한 번 투영하도록 비율을 복원했습니다. 여기서는 텍스처 원본을 표시합니다. 덤불·무기·지형은 화면을 향해 세우며 밑변 중앙이 기준점입니다. 체크 배경은 투명 영역입니다. 큰 그림의 4배 보기는 가로로 스크롤할 수 있습니다.</p><div class="grid">'
    for e in entries:
        im=sprites[e['id']];z=e['size_m'];label='바닥 데칼' if e['kind']=='ground_decal' else '지형 빌보드' if e['category']=='terrain' else '세운 소품'
        html+=f'<article class="tile"><h3>{e["name_ko"]}</h3><div class="tag">{label} · 폭 {z["width"]}m / 깊이 {z["depth"]}m / 높이 {z["height"]}m</div><small>{im.width} × {im.height}화소 · {e["file"]}</small><div>원본 1배</div><span class="swatch">'+picture(im,f'width="{im.width}" height="{im.height}"')+'</span><details><summary>4배 확대 보기</summary><span class="swatch">'+picture(im,f'width="{im.width*4}" height="{im.height*4}"')+'</span></details></article>'
    html+='</div><h2>배치와 확인 범위</h2><p>한 칸은 5m입니다. 작은 풀·잔돌은 0.5~1.2m, 흙과 그을음은 1.8~4m 범위로 제안합니다. 키 큰 소품은 접근로 바깥에 성기게 배치합니다. 토벽 세 단계와 잔해는 같은 캔버스·기준점으로 교체합니다. 나무 잔해는 통과 가능한 낮은 장애물 흔적으로 사용합니다.</p><p>투명도·시트 사각형·크기·길 연결 화소는 파일 기준으로 검사했습니다. 이 페이지의 원본 크기는 브라우저 배율 100% 기준입니다. 실제 지면 밝기, 원근, 깊이 정렬, 원거리 흐림과 고블린 이동 중 식별은 Unity 연결 후 확인이 남아 있습니다.</p></main><script>function set(i,v){document.getElementById("after"+i).style.clipPath="inset(0 0 0 "+v+"%)";document.querySelectorAll("input")[i].value=v}</script></html>'
    review=ROOT/'_HTML/5_그림_전장/codex_K-068_들판꾸미기_v1.html';review.parent.mkdir(parents=True,exist_ok=True);review.write_text(html,encoding='utf-8')
    checks=[]
    for e in entries:
        im=read(OUT/e['file']); assert im.getchannel('A').getextrema()[0]==0
        assert im.getchannel('A').getextrema()[1]>0
        sheet=read(OUT/e['sheet']);x,y,w,h=e['sheet_rect'];assert sheet.crop((x,sheet.height-y-h,x+w,sheet.height-y)).tobytes()==im.tobytes()
        checks.append({'id':e['id'],'size_px':list(im.size),'alpha_range':list(im.getchannel('A').getextrema()),'sheet_matches':True,'sha256':hashlib.sha256((OUT/e['file']).read_bytes()).hexdigest()})
    assert straight.crop((0,0,128,4)).tobytes()==straight.crop((0,124,128,128)).tobytes()
    assert curve.crop((0,0,128,4)).tobytes()==straight.crop((0,0,128,4)).tobytes()
    assert curve.crop((124,0,128,128)).tobytes()==edge.transpose(Image.Transpose.ROTATE_270).tobytes()
    (OUT/'verification.json').write_text(json.dumps({'sprite_count':len(entries),'sprites':checks,'path_edges_equal':True,'review_self_contained':True,'unity_run':False},indent=2),encoding='utf-8')
    print(json.dumps({'sprites':len(entries),'review_bytes':review.stat().st_size,'path_edges':'pass','atlas_roundtrip':'pass'}))

if __name__=='__main__':main()
