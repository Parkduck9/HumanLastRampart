from pathlib import Path
from PIL import Image
import json, hashlib, re, base64, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[4]
ART=ROOT/'Docs/References/codex_k028_v1'
checks=[]
def check(name, value):
    checks.append({'항목':name,'통과':bool(value)})
    if not value:
        raise AssertionError(name)

for p in (ART/'backgrounds').glob('*.png'):
    with Image.open(p) as im:
        check(p.name+' 규격',im.size==(3840,1080))
for p in (ART/'stations').glob('*.png'):
    with Image.open(p) as im:
        check(p.name+' 규격·알파',im.size==(1536,1024) and im.mode=='RGBA' and im.getchannel('A').getextrema()[0]==0)
for p in (ART/'result').glob('codex_portrait_*.png'):
    with Image.open(p) as im:
        check(p.name+' 규격·알파',im.size==(512,512) and im.mode=='RGBA' and im.getchannel('A').getextrema()[0]==0)
for p in (ART/'result').glob('codex_wall_*.svg'):
    ET.fromstring(p.read_text(encoding='utf-8'))
check('성벽 4면 × 3단계',len(list((ART/'result').glob('codex_wall_*.svg')))==12)
for p in (ART/'ui').glob('*.svg'):
    ET.fromstring(p.read_text(encoding='utf-8'))
check('상호작용·장소 이름·승패 SVG 7종',len(list((ART/'ui').glob('*.svg')))==7)
page=(ROOT/'_HTML/codex_K-028_정비마당_결과그림_v1.html').read_text(encoding='utf-8')
check('미치환 항목 없음','@@' not in page)
check('외부 이미지·스크립트 참조 없음',not re.search(r'(?:src|href)=[\"\'](?:https?:|\.\.?/)',page))
for encoded in re.findall(r'data:image/(?:png|svg\+xml);base64,([A-Za-z0-9+/=]+)',page):
    base64.b64decode(encoded,validate=True)
check('내장 그림 데이터 해독',True)
check('생성 원본 11장',len(list((ART/'originals').glob('*.png')))==11)
for p in (ART/'result').glob('codex_result_*.png'):
    with Image.open(p) as im:
        check(p.name+' 규격',im.size==(1920,1080))
script=re.search(r'<script>(.*?)</script>',page,re.S).group(1)
(ART/'qa/codex_page_script.js').write_text(script,encoding='utf-8')
before=json.loads((ART/'qa/codex_game_before.json').read_text(encoding='utf-8-sig'))
changed=[]
for row in before:
    p=Path(row['Path'])
    if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest().upper()!=row['Hash'].upper():
        changed.append(str(p))
old_paths={str(Path(r['Path'])).lower() for r in before}
new_paths={str(p).lower() for p in (ROOT/'Game').rglob('*') if p.is_file()}
check('보관된 작업 전 목록과 Game 해시·파일 목록 동일',not changed and old_paths==new_paths)
result={'검사수':len(checks),'검사':checks,'Game파일수':len(before),'미수행':['실제 브라우저 렌더·입력','Unity 실행·Play','사용자 아트 확정']}
(ART/'qa/codex_validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'검사수':len(checks),'Game파일수':len(before),'결과':'통과'},ensure_ascii=False))
