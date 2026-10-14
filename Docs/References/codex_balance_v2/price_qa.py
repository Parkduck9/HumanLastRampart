from pathlib import Path
import json
OUT=Path('Docs/References/codex_balance_v2')
PLANS={'A':{'archer':[5,8,12],'gunner':[8,12,18],'cannon_crew':[10,15,20]},'B':{'archer':[8,12,16],'gunner':[12,18,24],'cannon_crew':[15,20,25]}}
def quote(plan,kind,total,n):
 if plan not in PLANS or kind not in PLANS[plan] or not isinstance(total,int) or not isinstance(n,int) or not 0<=total<=2000 or not 0<=n<=2000-total:raise ValueError('잘못된 모집 범위')
 return sum(PLANS[plan][kind][0 if total+i<600 else 1 if total+i<1200 else 2] for i in range(n))
def recruit(plan,kind,total,wall,slot,n,gold,cannon=False):
 if min(total,wall,slot,n,gold)<0:raise ValueError('음수')
 if total+n>2000 or wall+n>500 or slot+n>(5 if cannon else 160):return total,wall,slot,gold,False
 price=quote(plan,kind,total,n)
 if price>gold:return total,wall,slot,gold,False
 return total+n,wall+n,slot+n,gold-price,True
def repair(hp,maxhp,requested,gold):
 if min(hp,maxhp,requested,gold)<0 or hp>maxhp:raise ValueError('잘못된 수리')
 amount=min(requested,maxhp-hp);price=(amount+9)//10
 if price>gold:return hp,gold,False
 return hp+amount,gold-price,True
checks=[]
def check(name,condition):assert condition,name;checks.append({'name':name,'result':'통과'})
check('A300명에서300명모집1500',quote('A','archer',300,300)==1500)
check('A599경계2명13골드',quote('A','archer',599,2)==13)
check('A1199경계2명20골드',quote('A','archer',1199,2)==20)
check('분할주문가격동일',quote('A','archer',590,30)==quote('A','archer',590,10)+quote('A','archer',600,20))
check('B가A보다비쌈',all(quote('B','archer',n,10)>quote('A','archer',n,10) for n in [300,600,1200]))
check('골드부족무변경',recruit('A','archer',300,75,20,10,49)==(300,75,20,49,False))
check('정확금액성공',recruit('A','archer',300,75,20,10,50)==(310,85,30,0,True))
check('성벽정원거부',recruit('A','archer',1000,499,100,2,9999)[-1]==False)
check('슬롯정원거부',recruit('A','archer',1000,300,159,2,9999)[-1]==False)
check('전체정원거부',recruit('A','archer',1999,300,100,2,9999)[-1]==False)
check('대포5명한도',recruit('A','cannon_crew',300,75,5,1,9999,True)[-1]==False)
check('부족대포보충',recruit('A','cannon_crew',300,75,4,1,10,True)==(301,76,5,0,True))
check('수리초과클램프',repair(9995,10000,100,1)==(10000,0,True))
check('수리부족무변경',repair(9000,10000,1000,99)==(9000,99,False))
check('만피무료',repair(10000,10000,100,0)==(10000,0,True))
check('두번째만피수리무료',repair(*repair(9995,10000,100,1)[:1],10000,100,0)==(10000,0,True))
for name,fn in [('음수요청',lambda:quote('A','archer',300,-1)),('알수없는병종',lambda:quote('A','unknown',300,1)),('HP초과오류',lambda:repair(10001,10000,1,1))]:
 ok=False
 try:fn()
 except ValueError:ok=True
 check(name,ok)
result={'status':'시험안, 실제Unity승패미측정','plans':PLANS,'thresholds':[600,1200],'caps':{'global':2000,'wall':500,'normal_post':160,'cannon_post':5},'repair':{'hp_per_gold':10,'default_click_hp':100},'quotes':[{'plan':p,'from':n,'recruit':q,'gold':quote(p,'archer',n,q)} for p in PLANS for n,q in [(300,100),(300,300),(590,30),(1000,400)]],'qa':{'passed':len(checks),'failed':0,'checks':checks}}
(OUT/'price_qa.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print('가격·정원·수리 경계 QA '+str(len(checks))+'/'+str(len(checks))+' 통과')
