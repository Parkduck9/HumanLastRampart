' HLR 작업 현황을 터미널 창 없이 켠다 (사용자 2026-10-22: start.cmd는 검은 창이 잠깐 뜸)
Set sh = CreateObject("WScript.Shell")
dir = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
sh.Run "powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & dir & "\tray.ps1""", 0, False
