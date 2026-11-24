// 브라우저가 없는 환경에서 실제 페이지 스크립트를 최소 DOM 모형으로 실행합니다.
// 글꼴 배치·CSS 가림·실제 키보드 포커스 검증을 대체하지 않습니다.
export async function verify(fs, vm, directory) {
  const source = await fs.readFile(directory + '/preview.html', 'utf8');
  const results = [];
  function check(name, condition) { results.push({ 항목: name, 통과: Boolean(condition) }); }
  class Element {
    constructor(tag = 'div') { this.tag = tag; this.children = []; this.attributes = {}; this.style = {}; this.dataset = {}; this.hidden = false; this.textContent = ''; this.clientWidth = 1440; }
    append(...items) { this.children.push(...items); for (const item of items) if (typeof item === 'object') item.parent = this; }
    replaceChildren(...items) { this.children = []; this.append(...items); }
    setAttribute(name, value) { this.attributes[name] = String(value); }
    getAttribute(name) { return this.attributes[name]; }
    remove() { this.parent.children = this.parent.children.filter(item => item !== this); }
    closest() { return null; }
    getBoundingClientRect() { return { left: 0, top: 0, width: this.clientWidth, height: this.clientWidth * 810 / 1440 }; }
  }
  const ids = Object.fromEntries([...source.matchAll(/\bid="([^"]+)"/g)].map(m => [m[1], new Element()]));
  const fold = ['commandBody', 'cannonBody'].map(id => { const e = new Element('button'); e.dataset.fold = id; return e; });
  const spells = [0, 1].map(i => { const e = new Element('button'); e.dataset.spell = String(i); return e; });
  const all = () => [...Object.values(ids), ...fold, ...spells, ...Object.values(ids).flatMap(e => e.children)];
  const document = {
    getElementById: id => ids[id] || all().find(e => e.id === id),
    createElement: tag => new Element(tag), createTextNode: text => ({ textContent: text }),
    querySelectorAll: selector => selector === '[data-spell]' ? spells : selector === '[data-fold]' ? fold : selector.includes('data-fold') ? fold.filter(e => e.getAttribute('aria-expanded') === 'true') : [],
    querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
  };
  const handlers = {};
  const context = vm.createContext({ document, window: { addEventListener: (name, f) => handlers[name] = f } });
  const script = source.match(/<script>([\s\S]*?)<\/script>/)[1];
  vm.runInContext(script, context);
  const run = code => vm.runInContext(code, context);
  check('초기 성벽 선택지 5개', ids.targets.children.length === 5);
  check('정확한 명령 6개', ids.commands.children.length === 6 && run('priorities.join("|")') === '체력 낮은 적부터|체력 높은 적부터|가까운 적부터|오르는 적부터|특수 적 먼저|밀집한 곳 쏘기');
  check('초기 네 성벽 가까운 적 우선', run('state.orders.every(n=>n===2)'));
  ids.targets.children[2].onclick(); ids.commands.children[4].onclick();
  check('동쪽만 특수 적 우선', run('state.orders.join()') === '2,4,2,2');
  ids.targets.children[0].onclick();
  check('전체 명령 혼합일 때 단일 활성 표시 없음', ids.commands.children.every(e => e.getAttribute('aria-pressed') === 'false'));
  ids.commands.children[1].onclick(); check('전체 명령 개별 명령 덮어쓰기', run('state.orders.every(n=>n===1)'));
  fold[0].onclick(); check('왼쪽 창 접기 및 접근성 상태', ids.commandBody.hidden && fold[0].getAttribute('aria-expanded') === 'false');
  fold[0].onclick(); check('펼칠 때 명령 보존', !ids.commandBody.hidden && run('state.orders.every(n=>n===1)'));
  ids.allFold.onclick(); check('두 창 함께 접기', ids.commandBody.hidden && ids.cannonBody.hidden);
  ids.allFold.onclick(); check('두 창 함께 펼치기', !ids.commandBody.hidden && !ids.cannonBody.hidden);
  ids.cannons.children[0].onclick(); run('place(720,240)');
  check('북쪽 1번 대포 조준 저장', run('state.aims["0-0"].join()') === '720,240');
  ids.cannonSides.children[1].onclick(); ids.cannons.children[0].onclick(); run('place(900,400)');
  check('동쪽 대포 독립 조준', run('Object.keys(state.aims).length') === 2);
  ids.clearAim.onclick(); check('선택한 대포 조준만 해제', run('state.aims["0-0"]!==undefined && state.aims["1-0"]===undefined'));
  spells[0].onclick(); check('마법 선택 시 대포 해제', run('state.spell===0 && state.cannon===null'));
  run('place(800,200)'); check('마법 위치 표식 생성', document.getElementById('marker-spell').className.includes('spell'));
  spells[0].onclick(); check('같은 마법 다시 누르면 해제', run('state.spell===null'));
  const key = value => handlers.keydown({ key: value, target: new Element(), preventDefault() {} });
  key('Q'); check('Q 첫째 마법', run('state.spell===0'));
  key('w'); check('W 둘째 마법', run('state.spell===1'));
  key('Escape'); check('Esc 선택 취소', run('state.spell===null && state.cannon===null'));
  ids.combat.onclick(); check('전투 중 문구 비교', run('state.combat') && ids.start.textContent === '지도 닫기');
  ids.start.onclick(); check('전투 중 지도 닫기 실제 전환 없음 표시', ids.toast.textContent.includes('실제 화면 전환 없음'));
  ids.reset.onclick(); check('초기화 시 조준·접기·전투 상태 초기화', run('!state.combat && Object.keys(state.aims).length===0') && ids.markers.children.length === 0 && !ids.commandBody.hidden);
  ids.mapAction.onkeydown({ key: 'Enter', preventDefault() {} }); check('선택 없이 지도 누르면 안내', ids.toast.textContent.includes('먼저 대포'));
  ids.cannons.children[2].onclick(); ids.mapAction.onkeydown({ key: 'Enter', preventDefault() {} });
  check('키보드 지도 위치 미리보기', run('state.aims["0-2"].join()') === '720,260');
  ids.viewport.clientWidth = 720; handlers.resize(); check('720폭에서 0.5배 및 높이 조정', ids.stage.style.transform === 'scale(0.5)' && ids.viewport.style.height === '405px');
  const copy = await fs.readFile(directory + '/../../../_HTML/codex_K-021_지도준비창_v1.html', 'utf8');
  check('검토 HTML과 보관 HTML 동일', source === copy);
  check('외부 리소스 요청 없음', !/(?:src|href)=["'](?:https?:|\/\/)/i.test(source) && !/fetch\(|XMLHttpRequest|localStorage/.test(script));
  check('치환용 표식 없음', !/__MAP__|__GRASS__|__STONE__/.test(source));
  const idsList = [...source.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);
  check('중복 식별자 없음', idsList.length === new Set(idsList).size);
  const report = { 검사방법: '실제 HTML 스크립트 + 최소 DOM 모형. 실제 브라우저·CSS 배치 검증 아님.', 통과: results.filter(r => r.통과).length, 전체: results.length, 결과: results };
  await fs.writeFile(directory + '/qa_results.json', JSON.stringify(report, null, 2) + '\n');
  return report;
}
