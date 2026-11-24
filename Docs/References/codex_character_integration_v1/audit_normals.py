"""K-081 normal PNG exact-copy installation and repeatable audit. No Unity/Git."""
from pathlib import Path
import hashlib
import json
import re
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
PACK = ROOT / 'Docs/References/codex_k081_v1'
OUT = Path(__file__).resolve().parent

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def guid(path):
    meta = Path(str(path) + '.meta')
    match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M) if meta.exists() else None
    return match.group(1) if match else None

def main():
    manifest = json.loads((PACK / 'manifest_current.json').read_text(encoding='utf-8-sig'))
    rows = []
    for sheet in manifest['sheets']:
        color_src = ROOT / sheet['source_game']
        normal_src = PACK / sheet['game']
        color_dst = ROOT / (sheet['current_game'] or 'Game/Assets/_Project/Art/Characters/Walk/goblin_wallhit4.png')
        normal_dst = color_dst.with_name(color_dst.stem + '_normal.png')
        color = Image.open(color_src).convert('RGBA')
        normal = Image.open(normal_src).convert('RGBA')
        source_alpha_equal = color.size == normal.size and color.getchannel('A').tobytes() == normal.getchannel('A').tobytes()
        assert source_alpha_equal, sheet['sheet']
        assert sha(color_src) == sheet['source_sha256'], sheet['sheet']
        row = dict(sheet=sheet['sheet'], source_color=sheet['source_game'], source_normal=str(normal_src.relative_to(ROOT)).replace('\\', '/'),
                   target_color=str(color_dst.relative_to(ROOT)).replace('\\', '/'), target_normal=str(normal_dst.relative_to(ROOT)).replace('\\', '/'),
                   source_color_sha256=sha(color_src), source_normal_sha256=sha(normal_src),
                   source_size=list(color.size), source_alpha_equal=source_alpha_equal, frames=sheet['frame_count'])
        if sheet['sheet'] == 'cannon_crew_ignite6.png':
            row.update(status='held_latest_expanded_ignite', reason_ko='현재 Game v5는 2592×384, K081 구형은 1728×384. 구형 법선 복사·연결 금지.',
                       installed_color_size=list(Image.open(color_dst).size), installed_color_sha256=sha(color_dst))
        else:
            before_guid = guid(normal_dst)
            existed = normal_dst.exists()
            normal_dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(normal_src, normal_dst)
            assert sha(normal_dst) == sha(normal_src)
            assert guid(normal_dst) == before_guid
            row.update(status='installed_copy_verified', existed_before=existed,
                       target_normal_sha256=sha(normal_dst), meta_guid_before=before_guid, meta_guid_after=guid(normal_dst))
            if color_dst.exists():
                installed = Image.open(color_dst).convert('RGBA')
                row.update(installed_color_size=list(installed.size), installed_color_sha256=sha(color_dst),
                           installed_color_matches_source=sha(color_dst) == sha(color_src),
                           installed_color_normal_alpha_equal=installed.size == normal.size and installed.getchannel('A').tobytes() == normal.getchannel('A').tobytes())
            else:
                row.update(installed_color_matches_source=False, installed_color_normal_alpha_equal=False)
        rows.append(row)
    report = dict(date='2026-10-21', manifest='Docs/References/codex_k081_v1/manifest_current.json',
                  source_sheets=len(rows), source_frames=sum(r['frames'] for r in rows),
                  copied_sheets=sum(r['status']=='installed_copy_verified' for r in rows), held_sheets=1,
                  copied_frames=sum(r['frames'] for r in rows if r['status']=='installed_copy_verified'),
                  held_frames=sum(r['frames'] for r in rows if r['status']!='installed_copy_verified'),
                  scope_ko='PNG 그대로 복사·크기·알파·해시·기존metaGUID 확인. 정의 참조/렌더링/Unity시험은 부모 작업이며 이 보고서의 완료 항목이 아님.', sheets=rows)
    (OUT/'normal_audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='sheets'},ensure_ascii=False))
    mismatches=[r['sheet'] for r in rows if r['status']=='installed_copy_verified' and not r.get('installed_color_normal_alpha_equal')]
    print('Pending color destination matching:',mismatches)

if __name__ == '__main__':
    main()
