"""Read-only Game audit and self-contained installed-character gallery."""
from pathlib import Path
from datetime import datetime
import base64
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    match = re.search(r"^guid: (\w+)", path.read_text(encoding="utf-8-sig"), re.M)
    return match[1] if match else None


def pixels_equal(a, b):
    return a.size == b.size and a.convert("RGBA").tobytes() == b.convert("RGBA").tobytes()


def data_uri(path):
    return "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode("ascii")


color_audit = read(HERE / "color_audit.json")
normal_audit = read(HERE / "normal_audit.json")
bindings = read(HERE / "normal_bindings.json")
manifest = read(ROOT / "Docs/References/codex_k081_v1/manifest_current.json")
binding_by_color = {x["color"].replace("\\", "/"): x for x in bindings}
normal_by_name = {x["sheet"]: x for x in normal_audit["sheets"]}
manifest_by_name = {x["sheet"]: x for x in manifest["sheets"]}
guid_to_paths = {}
for meta in (ROOT / "Game/Assets").rglob("*.meta"):
    value = guid(meta)
    if value:
        guid_to_paths.setdefault(value, []).append(meta.with_suffix(""))

direct_refs, pair_refs = {}, {}
for asset in (ROOT / "Game/Assets/_Project/Data/Combat").rglob("*.asset"):
    if asset.parent.name not in ("Units", "Enemies"):
        continue
    text = asset.read_text(encoding="utf-8-sig")
    for field, value in re.findall(r"^  (\w+): \{fileID: 2800000, guid: (\w+), type: 3\}", text, re.M):
        direct_refs.setdefault(value, []).append(f"{asset.name} : {field}")
    for color, normal in re.findall(r"- Sheet: \{fileID: 2800000, guid: (\w+), type: 3\}\s+Normal: \{fileID: 2800000, guid: (\w+), type: 3\}", text):
        pair_refs.setdefault(color, []).append({"asset": asset.name, "normal_guid": normal})

resources = {
    "crew_push6.png": "CannonArt.PushSheet → BattlePropView Resources.Load",
    "crew_pull6.png": "CannonArt.PullSheet → BattlePropView Resources.Load",
}
unused = {"ogre_static.png", "ladder_goblin_static.png", "ladder_goblin_walk.png"}
rows = []
for initial in color_audit["rows"]:
    name = initial["sheet"]
    m = manifest_by_name[name]
    target = ROOT / initial["target"]
    source = ROOT / initial["source"]
    binding = binding_by_color[initial["target"]]
    normal = ROOT / binding["normal"]
    source_normal = (HERE / "expanded_ignite_normal/cannon_crew_ignite6_normal.png"
                     if name == "cannon_crew_ignite6.png"
                     else ROOT / normal_by_name[name]["source_normal"])
    c, n = Image.open(target), Image.open(normal)
    cg, ng = guid(Path(str(target) + ".meta")), guid(Path(str(normal) + ".meta"))
    cell = [432, 384] if name == "cannon_crew_ignite6.png" else m["game_cell"]
    refs = direct_refs.get(cg, [])
    pairs = pair_refs.get(cg, [])
    row = dict(
        sheet=name, color=initial["target"], source_color=initial["source"],
        normal=str(normal.relative_to(ROOT)).replace("\\", "/"),
        source_normal=str(source_normal.relative_to(ROOT)).replace("\\", "/"),
        size=list(c.size), cell=cell, columns=c.width // cell[0], rows=c.height // cell[1],
        frame_order=m.get("frame_order", []),
        state="대체·보관용" if name in unused else "런타임 연결",
        reference_detail=("carrySheet가 MovingSheet로 우선 선택되어 보관 중인 이전 걷기 대안" if name == "ladder_goblin_walk.png"
                          else "현재 전투에 별도 서기 상태 없음" if name in unused else "정의 또는 Resources 실행 경로 존재"),
        references=refs, resource_reference=resources.get(name), color_guid=cg, normal_guid=ng,
        color_source_sha256=digest(source), color_game_sha256=digest(target),
        normal_source_sha256=digest(source_normal), normal_game_sha256=digest(normal),
        color_bytes_equal=digest(source) == digest(target),
        color_pixels_equal=pixels_equal(Image.open(source), c),
        normal_bytes_equal=digest(source_normal) == digest(normal),
        normal_pixels_equal=pixels_equal(Image.open(source_normal), n),
        normal_same_size=c.size == n.size,
        normal_alpha_equal=c.convert("RGBA").getchannel("A").tobytes() == n.convert("RGBA").getchannel("A").tobytes(),
        color_guid_unique=len(guid_to_paths.get(cg, [])) == 1,
        normal_guid_unique=len(guid_to_paths.get(ng, [])) == 1,
        binding_guid_matches=binding["normal_guid"] == ng,
        pose_pair_references=pairs,
        pose_pair_matches=all(x["normal_guid"] == ng for x in pairs),
    )
    row["checks_passed"] = all(row[x] for x in (
        "color_bytes_equal", "color_pixels_equal", "normal_bytes_equal", "normal_pixels_equal",
        "normal_same_size", "normal_alpha_equal", "color_guid_unique", "normal_guid_unique",
        "binding_guid_matches", "pose_pair_matches"))
    rows.append(row)

qa = []
for kind in ("edit", "play"):
    candidates = sorted((HERE / "qa").glob(f"*/{kind}.xml"), key=lambda p: p.stat().st_mtime)
    if candidates:
        latest = candidates[-1]
        try:
            a = ET.parse(latest).getroot().attrib
            qa.append({"kind": kind, "path": str(latest.relative_to(ROOT)).replace("\\", "/"),
                       "result": a.get("result"), "total": a.get("total"), "passed": a.get("passed"),
                       "failed": a.get("failed"), "inconclusive": a.get("inconclusive"),
                       "file_modified": datetime.fromtimestamp(latest.stat().st_mtime).isoformat(timespec="seconds")})
        except ET.ParseError:
            qa.append({"kind": kind, "status": "시험 XML 기록 중·미확정"})

summary = dict(generated_at=datetime.now().isoformat(timespec="seconds"),
               sheet_count=len(rows), frame_count=sum(x["columns"] * x["rows"] for x in rows),
               checks_passed=sum(x["checks_passed"] for x in rows),
               runtime_connected=sum(x["state"] == "런타임 연결" for x in rows),
               alternatives=[x["sheet"] for x in rows if x["state"] == "대체·보관용"],
               qa_snapshot=qa, rows=rows,
               limits_ko="정적 파일·규격·참조 검사. 이 갤러리는 실제 게임 캡처나 Unity/브라우저 실행 검증이 아니다. 실행 시험은 루트 담당의 별도 기록을 따른다.")
(HERE / "gallery_validation.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
embedded = []
for row in rows:
    item = dict(row)
    item["image"] = data_uri(ROOT / row["color"])
    item["normal_image"] = data_uri(ROOT / row["normal"])
    embedded.append(item)
reference_path = ROOT / "Docs/References/codex_hero_octopath_review_v6/generated.png"
payload = json.dumps({"summary": {k: v for k, v in summary.items() if k != "rows"}, "items": embedded,
                      "reference": data_uri(reference_path)}, ensure_ascii=False).replace("</", "<\\/")

HTML = r'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>최신 캐릭터 게임 적용 감사</title><style>
*{box-sizing:border-box}body{margin:0;background:#161a1e;color:#e9e6db;font:16px/1.6 system-ui,sans-serif}main{max-width:1480px;margin:auto;padding:28px}h1{font-size:28px;margin:0}h2{font-size:21px}p{margin:10px 0}.muted{color:#b2b7bc}button,select,input{font:inherit;color:inherit;background:#292f35;border:1px solid #65717e;border-radius:5px;padding:7px 12px}button{cursor:pointer}button:focus,select:focus{outline:2px solid #e0bd6c}.summary{border-left:4px solid #d1ae62;padding:10px 16px;background:#20252a}.tools{display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin:15px 0}.layout{display:grid;grid-template-columns:1fr 240px;gap:22px}.viewers{display:grid;grid-template-columns:1fr 1fr;gap:12px}.panel{padding:14px;background:#22292f;border:1px solid #424b54;border-radius:7px;min-width:0}.stage{height:420px;display:flex;align-items:center;justify-content:center;background-color:#333a40;background-image:linear-gradient(45deg,#424950 25%,transparent 25%),linear-gradient(-45deg,#424950 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#424950 75%),linear-gradient(-45deg,transparent 75%,#424950 75%);background-size:24px 24px;background-position:0 0,0 12px,12px -12px,-12px 0}canvas{image-rendering:pixelated;max-height:100%;max-width:100%;object-fit:contain}.ref{max-width:100%;max-height:420px;object-fit:contain;image-rendering:pixelated;background:#050505}code{overflow-wrap:anywhere;color:#e4c88a}.details{overflow-wrap:anywhere}.good{color:#a6dcb0}.hold{color:#e1bd79}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px}.tile{padding:10px;background:#20272e;border:1px solid #56616b;border-radius:7px;text-align:left;min-width:0}.tile strong{font-size:13px;overflow-wrap:anywhere;display:block}.tile canvas{width:100%;height:160px;object-fit:contain}.tile small{display:block}.tile.current{border:2px solid #e4c88a}table{border-collapse:collapse;width:100%;font-size:13px}td,th{padding:8px;border-bottom:1px solid #46505a;text-align:left;overflow-wrap:anywhere}.scroll{overflow:auto}#raw{width:100%;max-height:520px;object-fit:contain;image-rendering:pixelated}summary{cursor:pointer}.tiny{font-size:13px}@media(max-width:850px){main{padding:16px}.layout{grid-template-columns:1fr}.viewers{grid-template-columns:1fr}.stage{height:360px}.ref{max-height:250px}}
</style><main><h1>최신 캐릭터 · 게임 설치본 감사</h1><p class="muted">D-080 정정·D-081 기준 / 최신 원본과 실제 Game 폴더의 PNG를 직접 대조</p><div class="summary" id="summary"></div>
<p>아래 그림은 <b>현재 게임에 저장된 원색과 노멀맵</b>입니다. 노멀맵은 조명 방향용 보조 그림이며 자체 색으로 게임에 표시하지 않습니다. 자료의 보관과 실제 상태에서의 사용을 구분했습니다.</p>
<div class="tools"><label>시트 <select id="sheet"></select></label><button id="previous">이전 칸</button><button id="next">다음 칸</button><label>프레임 <input id="frame" type="range" min="0" value="0"></label><span id="frameLabel"></span></div>
<div class="layout"><section><h2 id="name"></h2><div class="viewers"><div class="panel"><b>게임 원색</b><div class="stage"><canvas id="color"></canvas></div></div><div class="panel"><b>게임 노멀맵</b><div class="stage"><canvas id="normal"></canvas></div></div></div><p id="detail" class="details tiny"></p><details><summary>게임 설치본 전체 시트</summary><img id="raw" alt="선택한 게임용 전체 색 시트"></details></section>
<aside class="panel"><b>사용자가 고른 기준 그림</b><p class="tiny">첫 generated.png<br>D-080 정정·D-081</p><img class="ref" id="reference" alt="사용자가 선택한 흰 머리 주인공 원본"><p class="tiny muted">인게임 동작은 이 비율과 인물 설정을 기준으로 제작했습니다. 본 그림 자체가 동작 시트라는 뜻은 아닙니다.</p></aside></div>
<h2>35개 시트 전체</h2><p class="muted tiny">누르면 위에서 프레임별로 볼 수 있습니다. 그림을 늘려 그리지 않고 원본 칸을 그대로 잘라 표시합니다.</p><div class="grid" id="grid"></div>
<h2>검사와 사용 경로</h2><p id="qa" class="tiny"></p><div class="scroll"><table><thead><tr><th>시트</th><th>상태</th><th>크기 / 칸</th><th>파일·알파·GUID</th><th>실제 정의 / Resources 참조</th></tr></thead><tbody id="audit"></tbody></table></div><p class="muted tiny">이 HTML은 서버·외부 이미지·외부 스크립트 없이 엽니다. 파일 내용의 정적 검사 결과이며 렌더링 자연스러움·접촉·그림자의 실제 게임 검증은 별도 캡처를 따릅니다.</p></main>
<script id="data" type="application/json">__PAYLOAD__</script><script>
const DATA=JSON.parse(document.getElementById('data').textContent),items=DATA.items;
const $=id=>document.getElementById(id);let selected=0,frame=0;const loaded=new Map();
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function image(src){if(!loaded.has(src))loaded.set(src,new Promise((resolve,reject)=>{const im=new Image;im.onload=()=>resolve(im);im.onerror=reject;im.src=src}));return loaded.get(src)}
async function draw(canvas,src,row,index){const im=await image(src),[w,h]=row.cell;canvas.width=w;canvas.height=h;const ctx=canvas.getContext('2d');ctx.imageSmoothingEnabled=false;ctx.clearRect(0,0,w,h);ctx.drawImage(im,index%row.columns*w,Math.floor(index/row.columns)*h,w,h,0,0,w,h)}
async function render(){const ix=selected,r=items[ix],count=r.columns*r.rows;frame=(frame+count)%count;$('sheet').value=ix;$('frame').max=count-1;$('frame').value=frame;$('frameLabel').textContent=`${frame+1} / ${count}`;$('name').textContent=r.sheet;$('raw').src=r.image;const refs=[...r.references,...(r.resource_reference?[r.resource_reference]:[])];$('detail').innerHTML=`<b class="${r.state==='대체·보관용'?'hold':'good'}">${esc(r.state)}</b> · ${esc(r.reference_detail)}<br>전체 ${r.size.join('×')} / 칸 ${r.cell.join('×')} / ${r.columns}열×${r.rows}행<br><code>${esc(r.color)}</code><br>${refs.map(esc).join('<br>')||'현재 전투 실행 경로 없음'}<br>원본과 색·노멀 파일 및 화소 일치 ${r.checks_passed?'통과':'확인 필요'} / 알파 좌표 일치 ${r.normal_alpha_equal?'통과':'실패'}`;document.querySelectorAll('.tile').forEach((el,i)=>el.classList.toggle('current',i===ix));await Promise.all([draw($('color'),r.image,r,frame),draw($('normal'),r.normal_image,r,frame)])}
const s=DATA.summary;$('summary').innerHTML=`<b>설치 ${s.sheet_count}시트 · ${s.frame_count}프레임 / 정적 검사 ${s.checks_passed}/${s.sheet_count} 통과</b><br>런타임 경로 ${s.runtime_connected}개 · 대체·보관 ${s.alternatives.length}개<br><span class="tiny">감사 생성 ${esc(s.generated_at)} / 보관: ${s.alternatives.map(esc).join(', ')}</span>`;$('reference').src=DATA.reference;
items.forEach((r,i)=>{const o=document.createElement('option');o.value=i;o.textContent=r.sheet;$('sheet').append(o);const tile=document.createElement('button');tile.className='tile';tile.innerHTML=`<canvas></canvas><strong>${esc(r.sheet)}</strong><small class="${r.state==='대체·보관용'?'hold':'good'}">${esc(r.state)}</small><small>${r.columns*r.rows}프레임 · ${r.checks_passed?'검사 통과':'확인 필요'}</small>`;tile.onclick=()=>{selected=i;frame=0;render();$('name').scrollIntoView({behavior:'smooth',block:'start'})};$('grid').append(tile);draw(tile.querySelector('canvas'),r.image,r,0);const tr=document.createElement('tr');tr.innerHTML=`<td>${esc(r.sheet)}</td><td>${esc(r.state)}</td><td>${r.size.join('×')}<br>${r.cell.join('×')}</td><td>${r.checks_passed?'통과':'확인 필요'}</td><td>${[...r.references,...(r.resource_reference?[r.resource_reference]:[])].map(esc).join('<br>')||esc(r.reference_detail)}</td>`;$('audit').append(tr)});
$('qa').textContent=s.qa_snapshot.length?'생성 시점 시험 XML 참고: '+s.qa_snapshot.map(x=>`${x.kind}: ${x.result||x.status}, 통과 ${x.passed??'-'} / 전체 ${x.total??'-'}, 실패 ${x.failed??'-'}, 유보 ${x.inconclusive??'-'} (${x.file_modified||''})`).join(' · ')+' — 진행 중인 후속 시험은 이 고정 스냅샷에 포함되지 않습니다.':'이 생성 시점에 완료된 시험 XML 없음. 실제 Play 검증은 루트 담당의 후속 기록을 따릅니다.';
$('sheet').onchange=()=>{selected=+$('sheet').value;frame=0;render()};$('frame').oninput=()=>{frame=+$('frame').value;render()};$('previous').onclick=()=>{frame--;render()};$('next').onclick=()=>{frame++;render()};render();
</script></html>'''
HTML = HTML.replace("__PAYLOAD__", payload)
output = ROOT / "_HTML/codex_캐릭터전체적용_감사_v1.html"
output.write_text(HTML, encoding="utf-8")
(HERE / "preview.html").write_text(HTML, encoding="utf-8")
assert len(rows) == 35 and all(x["checks_passed"] for x in rows)
assert "http://" not in HTML and "https://" not in HTML
print(json.dumps({k: v for k, v in summary.items() if k != "rows"}, ensure_ascii=False, indent=2))
print(output, output.stat().st_size)
