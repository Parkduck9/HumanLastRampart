from pathlib import Path
from PIL import Image
import json, hashlib

OUT=Path(__file__).resolve().parent
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
results=[]
for item in manifest['assets']:
    p=OUT/item['file']
    im=Image.open(p).convert('RGBA')
    a=im.getchannel('A')
    record=dict(file=item['file'],size=list(im.size),size_matches=list(im.size)==item['size'],alpha_extrema=list(a.getextrema()),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
    if item['role']=='title_logo':
        record['transparent_border']=all(a.crop(box).getextrema()==(0,0) for box in [(0,0,im.width,1),(0,im.height-1,im.width,im.height),(0,0,1,im.height),(im.width-1,0,im.width,im.height)])
        record['has_transparency']=a.getextrema()==(0,255)
        # 투명 영역에 숨은 RGB 값은 렌더링과 무관하다. 눈에 보이는 고립 픽셀만 센다.
        data=a.tobytes(); isolated=0
        for y in range(1,im.height-1):
            for x in range(1,im.width-1):
                i=y*im.width+x
                if data[i]>=128 and not any(data[i+dy*im.width+dx]>=128 for dy in [-1,0,1] for dx in [-1,0,1] if dx or dy):isolated+=1
        record['isolated_visible_pixels']=isolated
    if item['role'] in ['menu_background','story_background']:
        record['fully_opaque']=a.getextrema()==(255,255)
        record['dialogue_lower_third_mean_luminance_0_255']=round(sum(im.crop((0,720,1920,1080)).convert('L').get_flattened_data())/(1920*360),2)
    results.append(record)
assert all(r['size_matches'] for r in results)
assert all(r.get('fully_opaque',True) and r.get('transparent_border',True) and r.get('has_transparency',True) for r in results)
result=dict(assets=results,foot_baseline='not applicable: static backgrounds and logo',visual_review=['Korean logo text checked: 빛을 잇는 자들','No recognizable hero in newly generated images','No visible stray marks in inspected backgrounds; tiny anonymous expedition only','Lower third calm; fortress framing retained before/after relighting'])
(OUT/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([r for r in results if r.get('isolated_visible_pixels') is not None]))
