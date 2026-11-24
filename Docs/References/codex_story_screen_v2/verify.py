from pathlib import Path
import argparse,base64,hashlib,json,re,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--compile',action='store_true')
parser.add_argument('--unity-project',default=r'C:/Unity/1/HumanLastRampart/Game')
args=parser.parse_args()
ui=(ROOT/'Game/Assets/_Project/UI/ScreenView.cs').read_text(encoding='utf-8')
entry=(ROOT/'Game/Assets/_Project/Story/StoryEntry.cs').read_text(encoding='utf-8')
source=ROOT/'Game/Assets/_Project/Art/Characters/Static/wizard_static.png'
copy=ROOT/'Game/Assets/_Project/Story/Resources/StoryArt/wizard_front_chestnut.png'
html=(HERE/'preview.html').read_text(encoding='utf-8')
source_hash=hashlib.sha256(source.read_bytes()).hexdigest()
copy_hash=hashlib.sha256(copy.read_bytes()).hexdigest()
portrait=re.search(r'src="data:image/png;base64,([^"]+)"',html).group(1)
meta=Path(str(copy)+'.meta').read_text(encoding='utf-8')
original_meta=Path(str(source)+'.meta').read_text(encoding='utf-8')
checks={
'portrait_byte_identical':source_hash==copy_hash,
'portrait_new_guid':re.search(r'guid: (\w+)',meta).group(1)!=re.search(r'guid: (\w+)',original_meta).group(1),
'portrait_point_filter':bool(re.search(r'filterMode: 0',meta)),
'portrait_resource_name':bool(re.search(r'Resources.Load<Texture2D>\("StoryArt/wizard_front_chestnut"\)',entry)),
'next_and_ending_labels_preserved':'ending ? "메인으로" : "다음"' in entry,
'completion_signal_preserved':'() => Complete(ScreenOutcome.Done)' in entry,
'no_proposed_story_api':'StoryGraph' not in entry and 'StoryDef' not in entry,
'old_constructor_default_path':': this(owner, title, description, false, null, null)' in ui,
'new_story_constructor_path':': this(owner, title, description, true, character, speaker)' in ui,
'story_dialogue_bottom_anchors':'new Vector2(0.055f, 0.055f), new Vector2(0.945f, 0.335f)' in ui,
'portrait_aspect_preserved':'AspectRatioFitter.AspectMode.FitInParent' in ui,
'portrait_does_not_block_pointer':'image.raycastTarget = false;' in ui,
'html_copies_identical':(HERE/'preview.html').read_bytes()==(ROOT/'_HTML/codex_K013_스토리화면_v2.html').read_bytes(),
'html_embedded_png_matches':hashlib.sha256(base64.b64decode(portrait)).hexdigest()==copy_hash,
'html_not_real_capture_claim':'실제 Unity 캡처가 아닙니다' in html,
'html_no_remote_dependencies':not bool(re.search(r'(?:src|href)="https?://',html)),
}
compiled=[]
if args.compile:
 main=Path(args.unity_project)
 compiler=Path(r'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/DotNetSdkRoslyn/csc.dll')
 runtime=Path(r'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/NetCoreRuntime/dotnet.exe')
 with tempfile.TemporaryDirectory(prefix='hlr_k013_compile_') as temp:
  temp=Path(temp)
  for name in ('HLR.UI','HLR.Story'):
   candidates=list((main/'Library/Bee/artifacts').glob('*/'+name+'.rsp'))
   if not candidates: raise RuntimeError('Existing Unity response file unavailable: '+name)
   template=max(candidates,key=lambda p:p.stat().st_mtime).read_text(encoding='utf-8-sig').splitlines()
   lines=[]
   for line in template:
    if line.startswith('-out:'): line='-out:"'+str(temp/(name+'.dll'))+'"'
    elif line.startswith('-refout:'): line='-refout:"'+str(temp/(name+'.ref.dll'))+'"'
    elif line.startswith('"Assets/'): line='"'+str(ROOT/'Game'/line.strip('"'))+'"'
    elif name=='HLR.Story' and re.match(r'-r:".*HLR.UI.ref.dll"',line): line='-r:"'+str(temp/'HLR.UI.ref.dll')+'"'
    # Source generators are irrelevant here; use Unity reference set without invoking editor analyzers.
    if line.startswith('-analyzer:') or line.startswith('/analyzer:'): continue
    lines.append(line)
   rsp=temp/(name+'.rsp')
   rsp.write_text('\n'.join(lines),encoding='utf-8')
   process=subprocess.run([str(runtime),str(compiler),'@'+str(rsp)],cwd=main,capture_output=True,text=True,encoding='utf-8',errors='replace')
   compiled.append({'assembly':name,'exitCode':process.returncode,'output':(process.stdout+process.stderr).strip()})
   if process.returncode: break
checks.update({x['assembly']+'_compile':x['exitCode']==0 for x in compiled})
qa={'card':'K-013','checks':checks,'passed':sum(checks.values()),'total':len(checks),'compile':compiled,'compileMethod':'기존 메인 Unity Library의 읽기 전용 참조 rsp를 사용한 별도 Roslyn 컴파일; 출력은 임시 폴더, Unity 실행 아님','portraitSha256':copy_hash,'browserReview':'이 카드 작업자 미수행; 부모 검토 대기','unityReview':'미수행; 합친 뒤 조정 담당/사용자 실제 화면 검증 필요'}
(HERE/'qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(qa,ensure_ascii=False,indent=2))
assert all(checks.values()),'K-013 verification failed'
