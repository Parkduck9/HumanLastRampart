from pathlib import Path
from html.parser import HTMLParser
from PIL import Image
import hashlib, json, base64, io

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]

class Page(HTMLParser):
    def __init__(self):
        super().__init__();self.images=[];self.ids=[];self.external=[];self.details=0;self.controls=0
    def handle_starttag(self,tag,attrs):
        attrs=dict(attrs)
        if 'id' in attrs:self.ids.append(attrs['id'])
        if tag=='img':self.images.append(attrs['src'])
        if tag=='details':self.details+=1
        if tag=='input' and attrs.get('type')=='range':self.controls+=1
        for k in ['src','href']:
            if k in attrs and not attrs[k].startswith(('data:','#')):self.external.append(attrs[k])

before=json.loads((OUT/'game_before.json').read_text(encoding='utf-8-sig'))
assert all(hashlib.sha256(Path(x['Path']).read_bytes()).hexdigest().upper()==x['Hash'] for x in before)
assert {str(p).lower() for p in (ROOT/'Game').rglob('*') if p.is_file()}=={x['Path'].lower() for x in before}
html=(ROOT/'_HTML/5_그림_전장/codex_K-068_들판꾸미기_v1.html').read_text(encoding='utf-8')
page=Page();page.feed(html)
assert len(page.images)==76 and page.details==31 and page.controls==2
assert len(set(page.ids))==len(page.ids) and not page.external
for src in page.images:
    assert src.startswith('data:image/png;base64,')
    Image.open(io.BytesIO(base64.b64decode(src.split(',',1)[1]))).verify()
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
assert len(manifest['sprites'])==31
for e in manifest['sprites']:
    assert (OUT/e['file']).is_file()
    im=Image.open(OUT/e['sheet']);assert all(n>0 and n&(n-1)==0 for n in im.size)
record=json.loads((OUT/'verification.json').read_text(encoding='utf-8'))
record.update(game_files_unchanged=len(before),game_file_set_unchanged=True,embedded_review_images_verified=len(page.images),four_x_views=page.details,before_after_controls=page.controls,external_resources=page.external,browser_render_tested=False)
(OUT/'verification.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
print(json.dumps({k:record[k] for k in ['sprite_count','path_edges_equal','game_files_unchanged','embedded_review_images_verified','four_x_views','browser_render_tested']}))
