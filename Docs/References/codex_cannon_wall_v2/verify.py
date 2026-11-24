from pathlib import Path
from PIL import Image
import json,base64,re,tempfile,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
t=(HERE/'preview.html').read_text(encoding='utf-8');s=t.split('<script>',1)[1].split('</script>',1)[0]
a=json.loads(s.split('const assets=',1)[1].split(',extras=',1)[0]);assert len(a)==6
gen=json.loads((HERE/'generation.json').read_text(encoding='utf-8'))
for asset in a:
 folder=HERE/asset['id'];meta=json.loads((folder/'asset.json').read_text(encoding='utf-8'))
 for key,file in [('image','static_grid.png'),('source','source.png'),('json','asset.json')]:assert base64.b64decode(asset[key].split(',',1)[1])==(folder/file).read_bytes()
 assert (folder/'source.png').read_bytes()==Path(gen[asset['id']]['generatedSource']).read_bytes()
 im=Image.open(folder/'static_grid.png');assert im.mode=='RGBA' and list(im.size)==meta['size'];assert im.getchannel('A').getextrema()[0]==0
 if asset['id'].startswith('cannon'):assert meta['pivotTopLeft']==[256,352] and im.size==(512,384)
sheet=Image.open(HERE/'cannon_aim3_grid.png').convert('RGBA');assert sheet.size==(1536,384)
for i,ident in enumerate(['cannon_left','cannon_center','cannon_right']):assert sheet.crop((i*512,0,(i+1)*512,384)).tobytes()==Image.open(HERE/ident/'static_grid.png').convert('RGBA').tobytes()
assert (ROOT/'_HTML/codex_대포3방향_성벽3안_v2.html').read_bytes()==(HERE/'preview.html').read_bytes()
assert not re.search(r'(?:src|href)=["\']https?://',t)
with tempfile.NamedTemporaryFile(suffix='.js',mode='w',encoding='utf-8',delete=False) as f:f.write(s);p=Path(f.name)
try:subprocess.run(['C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe','--check',str(p)],check=True,capture_output=True)
finally:p.unlink()
q=json.loads((HERE/'qa.json').read_text(encoding='utf-8'));q.update(jsSyntax=True,embeddedAssetBytesExact=True,sourceBytesExact=True,sheetCellsExact=True,externalRequests=False,htmlCopiesExact=True);(HERE/'qa.json').write_text(json.dumps(q,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: 6 sources/PNG/JSON, sheet 3 cells, common pivot, standalone HTML+JS')
