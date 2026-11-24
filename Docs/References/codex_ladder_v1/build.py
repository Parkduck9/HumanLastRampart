from pathlib import Path
from PIL import Image
import numpy as np,base64,hashlib,json,shutil
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
OUT=HERE/'assets'
OUT.mkdir(exist_ok=True)
def rgba_bbox(im,threshold=16):
 a=np.asarray(im)[:,:,3];y,x=np.where(a>threshold)
 return (int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1))
def record_image(path):
 im=Image.open(path)
 return {'file':str(path.relative_to(HERE)).replace('\\','/'),'size':list(im.size),'mode':im.mode,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
assets={}
for key in ('ladder_front','ladder_side'):
 src=HERE/'sources'/(key+'.png')
 im=Image.open(src).convert('RGBA');bbox=rgba_bbox(im);crop=im.crop(bbox)
 h=1152;w=round(crop.width*h/crop.height)
 assert w<=224,(key,w)
 small=crop.resize((w,h),Image.Resampling.NEAREST)
 packed=Image.new('RGBA',(256,1536));left=128-w//2;top=1472-h
 packed.paste(small,(left,top));target=OUT/(key+'.png');packed.save(target)
 assets[key]={**record_image(target),'kind':'prop','frames':1,'cell':[256,1536],'pivot':[128,1472],'unityPivotBottomLeft':[0.5,1-1472/1536],'ppuCandidate':1152/4.3,'fullCanvasHeightMetersCandidate':1536*4.3/1152,'body':[left,top,w,h],'sourceRect':list(bbox),'proposalLengthMeters':4.3,'topHookReference':[128,top],'anchorStatus':'하단 접지 중심/상단 갈고리 중심 제안; 정밀 hook contact는 3D 연결 검토','alphaCropThreshold':16}
for key,source_id in [('carry_run','carry_run_fixed'),('deploy','deploy'),('climb','climb_fixed')]:
 im=Image.open(HERE/'sources'/(source_id+'.png')).convert('RGBA')
 parts=[];bounds=[]
 for f in range(4):
  start=round(f*im.width/4);end=round((f+1)*im.width/4);part=im.crop((start,0,end,im.height));b=rgba_bbox(part);parts.append(part.crop(b));bounds.append([start+b[0],b[1],b[2]-b[0],b[3]-b[1]])
 factor=min(260/max(p.height for p in parts),220/max(p.width for p in parts))
 packed=Image.new('RGBA',(1024,384));frames=[]
 for f,part in enumerate(parts):
  w,h=round(part.width*factor),round(part.height*factor);left=128-w//2;top=352-h
  packed.paste(part.resize((w,h),Image.Resampling.NEAREST),(f*256+left,top))
  frames.append({'index':f,'sourceRect':bounds[f],'body':[left,top,w,h],'pivot':[128,352]})
 target=OUT/(key+'4.png');packed.save(target)
 assets[key]={**record_image(target),'kind':'character','frames':4,'cell':[256,384],'pivot':[128,352],'unityPivotBottomLeft':[0.5,1-352/384],'ppuCandidate':max(f['body'][3] for f in frames)/0.35,'framesData':frames,'maxBodyHeight':max(f['body'][3] for f in frames),'source':'sources/'+source_id+'.png','sharedScaleWithinClip':factor,'fpsProposal':6,'loop':key!='deploy','alphaCropThreshold':16}
reference=HERE/'identity_reference.png'
if not reference.exists():shutil.copyfile(ROOT/'Game/Assets/_Project/Art/Characters/Static/ladder_goblin_static.png',reference)
manifest={'card':'K-017','task':'T-CX-058','status':'시안·사용자확정전·Game연결없음','wallHeightMeters':4,'goblinHeightReferenceMeters':0.35,'ladderLengthProposalMeters':4.3,'assets':assets,'technicalOperations':'기존 그림 복사 / alpha>16 실체 경계로 자르기 / 최근접 비율 축소 / 투명 격자 배치만. 픽셀 덧칠·색보정·알파 재작성 없음. 실체 crop 밖의 매우 낮은 알파 halo는 경계 자르기에서 빠질 수 있음.','limitations':['각 생성 시트의 손·가로대 정밀 접점과 후면 2번 프레임의 가려진 손은 사용자 검토 필요','정면/측면 소품은 개별 생성이라 가로대 수·위치의 완전한 3D 일치는 보장하지 않음','캐릭터 키0.35m 대비4m벽은 매우 커서 실제비율 합성에서 인물이 작게 보임. 상세 확대는 별도 표시','노멀맵·사다리 물리·최종 Unity 피벗/가림·게임 연결 미검증']}
(HERE/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
payload={'assets':{},'labels':{'carry_run':'사다리 들고 달리기','deploy':'아래에서 세우기','climb':'후면 타고 오르기'}}
for key,item in assets.items():
 payload['assets'][key]={**item,'uri':'data:image/png;base64,'+base64.b64encode((HERE/item['file']).read_bytes()).decode()}
identity='data:image/png;base64,'+base64.b64encode(reference.read_bytes()).decode()
props=''
for key,label in [('ladder_front','정면 사다리'),('ladder_side','측면 사다리')]:
 props+='<figure><img class="prop" src="'+payload['assets'][key]['uri']+'" alt="'+label+'"><figcaption>'+label+'</figcaption></figure>'
links=''.join('<a download="'+Path(item['file']).name+'" href="'+payload['assets'][key]['uri']+'">'+payload.get('labels',{}).get(key,key)+' PNG</a>' for key,item in assets.items())
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>K-017 사다리와 사다리병 시안</title><style>*{box-sizing:border-box}body{margin:0;background:#101613;color:#eee4cf;font:16px/1.6 "Malgun Gothic",sans-serif}main{max-width:1420px;margin:auto;padding:26px}h1,h2,h3{color:#d5b16b}h1{font-size:30px}.panel{padding:22px;background:#23302a;border:1px solid #81704c;margin:24px 0}.controls{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:14px 0}button,select{font:inherit;min-height:44px;background:#33473b;color:#eee4cf;border:1px solid #d5b16b;padding:8px 14px;cursor:pointer}button:focus-visible,select:focus-visible{outline:3px solid #f4d790;outline-offset:3px}label{display:flex;gap:8px;align-items:center}a{color:#f4d790}.downloads{display:flex;gap:18px;flex-wrap:wrap}.inspect{display:grid;grid-template-columns:minmax(280px,.65fr) minmax(0,1.35fr);gap:20px}.inspect>div{min-width:0}.inspect.zoomed{grid-template-columns:1fr}.scroll{width:100%;overflow-x:auto;overflow-y:auto;max-height:800px;min-height:384px}.scroll canvas{max-width:none}.scroll canvas{display:block;margin:auto;image-rendering:pixelated}#stage{display:block;width:100%;height:auto;image-rendering:pixelated}.props{display:flex;justify-content:center;gap:64px;background:repeating-conic-gradient(#26332b 0% 25%,#37443c 0% 50%) 50%/24px 24px}.prop{height:600px;max-width:42vw;object-fit:contain;image-rendering:pixelated}.identity{height:250px;image-rendering:pixelated}.frames{display:grid;grid-template-columns:repeat(4,1fr);gap:8px}.frames canvas{width:100%;height:auto;image-rendering:pixelated}figure{margin:12px 0}figcaption{text-align:center;color:#c4cdbf}.note{background:#18231d;padding:12px 16px;border-left:4px solid #d5b16b}.muted{color:#b9c2b0}input[type=range]{accent-color:#d5b16b;width:160px}@media(max-width:900px){.inspect{grid-template-columns:1fr}.props{gap:20px}.prop{height:450px}.frames{grid-template-columns:repeat(2,1fr)}}@media(max-width:600px){main{padding:14px}.panel{padding:16px}h1{font-size:25px}.controls{gap:8px}button,select{padding:8px 10px}}</style><main><p class="muted">K-017 / T-CX-058 · 시안 · 사용자 확정 전</p><h1>사다리와 사다리병 동작</h1><p>기존 녹색 피부·검은 모히칸·노란 눈·엄니·갈색 가죽옷의 2.2등신 고블린을 유지했습니다. 4m 성벽용 긴 사다리는 인물 칸에 축소하지 않고 별도 소품으로 나눴습니다.</p><section class="panel"><h2>동작 확인</h2><div class="controls"><label>동작<select id="clip"><option value="carry_run">들고 달리기</option><option value="deploy">세우기 · 1회</option><option value="climb">오르기 · 반복</option></select></label><button id="play" aria-pressed="true">정지</button><button id="prev">이전 프레임</button><button id="next">다음 프레임</button><label>프레임<input id="frame" type="range" min="0" max="3" value="0"><span id="frameText">1 / 4</span></label></div><div class="controls"><label>속도<select id="fps"><option value="4">4 FPS</option><option value="6" selected>6 FPS</option><option value="8">8 FPS</option></select></label><label>동작 확대<select id="scale"><option value="1" selected>1배</option><option value="2">2배</option></select></label><label>배경<select id="background"><option value="checker">체커</option><option value="dark">어두움</option><option value="light">밝음</option></select></label><label>합성 소품<select id="propView"><option value="ladder_front">정면</option><option value="ladder_side">측면</option></select></label></div><p id="loading" role="status">내장 PNG 읽는 중...</p><p id="phaseNote" class="muted"></p><div class="inspect" id="inspection"><div><h3>인물만 확대 · 256×384</h3><div class="scroll"><canvas id="detail" width="256" height="384" aria-label="선택한 사다리병 동작 프레임"></canvas></div><p class="muted">황동 십자 = 발 기준점 128·352. 인물만 분리된 시트입니다. 2배는 한 열로 넓히며 좁은 창에서는 가로 스크롤로 전체 칸을 확인할 수 있습니다.</p></div><div><h3>긴 사다리 별도 레이어 합성</h3><canvas id="stage" width="960" height="640" aria-label="4m 성벽과 분리 사다리 배치 목업"></canvas><p class="note">성벽 4m·병사 키 0.35m의 비율을 유지하므로 인물이 작게 보입니다. 배치는 설명용이며 실제 게임 캡처·물리 시험이 아닙니다. 세우기는 사다리를 아래에서 끌어올리고 기울이는 위치 연출이며 새로운 그림을 코드로 그리지 않습니다.</p></div></div></section><section class="panel"><h2>사다리 소품 · 정면과 측면</h2><div class="props">__PROPS__</div><p>칸 256×1536 / 하단 기준 128·1472 / 실체 높이 1152px. 길이 4.3m는 4m 벽에 기울여 거는 <strong>규격 제안</strong>입니다. 상단 갈고리 중심은 별도 기준이며 최종 걸침·가림은 3D 연결 후 확인해야 합니다.</p><p class="muted">정면과 측면은 독립 생성 시안이므로 가로대 개수·간격이 완전히 일치하는 정밀 3D 도면은 아닙니다.</p></section><section class="panel"><h2>4칸 전체 비교</h2><div id="sheets"></div><p>원안에서 달리기 1·3번의 같은 다리 반복을 발견해 3·4번을 다시 생성했습니다. 오르기는 왼팔 1·2번 / 오른팔 3·4번 방향을 보완했습니다. 후면 2번 손은 머리 뒤에 일부 가려져 사용자 확인이 필요합니다.</p></section><section class="panel"><h2>기존 인물 비교와 파일</h2><img class="identity" src="__IDENTITY__" alt="기존 사다리 고블린 원본"><p>왼쪽은 기존 원본입니다. 새 시트는 인물·소품 분리를 위해 들고 있던 짧은 사다리를 인물 그림에서 제외했습니다. 실제 손과 가로대 접점·표현 밀도는 사용자 검토 및 연결 시 조정합니다.</p><div class="downloads">__LINKS__</div><p>모든 PNG 원안과 보완 프롬프트는 sources/ 및 generation.json, 자르기·축소·앵커는 manifest.json에 보관했습니다. Game 파일·원본 아트·데이터를 수정하지 않았습니다. 노멀맵은 이 시안에 포함하지 않았습니다.</p></section></main><script id="payload" type="application/json">__PAYLOAD__</script><script>__JS__</script></html>'''
html=html.replace('__PROPS__',props).replace('__IDENTITY__',identity).replace('__LINKS__',links).replace('__PAYLOAD__',json.dumps(payload,ensure_ascii=False)).replace('__JS__',(HERE/'viewer.js').read_text(encoding='utf-8'))
(HERE/'preview.html').write_text(html,encoding='utf-8')
(ROOT/'_HTML/codex_K017_사다리_v1.html').write_text(html,encoding='utf-8')
# Technical contact sheet only; no new painted content.
contact=Image.new('RGBA',(1024,1152))
for row,key in enumerate(('carry_run','deploy','climb')):contact.paste(Image.open(HERE/assets[key]['file']),(0,row*384))
contact.save(HERE/'sheet_contact.png')
print('5 assets, 3 clips x4 frames; standalone HTML built')
