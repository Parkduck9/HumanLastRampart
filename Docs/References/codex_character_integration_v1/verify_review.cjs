// Mock DOM smoke test only. This is not browser rendering verification.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const html = fs.readFileSync(path.join(__dirname, 'preview.html'), 'utf8');
const payload = html.match(/<script id="data" type="application\/json">([\s\S]*?)<\/script>/)[1];
const script = html.match(/<\/script><script>([\s\S]*?)<\/script>/)[1];
new vm.Script(script);
const nodes = new Map(), calls = [];
function element(tag = 'div') {
  return {tag, children: [], value: '', innerHTML: '', textContent: '',
    classList: {toggle() {}}, append(child) {this.children.push(child);},
    getContext() {return {imageSmoothingEnabled: false, clearRect() {}, drawImage(...args) {calls.push(args.slice(1));}};},
    querySelector(tag) {return this.children.find(x => x.tag === tag) ?? (this.children.push(element(tag)), this.children.at(-1));},
    scrollIntoView() {}};
}
const document = {
  getElementById(id) {if (!nodes.has(id)) nodes.set(id, element()); return nodes.get(id);},
  createElement: element,
  querySelectorAll() {return document.getElementById('grid').children;}
};
document.getElementById('data').textContent = payload;
class MockImage {set src(value) {this.value = value; queueMicrotask(() => this.onload());}}
const context = vm.createContext({document, Image: MockImage, Promise, Map});
vm.runInContext(script, context);
async function settle() {await new Promise(resolve => setImmediate(resolve));}
(async () => {
  await settle();
  const assert = (condition, message) => {if (!condition) throw new Error(message);};
  assert(document.getElementById('grid').children.length === 35, '35 gallery tiles');
  assert(document.getElementById('sheet').children.length === 35, '35 select options');
  document.getElementById('sheet').value = 34;
  document.getElementById('sheet').onchange(); await settle();
  assert(document.getElementById('frameLabel').textContent === '1 / 3', 'command frame count');
  document.getElementById('previous').onclick(); await settle();
  assert(document.getElementById('frameLabel').textContent === '3 / 3', 'previous wraps');
  document.getElementById('next').onclick(); await settle();
  assert(document.getElementById('frameLabel').textContent === '1 / 3', 'next wraps');
  document.getElementById('sheet').value = 33;
  document.getElementById('sheet').onchange(); await settle();
  document.getElementById('frame').value = 15;
  document.getElementById('frame').oninput(); await settle();
  assert(document.getElementById('frameLabel').textContent === '16 / 16', 'hero last frame');
  const last = calls.at(-1);
  assert(last[0] === 864 && last[1] === 1152 && last[2] === 288 && last[3] === 384, 'hero cell crop coordinates');
  const result = {type: 'mock_DOM_only_not_browser', syntax: true, tiles: 35, manual_frame_controls: true, hero_final_frame_crop: true};
  fs.writeFileSync(path.join(__dirname, 'review_smoke_test.json'), JSON.stringify(result, null, 2));
  console.log(result);
})().catch(error => {console.error(error); process.exitCode = 1;});
