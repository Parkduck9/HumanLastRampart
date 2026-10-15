from pathlib import Path
import json, hashlib, re, subprocess, zipfile
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
checks=[]
def check(name,passed):
 checks.append({'check':name,'passed':bool(passed)})
 if not passed:raise AssertionError(name)
m=read(HERE/'manifest.json');check('재질8쌍',len(m['materials'])==8)
for material in m['materials']:
 c=Image.open(HERE/material['albedo']);n=Image.open(HERE/material['normal']);size=material['size']
 check(material['id']+' 규격/32pxm',list(c.size)==list(n.size)==size and all(v==32*t for v,t in zip(size,material['meters'])))
 check(material['id']+' PNG해시',sha(HERE/material['albedo'])==material['albedoSha256'] and sha(HERE/material['normal'])==material['normalSha256'])
 decoded=np.array(n,dtype=float)/127.5-1
 check(material['id']+' 노멀단위벡터',np.max(abs(np.linalg.norm(decoded,axis=2)-1))<.012 and decoded[:,:,2].min()>0)
 check(material['id']+' 미검증표기',material['seamlessCertified'] is False and material['normalShapeAlignmentCertified'] is False and material['unityVerified'] is False)
capture_record=read(HERE/'K-004/generation.json')['references'][0]
check('제작 당시 실제캡처 보관 해시 일치',sha(HERE/'K-004/actual_capture.png')==capture_record['sourceInfo']['sha256'])
for card in ['K-004','K-005','K-006']:
 check(card+' README·생성기록',all((HERE/card/f).exists() for f in ['README.md','generation.json']))
 with zipfile.ZipFile(HERE/(card+'_review_assets.zip')) as z:
  check(card+' ZIP정상',z.testzip() is None)
  check(card+' CARD_RESULT 제외',not any(n.endswith('CARD_RESULT.md') for n in z.namelist()))
html=(HERE/'preview.html').read_text(encoding='utf-8')
check('HTML사본 동일',sha(HERE/'preview.html')==sha(ROOT/'_HTML/codex_3D성벽_카메라_질감_포탑카드_v1.html'))
check('외부이미지/스크립트 없음',not re.search(r'<(?:img|script)[^>]+src=["\']https?://',html))
data=json.loads(re.search(r'<script id="data" type="application/json">(.*?)</script>',html,re.S).group(1))
check('내장 MD일치',data['readme']==(HERE/'README.md').read_text(encoding='utf-8'))
scripts=re.findall(r'<script>(.*?)</script>',html,re.S)
node='C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe'
check('JS구문',subprocess.run([node,'--check','--input-type=commonjs'],input='\n'.join(scripts),capture_output=True,text=True).returncode==0)
b=read(HERE/'browser_qa.json');check('브라우저16조합',len(b['materialChecks'])==16 and all(x['images'] and x['material']==x['renderMaterial'] for x in b['materialChecks']))
check('브라우저전환·조절·오류',b['cardsVisibleOneAtATime'] and b['towerLoaded'] and b['lightControlPassed'] and b['strengthControlPassed'] and b['consoleErrors']==0 and not b['defaultOverflow'])
report={'taskIds':['T-CX-052','T-CX-053','T-CX-054'],'technicalChecksPassed':len(checks),'failed':0,'checks':checks,'incomplete':[ 'K005 실제3D 모형의 반복/색노멀 정합/Unity 조명 확인','K004/K006 사용자 외형·포탑치수 검토','M072 합본 보관 뒤 Claude 카드 가지 정리'],'unityTested':False,'artApproved':False,'fileProtocolTested':False,'browser':b}
(HERE/'qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({'technicalPassed':len(checks),'failed':0,'artApproved':False}))
