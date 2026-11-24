"""Approved v8 frames -> fixed-grid asset packing. No redraw or source overwrite."""
from pathlib import Path
import hashlib
import json
from PIL import Image

HERE = Path(__file__).resolve().parent
REF = HERE.parent
META = REF / 'codex_hero_walk4_v8.json'
CW, CH = 256, 384
PX, PY = 128, 352
ROWS = ['back', 'left', 'front']

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    spec = json.loads(META.read_text(encoding='utf-8-sig'))
    paths = [REF / item['path'] for item in spec['sources']]
    before = {p.name: sha(p) for p in paths + [META]}
    sources = [Image.open(p).convert('RGBA') for p in paths]
    sheet = Image.new('RGBA', (CW * 4, CH * len(ROWS)))
    frames = []
    for row, direction in enumerate(ROWS):
        selected = sorted((f for f in spec['frames'] if f['direction'] == direction), key=lambda f: f['frame'])
        assert [f['frame'] for f in selected] == [0, 1, 2, 3]
        for col, f in enumerate(selected):
            source = sources[f['sourceIndex']]
            x, y, w, h = (f[k] for k in ('x', 'y', 'w', 'h'))
            assert x >= 0 and y >= 0 and x + w <= source.width and y + h <= source.height
            patch = source.crop((x, y, x + w, y + h))
            pivot_x = f['pivotX']
            if f['flipX']:
                patch = patch.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                pivot_x = w - pivot_x
            # Preserve the selected preview's source-normalization scale; do not fit each frame separately.
            scale = f['unitScale']
            width, height = round(w * scale), round(h * scale)
            patch = patch.resize((width, height), Image.Resampling.NEAREST)
            scaled_pivot = (round(pivot_x * width / w), round(f['pivotY'] * height / h))
            dx, dy = PX - scaled_pivot[0], PY - scaled_pivot[1]
            assert 0 <= dx and 0 <= dy and dx + width <= CW and dy + height <= CH
            cell = Image.new('RGBA', (CW, CH))
            cell.paste(patch, (dx, dy))  # exact RGBA copy, no alpha applied twice
            bbox = cell.getchannel('A').getbbox()
            assert bbox and bbox[0] > 0 and bbox[1] > 0 and bbox[2] < CW and bbox[3] < CH
            sheet.paste(cell, (col * CW, row * CH))
            frames.append(dict(direction=direction, frame=col, index=col + row * 4,
                               x=col * CW, y=row * CH, w=CW, h=CH,
                               pivotX=PX, pivotY=PY, sourceIndex=f['sourceIndex'],
                               sourceRect=[x, y, w, h], sourceUnitScale=scale,
                               packedRect=[dx, dy, width, height], alphaBBox=list(bbox)))
    target = HERE / 'hero_walk_grid.png'
    sheet.save(target)
    # Verify exported file exactly equals packed data, each cell has content, source hashes unchanged.
    assert Image.open(target).convert('RGBA').tobytes() == sheet.tobytes()
    assert before == {p.name: sha(p) for p in paths + [META]}
    output = dict(image=target.name, size=list(sheet.size), columns=4, rows=3,
                  cell=[CW, CH], footPivotTopLeft=[PX, PY],
                  unityPivotBottomLeft=[PX / CW, (CH - PY) / CH], aspect=CW / CH,
                  rowOrder=ROWS, framesPerAction=4, defaultFps=spec['defaultFps'],
                  playbackSequence=spec['playbackSequence'],
                  alternatePlaybackSequence=spec['alternatePlaybackSequence'],
                  rightFacing=dict(row=1, flipX=True, phaseColumns=[2, 3, 0, 1]),
                  runAvailable=False, runRow=None, frames=frames,
                  sourceSha256=before, sheetSha256=sha(target))
    (HERE / 'hero_walk_grid.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    qa = dict(status='PASS', frameCount=len(frames), dimensions=list(sheet.size),
              transparentBackground=True, allFramesInsideCell=True,
              commonFootPivot=[PX, PY], sourceFilesUnchanged=True,
              exactExportMatch=True, nearestNeighborOnly=True,
              nativeUnityPlayVerified=False)
    (HERE / 'qa.json').write_text(json.dumps(qa, indent=2), encoding='utf-8')
    print('PASS 12 frames: fixed cells, shared foot pivot, no clipping, unchanged sources, exact export')

if __name__ == '__main__':
    main()
