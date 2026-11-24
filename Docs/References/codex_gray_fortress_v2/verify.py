from pathlib import Path
import hashlib
import json
import re
import subprocess
import zipfile
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
manifest = read(HERE / 'manifest.json')
checks = []
def check(name, result):
    checks.append({'check': name, 'passed': bool(result)})
    if not result:
        raise AssertionError(name)

check('새 PNG 11종', len(manifest['assets']) == 11)
for a in manifest['assets']:
    p = HERE / a['file']
    im = Image.open(p)
    check(a['id'] + ' 규격·RGBA·해시', list(im.size) == a['size'] and im.mode == 'RGBA' and sha(p) == a['sha256'])
    if a['kind'] == 'cutout_reference':
        check(a['id'] + ' 투명 여백·접지', im.getpixel((0, 0))[3] == 0 and im.getchannel('A').getbbox()[3] == 352 and a['pivotPixelsTopLeft'] == [128, 352])
    else:
        check(a['id'] + ' 32px/m', all(n == 32 * m for n, m in zip(a['size'], a['meters'])))
for family in ['tower_square', 'battery_low']:
    members = [a for a in manifest['assets'] if a.get('family') == family]
    check(family + ' 세 상태 공통 배율', len(members) == 3 and len({a['sharedSourceScale'] for a in members}) == 1)
    heights = {a['state']: a['packedBodySize'][1] for a in members}
    check(family + ' 잔해 낮음', heights['rubble'] < heights['intact'])

html = (HERE / 'preview.html').read_text(encoding='utf-8')
check('HTML 사본 일치', sha(HERE / 'preview.html') == sha(ROOT / '_HTML/codex_회색성벽_나무_포탑비교_v2.html'))
check('이미지·스크립트 외부 의존 없음', not re.search(r'<(?:img|script)[^>]+src=["\']https?://', html))
data = json.loads(re.search(r'<script id="asset-data" type="application/json">(.*?)</script>', html, re.S).group(1))
check('내장 MD 일치', data['readme'] == (HERE / 'README.md').read_text(encoding='utf-8'))
check('내장 PNG 16개', len(data['images']) == 16 and all(v.startswith('data:image/png;base64,') for v in data['images'].values()))
with zipfile.ZipFile(HERE / 'gray_fortress_v2_assets.zip') as z:
    check('에셋 ZIP 손상 없음', z.testzip() is None)
    check('사용자 타게임 참고 이미지 ZIP 제외', 'user_octopath_reference.png' not in z.namelist())
    check('ZIP 새 PNG 11개', sum(n.endswith('.png') for n in z.namelist()) == 11)
browser = read(HERE / 'browser_qa.json')
check('브라우저 이미지/선택/크기/콘솔', browser['imagesLoaded'] and browser['downloadLinkCount'] == 11 and browser['smallLightPassed'] and browser['oldSceneTogglePassed'] and browser['consoleErrors'] == 0 and not browser['defaultViewportOverflow'])
check('질감 6조합·포탑 6조합 확인', len(browser['checks']) == 12 and all(c.get('imageLoaded', c.get('newImage', False)) for c in browser['checks']))
scripts = re.findall(r'<script>(.*?)</script>', html, re.S)
node = Path('C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe')
checked = subprocess.run([str(node), '--check', '--input-type=commonjs'], input='\n'.join(scripts), text=True, capture_output=True)
check('JS 구문', checked.returncode == 0)
report = {'task': 'T-CX-051', 'checks': checks, 'passed': len(checks), 'failed': 0, 'browser': browser, 'limitations': ['완전한 무봉제·Unity·실제 조명 비교 미검증', 'file://·4K 열람 미실행', 'PNG는 레퍼런스/임시 정적 컷아웃, 3D 메시 없음', '새 노멀맵 없음']}
(HERE / 'qa.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'passed': len(checks), 'failed': 0}, ensure_ascii=False))
