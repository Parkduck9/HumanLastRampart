@echo off
rem Build HlrBoard.exe with the built-in .NET Framework compiler (nothing to download)
setlocal
set "HERE=%~dp0"
set "FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=C:\Windows\Microsoft.NET\Framework\v4.0.30319"
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%makeico.ps1" -Out "%HERE%HlrBoard.ico"
"%FW%\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu /out:"%HERE%..\HlrBoard.exe" /win32icon:"%HERE%HlrBoard.ico" /win32manifest:"%HERE%HlrBoard.manifest" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "%HERE%HlrBoard.cs"
if errorlevel 1 (echo BUILD FAILED& exit /b 1)
echo OK: %HERE%..\HlrBoard.exe