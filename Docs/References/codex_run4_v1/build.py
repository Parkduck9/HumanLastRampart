"""달리기 생성 원본 포장 및 독립 HTML. 그림 편집 없이 전체 포즈 최근접 축척."""
from pathlib import Path
from PIL import Image
import json,hashlib,statistics,base64,re
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
OLD=HERE.parent/'codex_walk6_v1'
ITEMS=[('archer','궁수'),('gunner','총병'),('cannon_crew','대포병'),('wizard','마법사')]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
dump=lambda p,v:p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def pack(ident,label):
 folder=HERE/ident;raw=Image.open(folder/'source.png').convert('RGBA');parts=[]
 # 생성 원본의 실제 투명 여백에서 칸을 나눠 넓은 보폭이 잘리지 않게 한다.
 alpha=raw.getchannel('A');projection=[sum(alpha.getpixel((x,y))>128 for y in range(raw.height)) for x in range(raw.width)]
 edges=[0]
 for i in range(1,4):
  expected=raw.width*i/4;radius=int(raw.width*.055)
  candidates=range(max(edges[-1]+1,int(expected)-radius),min(raw.width,int(expected)+radius))
  edges.append(min(candidates,key=lambda x:(projection[x],abs(x-expected))))
 edges.append(raw.width)
 for i in range(4):
  left,right=edges[i:i+2];part=raw.crop((left,0,right,raw.height))
  box=part.getchannel('A').point(lambda v:255 if v>128 else 0).getbbox();assert box
  patch=part.crop(box);w,h=patch.size
  xs=[x for y in range(int(h*.08),max(1,int(h*.4))) for x in range(w) if patch.getpixel((x,y))[3]>100]
  pivot=statistics.median(xs) if xs else w/2
  parts.append((patch,box,left,pivot))
 # 공통 지면을 사용한다. 프레임마다 발을 바닥에 맞추면 공중 자세가 사라진다.
 baseline=max(p[1][3] for p in parts);top=min(p[1][1] for p in parts)
 scale=min(300/(baseline-top),112/max(p[3] for p in parts),112/max(p[0].width-p[3] for p in parts))
 sheet=Image.new('RGBA',(1024,384));entries=[]
 for i,(patch,box,left,pivot) in enumerate(parts):
  w,h=patch.size;ww,hh=round(w*scale),round(h*scale);frame=patch.resize((ww,hh),Image.Resampling.NEAREST)
  x=128-round(pivot*ww/w);y=352-round((baseline-box[1])*scale)
  assert x>0 and y>0 and x+ww<256 and y+hh<384,(ident,i,x,y,ww,hh)
  cell=Image.new('RGBA',(256,384));cell.paste(frame,(x,y));cell.save(folder/f'frame_{i+1}.png');sheet.paste(cell,(i*256,0))
  entries.append(dict(frame=i,packedRect=[x,y,ww,hh],groundGap=352-y-hh,sourceRect=[left+box[0],box[1],w,h]))
 sheet.save(folder/'run4_grid.png');hashes=[sha(folder/f'frame_{i+1}.png') for i in range(4)];assert len(set(hashes))==4
 info=dict(id=ident,label=label,action='run',facing='side',columns=4,rows=1,cell=[256,384],size=[1024,384],frames=4,fps=8,frameOrder=[0,1,2,3],footPivotTopLeft=[128,352],groundPolicy='shared source baseline; airborne boots stay above ground',uniformScale=scale,sourceSha256=sha(folder/'source.png'),sheetSha256=sha(folder/'run4_grid.png'),frameHashes=hashes,framesData=entries,rightPreview='mirror',status='user-review-draft',unityPlayVerified=False)
 dump(folder/'run4_grid.json',info);return info
def data(p,mime):return 'data:'+mime+';base64,'+base64.b64encode(p.read_bytes()).decode()
def asset(folder,info,action):
 ident=info['id'];name='run4_grid' if action=='run' else 'walk4_grid'
 return dict(id=ident,label=info['label'],facing=info['facing'],action=action,fps=8 if action=='run' else 6,note=('달리기 4프레임 · 기본 8FPS · 오른쪽은 반전' if action=='run' else '기존 정면 걷기 유지 · 기본 6FPS'),sheet=data(folder/(name+'.png'),'image/png'),json=data(folder/(name+'.json'),'application/json'),frames=[data(folder/f'frame_{i+1}.png','image/png') for i in range(4)])
def html(assets):
 text=(OLD/'preview.html').read_text(encoding='utf-8')
 text=re.sub(r'const assets=.*?;\s*const config=',lambda m:'const assets='+json.dumps(assets,ensure_ascii=False)+';\nconst config=',text,flags=re.S)
 text=text.replace('fps:6,move:','fps:0,move:').replace('dt*config.fps','dt*(config.fps||p.asset.fps)').replace("side'?75:35","side'?150:35")
 text=text.replace("'좌우 걷기'","'좌우 달리기'").replace("asset.id+'_walk4_grid", "asset.id+'_'+(asset.action==='run'?'run4':'walk4')+'_grid")
 text=text.replace('value="6" selected','value="6"').replace('<select id="fps">','<select id="fps"><option value="0" selected>기본: 아군8 / 적6</option>')
 # 이전 검토 문서 내장은 새 README로 교체한다.
 start=text.find('# 캐릭터 6종 걷기');end=text.find('</pre>',start)
 if start>=0 and end>start:text=text[:start]+(HERE/'README.md').read_text(encoding='utf-8')+text[end:]
 text=text.replace('캐릭터 6종 걷기 4프레임','아군 달리기 · 적 걷기 4프레임').replace('6종 걷기','아군 달리기·적 걷기')
 text=re.sub(r'<title>.*?</title>','<title>아군 달리기 · 적 걷기 4프레임</title>',text,count=1)
 text=re.sub(r'<h1>.*?</h1>','<h1>아군 달리기 4종 · 기존 적 걷기 2종</h1>',text,count=1)
 text=re.sub(r'</h1><p>.*?</p>','</h1><p>아군 달리기를 재제작했습니다. 1→2→3→4 반복 · 아군 기본 8FPS / 적 6FPS · 사용자 검토 초안</p>',text,count=1)
 if len(assets)==1:text=re.sub(r'<h1>.*?</h1>','<h1>'+assets[0]['label']+' · 좌우 달리기 4프레임</h1>',text,count=1)
 return text
if __name__=='__main__':
 infos=[pack(*item) for item in ITEMS];assets=[asset(HERE/i['id'],i,'run') for i in infos]
 for ident in ['ogre','ladder_goblin']:
  info=json.loads((OLD/ident/'walk4_grid.json').read_text(encoding='utf-8'));assets.append(asset(OLD/ident,info,'walk'))
 for item in json.loads((HERE/'reference_hashes.json').read_text(encoding='utf-8')).values():assert sha(ROOT/item['path'])==item['sha256']
 dump(HERE/'manifest.json',dict(allies=infos,enemies='../codex_walk6_v1/{ogre,ladder_goblin} unchanged'))
 combined=html(assets);(HERE/'preview.html').write_text(combined,encoding='utf-8');(ROOT/'_HTML/codex_아군달리기_적걷기4프레임_v2.html').write_text(combined,encoding='utf-8')
 for a in assets[:4]:
  page=html([a]);(HERE/a['id']/'preview.html').write_text(page,encoding='utf-8');(ROOT/f"_HTML/codex_{a['label']}달리기4프레임_v1.html").write_text(page,encoding='utf-8')
 dump(HERE/'qa.json',dict(status='PASS',newSheets=4,uniqueFrames=16,rgba=True,noClipping=True,sharedGroundBaseline=True,referencesUnchanged=True,enemyBytesReused=True,unityPlayVerified=False))
 print('PASS: 4 run sheets / 16 frames; 2 enemy sheets unchanged')
