from pathlib import Path
from PIL import Image
import json,hashlib
root=Path(__file__).parent
result=[]
for name in ['ogre','ladder_goblin','archer','gunner','cannon_crew','wizard']:
    p=root/(name+'.png')
    im=Image.open(p).convert('RGBA');alpha=im.getchannel('A');hist=alpha.histogram();bbox=alpha.point(lambda x:255 if x>=128 else 0).getbbox()
    assert hist[0]>0,name+' 투명픽셀 없음'
    assert bbox is not None,name+' 내용 없음'
    assert bbox[0]>0 and bbox[1]>0 and bbox[2]<im.width and bbox[3]<im.height,name+' 불투명영역 경계 닿음'
    result.append(dict(name=name,size=list(im.size),transparent_pixels=hist[0],partial_alpha_pixels=sum(hist[1:255]),solid_pixels=hist[255],visible_bounds=list(bbox),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(root/'png_analysis.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('6PNG 투명도·내용·경계 검사 통과')
