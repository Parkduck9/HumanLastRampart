from pathlib import Path
from html.parser import HTMLParser
from io import BytesIO
import base64, hashlib, json, subprocess, tempfile, sys
from PIL import Image

# 내장 그림의 디코딩과 원본 보존을 검사하고 로컬 HTML을 실제 렌더링한다.
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
PAGE=ROOT/'_HTML/4_그림_캐릭터/codex_K-065_아군6판_다시_v1.html'

class CheckImages(HTMLParser):
    def __init__(self):
        super().__init__()
        self.images=[]
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='img':
            assert a['src'].startswith('data:image/png;base64,')
            im=Image.open(BytesIO(base64.b64decode(a['src'].split(',',1)[1])))
            im.load()
            assert im.size==(64,96)
            assert (int(a['width']),int(a['height'])) in [(64,96),(256,384)]
            assert a.get('alt')
            self.images.append(a['alt'])

def run():
    p=CheckImages()
    p.feed(PAGE.read_text(encoding='utf-8'))
    assert len(p.images)==48
    before=json.loads((OUT/'source_hashes_before.json').read_text(encoding='utf-8'))
    assert all(hashlib.sha256((ROOT/k).read_bytes()).hexdigest()==v for k,v in before.items())
    result={'내장그림디코딩':len(p.images),'변경없는원본파일':len(before),'브라우저':[], '브라우저시도기록':'이 환경에서 일반 headless 실행은 GPU 프로세스 접근 실패, 프로세스 내부 GPU 방식은 45초 시간 초과. 실제 페이지 렌더링 검증은 완료하지 못함.'}
    chrome=Path('C:/Program Files/Google/Chrome/Application/chrome.exe')
    if chrome.exists() and '--browser' in sys.argv:
        for suffix,fragment in [('top',''),('4x','#scale-4')]:
            with tempfile.TemporaryDirectory(prefix='k065-chrome-') as profile:
                target=OUT/('review_browser_'+suffix+'.png')
                args=[str(chrome),'--headless','--disable-gpu','--in-process-gpu','--disable-software-rasterizer','--hide-scrollbars','--no-first-run','--no-default-browser-check','--user-data-dir='+profile,'--window-size=1440,1100','--force-device-scale-factor=1','--virtual-time-budget=3000','--screenshot='+str(target),PAGE.as_uri()+fragment]
                process=subprocess.run(args,capture_output=True,timeout=45)
                assert process.returncode==0,process.stderr.decode(errors='replace')[-2000:]
                with Image.open(target) as im:
                    assert im.size==(1440,1100)
                result['브라우저'].append({'화면':suffix,'크기':[1440,1100],'결과':'로컬 파일 직접 렌더링 성공'})
    (OUT/'review_validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))

if __name__=='__main__': run()
