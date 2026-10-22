const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert');
const out = __dirname;
const root = path.resolve(out, '../../..');
const page = path.join(root, '_HTML/4_그림_캐릭터/codex_K-063_아군6판방향_v1.html');
const html = fs.readFileSync(page, 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements = {};
function element(id) {
  return elements[id] ||= {style: {setProperty(k, v) {this[k] = v;}}, addEventListener(type, callback) {this[type] = callback;}};
}
const document = {body: {className: ''}, getElementById: element};
vm.runInNewContext(script, {document, Number});
element('bg').change({target: {value: 'light'}});
assert.equal(document.body.className, 'light');
element('bg').change({target: {value: 'dark'}});
assert.equal(document.body.className, 'dark');
for (const h of [540, 1080]) {
  element('screen').change({target: {value: String(h)}});
  const s = element('stage').style;
  assert.equal(s.height, h + 'px');
  assert.equal(parseFloat(s.width), h * 16 / 9);
  const tile = parseFloat(s['--tile']);
  assert(Math.abs(tile * 96 / 112 / h - .13) < 1e-10);
}
assert(!/(?:src|href)="https?:\/\//.test(html));
const images = [...html.matchAll(/<img[^>]+src="data:image\/png;base64,([^"]+)"/g)];
assert(images.length >= 40);
for (const image of images) {
  const bytes = Buffer.from(image[1], 'base64');
  assert.equal(bytes.subarray(1, 4).toString(), 'PNG');
  assert.equal(bytes.readUInt32BE(16), 56);
  assert.equal(bytes.readUInt32BE(20), 112);
}
const report = {내장이미지수: images.length, 내장이미지규격: true, 외부의존없음: true, 전환스크립트구문: true, 배경전환로직: true, 화면높이540과1080의인물13퍼센트계산: true, 실제브라우저검증: false, 사유: '브라우저 연결 도구의 사용 가능한 브라우저 목록이 비어 있음'};
fs.writeFileSync(path.join(out, 'HTML_검사결과.json'), JSON.stringify(report, null, 2), 'utf8');
console.log(JSON.stringify(report));
