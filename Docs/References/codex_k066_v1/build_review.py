from pathlib import Path
from PIL import Image
import base64, json, re

P=Path(__file__).resolve().parent
ROOT=P.parents[2]
dest=ROOT/'_HTML/4_그림_캐릭터/codex_K-066_적6판_다시_v1.html'
names={'goblin':'곤봉 고블린','ladder_empty':'사다리병 · 빈손','ladder_carry':'사다리병 · 운반','ogre':'오우거'}

def uri(path):return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode()

def figure(name,label):
    im=Image.open(P/(name+'.png'))
    w,h=im.size
    return f'<figure><div class="stage"><img src="{uri(P/(name+".png"))}" width="{w}" height="{h}" style="--w:{w};--h:{h}" alt="{label}"></div><figcaption>{label}</figcaption></figure>'

def gallery(items,scale=4):
    return f'<div class="gallery" style="--scale:{scale}">'+''.join(figure(n,l) for n,l in items)+'</div>'

css='''*{box-sizing:border-box}html{color-scheme:dark}body{margin:0;background:#101721;color:#edf1f6;font:16px/1.65 system-ui,"Malgun Gothic",sans-serif}main{max-width:1720px;margin:auto;padding:38px 36px 90px}h1{font-size:32px;line-height:1.35;margin:12px 0}h2{font-size:24px;margin:42px 0 10px}h3{font-size:19px;margin:10px 0}p{max-width:1100px;color:#cbd3de}.tag{color:#f0c779;font-weight:700}.notice{border-left:4px solid #e5b868;padding:14px 20px;background:#222b37;border-radius:8px}.toolbar{position:sticky;top:0;z-index:5;background:#101721ed;border-bottom:1px solid #344052;padding:12px 0;display:flex;gap:12px;align-items:center;flex-wrap:wrap}button{font:inherit;cursor:pointer;padding:8px 20px;border:1px solid #54667e;border-radius:7px;background:#253349;color:white}button[aria-pressed=true]{background:#e0b56e;color:#152030;border-color:#e0b56e}section{border:1px solid #334154;border-radius:12px;padding:22px;margin:24px 0;background:#17212e}.scroll{overflow:auto}.gallery{display:flex;gap:14px;align-items:flex-start;overflow-x:auto;padding:12px 0 20px}.gallery figure{margin:0;flex:0 0 calc(96px * var(--scale))}.stage{position:relative;width:calc(96px * var(--scale));height:calc(112px * var(--scale));display:flex;align-items:flex-end;justify-content:center;background-color:#222c39;background-image:linear-gradient(45deg,#273444 25%,transparent 25%),linear-gradient(-45deg,#273444 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#273444 75%),linear-gradient(-45deg,transparent 75%,#273444 75%);background-size:16px 16px;background-position:0 0,0 8px,8px -8px,-8px 0}.stage:after{content:"";position:absolute;left:0;right:0;top:calc(104px * var(--scale));height:1px;background:#edbd67}.stage img{width:calc(var(--w) * var(--scale) * 1px);height:calc(var(--h) * var(--scale) * 1px);margin-bottom:0;image-rendering:pixelated;object-fit:contain}.stage img[height="96"]{margin-bottom:calc(16px * var(--scale))}figcaption{padding:8px 5px;font-size:14px;color:#e3eaf3;line-height:1.45}table{border-collapse:collapse;width:100%;font-size:15px}th,td{text-align:left;padding:10px 13px;border-bottom:1px solid #3a4656;vertical-align:top}th{color:#f0ca83}.muted{color:#9faec0;font-size:14px}.source{display:flex;gap:30px;align-items:flex-start;flex-wrap:wrap}.source img{max-width:280px;max-height:420px;object-fit:contain;background:#080b10}.pal{display:flex;gap:3px;flex-wrap:wrap;margin:8px 0}.swatch{width:25px;height:25px;border:1px solid #8392a1}code{color:#e8c88f;font-size:13px}details{margin:16px 0}summary{cursor:pointer;color:#e6c88e}a{color:#a4c9f4}@media(max-width:700px){main{padding:20px 14px}h1{font-size:25px}section{padding:12px}.toolbar{font-size:14px}table{min-width:650px}}'''
css += '.stage img[height="96"]{margin-bottom:0}'
parts=[f'<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-066 적 서기 기준 그림 다시 · 선택본 기준</title><style>{css}</style><main>',
'<div class="tag">K-066 · 적 서기 참고 그림 · 2026-10-20</div><h1>처음 고른 주인공의 도트 방향으로</h1><p>큰 머리와 또렷한 눈, 검은 테와 큼직한 점. 곤봉 고블린·빈손 사다리병·사다리 운반병·오우거의 앞·옆 서기를 비교합니다.</p>',
'<div class="notice"><b>기준은 generated.png 처음 생성본입니다.</b> D-080 정정에 따라 머리·눈을 줄였던 corrected.png 및 K-063/K-064는 비교 자료로만 표시합니다. 이번 결과는 정지 참고 그림이며 게임에 연결하지 않았습니다.</div>',
'<div class="toolbar"><b>비교 배율</b><button id="one" aria-pressed="false" onclick="scaleAll(1)">1배 · 실제 점</button><button id="four" aria-pressed="true" onclick="scaleAll(4)">4배 · 최근접</button><span>모든 그림은 같은 배율 · 금색 가로선은 같은 발선</span></div>',
'<h2>기준 이미지 실측</h2><p>원본은 1024×1536화소입니다. 알파128 이상 인물 범위는 x266~764, y224~1390(오른쪽·아래 끝 포함), 499×1167화소입니다. 머리카락 꼭대기 y224부터 턱 부근 y610까지 약386화소, 인물 키의 약33.1%입니다.</p>',
'<p><b>원본의 큼직한 블록은 약16화소 간격</b>으로도 보입니다. 원본 전체를 최근접 64×96으로 줄이면 인물은 약73점입니다. 생성 이미지에는 불균일한 점과 번짐이 있어 유일한 원래 격자를 확정할 수 없습니다. 카드의 <b>52점 제작 규격</b>에는 인물 경계를 22×52로 환산했습니다. 이때 1점은 원본 약22.44화소, 머리는 약17점(32.7%), 외곽선은 주로1점입니다. 52점을 원본에서 자동 검출한 값으로 표시하지 않습니다.</p>',
gallery([('hero_source_grid16','원본 전체 ÷16 · 인물 약73점 · 격자 추정 확인'),('hero_generated_grid','선택본 → 제작 기준 52점 · 머리 약17점'),('hero_corrected_comparison','이전 보정본 → 비교용52점 · 채택 기준 아님')]),
'<p class="muted">첫 그림은 격자 확인용으로 높이를 맞추지 않았습니다. 이후 비교는 모두 주인공52점, 동일 발선으로 맞춥니다. 원본 범위의 알파와 최근접 표본을 유지했으며 주인공을 다시 그리지 않았습니다.</p>',
'<details><summary>선택된 처음 생성본 원본 보기</summary><div class="source"><img alt="사용자가 고른 주인공 generated.png 처음 생성본" src="'+uri(P.parent/'codex_hero_octopath_review_v6/generated.png')+'"><p>흰 뾰족머리, 파란 홍채와 흰 반사점, 짧은 몸통·다리, 큰 갈색 장화와 둥근 손. 적의 홍채색은 기존 황색/붉은색을 유지합니다.</p></div></details>',
'<h2>사람과 종별 크기 비교</h2><p>왼쪽부터 선택 주인공52점 / 기존 인간 궁수52점(뒤 서기 원화) / 고블린40점 / 사다리병42점 / 사다리 운반병 몸42점·장비 포함54점 / 오우거68점입니다. 인간 대비 약77% / 81% / 131%인 <b>참고 그림의 제작 크기</b>입니다. 현재 게임의 원근·표시 크기를 측정한 화면이 아닙니다.</p>',
gallery([('hero_generated_grid','선택 주인공 · 52점'),('soldier_reference','기존 인간 궁수 · 52점 · 뒤'),('goblin_front','고블린 · 40점'),('ladder_empty_front','사다리병 · 42점'),('ladder_carry_front','운반병 몸42점 · 사다리 포함54점'),('ogre_front','오우거 · 68점')]),
'<h2>앞·옆 서기 8장</h2><p>각 줄의 주인공·병사·적은 같은 한 점 크기와 같은 발선으로 정렬합니다. 오른쪽 그림은 왼쪽을 향한 측면입니다.</p>']
palettes=json.loads((P/'palettes.json').read_text(encoding='utf-8'))
for name,label in names.items():
    parts.append('<section><h3>'+label+'</h3>'+gallery([('hero_generated_grid','선택 주인공 · 기준'),('soldier_reference','기존 인간 병사 · 뒤 서기'),(name+'_front',label+' · 앞 서기'),(name+'_side',label+' · 옆 서기')]))
    colors=palettes[name]['colors']
    parts.append('<div class="pal">'+''.join(f'<span class="swatch" style="background:{c}" title="{c}"></span>' for c in colors)+'</div><p class="muted">기존 시트에서 뽑은 제한 팔레트. 색상표 출처: <code>'+palettes[name]['source']+'</code></p></section>')
parts+=['<h2>K-063 / K-064 보정 방향과 달라진 이유</h2><p>이전 결과는 머리·눈을 줄인 보정본과 인물96점 방향을 따랐습니다. 이번에는 처음 선택본의 머리 약1/3, 읽히는 눈, 52점 기준의 큼직한 점을 따릅니다. 아래 이전 결과는 형태 차이를 보기 위해 현재와 같은 인물 키로 최근접 축소했습니다. 기존 파일은 변경하지 않았습니다.</p>',
gallery([('hero_generated_grid','선택본 · 큰 머리와 눈'),('hero_corrected_comparison','이전 주인공 보정본'),('k063_soldier','K-063 이전 아군 · 52점 환산'),('soldier_reference','기존 게임 병사 · 크기 기준')])]
for name,label in names.items():
    parts.append('<section><h3>'+label+' · 이전 방향과 비교</h3>'+gallery([('hero_generated_grid','선택본 기준'),('k064_'+name,'K-064 이전 방향 · 같은 키 환산'),(name+'_front','K-066 이번 앞 서기'),(name+'_side','K-066 이번 옆 서기')])+'</section>')
parts+=['<h2>현재 게임 원화와 전후 비교</h2><p>현재 원화의 첫 칸을 추출해 이번 결과와 같은 인물/장비 높이로 맞췄습니다. 새 그림의 덩어리·눈·외곽선 변화용 비교이며 실제 게임 캡처가 아닙니다. 기존 측면 원화와 사다리병 빈손 원화는 별도 존재하지 않아 전 모습으로 꾸미지 않았습니다.</p>']
for name in ['goblin','ladder_carry','ogre']:
    before_source={'goblin':'Goblin/goblin_walk_attack4.png','ladder_carry':'Walk/ladder_goblin_walk.png','ogre':'Walk/ogre_walk.png'}[name]
    parts.append('<section><h3>'+names[name]+'</h3>'+gallery([('before_'+name,'현재 게임 걷기 원화 · 첫 칸'),(name+'_front','이번 앞 서기'),(name+'_side','이번 옆 서기')])+'<p class="muted">이전 원화 출처: <code>'+before_source+'</code></p></section>')
parts+=['<h2>출력 규격과 확인</h2><div class="scroll"><table><tr><th>대상</th><th>칸 / 중심 / 발선</th><th>인물 / 머리 높이</th><th>머리 비율</th></tr>']
manifest=json.loads((P/'manifest.json').read_text(encoding='utf-8'))
for rec in manifest:
    label=names[rec['name'].rsplit('_',1)[0]]+(' 앞' if rec['name'].endswith('front') else ' 옆')
    parts.append(f'<tr><td>{label}</td><td>{rec["cell"][0]}×{rec["cell"][1]} / x{rec["center_x"]} / y{rec["foot_baseline"]}</td><td>몸 약{rec["body_height_estimate"]}점 / 머리 약{rec["head_height_estimate"]}점</td><td>약{rec["head_ratio_estimate"]*100:.1f}%</td></tr>')
parts+=['</table></div><p>머리 끝은 턱의 육안 표식이며 1점 안팎의 오차가 있습니다. 주인공 기준32.7%에 비해 고블린은37.5%로 조금 더 큽니다. 외곽선은 주로1점이며 손·겹친 장비 등 일부 어두운 덩어리는2점 이상입니다. 재질별 약3단 명암을 목표로 제한 팔레트를 사용했습니다.</p>',
'<p>모든 최종 PNG의 칸·발선·알파0/255·기존 시트 색상 집합·4×4 확대 블록을 검사했습니다. 색과 장비를 유지하고 원안은 별도 보관했습니다. 사다리병 두 상태는 같은 운반 원안에서 파생했지만 손·머리카락의 개별 점이 완전히 동일한 동작 프레임은 아닙니다.</p>',
'<p class="muted">정지 그림만 제작했습니다. 동작 시트·노멀맵·게임 연결·Unity Play·실게임 캡처는 이번 결과에 포함되지 않습니다. D-074에 따른 연결 담당은 Claude입니다. 관련 제작 자료: Docs/References/codex_k066_v1/README.md</p>',
'<script>function scaleAll(n){document.querySelectorAll(".gallery").forEach(e=>e.style.setProperty("--scale",n));document.getElementById("one").setAttribute("aria-pressed",String(n===1));document.getElementById("four").setAttribute("aria-pressed",String(n===4));}</script></main></html>']
dest.write_text('\n'.join(parts),encoding='utf-8')
print(dest)
print('내장 이미지',sum(p.count('data:image/png;base64,') for p in parts))
