@echo off
rem Builds dwx.exe, the update-check/update CLI, with the in-box .NET
rem Framework compiler. No SDK, no NuGet - same approach as ..\build.cmd.

setlocal
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set FW=C:\Windows\Microsoft.NET\Framework\v4.0.30319

if not exist "%~dp0..\bin" mkdir "%~dp0..\bin"

rem /noconfig: see ..\build.cmd for why csc.rsp must be skipped.
"%CSC%" /nologo /noconfig /target:exe /platform:x86 /optimize+ /warn:4 ^
 /out:"%~dp0..\bin\dwx.exe" ^
 /reference:"%FW%\mscorlib.dll" ^
 /reference:"%FW%\System.dll" ^
 "%~dp0Dwx.cs"

if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK
exit /b 0
