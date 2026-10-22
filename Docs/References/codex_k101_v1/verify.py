from pathlib import Path
import base64
import hashlib
import io
import json
import re
import subprocess
import shutil
from PIL import Image
import numpy as np

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
PAGE=ROOT/'_HTML/6_그림_화면/codex_K-101_반사가면재제작_v1.html'

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def verify():
    manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf8'))
    assert manifest['sheet_count']==7 and manifest['frames_verified']==32
    for s in manifest['sheets']:
        src=ROOT/s['source']; target=OUT/s['mask']
        a=np.array(Image.open(src).convert('RGBA')); b=np.array(Image.open(target))
        assert Image.open(target).mode=='RGBA'
        assert Image.open(target).size==tuple(s['size'])
        assert np.array_equal(a[:,:,3],b[:,:,3])
        assert (b[:,:,2]==0).all() and (b[a[:,:,3]==0,:3]==0).all()
        n=np.array(Image.open(OUT/s['native_mask']))
        assert np.array_equal(np.repeat(np.repeat(n,3,0),3,1),b)
        preview=np.array(Image.open(OUT/s['preview_4x']))
        assert np.array_equal(preview,np.repeat(np.repeat(n,4,0),4,1))
        assert sha(src)==s['source_sha256'] and sha(target)==s['mask_sha256']
        assert s['frame_count']==s['grid'][0]*s['grid'][1]
        cw,ch=s['cell']
        for f in s['frames']:
            x=f['column']*cw; y=f['row']*ch
            aa=a[y:y+ch,x:x+cw,3]; bb=b[y:y+ch,x:x+cw,3]
            assert np.array_equal(aa,bb)
            assert np.where(bb>=128)[0].max()+1==360
        assert all(s['verification'].values())
    html=PAGE.read_text(encoding='utf8')
    script=re.search(r'<script>(.*?)</script>',html,re.S).group(1)
    data=json.loads(re.search(r'const SHEETS=(.*?);\nconst \$',script,re.S).group(1))
    assert len(data)==7
    decoded=0
    for d,s in zip(data,manifest['sheets']):
        for key,path in [('sprite',ROOT/s['source']),('spec',OUT/s['mask']),('material',OUT/s['material_preview'])]:
            raw=base64.b64decode(d[key].split(',',1)[1],validate=True)
            assert raw==path.read_bytes()
            with Image.open(io.BytesIO(raw)) as im:
                im.load()
                assert im.size==tuple(s['size'])
            decoded+=1
    assert not re.search(r'<(?:script|link)[^>]+(?:src|href)=',html)
    node=shutil.which('node')
    if not node:
        candidate=Path.home()/'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
        if candidate.exists(): node=str(candidate)
    assert node, 'Node 실행 파일 필요'
    result=subprocess.run([node,'--check','-'],input=script,text=True,capture_output=True,encoding='utf8')
    assert result.returncode==0,result.stderr
    result=subprocess.run([node,str(OUT/'verify_review.cjs'),str(PAGE)],text=True,capture_output=True,encoding='utf8')
    assert result.returncode==0,result.stderr
    runtime=json.loads(result.stdout)
    report={'all_pass':True,'sheets':7,'frames':32,'embedded_images_decoded':decoded,
        'source_hashes_unchanged':True,'javascript_syntax_pass':True,'script_harness':runtime,
        'browser_visual_check_performed':False,'browser_note':'연결된 브라우저 없음; iab 생성도 사용할 수 없음',
        'unity_run':False,'scope_note':'Game/ 및 협업 로그 수정 없음, Git 쓰기 없음'}
    (OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':
    verify()
