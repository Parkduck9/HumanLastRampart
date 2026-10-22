# 유니티 시험 도구

## 시험 한 줄 결과
```
powershell -File Tools\Test\run-tests.ps1 -Project <작업폴더>\Game -Mode EditMode -Filter "<테스트 이름 일부>"
powershell -File Tools\Test\run-tests.ps1 -Project <작업폴더>\Game -Mode EditMode -Assemblies "HLR.Combat.Tests"
```
- `-Mode` EditMode / PlayMode / All(기본), `-Filter` 테스트 이름, `-Assemblies` 어셈블리 이름, `-OutDir` 결과 위치, `-NoGraphics` 그래픽 끄기(기본은 켬).
- 출력은 모드별 한 줄 요약, 실패 이름 최대 10개(첫 줄 메시지), xml/log 경로뿐이다. xml/log를 통째로 읽지 말 것.
- 결과 xml이 없으면 로그에서 error/Exception 줄 15개만 보여 준다(컴파일 오류).
- 같은 프로젝트를 다른 유니티가 열고 있으면 "잠김" 한 줄을 내고 종료(코드 2). 유니티를 강제로 끄지 말 것.
- 종료 코드: 0 실패 없음, 1 실패 있음.

## 작업 폴더 만들기
```
powershell -File Tools\Test\new-worktree.ps1 -Name W17 -Branch feature/w17-xxx
```
`C:\Unity\1\HLR-work\<Name>`을 develop에서 만들고, 메인 Library를 복사한다(ScriptAssemblies, Bee, ScriptMapper, MonoManager.asset, 잠금 파일 제외).

## 규칙
**코딩 중에는 `-Filter`/`-Assemblies`로 관련 시험만 돌린다. 전체 시험은 통합 시점에 조정 담당이 한 번만 돌린다.**