from pathlib import Path
from PIL import Image, ImageDraw
import base64, hashlib, io, json, math, shutil

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
NATIVE = (1280, 360)
GAME = (3840, 1080)
CROP = (0, 160, 2048, 736)
NN = Image.Resampling.NEAREST

def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def uri(im):
    b = io.BytesIO()
    im.save(b, format='PNG')
    return 'data:image/png;base64,' + base64.b64encode(b.getvalue()).decode()

def native_point(p):
    return (round(p[0] * .625), round((p[1] - 160) * .625))

def game_point(p):
    return tuple(v * 3 for v in native_point(p))

def ppm(depth):
    return (219 / 1.7) / (1 + depth / 8)

def row(depth):
    return -60 + 1080 / (1 + depth / 8)

def depth_at(y):
    return 8 * (1080 / (y + 60) - 1)

def scale_at(y):
    return 3 * (y + 60) / 1080

def draw_hero(canvas, hero, x, y):
    s = scale_at(y)
    w, h = round(96 * s), round(128 * s)
    px, py = round(48 * w / 96), round(120 * h / 128)
    shadow = Image.new('RGBA', GAME)
    d = ImageDraw.Draw(shadow)
    rx, ry = round(13 * s), round(3 * s)
    d.ellipse((x-rx, y-ry, x+rx, y+ry), fill=(26, 19, 24, 85))
    canvas.alpha_composite(shadow)
    canvas.alpha_composite(hero.resize((w, h), NN), (round(x-px), round(y-py)))
    return {'foot_px': [x, y], 'depth_m': round(depth_at(y), 5), 'native_hero_scale': round(s, 6), 'figure_height_px': round(73*s, 3), 'cell_px': [w,h], 'cell_pivot_px': [px,py]}

def build():
    for name in ['native', 'occluders', 'previews', 'source']:
        (OUT / name).mkdir(parents=True, exist_ok=True)
    source = OUT / 'source/generated_wide.png'
    im = Image.open(source).convert('RGB')
    assert im.size == (2048,768), im.size
    bg = im.crop(CROP).resize(NATIVE, NN).quantize(colors=192).convert('RGB')
    bg.save(OUT / 'native/prep_yard.png')
    bg_game = bg.resize(GAME, NN)
    bg_game.save(OUT / 'prep_yard.png')
    # 영역은 생성 원안의 좌표이며, 모든 납품 레이어는 같은 격자 변환을 거친다.
    specs = [
        ('left_pillar', '앞쪽 왼쪽 돌기둥', 1053, [[(143,563),(155,553),(184,553),(185,545),(211,545),(215,539),(230,541),(231,549),(249,552),(249,560),(265,568),(270,589),(270,700),(252,713),(216,711),(197,703),(159,700),(148,687)]], [(150,683),(270,683),(270,715),(150,715)]),
        ('barrels', '앞쪽 나무통 두 개', 1053, [[(365,617),(373,608),(387,603),(413,604),(431,611),(440,624),(447,670),(443,687),(434,698),(415,704),(388,701),(373,691),(366,675),(362,643)],[(432,657),(440,649),(459,644),(480,646),(493,654),(498,670),(502,696),(496,710),(481,720),(455,722),(438,715),(431,701),(428,677)]], [(363,685),(440,685),(440,700),(499,700),(499,724),(429,724),(429,709),(363,709)]),
        ('left_banner', '앞쪽 왼쪽 깃발과 기둥', 1020, [[(67,459),(75,462),(79,472),(78,484),(86,488),(138,504),(146,508),(146,517),(139,522),(136,517),(136,646),(132,655),(127,646),(119,650),(111,660),(104,653),(93,661),(86,653),(81,658),(78,512),(72,510),(76,671),(66,676),(64,505),(59,501),(57,490),(62,484),(64,476),(63,467)]], [(61,665),(80,665),(80,682),(61,682)]),
        ('right_banner', '앞쪽 오른쪽 깃발과 기둥', 1080, [[(1831,501),(1839,499),(1845,505),(1844,518),(1850,524),(1904,544),(1911,548),(1910,559),(1903,561),(1897,555),(1898,733),(1893,742),(1885,730),(1876,738),(1868,731),(1857,742),(1848,733),(1841,737),(1840,548),(1837,547),(1841,753),(1831,757),(1828,542),(1822,537),(1821,527),(1827,519),(1827,510)]], [(1827,736),(1847,736),(1847,767),(1827,767)])
    ]
    occs = []
    for ident, name, baseline, polygons, footprint in specs:
        baseline = {'left_pillar':1038, 'left_banner':969}.get(ident,baseline)
        alpha = Image.new('L', NATIVE)
        d = ImageDraw.Draw(alpha)
        for poly in polygons:
            d.polygon([native_point(p) for p in poly], fill=255)
        layer = bg.convert('RGBA')
        layer.putalpha(alpha)
        # 완전 투명한 곳의 RGB도 비워 가장자리 보간 때 색이 섞이지 않게 한다.
        clean = Image.new('RGBA', NATIVE)
        clean.paste(layer, (0,0), alpha)
        clean.save(OUT / f'native/{ident}.png')
        game = clean.resize(GAME, NN)
        game.save(OUT / f'occluders/{ident}.png')
        clean.resize((5120,1440), NN).save(OUT / f'previews/{ident}_4x.png')
        occs.append({'id': ident, 'name_ko': name, 'file': f'occluders/{ident}.png', 'native_file': f'native/{ident}.png', 'size_px': list(GAME), 'pivot_px': [0,1080], 'bounds_px': list(game.getbbox()), 'sort_foot_row_px': baseline, 'collision_polygon_px': [game_point(p) for p in footprint], '_image': game})
    mask = Image.new('L', NATIVE, 255)
    d = ImageDraw.Draw(mask)
    # 왼쪽/오른쪽 앞담과 적재물은 넉넉히 막고, 나무통 밑면만 별도로 막는다.
    for poly in [[(0,539),(154,539),(281,620),(356,641),(373,736),(0,736)],[(2048,537),(1930,537),(1895,583),(1705,583),(1680,644),(1580,666),(1570,736),(2048,736)]]:
        d.polygon([native_point(p) for p in poly], fill=0)
    for occ in occs:
        d.polygon([(x//3,y//3) for x,y in occ['collision_polygon_px']], fill=0)
    # 점의 중심이 서는 영역이다. 충돌 면 주위에 약 0.2m 몸 반경 여유를 추가한다.
    from PIL import ImageFilter
    mask = mask.filter(ImageFilter.MinFilter(7))
    d = ImageDraw.Draw(mask)
    for rect in [(0,0,1279,159),(0,341,1279,359),(0,0,29,359),(1251,0,1279,359)]:
        d.rectangle(rect, fill=0)
    mask.save(OUT / 'native/walkable_mask.png')
    mask_game = mask.resize(GAME,NN)
    mask_game.save(OUT / 'walkable_mask.png')
    hero_path = ROOT / 'Docs/References/codex_k070_v1/hero_native_96x128.png'
    hero = Image.open(hero_path).convert('RGBA')
    stations = []
    for ident,name,x in [('repair','성벽 수리',750),('barracks','병영',1305),('traps','함정 공방',2010),('shop','상점',2700),('stairs','성벽 계단',3330)]:
        stations.append({'id': ident,'name_ko':name,'door_px':[x,459],'interaction_px':[x,510],'trigger_rect_px':[x-90,492,180,90],'world_interaction_m':[round((x-1920)/ppm(depth_at(510)),5),round(depth_at(510),5)],'approach_lane_px':[x,510,x,990]})
    for st in stations:
        lane_end = 990
        while mask_game.getpixel((st['interaction_px'][0],lane_end)) != 255:
            lane_end -= 3
        st['approach_lane_px'][-1] = lane_end
    mock = bg_game.convert('RGBA')
    placements = []
    for st,y in zip(stations,[510,615,1008,870,735]):
        x=st['interaction_px'][0]
        assert mask_game.getpixel((x,y)) == 255
        p = draw_hero(mock,hero,x,y)
        p['station_id']=st['id']
        placements.append(p)
    for occ in occs:
        mock.alpha_composite(occ['_image'])
    mock.convert('RGB').save(OUT/'mockup_stations.png')
    bg.resize((5120,1440),NN).save(OUT/'previews/prep_yard_4x.png')
    mask.resize((5120,1440),NN).save(OUT/'previews/walkable_mask_4x.png')
    mock_native = mock.resize(NATIVE,NN)
    mock_native.save(OUT/'native/mockup_stations.png')
    mock_native.resize((5120,1440),NN).save(OUT/'previews/mockup_stations_4x.png')
    hero.save(OUT/'native/hero_reference_96x128.png')
    camera_h = 1080*math.cos(math.pi/6)/(219/1.7)
    focal = (219/1.7)*8*math.cos(math.pi/6)
    public_occs = [{k:v for k,v in o.items() if k!='_image'} for o in occs]
    manifest = {'card':'K-089','version':1,'status':'asset proposal; engine integration pending','coordinate_system':{'origin':'top_left','x':'right','y':'down','rect':'[x,y,width,height]; right/bottom exclusive','pixel_edges':'foot is the boundary below the last opaque row'},'background':{'file':'prep_yard.png','native_file':'native/prep_yard.png','size_px':list(GAME),'native_size_px':list(NATIVE),'upscale':3,'filter':'nearest','frame_count':1,'pivot_px':[0,1080],'source':'source/generated_wide.png','source_crop_px':list(CROP),'processing':'crop excess skyline/foreground without stretching; nearest 1280x360; 192-color palette; nearest 3x'},'walkable':{'file':'walkable_mask.png','native_file':'native/walkable_mask.png','white':255,'black':0,'semantics':'hero foot center; approximately 0.2m clearance baked in, no second inflation','horizon_y_px':-60,'horizon_visible':False,'nearest_row_px':1020,'farthest_row_px':480,'near_depth_m':0,'far_depth_m':8,'depth_m':8,'near_horizontal_pixels_per_metre':219/1.7,'far_horizontal_pixels_per_metre':109.5/1.7,'near_projected_depth_pixels_per_metre':135,'far_projected_depth_pixels_per_metre':33.75,'near_native_hero_scale':3,'far_native_hero_scale':1.5,'near_game_sheet_scale':1,'far_game_sheet_scale':.5,'near_hero_figure_height_px':219,'far_hero_figure_height_px':109.5,'world_origin_px':[1920,1020],'formula':{'d':'depth from nearest line, 0..8 metres','q':'1 + d/8','x_px':'1920 + (219/1.7) * X_m / q','y_px':'-60 + 1080/q','inverse_d_m':'8*(1080/(y_px+60)-1)','inverse_X_m':'(x_px-1920)*(1+d_m/8)/(219/1.7)','hero_native_scale':'3/q'},'notes_ko':'위아래 이동 8m를 우선한다. 브리프의 이전 2~4m 예시보다 최종 사용자 결정 6~10m를 우선했다. 먼 선은 전역 깊이 한계이고 실제 통행은 마스크를 따른다.'},'camera_suggestion':{'pitch_down_deg':30,'focal_length_px':focal,'principal_point_px':[1920,-60+focal*math.tan(math.pi/6)],'height_m':camera_h,'near_ground_forward_m':(8*math.cos(math.pi/6)-camera_h*.5)/math.cos(math.pi/6),'camera_model':'off-axis pinhole, world X horizontal; ground Z away; screen-facing billboard hero; panorama is a fixed projection with horizontal image crop/pan','calibration_limit_ko':'생성 그림의 모든 돌·건물을 실측한 카메라 복원은 아니다. 바닥 투영과 캐릭터 배치를 위한 일관된 제안값이다.'},'stations':stations,'occluders':public_occs,'occlusion':{'background_includes_props':True,'rule':'base background -> shadow -> hero -> occluder only when hero foot y <= occluder sort_foot_row_px; in front, draw hero after that occluder','alpha':'binary straight alpha; extracted pixels equal background at identical coordinates','notes_ko':'배경에도 같은 물체가 있다. 별도 레이어는 뒤로 간 인물 위에 물체를 다시 그리는 용도다. 정적인 지면 그림자는 배경에 남고 인물 그림자는 별도로 그린다.'},'hero':{'source':str(hero_path.relative_to(ROOT)).replace('\\','/'),'native_cell_px':[96,128],'native_pivot_px':[48,120],'native_figure_height_px':73,'game_cell_px':[288,384],'game_pivot_px':[144,360],'orientation':'single existing front idle frame; not a new motion sheet'},'mockup':{'file':'mockup_stations.png','frame_count':1,'size_px':list(GAME),'placements':placements,'notes_ko':'다섯 인물은 같은 영웅을 서로 다른 깊이에 놓은 비교 표본이다. 실제 게임 캡처가 아니다.'},'preview_policy':'previews are native image x4 nearest; mockup preview is game image x4/3 to preserve actual compositing','sources_sha256':{str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in [hero_path, ROOT/'Game/Assets/_Project/Art/UI/Resources/ScreenArt/prep.png',ROOT/'Game/Assets/_Project/Meta/PrepYard.cs',ROOT/'Game/Assets/_Project/Meta/PrepEntry.cs']}}
    manifest['preview_policy']='previews are native images x4 nearest; native mockup is downsampled from the composited game mockup before 4x enlargement'
    dump(OUT/'manifest.json',manifest)
    payload = {'manifest':manifest,'images':{'background':uri(bg_game),'mask':uri(mask_game),'hero':uri(hero),'mockup':uri(mock),'current':uri(Image.open(ROOT/'Game/Assets/_Project/Art/UI/Resources/ScreenArt/prep.png'))},'occluders':[{'id':o['id'],'uri':uri(o['_image'])} for o in occs]}
    template=(OUT/'source/review_template.html').read_text(encoding='utf-8')
    html=template.replace('__PAYLOAD__',json.dumps(payload,ensure_ascii=False))
    review=ROOT/'_HTML/6_그림_화면/codex_K-089_정비마당배경_v1.html'
    review.parent.mkdir(parents=True,exist_ok=True)
    review.write_text(html,encoding='utf-8')
    print('Built K-089 assets and embedded review.')

if __name__=='__main__':
    build()
