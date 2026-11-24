import base64
import json
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]

def embedded(path):
    return 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode()

assets = []
for path in sorted(HERE.glob('*.png')):
    with Image.open(path) as im:
        alpha = im.convert('RGBA').getchannel('A')
        assets.append({'file': path.name, 'size': list(im.size), 'alpha_range': list(alpha.getextrema()), 'content_bounds': alpha.getbbox(), 'frame_count': 1, 'pivot': None, 'frame_order': ['static']})

manifest = {
    'card': 'K-083', 'status': '검토용·게임 미연결',
    'title': 'Human Last Rampart', 'title_status': 'DECISIONS.md에서 표시 제목 확정 항목을 찾지 못해 카드 지시의 영문 임시 제목 사용',
    'tool': 'built-in image_gen', 'prompts': 'prompts.json',
    'hero_reference': 'Docs/References/codex_hero_octopath_review_v6/generated.png',
    'size_decision': '문서 최신 D-080 정정은 원본 인물 약 52점으로 기술. 이후 52/73점 확정 항목 없음. 이 카드는 전투 인물 시트가 아닌 키 아트.',
    'sheet_contract': '정지 로고·배경·표지이므로 256×384 격자와 프레임 발선 피벗은 적용하지 않음',
    'menu_overlay': {'reference_resolution': [1920, 1080], 'x': 128, 'y': 284, 'width': 624, 'height': 512, 'source': 'MainMenuEntry.cs 및 ScreenView.DockLeft 읽기'},
    'export': '생성 배경·표지 1672×941 → 1920×1080 최근접 보간. 약 0.05% 화면비 보정. 로고 원본 크기·알파 보존.',
    'preview_crops': {'hero_detail_4x.png': [1450,360,256,320], 'logo_detail_4x.png': [700,170,256,192]},
    'cover_note': '이미지 생성 도구로 로고를 합성한 표지 변형. 원본 배경과 픽셀 단위 동일성은 보장하지 않음.',
    'assets': assets,
}
(HERE/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')

img = {p.stem: embedded(p) for p in HERE.glob('*.png')}
old = embedded(ROOT/'Game/Assets/_Project/Art/UI/Resources/ScreenArt/main.png')
hero = embedded(ROOT/'Docs/References/codex_hero_octopath_review_v6/generated.png')
html = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-083 · 마지막 성벽의 황혼</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#101613;color:#eee4cf;font:16px/1.65 'Malgun Gothic',sans-serif}main{max-width:1440px;margin:auto;padding:36px 24px 80px}h1{font-size:36px;margin:4px 0 12px}h2{margin-top:48px;color:#d5b16b;font-size:23px}p{max-width:960px;color:#c5c9c1}.eyebrow{color:#d5b16b;letter-spacing:.16em;font-size:13px}.badge{display:inline-block;border:1px solid #8a754b;padding:3px 12px;border-radius:20px}img{display:block;max-width:100%;height:auto}.cover{border:1px solid #54604d;border-radius:8px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}figure{margin:0}figcaption{padding:10px 0;color:#c5c9c1}button{background:#26352d;border:1px solid #d5b16b;color:#eee4cf;border-radius:4px;padding:10px 16px;cursor:pointer;margin:0 8px 14px 0;font:inherit}button[aria-pressed=true]{background:#d5b16b;color:#101613}.stage{position:relative;max-width:100%;margin:auto}.stage.small{width:960px}.menu{position:absolute;left:6.6667%;top:26.2963%;width:32.5%;height:47.4074%;background:#e6d4adf5;border:2px solid #a98b50;color:#302c22;padding:2.2%;font-size:clamp(8px,1.2vw,17px);box-shadow:0 10px 30px #0008}.menu b{display:block;font-size:1.2em;margin-bottom:5%}.menu span{display:block;border:1px solid #786b4b;padding:2% 6%;margin-top:5%;text-align:center}.menu small{display:block;font-size:.75em}.hidden{display:none}.checker{background-color:#344038;background-image:linear-gradient(45deg,#263029 25%,transparent 25%),linear-gradient(-45deg,#263029 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#263029 75%),linear-gradient(-45deg,transparent 75%,#263029 75%);background-size:24px 24px;background-position:0 0,0 12px,12px -12px,-12px 0;padding:24px;border:1px solid #62705c;display:grid;place-items:center}.checker img{width:100%}.wide{margin-top:20px}.details img{image-rendering:pixelated;max-height:500px;object-fit:contain;object-position:left}.reference{display:grid;grid-template-columns:180px 1fr;gap:24px;align-items:center}.reference img{max-height:260px;width:auto}.note{border-left:3px solid #b59455;padding:12px 20px;background:#1d2922}a{color:#e2bf7a}@media(max-width:750px){main{padding:24px 14px}.grid{grid-template-columns:1fr}h1{font-size:27px}.checker{padding:12px}.reference{grid-template-columns:100px 1fr}}
</style><main><div class="eyebrow">K-083 · 타이틀과 메인 화면 · 1판</div><h1>마지막 성벽의 황혼</h1><p><span class="badge">검토용 · 게임 미연결</span></p><p>황동과 석재의 제목, 성벽 위 장교, 황혼 속 고블린 무리. 왼쪽은 메뉴가 놓일 수 있도록 어둡고 차분하게 남겼습니다.</p>
<img class="cover" src="@@cover@@" alt="영문 로고가 합성된 16대9 발표용 표지">
<p class="note">표시 제목 확정 항목을 결정 문서에서 찾지 못해 <strong>Human Last Rampart</strong>를 임시 사용했습니다. 기존 메뉴의 한글 제목은 확인했지만 제목 확정으로 간주하지 않았습니다. 로고는 독립 투명 파일이므로 문구 교체가 가능합니다.</p>
<h2>배경과 메뉴 자리</h2><p>1920×1080 기준 왼쪽 여백 128px, 판 폭 624px, 이어하기가 있을 때 높이 512px인 현재 코드 배치를 겹쳐 봅니다. 아래 판은 위치 확인용 도식이며 실제 게임 캡처가 아닙니다.</p>
<button id="toggle" aria-pressed="true">메뉴 자리 표시</button><button id="size" aria-pressed="false">960×540 크기로 보기</button>
<div class="stage" id="stage"><img src="@@main_menu_keyart@@" alt="문구 없는 황혼의 성벽 키 아트"><div class="menu" id="menu"><b>인류의 마지막 성벽</b><small>제목·단추 위치 확인용</small><span>이어하기</span><span>새로 출정</span><span>설정　　종료</span></div></div>
<h2>현재 게임 배경과 비교</h2><div class="grid"><figure><img src="@@old@@" alt="현재 게임에서 읽은 메인 배경"><figcaption>현재 게임 배경 · 읽기 전용 원본</figcaption></figure><figure><img src="@@main_menu_keyart@@" alt="새 메인 배경"><figcaption>새 배경 · 큰 머리와 파란 눈, 오른쪽의 적 무리</figcaption></figure></div>
<h2>투명 제목 로고</h2><p>바둑판은 투명도 확인용 화면입니다. 두 줄 기본형과 한 줄 가로형을 각각 제공합니다.</p><div class="checker"><img src="@@title_logo@@" alt="황동과 석재 두 줄 제목 로고"></div><div class="checker wide"><img src="@@title_logo_wide@@" alt="황동과 석재 한 줄 가로형 제목 로고"></div>
<h2>960×540 축소 검토</h2><img src="@@cover_960@@" width="960" height="540" alt="960×540 표지 실제 크기 미리보기"><p>로고 철자와 단어 구분, 주인공의 눈과 장화, 오우거 실루엣을 확인할 수 있습니다.</p>
<h2>4배 세부 확대</h2><div class="grid details"><figure><img src="@@hero_detail_4x@@" alt="영웅 부분 최근접 4배 확대"><figcaption>배경의 영웅 부분 · 최근접 4배</figcaption></figure><figure><img src="@@logo_detail_4x@@" alt="로고 석재와 황동 부분 최근접 4배 확대"><figcaption>로고 부분 · 최근접 4배</figcaption></figure></div>
<h2>따른 영웅 원본</h2><div class="reference"><img src="@@hero@@" alt="사용자가 선택한 최초 생성 영웅 원본"><p>6판의 최초 생성본을 참고했습니다. 큰 머리, 흰 머리카락, 파란 눈, 검정 장교복과 황동 장식, 짧고 튼튼한 체형, 갈색 장화가 기준입니다. 배경 인물은 새로 생성한 키 아트이며 게임용 동작 시트가 아닙니다.</p></div>
<h2>전달 상태</h2><p>정지 이미지 카드이므로 동작 애니메이션·발선 피벗·시트 프레임 비교는 적용 대상이 아닙니다. 배경과 표지는 1920×1080으로 내보냈으며, 투명 로고와 문구 없는 배경을 분리했습니다. 모든 검토 이미지는 이 페이지에 내장되어 서버와 인터넷 없이 열립니다. Unity 연결과 실제 화면 검증은 남아 있습니다.</p>
</main><script>
document.getElementById('toggle').onclick=function(){const hidden=document.getElementById('menu').classList.toggle('hidden');this.setAttribute('aria-pressed',String(!hidden))};
document.getElementById('size').onclick=function(){const small=document.getElementById('stage').classList.toggle('small');this.setAttribute('aria-pressed',String(small))};
</script></html>'''
img.update(old=old, hero=hero)
for key, data in img.items():
    html = html.replace('@@'+key+'@@', data)
assert '@@' not in html
(ROOT/'_HTML/6_그림_화면/codex_K-083_타이틀_v1.html').write_text(html, encoding='utf-8')
print(json.dumps({'assets':assets,'html_embedded_images':html.count('data:image/png;base64,')}, ensure_ascii=False))
