from PIL import Image
from pathlib import Path
for p in Path(__file__).parent.glob('*source.png'):
    im=Image.open(p).convert('RGBA')
    print(p.name,im.size,'모서리',im.getpixel((0,0))[3])
    cols,rows=(1,1) if p.name=='cannon_source.png' else (3,2)
    for i in range(cols*rows):
        x=i%cols*im.width//cols;y=i//cols*im.height//rows
        a=im.crop((x,y,x+im.width//cols,y+im.height//rows)).getchannel('A')
        print(i,[a.point(lambda a:a if a>t else 0).getbbox() for t in (0,128,240)])
