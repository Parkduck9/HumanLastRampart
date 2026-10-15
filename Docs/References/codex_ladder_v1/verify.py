from pathlib import Path
from PIL import Image
import json,hashlib,re,base64
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
m=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'));g=json.loads((HERE/'generation.json').read_text(encoding='utf-8'))
checks={}
for key,a in m['assets'].items():
 p=HERE/a['file'];im=Image.open(p)
 checks[key+'_rgba']=im.mode=='RGBA'
 checks[key+'_size']=list(im.size)==a['size']
 checks[key+'_sha256']=hashlib.sha256(p.read_bytes()).hexdigest()==a['sha256']
 checks[key+'_has_transparency']=im.getchannel('A').getextrema()[0]==0
 if a['kind']=='character':
  checks[key+'_cell_pivot']=a['cell']==[256,384] and a['pivot']==[128,352] and a['frames']==4
  checks[key+'_body_inside_cell']=all(0<=f['body'][0] and f['body'][0]+f['body'][2]<=256 and 0<=f['body'][1] and f['body'][1]+f['body'][3]<=352 for f in a['framesData'])
  frames=[im.crop((i*256,0,(i+1)*256,384)).tobytes() for i in range(4)]
  checks[key+'_4_distinct_frames']=len({hashlib.sha256(x).hexdigest() for x in frames})==4
 else:checks[key+'_prop_anchor']=a['pivot']==[128,1472] and a['body'][3]==1152 and a['topHookReference']==[128,320]
for r in g['records']:
 checks['source_'+r['id']+'_hash']=hashlib.sha256((HERE/r['savedSource']).read_bytes()).hexdigest()==r['sourceSha256']
 checks['source_'+r['id']+'_prompt']=bool(r['prompt'])
original=ROOT/'Game/Assets/_Project/Art/Characters/Static/ladder_goblin_static.png'
checks['identity_reference_exact']=original.read_bytes()==(HERE/'identity_reference.png').read_bytes()
html=(HERE/'preview.html').read_text(encoding='utf-8')
checks['html_standalone_copies']=html==(ROOT/'_HTML/codex_K017_사다리_v1.html').read_text(encoding='utf-8')
checks['html_no_external_dependencies']=not bool(re.search(r'(?:src|href)="https?://',html))
checks['html_not_actual_game_claim']='실제 게임 캡처·물리 시험이 아닙니다' in html
payload=json.loads(re.search(r'<script id="payload" type="application/json">(.*?)</script>',html,re.S).group(1))
checks['5_embedded_assets_match']=all(base64.b64decode(a['uri'].split(',')[1])==(HERE/a['file']).read_bytes() for a in payload['assets'].values())
playback=json.loads((HERE/'playback_qa.json').read_text(encoding='utf-8'))
checks['playback_logic_all_pass']=playback['passed']==playback['total']
result={'card':'K-017','task':'T-CX-058','checks':checks,'passed':sum(checks.values()),'total':len(checks),'playbackLogic':playback,'visualReview':'생성원안7개와최종12칸직접확인. 달리기다리교대보완, 오르기2번손가림·가로대불일치·정밀손접점은시안한계.','browserReview':json.loads((HERE/'browser_qa.json').read_text(encoding='utf-8')) if (HERE/'browser_qa.json').exists() else '작업자CUA apps/browsers없음; 부모검토대기','unityReview':'미수행; Game 연결 없음','productionReady':False}
(HERE/'qa.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'passed':result['passed'],'total':result['total'],'playback':[playback['passed'],playback['total']]},ensure_ascii=False))
assert all(checks.values())
