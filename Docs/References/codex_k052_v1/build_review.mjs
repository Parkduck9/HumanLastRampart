import fs from 'node:fs/promises';
import path from 'node:path';
export async function build(root) {
 const dir=path.join(root,'Docs/References/codex_k052_v1');
 const json=async n=>JSON.parse((await fs.readFile(path.join(dir,n),'utf8')).replace(/^\uFEFF/,''));
 const data={palette:await json('selected_palette.json'),used:await json('used_palette.json'),walk:await json('walk_measurements.json'),ignite:await json('ignite_measurements.json'),outlines:await json('outline_measurements.json'),images:{}};
 const paths={cannon:'cannon_body.png',chassis:'cannon_chassis.png',wheel:'cannon_wheel.png',pull:'crew_pull6.png',push:'crew_push6.png',ignite:'crew_ignite6.png',aim:'../../../Game/Assets/_Project/Art/Battle/Resources/BattleArt/cannon_aim3.png',crew:'../../../Game/Assets/_Project/Art/Characters/Static/cannon_crew_static.png',oldCannon:'../codex_k048_v1/cannon_body.png',oldPull:'../codex_k048_v1/crew_pull6.png',oldPush:'../codex_k048_v1/crew_push6.png',oldContact:'../codex_k048_v1/contact_review.png'};
 paths.run='../../../Game/Assets/_Project/Art/Characters/Run/cannon_crew_run.png';
 for(const [key,file] of Object.entries(paths))data.images[key]='data:image/png;base64,'+(await fs.readFile(path.join(dir,file))).toString('base64');
 data.used.crew=[...new Set([...data.used.pull,...data.used.push,...data.used.ignite])];
 const template=await fs.readFile(path.join(dir,'review_template.html'),'utf8');
 const html=template.replace('__DATA__',JSON.stringify(data));
 const out=path.join(root,'_HTML/4_그림_캐릭터/codex_K-052_대포끌기_3차_v1.html');
 await fs.mkdir(path.dirname(out),{recursive:true});await fs.writeFile(out,html);
 return {file:out,images:Object.keys(data.images).length,bytes:Buffer.byteLength(html)};
}
