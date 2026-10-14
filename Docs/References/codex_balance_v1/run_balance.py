from pathlib import Path
import re,json,math,hashlib,subprocess,xml.etree.ElementTree as ET
ROOT=Path.cwd();OUT=ROOT/'Docs/References/codex_balance_v1';D=ROOT/'Game/Assets/_Project/Data/Combat';Q=ROOT/'Docs/collab/live/review/qa/play_20261014'
def field(p,k):
 s=p.read_text(encoding='utf-8-sig');m=re.search(r'^  '+re.escape(k)+r': ([0-9.]+)$',s,re.M);assert m,(p,k);return float(m.group(1))
def observations(name):
 s=(Q/(name+'.txt')).read_text(encoding='utf-8-sig');r=[]
 for m in re.finditer(r'결과 반영 확인: 골드 (\d+)→(\d+), 병사 (\d+)→(\d+), 성벽 ([\d/]+), 다음 번호 (\d+)',s):
  a,b,c,d,w,n=m.groups();r.append(dict(battle=int(n)-1,reward=int(b)-int(a),walls=list(map(int,w.split('/'))),soldiers=int(d)))
 losses=re.findall(r'결과: Defeat (\w+) 처치 (\d+) 골드 (\d+)',s)
 return r,losses
start=int(field(D/'Start/campaign_start.asset','startGold'));cost=int(field(D/'Units/unit_archer.asset','recruitCost'));maxhp=10000
policies=[dict(round=1,name='현행 기준',hp_per_gold=None,recruit_per_wall=0),dict(round=2,name='고가 수리 시험',hp_per_gold=1,recruit_per_wall=0),dict(round=3,name='저가 수리+모집 시험',hp_per_gold=10,recruit_per_wall=8)]
def quote(walls,hp_per_gold):
 if hp_per_gold is None:return 0
 return sum((maxhp-h+hp_per_gold-1)//hp_per_gold for h in walls)
def repair(walls,gold,unit):
 c=quote(walls,unit)
 if c>gold:return list(walls),gold,0,False
 return [maxhp]*4,gold-c,c,True
cases=[];base={}
for difficulty in ['easy','realistic']:
 obs,loss=observations(difficulty);base[difficulty]=dict(victories=obs,first_defeat=loss[0] if loss else None)
 for p in policies:
  gold=start;walls=[maxhp]*4;old=[maxhp]*4;soldiers=300;rows=[]
  for o in obs:
   # 固定된 실측 피해/보상 재생. 수리/모집이 전투 결과를 바꾼 효과는 계산하지 않는다.
   damage=[a-b for a,b in zip(old,o['walls'])];old=o['walls'];walls=[max(0,h-d) for h,d in zip(walls,damage)];gold+=o['reward'];before=gold;requested=0;paid=0;ok=None
   if p['hp_per_gold'] is not None:requested=quote(walls,p['hp_per_gold']);walls,gold,paid,ok=repair(walls,gold,p['hp_per_gold'])
   recruit=4*p['recruit_per_wall'];recruit_paid=recruit*cost
   if recruit_paid>gold:recruit=0;recruit_paid=0
   gold-=recruit_paid;soldiers+=recruit
   rows.append(dict(battle=o['battle'],fixed_reward=o['reward'],fixed_damage=damage,gold_before_maintenance=before,repair_quote=requested,repair_paid=paid,repair_success=ok,recruited=recruit,recruit_paid=recruit_paid,gold_after=gold,soldiers=soldiers,walls=walls[:],minimum_wall=min(walls)))
  cases.append(dict(**p,difficulty=difficulty,rows=rows,combat_result='1차 기준의 관측된 승리 구간만 경제 재생. 2·3차 실제 승패 미측정.'))
checks=[]
def check(name,truth):assert truth,name;checks.append(dict(name=name,result='통과'))
check('시작 골드 원본100',start==100);check('궁수 모집비 원본5',cost==5)
check('만피 수리 무료',quote([10000]*4,10)==0);check('1HP 손상 올림1골드',quote([9999,10000,10000,10000],10)==1)
check('10HP 손상1골드',quote([9990,10000,10000,10000],10)==1);check('11HP 손상2골드',quote([9989,10000,10000,10000],10)==2)
check('벽별 올림 비용',quote([9999]*4,10)==4)
w=[8958,9828,9930,9963];r=repair(w,1100,1);check('골드 부족 시 HP/골드 무변경',r==(w,1100,0,False));check('현실 첫 수리10HP당1골드134',quote(w,10)==134)
r=repair(w,1100,10);check('수리 이후 최대HP·골드 보존',r==([10000]*4,966,134,True));r2=repair(r[0],r[1],10);check('중복 수리 차감0',r2==([10000]*4,966,0,True))
for c in cases:
 for x in c['rows']:
  check(f"{c['round']}차/{c['difficulty']}/전투{x['battle']} 골드회계",x['gold_before_maintenance']-x['repair_paid']-x['recruit_paid']==x['gold_after'] and x['gold_after']>=0)
  check(f"{c['round']}차/{c['difficulty']}/전투{x['battle']} 체력범위",all(0<=v<=10000 for v in x['walls']))
wave=[]
for i in range(1,5):
 s=(D/f'Waves/wave_{i}.asset').read_text(encoding='utf-8-sig');counts=list(map(int,re.findall(r'    Count: (\d+)',s)))
 wave.append(dict(battle=i,realistic=sum(counts),easy=sum(max(1,round(n*.5)) for n in counts)))
check('현실 웨이브 규모1000/3000/8000/20000',[x['realistic'] for x in wave]==[1000,3000,8000,20000])
x=ET.parse(Q/'results_easy_realistic.xml').getroot();qa=dict(total=x.attrib.get('total'),passed=x.attrib.get('passed'),failed=x.attrib.get('failed'),result=x.attrib.get('result'))
files=[D/'Start/campaign_start.asset',D/'Units/unit_archer.asset']+[D/f'Waves/wave_{i}.asset' for i in range(1,5)]+[Q/'easy.txt',Q/'realistic.txt',Q/'results_easy_realistic.xml']
snapshot={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
result=dict(model='관측 전투 결과를 고정한 정비 경제 재생 모델; Unity 전투 시뮬레이터 아님',head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),policies=policies,baseline=base,waves=wave,cases=cases,model_qa=dict(passed=len(checks),failed=0,checks=checks),claude_previous_play_qa=qa,sources_sha256=snapshot)
(OUT/'results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({'qa':len(checks),'waves':wave,'cases':[dict(round=c['round'],difficulty=c['difficulty'],gold=c['rows'][-1]['gold_after'],soldiers=c['rows'][-1]['soldiers'],minWall=c['rows'][-1]['minimum_wall']) for c in cases]},ensure_ascii=False))

