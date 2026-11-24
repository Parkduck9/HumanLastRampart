"""PNG를 읽어 규격과 색·칸 순서를 검사한다. 그림은 수정하지 않는다."""
from pathlib import Path
from PIL import Image
import hashlib, json

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
checks=[]
def check(name,value):
    checks.append({'검사':name,'통과':bool(value)})
    assert value,name
manifest={'카드':'K-056','상태':'사용자 확정 전 시험 그림','원본_칸':[64,96],'원본_발':[32,88],'게임용_칸':[256,384],'게임용_발':[128,352],'좌표':'왼쪽 위 원점, 발끝의 다음 격자선 y=88 / 352','그림':[]}
pal={k:{tuple(bytes.fromhex(h[1:])) for h in (OUT/f'{k}_palette.txt').read_text().splitlines()} for k in ['archer','hero']}
art=ROOT/'Game/Assets/_Project/Art/Characters'
sources={'archer':[art/'Static/archer_static.png',art/'Fire/archer_fire.png'],'hero':[art/'Hero/hero_back_idle.png',art/'Hero/hero_walk_run_grid.png']}
for person,paths in sources.items():
    source_colors=set()
    for p in paths:
        with Image.open(p) as im: source_colors.update(v[:3] for v in im.get_flattened_data() if v[3]>=180)
    check(person+' 선택 팔레트가 실제 원본 색에 포함',pal[person]<=source_colors)
for path in sorted(OUT.glob('*_64.png')):
    with Image.open(path) as im,Image.open(path.with_name(path.name.replace('_64','_256'))) as big:
        check(path.stem+' 최근접 4배 픽셀 동일',big.tobytes()==im.resize((im.width*4,im.height*4),Image.Resampling.NEAREST).tobytes())
        check(path.stem+' 반투명 픽셀 없음',set(im.getchannel('A').get_flattened_data())<={0,255})
        check(path.stem+' 칸 배수',im.width%64==0 and im.height%96==0)
        colors={v[:3] for v in im.get_flattened_data() if v[3]}
        allowed=pal['archer'] if path.name.startswith('archer') else pal['hero'] if path.name.startswith('hero') else pal['archer']|pal['hero']
        check(path.stem+' 허용 팔레트만 사용',colors<=allowed)
        manifest['그림'].append({'파일':path.name,'크기':list(im.size),'색수':len(colors)})
for i in range(15):
    with Image.open(OUT/f'frame_{i:02}_64.png') as im:
        q=im.getbbox();check(f'{i} 인물 키 48점',q[3]-q[1]==48);check(f'{i} 발 기준선과 여백',q[3]==88 and q[0]>0 and q[2]<64 and q[1]>0)
for ids in [[1,2,3,4],[5,6,7,8],[11,12,13,14]]:
    hashes=[hashlib.sha256(Image.open(OUT/f'frame_{i:02}_64.png').tobytes()).hexdigest() for i in ids]
    check(str(ids)+' 모든 동작 칸 서로 다름',len(set(hashes))==4)
with Image.open(OUT/'archer_fire6_compat_64.png') as six:
    for col,src in enumerate([5,6,7,7,8,8]):
        check('사격 6칸 대응 '+str(col),six.crop((col*64,0,(col+1)*64,96)).tobytes()==Image.open(OUT/f'frame_{src:02}_64.png').tobytes())
manifest['원본_해시']={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for paths in sources.values() for p in paths}
manifest['칸_순서']={'archer_back_idle':[0],'archer_back_walk4':[1,2,3,4],'archer_fire4':[5,6,7,8],'archer_fire6_compat':[5,6,7,7,8,8],'hero_back_idle':[10],'hero_front_idle':[9],'hero_side_run4':[11,12,13,14],'hero_layout_preview':[[10,None,None,None],[None]*4,[11,12,13,14],[9,None,None,None]]}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(OUT/'qa.json').write_text(json.dumps({'검사수':len(checks),'결과':checks,'실제_브라우저':'연결된 브라우저 없음. 미검증','Unity':'실행하지 않음'},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(checks)}개 검사 통과')
