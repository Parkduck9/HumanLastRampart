"""내장 자료와 원본 불변 검사. 실제 브라우저/Unity 검증을 대신하지 않는다."""
from pathlib import Path
from PIL import Image
import json,re,base64,hashlib,subprocess,tempfile
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2];OLD=HERE.parent/'codex_walk6_v1'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
htmls=[HERE/'preview.html']+[HERE/i/'preview.html' for i in ['archer','gunner','cannon_crew','wizard']]
for page in htmls:
 text=page.read_text(encoding='utf-8');assert not re.search(r'(?:src|href)=["\']https?://',text)
 script=text.split('<script>',1)[1].split('</script>',1)[0]
 with tempfile.NamedTemporaryFile(suffix='.js',mode='w',encoding='utf-8',delete=False) as f:f.write(script);temp=Path(f.name)
 try:subprocess.run(['C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe','--check',str(temp)],check=True,capture_output=True)
 finally:temp.unlink()
 assets=json.loads(script.split('const assets=',1)[1].split(';\nconst config=',1)[0]);assert len(assets)==(6 if page.parent==HERE else 1)
 for asset in assets:
  folder=(HERE if asset['action']=='run' else OLD)/asset['id'];name='run4_grid' if asset['action']=='run' else 'walk4_grid'
  assert base64.b64decode(asset['sheet'].split(',',1)[1])==(folder/(name+'.png')).read_bytes()
  assert base64.b64decode(asset['json'].split(',',1)[1])==(folder/(name+'.json')).read_bytes()
  for i,url in enumerate(asset['frames']):assert base64.b64decode(url.split(',',1)[1])==(folder/f'frame_{i+1}.png').read_bytes()
for item in json.loads((HERE/'reference_hashes.json').read_text(encoding='utf-8')).values():assert sha(ROOT/item['path'])==item['sha256']
for ident in ['archer','gunner','cannon_crew','wizard']:
 info=json.loads((HERE/ident/'run4_grid.json').read_text(encoding='utf-8'));assert len(set(info['frameHashes']))==4
 image=Image.open(HERE/ident/'run4_grid.png');assert image.mode=='RGBA' and image.size==(1024,384)
 assert min(info['framesData'][i]['groundGap'] for i in [1,3])>0
q=json.loads((HERE/'qa.json').read_text(encoding='utf-8'));q.update(htmlJsSyntax=True,embeddedAssetsExact=True,externalRequests=False,flightFramesAboveGround=True);(HERE/'qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: 5 standalone HTML / JS / embedded bytes / 4 unique frames per ally / enemy hashes / airborne ground gaps')
