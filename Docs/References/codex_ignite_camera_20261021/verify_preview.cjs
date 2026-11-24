const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert');
const html = fs.readFileSync(path.join(__dirname, 'preview.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const nodes = {};
for (const id of ['shot', 'frameLabel', 'play', 'previous', 'next', 'fourth', 'zoom', 'stage']) {
  nodes[id] = {attrs: {}, textContent: '', setAttribute(k, v) { this.attrs[k] = v; },
    classList: {full: false, toggle() { return this.full = !this.full; }} };
}
let interval = null;
const listeners = {};
const context = vm.createContext({document: {getElementById: id => nodes[id], addEventListener: (key, fn) => listeners[key] = fn},
  setInterval: fn => { interval = fn; return 1; }, clearInterval: () => interval = null});
vm.runInContext(script, context);
const checks = [];
function check(name, fn) { fn(); checks.push(name); }
check('initial frame 4 paused', () => assert.strictEqual(nodes.frameLabel.textContent, '4 / 6번 칸'));
check('next frame', () => {nodes.next.onclick(); assert.strictEqual(nodes.frameLabel.textContent, '5 / 6번 칸');});
check('previous frame', () => {nodes.previous.onclick(); assert.strictEqual(nodes.frameLabel.textContent, '4 / 6번 칸');});
check('play starts interval', () => {nodes.play.onclick(); assert.strictEqual(nodes.play.textContent, '정지'); assert(interval);});
check('play advances frame', () => {interval(); assert.strictEqual(nodes.frameLabel.textContent, '5 / 6번 칸');});
check('pause stops interval', () => {nodes.play.onclick(); assert.strictEqual(interval, null); assert.strictEqual(nodes.play.textContent, '재생');});
check('frame 4 button stops playback', () => {nodes.play.onclick(); nodes.fourth.onclick(); assert.strictEqual(interval, null); assert.strictEqual(nodes.frameLabel.textContent, '4 / 6번 칸');});
check('wrap 6 to 1', () => {nodes.next.onclick(); nodes.next.onclick(); nodes.next.onclick(); assert.strictEqual(nodes.frameLabel.textContent, '1 / 6번 칸');});
check('wrap 1 to 6', () => {nodes.previous.onclick(); assert.strictEqual(nodes.frameLabel.textContent, '6 / 6번 칸');});
check('zoom on', () => {nodes.zoom.onclick(); assert.strictEqual(nodes.zoom.attrs['aria-pressed'], 'true');});
check('zoom off', () => {nodes.zoom.onclick(); assert.strictEqual(nodes.zoom.attrs['aria-pressed'], 'false');});
check('keyboard next', () => {listeners.keydown({key: 'ArrowRight', preventDefault(){}}); assert.strictEqual(nodes.frameLabel.textContent, '1 / 6번 칸');});
check('keyboard previous', () => {listeners.keydown({key: 'ArrowLeft', preventDefault(){}}); assert.strictEqual(nodes.frameLabel.textContent, '6 / 6번 칸');});
const reportPath = path.join(__dirname, 'preview_validation.json');
const report = JSON.parse(fs.readFileSync(reportPath, 'utf8'));
report.node_vm_interaction_checks = {passed: checks.length, checks, limitation: 'Mock DOM script checks; not an actual browser layout or interaction test'};
fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf8');
console.log(`${checks.length}/${checks.length} Node VM script checks passed; actual browser verification remains separate.`);
