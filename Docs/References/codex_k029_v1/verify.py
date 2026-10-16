from pathlib import Path
from PIL import Image, ImageChops
import json, re, base64, hashlib
root=Path(__file__).resolve().parent
repo=root.parents[2]
manifest=json.loads((root/'manifest.json').read_text(encoding='utf-8'))
checks=[]
def check(name,condition):
    checks.append(dict(name=name,passed=bool(condition)))
    if not condition: raise AssertionError(name)
images={}
html=(repo/'_HTML/codex_K-029_함정지도_v1.html').read_text(encoding='utf-8')
embedded=json.loads(re.search(r'const sources=(.*?);\n',html).group(1))
for a in manifest['assets']:
    data=(root/a['file']).read_bytes();im=Image.open(root/a['file']).convert('RGBA');images[a['id']]=im
    check(a['id']+' 크기',im.size==(a['width'],a['height']) and (im.size==(32,32) or a['id']=='castle_perimeter'))
    check(a['id']+' 내장 일치',base64.b64decode(embedded[a['id']].split(',')[1])==data)
    check(a['id']+' 해시',hashlib.sha256(data).hexdigest()==a['sha256'])
for a in manifest['assets']:
    if a['group']=='지형':
        im=images[a['id']]
        check(a['id']+' 불투명',im.getchannel('A').getextrema()==(255,255))
        check(a['id']+' 좌우 경계',im.crop((0,0,1,32)).tobytes()==im.crop((31,0,32,32)).tobytes())
        check(a['id']+' 상하 경계',im.crop((0,0,32,1)).tobytes()==im.crop((0,31,32,32)).tobytes())
    if a['group']=='격자':
        check(a['id']+' 반투명',0<images[a['id']].getchannel('A').getextrema()[0]<255 and images[a['id']].getchannel('A').getextrema()[1]<255)
for m in range(16):
    for n in range(16):
        a,b=images['path_'+str(m)],images['path_'+str(n)]
        if bool(m&2)==bool(n&8):check(f'길 동서 {m}-{n}',a.crop((31,0,32,32)).tobytes()==b.crop((0,0,1,32)).tobytes())
        if bool(m&4)==bool(n&1):check(f'길 남북 {m}-{n}',a.crop((0,31,32,32)).tobytes()==b.crop((0,0,32,1)).tobytes())
reconstructed=Image.new('RGBA',(256,256))
for a in manifest['assets']:
    if a['group']=='성 조각':reconstructed.paste(images[a['id']],tuple(v*32 for v in a['cell']))
check('성 조각 원위치 복원',reconstructed.tobytes()==images['castle_perimeter'].tobytes())
check('외부 리소스 없음',not re.search(r'(?:src|href)=["\'](?:https?:|/|\.\.)',html))
check('치환 표식 없음','__MANIFEST__' not in html and '__IMAGES__' not in html)
(root/'qa/verification.json').write_text(json.dumps(dict(passed=len(checks),checks=checks,browser='연결 가능한 브라우저 없음. 실제 렌더링 미검증',unity='미실행'),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(checks),'항목 통과')
