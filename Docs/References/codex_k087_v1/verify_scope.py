from pathlib import Path
import struct, hashlib, json

root=Path(__file__).resolve().parents[3]
gitfile=(root/'.git').read_text().strip()
gitdir=Path(gitfile.split(': ',1)[1])
data=(gitdir/'index').read_bytes()
signature,version,count=struct.unpack('>4sII',data[:12])
assert signature==b'DIRC' and version in [2,3], '지원하지 않는 인덱스 형식'
offset=12; checked=0; mismatches=[]; eol_normalized=0

def blob_hash(b):
    return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).digest()

for _ in range(count):
    start=offset; expected=data[offset+40:offset+60]
    flags=struct.unpack('>H',data[offset+60:offset+62])[0]; offset+=62
    if flags&0x4000: offset+=2
    end=data.index(b'\0',offset); name=data[offset:end].decode('utf-8')
    offset=start+((end+1-start+7)//8)*8
    if not name.startswith('Game/'): continue
    checked+=1; file=root/name
    if not file.is_file(): mismatches.append(name); continue
    content=file.read_bytes()
    if blob_hash(content)!=expected:
        if b'\0' not in content and blob_hash(content.replace(b'\r\n',b'\n'))==expected: eol_normalized+=1
        else: mismatches.append(name)

report=dict(card='K-087',method='read-only Git index blob hash comparison; CRLF-to-LF fallback for text',index_version=version,game_files_checked=checked,eol_normalized_matches=eol_normalized,mismatches=mismatches,all_game_files_match_index=not mismatches,original_branch=(gitdir.parent.parent/'HEAD').read_text().strip(),card_branch=(gitdir/'HEAD').read_text().strip(),git_writes=False,unity_run=False)
out=root/'Docs/References/codex_k087_v1/scope_validation.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
if mismatches: raise SystemExit(1)
