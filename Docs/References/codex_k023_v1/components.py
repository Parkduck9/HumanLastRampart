"""투명 원안의 분리된 실루엣을 온전히 칸별 포장하기 위한 연결 영역 분석."""
from collections import deque
import numpy as np

def components(alpha):
    mask = np.asarray(alpha) > 128
    h,w = mask.shape
    labels = np.zeros((h,w),dtype=np.int32)
    result = []
    index = 0
    for y,x in zip(*np.where(mask)):
        if labels[y,x]:
            continue
        index += 1
        labels[y,x] = index
        queue = deque([(int(x),int(y))])
        left=right=int(x)
        top=bottom=int(y)
        area=0
        while queue:
            xx,yy=queue.popleft()
            area+=1
            left=min(left,xx);right=max(right,xx)
            top=min(top,yy);bottom=max(bottom,yy)
            for nx,ny in ((xx-1,yy),(xx+1,yy),(xx,yy-1),(xx,yy+1)):
                if 0<=nx<w and 0<=ny<h and mask[ny,nx] and not labels[ny,nx]:
                    labels[ny,nx]=index
                    queue.append((nx,ny))
        result.append({'index':index,'area':area,'bbox':[left,top,right+1,bottom+1]})
    return labels, sorted(result,key=lambda a:a['area'],reverse=True)

if __name__=='__main__':
    from PIL import Image
    from pathlib import Path
    for p in Path(__file__).parent.joinpath('source').glob('*.png'):
        im=Image.open(p)
        if im.mode=='RGBA':
            _,parts=components(im.getchannel('A'))
            print(p.name,parts[:6])
