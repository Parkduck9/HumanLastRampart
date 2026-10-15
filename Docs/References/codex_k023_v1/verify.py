"""실제 실행한 파일·격자·벡터·독립 문서 자료 검사만 기록한다."""
from pathlib import Path
from PIL import Image
import numpy as np
import json,hashlib,re,base64

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
checks=[]
def check(name,value):
    assert value,name
    checks.append(name)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
for a in manifest['assets']:
    ident=a['id']
    sheet=Image.open(HERE/a['sheet'])
    check(ident+' 시트 크기·RGBA',sheet.size==tuple(a['size']) and sheet.mode=='RGBA')
    hashes=[]
    for i in range(a['count']):
        p=HERE/'sheets'/f'{ident}_{i+1:02}.png'
        im=Image.open(p)
        box=im.getchannel('A').getbbox()
        check(f'{ident} {i+1} 격자 복원',im.size==(256,384) and im.tobytes()==sheet.crop((i*256,0,(i+1)*256,384)).tobytes())
        check(f'{ident} {i+1} 지면·잘림 없음',box[0]>=4 and box[1]>=4 and box[2]<=252 and box[3]==352)
        check(f'{ident} {i+1} 투명 알파',set(np.unique(np.asarray(im.getchannel('A'))))=={0,255})
        hashes.append(sha(p))
    check(ident+' 고유 동작',len(set(hashes))==a['count'])
    check(ident+' 기준점',a['pivotTopLeft']==[128,352])
    check(ident+' 원본 그림 보존',sha(ROOT/a['referencePath'])==a['referenceSha256'])
for a in manifest['normalSamples']:
    normal=Image.open(HERE/a['path'])
    albedo=Image.open(HERE/a['albedoPath']).convert('RGBA')
    check(a['id']+' 노멀 크기·RGB',normal.size==albedo.size==(256,384) and normal.mode=='RGB')
    data=np.asarray(normal)
    mask=np.asarray(albedo)[:,:,3]
    check(a['id']+' 빈 영역 평면',bool(np.all(data[mask<=32]==[128,128,255])))
    vec=data.astype(float)/127.5-1
    check(a['id']+' 단위 벡터',float(np.abs(np.linalg.norm(vec,axis=2)-1).max())<.012)
    check(a['id']+' 색 원본 바이트 동일',sha(HERE/a['albedoPath'])==sha(ROOT/a['referencePath'])==a['referenceSha256'])
page=(ROOT/'_HTML/codex_K-023_아군보강_v1.html').read_text(encoding='utf-8')
check('외부 자료 의존 없음',not re.search(r'<(?:script|img)[^>]+src=[\"\'](?!data:)',page) and 'fetch(' not in page)
check('내장 이미지 모두 PNG 해독',all(Image.open(__import__('io').BytesIO(base64.b64decode(x))).format=='PNG' for x in re.findall(r'data:image/png;base64,([A-Za-z0-9+/=]+)',page)))
check('치환 완료','__ASSETS__' not in page and '__NORMALS__' not in page)
check('동작 전체 19칸',sum(a['count'] for a in manifest['assets'])==19)
check('정적 노멀 3장만',len(list((HERE/'normals').glob('*_normal.png')))==3)
(HERE/'qa.json').write_text(json.dumps({'status':'통과','checks':checks,'count':len(checks), 'browserVerified':False,'browserLimitation':'cua가 사용 가능한 앱·브라우저를 반환하지 않아 실제 브라우저 조작 검증 불가','unityVerified':False},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'기술 검사 {len(checks)}개 통과 / 실제 브라우저·Unity 미검증')
