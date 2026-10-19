from pathlib import Path
from PIL import Image
import json, base64, hashlib, html

P = Path(__file__).resolve().parent
ROOT = P.parents[2]
NN = Image.Resampling.NEAREST
ART = ROOT / 'Game/Assets/_Project/Art/Characters'

def save(im, name):
    im.save(P / (name + '.png'))
    im.resize((im.width * 4, im.height * 4), NN).save(P / (name + '_4x.png'))

def solid(im):
    im = im.convert('RGBA')
    im.putalpha(im.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    return im

def cell(im, size, foot, height, anchor=None):
    box = im.getbbox()
    crop = im.crop(box)
    width = round(crop.width * height / crop.height)
    crop = crop.resize((width, height), NN)
    left = (size[0] - width) // 2 if anchor is None else round(size[0] / 2 - (anchor - box[0]) * height / (box[3] - box[1]))
    assert left >= 0 and left + width <= size[0], (left, width, size)
    out = Image.new('RGBA', size)
    out.paste(crop, (left, foot - height))
    return out

def palette(source, targets):
    im = solid(Image.open(ART / source).crop((0, 0, 256, 384)))
    colors = sorted({p[:3] for p in im.getdata() if p[3]})
    def dist(a,b): return sum(w * (x-y)**2 for x,y,w in zip(a,b,(2,3,2)))
    return list(dict.fromkeys(min(colors, key=lambda c: dist(c, tuple(bytes.fromhex(t)))) for t in targets.split()))

def recolor(im, pal):
    im = im.copy()
    cache = {}
    def convert(c):
        if not c[3]: return (0,0,0,0)
        if c not in cache:
            cache[c] = min(pal, key=lambda p: sum(w*(a-b)**2 for a,b,w in zip(c,p,(2,3,2)))) + (255,)
        return cache[c]
    im.putdata([convert(c) for c in im.getdata()])
    return im

# 선택본은 임계 알파의 인물 범위를 최근접으로 환산한다. 생성 그림의 격자 복원 추정과 출력 규격을 구분한다.
hero_source = Image.open(P.parent / 'codex_hero_octopath_review_v6/generated.png').convert('RGBA')
hero_mask = solid(hero_source)
hero_box = hero_mask.getbbox()
hero = cell(hero_mask, (64,96), 88, 52)
save(hero, 'hero_generated_grid')
save(hero_source.resize((64,96), NN), 'hero_source_grid16')
measurement = {'source_size': list(hero_source.size), 'alpha128_bounds': list(hero_box),
    'source_height': hero_box[3]-hero_box[1], 'head_source_y': [224,610],
    'head_ratio_estimate': 386/1167, 'visible_source_grid_estimate_px':16,
    'output_normalization_px_per_dot':1167/52, 'output_figure_height':52,
    'output_head_estimate':17, 'outline_native_estimate':1,
    'method':'알파128 경계. 머리 끝 y610은 육안 표식. 52점은 출력 정규화이며 유일한 원본 격자 검출값이 아님.'}
(P/'measurement.json').write_text(json.dumps(measurement,ensure_ascii=False,indent=2),encoding='utf-8')

targets = {
 'goblin':'100909 232015 525c23 838d37 a7ad59 261c18 45352a 69523c 39251a 70482e a87a49 e8a31a ffcf51 fff5df',
 'ladder_empty':'100909 282329 4c454a 766b70 414b1c 788b2e a8b64b 342015 74442b b67a48 d0a983 f5e4c5 974e3b eba72e ffd45a fff8df',
 'ogre':'100909 494a38 777b5d a3a383 382319 78432a ad6c43 494343 888b88 d7d3c6 8e1713 d12b20 f9ead0'}
sources = {'goblin':'Goblin/goblin_walk_attack4.png', 'ladder_empty':'Static/ladder_goblin_static.png', 'ogre':'Static/ogre_static.png'}
pals = {k:palette(sources[k],targets[k]) for k in sources}
pals['ladder_carry'] = pals['ladder_empty']
configs = [
 ('goblin','goblin_selected.png',40,(64,96),88,[550,1320],[430,430]),
 ('ladder_empty','ladder_empty_selected.png',42,(64,96),88,[440,1160],[404,406]),
 ('ladder_carry','ladder_carry_proportioned.png',54,(64,96),88,[357,1250],[493,493]),
 ('ogre','ogre_selected.png',68,(96,112),104,[492,1260],[414,414])]
manifest=[]
for name, filename, height, size, foot, anchors, chins in configs:
    src=solid(Image.open(P/filename))
    alpha=src.getchannel('A')
    split=min(range(src.width*4//10,src.width*6//10), key=lambda x:(sum(alpha.getpixel((x,y))>0 for y in range(src.height)),abs(x-src.width//2)))
    for idx, direction in enumerate(('front','side')):
        offset=0 if idx==0 else split
        region=src.crop((offset,0,split if idx==0 else src.width,src.height))
        box=region.getbbox()
        anchor=anchors[idx]-offset
        out=cell(region,size,foot,height,anchor)
        out=recolor(out,pals[name])
        file=name+'_'+direction
        save(out,file)
        bodytop = [274,279][idx] if name=='ladder_carry' else box[1]
        bodyheight=round((box[3]-bodytop)*height/(box[3]-box[1]))
        headheight=round((chins[idx]-bodytop)*height/(box[3]-box[1]))
        record={'name':file,'cell':size,'foot_baseline':foot,'center_x':size[0]//2,
            'opaque_bbox':out.getbbox(),'body_height_estimate':bodyheight,'head_height_estimate':headheight,
            'head_ratio_estimate':round(headheight/bodyheight,3),'source':filename,'source_crop':box,
            'colors':len({p for p in out.getdata() if p[3]}),
            'sha256':hashlib.sha256((P/(file+'.png')).read_bytes()).hexdigest()}
        manifest.append(record)

# 비교용 원화는 첫 칸을 자른다. 새 측면이나 빈손 원화를 기존 모습으로 꾸미지 않는다.
for name, source, height in [('goblin',sources['goblin'],40),('ladder_carry','Walk/ladder_goblin_walk.png',54),('ogre','Walk/ogre_walk.png',68),('soldier','Static/archer_static.png',52)]:
    src=solid(Image.open(ART/source).crop((0,0,256,384)))
    out=cell(src,(96,112),104,height)
    save(out, 'before_'+name if name!='soldier' else 'soldier_reference')

for name in ['goblin','ladder_empty','ladder_carry','ogre']:
    src=solid(Image.open(P.parent/'codex_k064_v1'/f'{name}_front.png'))
    # 이전 방향도 현재 인물 키로 맞춰 점 밀도와 머리 비율의 차이를 비교한다.
    target=next(c[2] for c in configs if c[0]==name)
    save(cell(src,(96,112),104,target),'k064_'+name)
src=solid(Image.open(P.parent/'codex_k063_v1/archer_front_56x112.png'))
save(cell(src,(96,112),104,52),'k063_soldier')
src=solid(Image.open(P.parent/'codex_hero_octopath_review_v6/hero_idle_56x112.png'))
save(cell(src,(96,112),104,52),'hero_corrected_comparison')
(P/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(P/'palettes.json').write_text(json.dumps({k:{'source':sources.get(k,sources['ladder_empty']),'colors':['#'+bytes(c).hex() for c in v]} for k,v in pals.items()},ensure_ascii=False,indent=2),encoding='utf-8')

def aligned(name):
    im=Image.open(P/(name+'.png')).convert('RGBA')
    pad=Image.new('RGBA',(96,112))
    pad.paste(im,((96-im.width)//2,112-im.height))
    return pad

def lineup(names,name):
    out=Image.new('RGBA',(96*len(names),112))
    for i,f in enumerate(names):out.paste(aligned(f),(i*96,0))
    save(out,name)

lineup(['hero_generated_grid','soldier_reference','goblin_front','ladder_empty_front','ladder_carry_front','ogre_front'],'lineup_front')
lineup(['hero_generated_grid','soldier_reference','goblin_side','ladder_empty_side','ladder_carry_side','ogre_side'],'lineup_side')
lineup(['hero_generated_grid','hero_corrected_comparison','soldier_reference','k063_soldier'],'direction_humans')
for name in ['goblin','ladder_empty','ladder_carry','ogre']:
    names=['hero_generated_grid','k064_'+name,name+'_front',name+'_side']
    lineup(names,'direction_'+name)
    if name!='ladder_empty':lineup(['before_'+name,name+'_front',name+'_side'],'before_after_'+name)

checks=[]
for rec in manifest:
    n=rec['name'];im=Image.open(P/(n+'.png')).convert('RGBA');big=Image.open(P/(n+'_4x.png')).convert('RGBA')
    assert im.size==tuple(rec['cell'])
    assert im.getbbox()[3]==rec['foot_baseline']
    assert set(im.getchannel('A').getdata()) <= {0,255}
    assert list(big.getdata()) == list(im.resize(big.size,NN).getdata())
    pal=pals[n.rsplit('_',1)[0]]
    assert all(c[:3] in pal for c in im.getdata() if c[3])
    checks.append(n+': 칸·발선·이진 알파·최근접4배·기존 시트 팔레트 검사 통과')
(P/'validation.txt').write_text('\n'.join(checks),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False,indent=2))
