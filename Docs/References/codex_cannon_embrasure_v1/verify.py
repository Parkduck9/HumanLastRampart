from pathlib import Path
from PIL import Image
import json,base64,re,tempfile,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
t=(HERE/'preview.html').read_text(encoding='utf-8');s=t.split('<script>',1)[1].split('</script>',1)[0];assets=json.loads(s.split('const assets=',1)[1].split(';const images=',1)[0]);assert len(assets)==9
gen=json.loads((HERE/'generation.json').read_text(encoding='utf-8'))
for ident in ['wall_cannon_bay','parapet_cannon_front']:
 folder=HERE/ident;info=json.loads((folder/'asset.json').read_text(encoding='utf-8'));a=assets[ident]
 assert (folder/'source.png').read_bytes()==Path(gen[ident]['generatedSource']).read_bytes()
 for key,name in [('image','static_grid.png'),('source','source.png'),('json','asset.json')]:assert base64.b64decode(a[key].split(',',1)[1])==(folder/name).read_bytes()
 im=Image.open(folder/'static_grid.png');assert im.mode=='RGBA' and im.size==(1024,512) and info['centralOpeningAlpha']==0
 assert info['frames']==1 and info['fps']==0
assert not re.search(r'(?:src|href)=["\']https?://',t)
assert (HERE/'preview.html').read_bytes()==(ROOT/'_HTML/codex_대포사격구성벽_v1.html').read_bytes()
with tempfile.NamedTemporaryFile(mode='w',encoding='utf-8',suffix='.js',delete=False) as f:f.write(s);p=Path(f.name)
try:subprocess.run(['C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe','--check',str(p)],check=True,capture_output=True)
finally:p.unlink()
q=json.loads((HERE/'qa.json').read_text(encoding='utf-8'));q.update(sourceBytesExact=True,embeddedAssetBytesExact=True,htmlCopiesExact=True,jsSyntax=True,externalRequests=False);(HERE/'qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: 2 source/PNG/JSON, transparent opening, 9 embedded images, standalone HTML+JS')
