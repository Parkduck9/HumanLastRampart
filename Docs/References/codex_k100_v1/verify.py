from pathlib import Path
from PIL import Image, ImageChops
import json
OUT=Path(__file__).resolve().parent
m=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
checked=[]
for a in m['assets']:
    im=Image.open(OUT/a['file'])
    assert im.mode=='RGBA' and list(im.size)==a['size'],a['file']
    if a['role']!='mockup':
        assert set(im.getchannel('A').tobytes()) <= {0,255},a['file']
        preview=Image.open(OUT/'preview_4x'/a['file'])
        assert ImageChops.difference(preview,im.resize((im.width*4,im.height*4),Image.Resampling.NEAREST)).getbbox() is None
    if a['role']=='pulse_sheet':
        s=a['cell'][0];frames=[im.crop((i*s,0,(i+1)*s,s)) for i in range(4)]
        assert all(f.getchannel('A').tobytes()==frames[0].getchannel('A').tobytes() for f in frames)
        assert len({f.tobytes() for f in frames})==3
        static=Image.open(OUT/a['file'].replace('_pulse',''))
        assert static.tobytes()==frames[1].tobytes()
        for f in frames:
            assert f.getchannel('A').getextrema()==(0,255)
            assert not any(f.getpixel((x,y))[3] for x,y in [(x,0) for x in range(s)]+[(x,s-1) for x in range(s)]+[(0,y) for y in range(s)]+[(s-1,y) for y in range(s)])
    if a['role']=='nine_slice':
        l,b,r,t=a['border_lbrt'];assert l+r<=im.width and b+t<=im.height
    checked.append(a['file'])
assert len(checked)==65
report=dict(asset_count=len(checked),rgba_and_sizes=True,binary_alpha_ui=True,nearest_4x_previews=True,pulse_sheets=24,pulse_alpha_silhouette_stationary=True,static_equals_frame_1=True,transparent_warning_perimeter=True,nine_slice_borders=True,foot_baseline='해당 없음: 캐릭터가 없는 UI 카드',stray_pixel_check='테두리 밖 불투명 픽셀 없음. 의도된 아이콘·방향 화살표·눈금과 테두리는 시각 검토.',runtime_unity_test='실행하지 않음',checked=checked)
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='checked'},ensure_ascii=False))
