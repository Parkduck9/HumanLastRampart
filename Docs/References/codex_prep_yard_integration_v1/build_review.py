"""실제 Unity 촬영을 그대로 내장한 정비 마당·함정 적용 보고서."""
from pathlib import Path
import base64, html, json, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
FINAL = ROOT / 'Docs/References/codex_character_integration_v1/qa/final'
YARD = HERE / 'qa/prompt_final/captures'
items = []
for label, folder, pattern in [
    ('정비 마당 가까운 바닥 — 최신 주인공', YARD, '*yard_00_near.png'),
    ('앞뒤 이동·원근 — E 안내는 간판 아래', YARD, '*yard_01_far.png'),
    ('나무통 뒤의 인물 가림', YARD, '*yard_02_foreground.png'),
    ('계단의 출정 확인', YARD, '*yard_03_stairs.png'),
    ('폭발 수명 뒤 — 남는 구덩이는 지면에 배치', FINAL / 'captures', '*ground_crater_after_burst.png'),
    ('최신 캐릭터가 연결된 실제 전투 준비', FINAL / 'captures', '*characters_00_installed_battle.png')]:
    p = next(folder.glob(pattern))
    items.append((label, p))
tests = {}
for mode in ['edit', 'play']:
    tests[mode] = ET.parse(FINAL / f'{mode}.xml').getroot().attrib
tests['yard_prompt'] = ET.parse(HERE / 'qa/prompt_final/play.xml').getroot().attrib
(HERE / 'results.json').write_text(json.dumps({'date': '2026-10-21', 'tests': tests,
    'images': [{'label': l, 'path': str(p.relative_to(ROOT))} for l, p in items]}, ensure_ascii=False, indent=2), encoding='utf-8')
page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>정비 마당·함정 실제 적용</title>
<style>body{font:17px/1.7 system-ui;background:#1b2027;color:#ece6d9;max-width:1200px;margin:auto;padding:24px}img{width:100%;height:auto;image-rendering:pixelated}figure{margin:32px 0}figcaption{font-weight:bold;margin:12px 0}.note{border-left:4px solid #bf9b4d;padding:16px;background:#292e33}li{margin:8px 0}</style>
<h1>정비 마당·함정 실제 게임 적용</h1><p>2026-10-21 · 목업이 아닌 Unity CLI 실제 촬영입니다.</p>
<div class="note">전체 Edit 128 통과·기존 1 유보·실패 0 / Play 50/50 통과.<br>마지막 E 안내 위치 보정 뒤 정비 마당 대상 시험 1/1 추가 통과.</div>
<h2>적용한 동작</h2><ul><li>WASD·방향키로 좌우와 앞뒤 8m를 이동합니다. 가까이 오면 커지고 멀어지면 작아집니다.</li><li>실제 3D 바닥·인물 캡슐·기둥과 나무통의 충돌체, 통행 마스크를 연결했습니다.</li><li>앞쪽 소품 뒤에서는 인물이 가려지며, 수리·병영·공방·상점·계단에서 E로 기존 기능을 엽니다.</li><li>창과 출정 확인이 열린 동안 이동을 막고, 화면을 떠날 때 전용 카메라·렌더 텍스처·공간을 정리합니다.</li><li>함정 폭발 효과는 수명 뒤 제거되고, 남는 구덩이 그림은 지면 0.025m에 놓입니다.</li></ul>
<p>그림자는 인물 실루엣을 실제 바닥 평면에 투영한 방식입니다. 배경의 건물 그림자는 원래 그림에 포함되어 있습니다. 자유 회전 카메라나 건물 전체의 실시간 조명 재구성은 하지 않았습니다.</p>'''
for label, p in items:
    data = base64.b64encode(p.read_bytes()).decode('ascii')
    page += '<figure><figcaption>' + html.escape(label) + '</figcaption><img alt="실제 Unity 촬영" src="data:image/png;base64,' + data + '"></figure>'
page += '<h2>남은 확인</h2><p>외형·동작 자연스러움은 사용자 직접 플레이로 최종 판단합니다. 대규모 전투 중반·마법 GPU 성능 측정은 별도 미완료입니다. 커밋·푸시는 하지 않았습니다.</p></html>'
(HERE / 'preview.html').write_text(page, encoding='utf-8')
(ROOT / '_HTML/codex_정비마당과함정_게임적용_v1.html').write_text(page, encoding='utf-8')
print('실제 사진', len(items), 'HTML', len(page.encode('utf-8')))
