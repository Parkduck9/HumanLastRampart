from pathlib import Path
from PIL import Image, ImageChops
import hashlib, json

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
M=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
NN=Image.Resampling.NEAREST

def components(im):
    points={(x,y) for y in range(im.height) for x in range(im.width) if im.getpixel((x,y))}
    sizes=[]
    while points:
        q=[points.pop()]; count=0
        while q:
            x,y=q.pop();count+=1
            for dx in [-1,0,1]:
                for dy in [-1,0,1]:
                    p=(x+dx,y+dy)
                    if p in points:
                        points.remove(p);q.append(p)
        sizes.append(count)
    return sorted(sizes)

def main():
    bg=Image.open(OUT/'prep_yard.png').convert('RGBA')
    assert bg.size==(3840,1080)
    assert bg.getchannel('A').getextrema()==(255,255)
    assert ImageChops.difference(bg,Image.open(OUT/'native/prep_yard.png').convert('RGBA').resize(bg.size,NN)).getbbox() is None
    mask=Image.open(OUT/'walkable_mask.png')
    assert mask.mode=='L' and mask.size==bg.size and set(mask.tobytes())=={0,255}
    assert mask.getpixel((2010,480))==255 and mask.getpixel((2010,1020))==255
    assert mask.getpixel((2010,477))==0 and mask.getpixel((2010,1023))==0
    native_mask=Image.open(OUT/'native/walkable_mask.png')
    mask_components=components(native_mask)
    assert len(mask_components)==1,mask_components
    layers=[]
    for o in M['occluders']:
        im=Image.open(OUT/o['file']).convert('RGBA'); native=Image.open(OUT/o['native_file'])
        assert im.size==bg.size and native.size==(1280,360)
        assert set(im.getchannel('A').tobytes())=={0,255}
        assert ImageChops.difference(im,native.resize(bg.size,NN)).getbbox() is None
        # 밝기 차가 있는 모든 점이 투명 영역 밖으로 나오지 않아야 한다.
        diff=ImageChops.difference(im.convert('RGB'),bg.convert('RGB'))
        for channel in diff.split():
            assert ImageChops.multiply(channel,im.getchannel('A')).getbbox() is None
        sizes=components(native.getchannel('A'))
        assert min(sizes)>2,sizes
        layers.append({'file':o['file'],'size':list(im.size),'alpha_values':[0,255],'native_connected_component_sizes':sizes,'background_pixels_match':True})
        preview=Image.open(OUT/f"previews/{o['id']}_4x.png")
        assert preview.size==(5120,1440)
        assert ImageChops.difference(preview,native.resize(preview.size,NN)).getbbox() is None
    hero=Image.open(OUT/'native/hero_reference_96x128.png')
    assert hero.size==(96,128) and hero.getbbox()[3]==120 and hero.getbbox()[3]-hero.getbbox()[1]==73
    placements=[]
    for p in M['mockup']['placements']:
        x,y=p['foot_px'];assert mask.getpixel((x,y))==255
        q=1+p['depth_m']/8
        assert abs((-60+1080/q)-y)<.001
        assert abs(3/q-p['native_hero_scale'])<.00001
        w,h=p['cell_px']; scaled=hero.resize((w,h),NN)
        actual_foot=y-p['cell_pivot_px'][1]+scaled.getbbox()[3]
        assert abs(actual_foot-y)<=1
        placements.append({'station':p['station_id'],'target_foot_row':y,'actual_opaque_bottom_edge':actual_foot,'on_walkable_ground':True})
    station_checks=[]
    for st in M['stations']:
        x,y,w,h=st['trigger_rect_px']
        assert mask.crop((x,y,x+w,y+h)).getextrema()==(255,255)
        assert mask.getpixel(tuple(st['interaction_px']))==255
        sx,sy,ex,ey=st['approach_lane_px']
        assert all(mask.getpixel((sx,yy))==255 for yy in range(sy,ey+1))
        station_checks.append(st['id'])
    assert mask.getpixel((760,930))==255
    for fn in ['prep_yard','walkable_mask','mockup_stations']:
        im=Image.open(OUT/f'native/{fn}.png')
        preview=Image.open(OUT/f'previews/{fn}_4x.png')
        assert preview.size==(5120,1440)
        assert ImageChops.difference(preview,im.resize(preview.size,NN)).getbbox() is None
    sources={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==h for p,h in M['sources_sha256'].items()}
    assert all(sources.values())
    report={'passed':True,'background_size':[3840,1080],'native_size':[1280,360],'nearest_3x_exact':True,'mask_binary':True,'mask_connected_components':len(mask_components),'near_far_endpoint_rows':[1020,480],'depth_m':8,'hero_near_far_heights':[219,109.5],'occluders':layers,'hero_native_foot_boundary':120,'mockup_placements':placements,'station_triggers_and_lanes_checked':station_checks,'preview_native_4x_exact':True,'sources_unchanged':sources,'unity_run':False}
    (OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=True))

if __name__=='__main__': main()
