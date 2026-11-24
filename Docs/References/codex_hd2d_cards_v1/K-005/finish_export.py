"""Package existing pixel maps for Claude; never write Game or Unity .meta."""
from pathlib import Path
from PIL import Image
import numpy as np
import json,shutil,hashlib,base64,zipfile
HERE=Path(__file__).resolve().parent;BUNDLE=HERE.parent;ROOT=BUNDLE.parents[2]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
uri=lambda p:'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode('ascii')
materials=read(HERE/'manifest.json');chosen=read(HERE/'finalization_selection.json')
dest=HERE/'game_export/World';dest.mkdir(parents=True,exist_ok=True)
checks=[]
def check(name,ok):
 checks.append({'check':name,'passed':bool(ok)})
 if not ok:raise AssertionError(name)
for m in materials:
 d=HERE/m['id']
 for kind in ['albedo','normal']:
  source=d/(kind+'.png');out=dest/(m['id']+'_'+kind+'.png');shutil.copyfile(source,out)
  check(out.name+' hash/size',sha(source)==sha(out) and list(Image.open(out).size)==m['size'])
 check(m['id']+' 32px/m',all(px==32*meter for px,meter in zip(m['size'],m['meters'])))
 if m['id'] in chosen:
  check(m['id']+' visual/crop',chosen[m['id']]['visualReviewPassed'] and m['selectedCropPixels']==chosen[m['id']]['cropPixels'])
  check(m['id']+' color repeat metric',max(m['seam']['ratioX'],m['seam']['ratioY'])<=1.25)
  raw=np.array(Image.open(d/'normal.png'),dtype=float)
  ix=float(abs(raw[:,1:]-raw[:,:-1]).mean());iy=float(abs(raw[1:]-raw[:-1]).mean())
  rx=float(abs(raw[:,0]-raw[:,-1]).mean()/max(ix,.001));ry=float(abs(raw[0]-raw[-1]).mean()/max(iy,.001))
  check(m['id']+' normal repeat metric',max(rx,ry)<=1.25)
geometry={'decision':'D-068','wallSideMeters':40,'wallHeightMeters':4,'walkwayWidthMeters':2,'soldierHeightMeters':.35,'parapetWallHeightMeters':.105,'parapetThicknessMeters':.30,'merlonHeightMeters':.105,'merlonWidthMeters':.26,'merlonGapMeters':.21,'cannonOpeningWidthMeters':1.12,'cornerPlatformSizeMeters':[4.5,4.5],'cornerPlatformHeightMeters':4,'outerTowerSizeMeters':[2.6,2.6],'outerTowerAboveWalkwayMeters':2.1,'outerTowerGroundTopMeters':6.1}
package={'decision':geometry,'pixelsPerMeter':32,'materials':materials,'threeSurfacesRepeatReviewed':list(chosen),'unityVerified':False,'normalOrientationUnityVerified':False,'gameFolderModified':False,'importNote':'Claude copies World/<surface>_albedo.png and <surface>_normal.png to Resources/BattleArt/World, Unity creates .meta'}
(HERE/'game_export/manifest.json').write_text(json.dumps(package,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(HERE/'game_export/README.md').write_text('# D-068 · 3D 표면 내보내기\n\n작성 Codex. World의 이름은 BattleWorldMaterials 읽기규칙과 같다. 색sRGB/노멀Normal map·Linear, Point, Repeat, 압축None. Mipmap켜기(현행README기준). 타일은너비/높이÷32m를덮는다. 128×64=4×2m, 128×128=4×4m, 256×256=8×8m.\n\n성벽·난간3종은D068비율로새생성하고동일영역크롭·3×3육안및경계/벡터검사완료. 미러/블렌드/픽셀덧칠없음. 나머지5종은기존후보유지. 게임복사/임포트/meta/실제3D조명·작은난간가독성확인은Claude담당. 자동숫자는무봉제/라이팅완전보증이아님.\n',encoding='utf-8')
with zipfile.ZipFile(HERE/'D068_world_maps.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted((HERE/'game_export').rglob('*')):
  if p.is_file():z.write(p,p.relative_to(HERE/'game_export').as_posix())
data=[]
for key in ['wall_stone','parapet_top','parapet_side']:
 m=next(x for x in materials if x['id']==key)
 data.append({'id':key,'name':m['label'],'size':m['size'],'before':uri(HERE/key/'albedo_before_D068.png'),'after':uri(HERE/key/'albedo.png'),'normal':uri(HERE/key/'normal.png'),'ratio':[round(m['seam']['ratioX'],2),round(m['seam']['ratioY'],2)]})
template='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-005 · D068 질감 마무리</title><style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#111918;color:#e9e4d4;font:16px/1.75 'Malgun Gothic',sans-serif}main{max-width:1200px;margin:auto;padding:32px 22px}h1{font-size:34px}p{max-width:1000px}.note{padding:15px;border-left:3px solid #d5b16b;background:#26352d}select{font:inherit;padding:9px;background:#2b3c31;color:inherit}label{margin-right:18px}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin-top:20px}article{min-width:0;background:#25312a;border:1px solid #526147;border-radius:6px;overflow:hidden}.tile{aspect-ratio:1;background-repeat:repeat;image-rendering:pixelated}article p{padding:0 15px}a{color:#e4c88e}.meta{font-size:13px;color:#bac6b2}table{width:100%;border-collapse:collapse}td{border-bottom:1px solid #41533e;padding:8px}button,a.download{display:inline-block;margin:10px 15px;padding:7px 12px;border:1px solid #9f8b60}svg{width:100%;height:auto;display:block;background:#cbd0bd}details{margin-top:25px}pre{white-space:pre-wrap;font:14px/1.7 'Malgun Gothic',sans-serif}@media(max-width:760px){.grid{grid-template-columns:1fr}}</style><main><h1>D-068 · 작은 난간에 맞춘 회색 석재</h1><p>성벽·난간 윗면·난간 옆면의 반복 경계를 다듬었습니다. 생성된 색/노멀의 같은 위치를 잘라 최근접 축소했으며, 미러 복제·경계 덧칠은 하지 않았습니다.</p><p class="note">시안1·바깥쪽 모서리 포탑이 현재 확정입니다. 난간높이0.105m≈3.4px, 두께0.30m≈9.6px(32px/m). 작은 면에서는 잔무늬보다 차분한 회색 면이 우선입니다.</p><label>표면 <select id="surface"></select></label><label>반복 <select id="repeat"><option value="1">1×1</option><option value="3" selected>3×3</option></select></label><div class="grid"><article><div id="before" class="tile" role="img" aria-label="수정 전 타일"></div><p>수정 전</p></article><article><div id="after" class="tile" role="img" aria-label="D068 수정 후 타일"></div><p>D068 수정 후</p><a id="afterDL" class="download">색 PNG 저장</a></article><article><div id="normal" class="tile" role="img" aria-label="수정 후 노멀 타일"></div><p>수정 후 노멀</p><a id="normalDL" class="download">노멀 PNG 저장</a></article></div><p id="metric" class="meta"></p><h2>확정 구조와 질감의 단위</h2><table><tr><td>성벽 한 면 / 높이 / 통로 폭</td><td>40m / 4m / 2m</td></tr><tr><td>모서리 발판</td><td>4.5×4.5m · 통로와 같은 높이4m</td></tr><tr><td>바깥쪽 솟은 포탑</td><td>2.6×2.6m · 통로 위2.1m</td></tr><tr><td>돌출부 높이 / 폭 / 사이</td><td>0.105m / 0.26m / 0.21m</td></tr><tr><td>대포 사격구</td><td>폭1.12m</td></tr><tr><td>난간 타일 한 장</td><td>128×64px = 4×2m</td></tr></table><p class="meta">이 수치는 D068 기준입니다. 이전 K006의5×5×7m 그림은 과거 제안 보관본입니다.</p><h2>검증과 남은 확인</h2><p>3×3 색/노멀 반복에서 별도 경계 줄무늬를 찾지 못했고 경계 차이는 내부 변화보다 작았습니다. 벽돌의 정기적인 반복 무늬는 남습니다. 실제 3D 모형·조명·작은 난간에서의 가독성, 노멀 Y방향은 Claude의 Unity 확인이 필요합니다.</p><p>8쌍 내보내기는 K-005/D068_world_maps.zip에 있습니다. World/&lt;표면&gt;_albedo.png·_normal.png 이름으로 준비했으며 실제 Game 폴더는 수정하지 않았습니다.</p><details><summary>제작/검토 기록</summary><pre id="record"></pre></details><p class="meta">K-005 / T-CX-053 · 수정 전후 비교 도구 · 실제 Unity 캡처 아님</p></main><script id="data" type="application/json">__DATA__</script><script>'use strict';const DATA=JSON.parse(document.getElementById('data').textContent),$=id=>document.getElementById(id);for(const m of DATA.maps){const o=document.createElement('option');o.value=m.id;o.textContent=m.name;$('surface').append(o)}function show(){const m=DATA.maps.find(v=>v.id===$('surface').value),n=Number($('repeat').value);for(const id of ['before','after','normal']){$(id).style.backgroundImage='url('+m[id]+')';$(id).style.backgroundSize=(100/n)+'% '+(100/n)+'%';$(id).style.aspectRatio=m.size[0]+'/'+m.size[1]}for(const [id,key]of[['afterDL','after'],['normalDL','normal']]){$(id).href=m[key];$(id).download=m.id+'_'+(key==='after'?'albedo':'normal')+'.png'}$('metric').textContent='현재 경계/내부 차이 비: 가로 '+m.ratio[0]+', 세로 '+m.ratio[1]+' · 실제 Unity 합격 판정과 구분합니다.'}$('surface').addEventListener('change',show);$('repeat').addEventListener('change',show);$('record').textContent=DATA.record;show();</script></html>'''
html=template.replace('__DATA__',json.dumps({'maps':data,'record':read(HERE/'finalization_selection.json')},ensure_ascii=False).replace('</','<\\/'))
# Human-readable metadata in details; retain full machine records alongside it.
html=html.replace("$('record').textContent=DATA.record;","$('record').textContent=JSON.stringify(DATA.record,null,2);")
(HERE/'finalization_preview.html').write_text(html,encoding='utf-8');(ROOT/'_HTML/codex_K005_질감마무리_D068.html').write_text(html,encoding='utf-8')
report={'task':'T-CX-053','decision':'D-068','technicalChecksPassed':len(checks),'checks':checks,'threeSurfaceRepeatVisualReviewed':True,'normalShapeAlignmentCertified':False,'unityVerified':False,'gameChanges':False}
(HERE/'finalization_qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'checksPassed':len(checks),'mapPairs':len(materials),'gameChanges':False}))
