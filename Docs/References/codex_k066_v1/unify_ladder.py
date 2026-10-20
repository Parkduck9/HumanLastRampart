from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib

P = Path(__file__).resolve().parent

def unify():
    palette = json.loads((P/'palettes.json').read_text(encoding='utf-8'))['ladder_empty']['colors']
    edge, hair, dark, grey, skinshade, skin, skinlight, leather, wood, woodlight, bone, bright, red, gold, yellow, white = palette
    records = json.loads((P/'manifest.json').read_text(encoding='utf-8'))
    for direction in ('front', 'side'):
        base = Image.open(P/f'ladder_empty_{direction}.png').convert('RGBA')
        body = base.copy()
        pen = ImageDraw.Draw(body)
        if direction == 'front':
            # 오른팔만 접고 손가락을 오른쪽 사다리 기둥에 감는다.
            pen.rectangle((40,65,43,75), fill=(0,0,0,0))
            points = [(39,63),(42,68),(48,68)]
            rail = (47,55)
        else:
            # 머리·옷·다리를 보존하고 보이는 팔만 앞으로 접는다.
            pen.rectangle((35,65,39,75), fill=(0,0,0,0))
            points = [(35,63),(34,68),(20,68)]
            rail = (12,20)
        out = Image.new('RGBA',base.size)
        ladder = ImageDraw.Draw(out)
        for x in rail:
            ladder.rectangle((x-1,34,x+2,85),fill=edge)
            ladder.rectangle((x,35,x+1,84),fill=wood)
            ladder.line((x,35,x,84),fill=woodlight)
        for y in range(39,83,8):
            ladder.rectangle((rail[0]+2,y-1,rail[1]-1,y+2),fill=edge)
            ladder.rectangle((rail[0]+2,y,rail[1]-1,y+1),fill=woodlight)
        out.alpha_composite(body)
        pen = ImageDraw.Draw(out)
        pen.line(points, fill=edge, width=5)
        pen.line(points, fill=skinshade, width=3)
        pen.line([(x,y-1) for x,y in points],fill=skin,width=1)
        handx, handy = points[-1]
        pen.rectangle((handx-1,handy-2,handx+2,handy+2),fill=edge)
        pen.rectangle((handx,handy-1,handx+1,handy+1),fill=skinlight)
        pen.point((handx+1,handy),fill=skinshade)
        name = f'ladder_carry_{direction}'
        out.save(P/f'{name}.png')
        out.resize((256,384),Image.Resampling.NEAREST).save(P/f'{name}_4x.png')
        # 두 상태의 머리와 하체는 화소까지 동일하다.
        assert out.crop((23,46,43,61)).tobytes() == base.crop((23,46,43,61)).tobytes()
        assert out.crop((24,78,44,88)).tobytes() == base.crop((24,78,44,88)).tobytes()
        for rec in records:
            if rec['name'] == name:
                rec.update(opaque_bbox=list(out.getbbox()),colors=len({c for c in out.getdata() if c[3]}),
                    source=f'ladder_empty_{direction}.png + unify_ladder.py',source_crop=None,
                    head_height_estimate=15,head_ratio_estimate=0.357,
                    sha256=hashlib.sha256((P/f'{name}.png').read_bytes()).hexdigest())
    (P/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__ == '__main__':
    unify()

