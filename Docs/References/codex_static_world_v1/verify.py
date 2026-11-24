from pathlib import Path
from PIL import Image
import json,re,base64,tempfile,subprocess,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
text=(HERE/'preview.html').read_text(encoding='utf-8');script=text.split('<script>',1)[1].split('</script>',1)[0]
assert not re.search(r'(?:src|href)=["\']https?://',text)
with tempfile.NamedTemporaryFile(mode='w',encoding='utf-8',suffix='.js',delete=False) as f:f.write(script);temp=Path(f.name)
try:subprocess.run(['C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe','--check',str(temp)],check=True,capture_output=True)
finally:temp.unlink()
assets=json.loads(script.split('const assets=',1)[1].split(';\ndocument',1)[0]);assert len(assets)==8
origins=json.loads((HERE/'generation.json').read_text(encoding='utf-8'));origins.update(json.loads((HERE/'corrections.json').read_text(encoding='utf-8')))
for asset in assets:
 folder=HERE/asset['id'];packed=Image.open(folder/'static_grid.png');info=json.loads((folder/'asset.json').read_text(encoding='utf-8'))
 assert packed.mode=='RGBA' and list(packed.size)==asset['size'] and info['frames']==1 and info['fps']==0
 assert (folder/'source.png').read_bytes()==Path(origins[asset['id']]['generatedSource']).read_bytes()
 for key,name in [('image','static_grid.png'),('source','source.png'),('json','asset.json')]:assert base64.b64decode(asset[key].split(',',1)[1])==(folder/name).read_bytes()
 if asset['id']!='ground':
  assert packed.getchannel('A').getextrema()[0]==0
  assert all(packed.getpixel((x,0))[3]==0 and packed.getpixel((x,packed.height-1))[3]==0 for x in range(packed.width))
 else:assert packed.getchannel('A').getextrema()==(255,255)
assert (HERE/'preview.html').read_bytes()==(ROOT/'_HTML/codex_전투정적에셋목록_v1.html').read_bytes()
qa=json.loads((HERE/'qa.json').read_text(encoding='utf-8'));qa.update(jsSyntax=True,embeddedAssetsExact=True,externalRequests=False,groundOpaque=True,objectTransparency=True,htmlCopiesExact=True);(HERE/'qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: 8 PNG/metadata/embedded original bytes, JS syntax, transparency, two HTML copies')
