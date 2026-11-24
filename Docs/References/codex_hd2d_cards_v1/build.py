"""Review bundle: split generated pairs, encode normals, embed static media."""
from pathlib import Path
import json, base64, shutil, hashlib, zipfile
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
write=lambda p,o:p.write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
uri=lambda p:'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode('ascii')

def build():
    jobs=read(HERE/'K-005/generation.json')['requests']; materials=[]
    selected=read(HERE/'K-005/finalization_selection.json') if (HERE/'K-005/finalization_selection.json').exists() else {}
    sheet=Image.new('RGB',(8*128,256),'#26332c')
    for idx,j in enumerate(jobs):
        dest=HERE/'K-005'/j['id'];dest.mkdir(exist_ok=True)
        source=dest/'source.png'
        if not source.exists():shutil.copyfile(j['sourcePath'],source)
        im=Image.open(source).convert('RGB');w,h=im.size
        assert w==h*2,(j['id'],im.size)
        size=tuple(j['size'])
        rect=tuple(selected[j['id']]['cropPixels']) if j['id'] in selected else (0,0,w//2,h)
        normal_rect=(rect[0]+w//2,rect[1],rect[2]+w//2,rect[3])
        color=im.crop(rect).resize(size,Image.Resampling.NEAREST)
        raw=np.array(im.crop(normal_rect).resize(size,Image.Resampling.NEAREST),dtype=float)
        n=raw/127.5-1
        n[:,:,2]=np.maximum(.05,n[:,:,2])
        n/=np.maximum(np.linalg.norm(n,axis=2,keepdims=True),1e-8)
        enc=np.rint((n*.5+.5)*255).clip(0,255).astype(np.uint8)
        color.save(dest/'albedo.png');Image.fromarray(enc).save(dest/'normal.png')
        c=np.array(color,dtype=float)
        sx=float(abs(c[:,0]-c[:,-1]).mean());sy=float(abs(c[0]-c[-1]).mean())
        ix=float(abs(c[:,1:]-c[:,:-1]).mean());iy=float(abs(c[1:]-c[:-1]).mean())
        err=float(abs(np.linalg.norm(enc.astype(float)/127.5-1,axis=2)-1).max())
        info={'id':j['id'],'label':j['name'],'size':j['size'],'meters':j['meters'],'pixelsPerMeter':32,'albedo':f"K-005/{j['id']}/albedo.png",'normal':f"K-005/{j['id']}/normal.png",'source':f"K-005/{j['id']}/source.png",'sourceSize':[w,h],'sourceSha256':sha(source),'albedoSha256':sha(dest/'albedo.png'),'normalSha256':sha(dest/'normal.png'),'normalMaxUnitError':err,'normalConvention':'tangent Y+ requested; Unity orientation not verified','wrap':'Repeat','filter':'Point','normalStrengthSuggestion':.45,'seam':{'edgeX':sx,'edgeY':sy,'interiorX':ix,'interiorY':iy,'ratioX':sx/max(ix,.001),'ratioY':sy/max(iy,.001)},'seamlessCertified':False,'normalShapeAlignmentCertified':False,'unityVerified':False,'processing':'same crop coordinates in left/right panels when D068 selected, nearest resize, normalize normals; no mirror/blend/repaint'}
        info['selectedCropPixels']=list(rect)
        info['D068Finalization']=selected.get(j['id'])
        info['repeatBoundaryMetricsPass']=max(info['seam']['ratioX'],info['seam']['ratioY'])<=1.25
        write(dest/'material.json',info);materials.append(info)
        sheet.paste(color.resize((128,128),Image.Resampling.NEAREST),(idx*128,0))
        sheet.paste(Image.fromarray(enc).resize((128,128),Image.Resampling.NEAREST),(idx*128,128))
    sheet.save(HERE/'K-005/contact_sheet.png');write(HERE/'K-005/manifest.json',materials)
    (HERE/'K-005/README.md').write_text('# K-005 · D068 3D 표면8쌍\n\n작성 Codex/T-CX-053. 성벽·난간3종은 D068의작은난간비율에맞춰새생성, 같은위치크롭/최근접축소/노멀정규화/3×3색노멀반복검토를마쳤다. 반복경계지표는모두내부변화보다작다. 미러/경계덧칠없음. 나머지5종은기존후보유지. 원안은source_before_D068.png와albedo/normal_before_D068.png, 선택기록은finalization_selection.json.\n\ngame_export/World16PNG와D068_world_maps.zip은Claude재질명명규칙과같다. 실제Game복사/임포트/Unity조명·노멀Y방향·작은난간가독성확인은미실행. 픽셀동일경계의수학적보장이나최종게임아트승인을뜻하지않는다. 자세한내용은상위README/메타참고.\n',encoding='utf-8')
    data={'images':{'actual':uri(HERE/'K-004/actual_capture.png'),'camera':uri(HERE/'K-004/reference.png'),'tower':uri(HERE/'K-006/orthographic_reference.png')},'materials':[dict(m,albedoData=uri(HERE/m['albedo']),normalData=uri(HERE/m['normal'])) for m in materials],'readme':(HERE/'README.md').read_text(encoding='utf-8')}
    html=(HERE/'template.html').read_text(encoding='utf-8').replace('__DATA__',json.dumps(data,ensure_ascii=False).replace('</','<\\/'))
    (HERE/'preview.html').write_text(html,encoding='utf-8')
    (ROOT/'_HTML/codex_3D성벽_카메라_질감_포탑카드_v1.html').write_text(html,encoding='utf-8')
    # Each card remains physically separate for later worktree transfer.
    for number in ['004','005','006']:
        card=HERE/('K-'+number)
        with zipfile.ZipFile(HERE/('K-'+number+'_review_assets.zip'),'w',zipfile.ZIP_DEFLATED) as z:
            for p in sorted(card.rglob('*')):
                if p.is_file() and p.name != 'CARD_RESULT.md':z.write(p,p.relative_to(HERE).as_posix())
    write(HERE/'manifest.json',{'author':'Codex','cards':['K-004','K-005','K-006'],'tasks':['T-CX-052','T-CX-053','T-CX-054'],'phase':'M072_consolidated_review_D068_three_surface_repeat_review_complete','gameChanges':False,'materials':materials,'cameraSize':list(Image.open(HERE/'K-004/reference.png').size),'towerSize':list(Image.open(HERE/'K-006/orthographic_reference.png').size)})
    print(json.dumps({'materialPairs':len(materials),'htmlBytes':len(html.encode('utf-8')),'seamRatios':{m['id']:[round(m['seam']['ratioX'],2),round(m['seam']['ratioY'],2)] for m in materials}}))

if __name__=='__main__':build()
