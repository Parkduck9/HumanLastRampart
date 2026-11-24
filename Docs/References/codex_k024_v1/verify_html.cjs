const fs = require('node:fs');
const path = require('node:path');
const htmlPath = path.resolve(__dirname, '../../../_HTML/codex_K-024_성벽손상_전장흔적_v1.html');
const html = fs.readFileSync(htmlPath, 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
new Function(script);
const data = new Function(script.split('const loaded=')[0] + 'return {manifest,images,sources};')();
let count = 0;
for (const asset of data.manifest.assets) {
  for (const stage of asset.stages) {
    const buffer = Buffer.from(data.images[stage.path].split(',')[1], 'base64');
    if (buffer.readUInt32BE(16) !== stage.width || buffer.readUInt32BE(20) !== stage.height) throw Error('내장 PNG 크기 불일치');
    if (!buffer.equals(fs.readFileSync(path.join(__dirname, stage.path)))) throw Error('내장 PNG와 파일 불일치');
    count++;
  }
}
if (count !== 14 || data.sources.length !== 3) throw Error('그림 수 불일치');
fs.writeFileSync(path.join(__dirname, 'html_qa.json'), JSON.stringify({syntax:true,embeddedPngMatches:count,embeddedSources:3,browser:'실제 브라우저 조작 미검증'}, null, 2));
console.log('HTML 구문 및 내장 PNG 14장 파일 일치 확인 통과');
