import fs from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';

// 내장 리소스의 누락·변경과 스크립트 구문만 확인한다. 브라우저 화면 검사는 아니다.
export async function verify(root) {
  const html = await fs.readFile(path.join(root, '../../../_HTML/codex_K-020_map_markers_v1.html'), 'utf8');
  const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
  new vm.Script(script);
  const embedded = JSON.parse(script.match(/const sources=(.*?),manifest=/)[1]);
  const manifest = JSON.parse(await fs.readFile(path.join(root, 'manifest.json'), 'utf8'));
  const checks = [{ name: '내장 스크립트 구문', pass: true }];
  for (const a of manifest.assets) {
    const bytes = await fs.readFile(path.join(root, 'png', a.file));
    const encoded = Buffer.from(embedded[a.file].split(',')[1], 'base64');
    checks.push({ name: a.file + ' 내장 사본·크기 일치', pass: bytes.equals(encoded) && encoded.readUInt32BE(16) === a.width && encoded.readUInt32BE(20) === a.height });
  }
  checks.push({ name: '외부 스크립트·스타일·웹 요청 없음', pass: !/<script[^>]+src=|<link[^>]+href=|fetch\(|XMLHttpRequest|https?:\/\//i.test(html) });
  checks.push({ name: '이미지 12개 내장', pass: Object.keys(embedded).length === 12 });
  const result = { type: '정적 검사', browserVerified: false, checks };
  await fs.writeFile(path.join(root, 'review_verification.json'), JSON.stringify(result, null, 2) + '\n');
  if (checks.some(c => !c.pass)) throw new Error('검토 페이지 정적 검사 실패');
  return result;
}
