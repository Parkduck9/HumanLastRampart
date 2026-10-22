@echo off
start "HLR watch" /min powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0watch.ps1"
start "" "%~dp0..\..\_HTML\live\board.html"
