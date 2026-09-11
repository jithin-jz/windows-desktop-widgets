@echo off
rem Builds DesktopWidgets.exe with the in-box .NET Framework compiler.
rem No SDK or NuGet packages required - every reference below ships with Windows.

setlocal
rem 32-bit build: measured 12 MB lower private bytes than x64 for identical
rem behaviour, because pointers and the NGEN images are half the size.
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set GAC=C:\Windows\Microsoft.NET\assembly
set FW=C:\Windows\Microsoft.NET\Framework\v4.0.30319

rem WinRT: reference the individual winmd files in System32. The unified
rem Windows.WinMD facade under Windows Kits is too old on this machine to contain
rem Windows.Media.Control, and System.Runtime.WindowsRuntime is deliberately not
rem referenced because its await/stream extensions bind to that unified identity.
rem WinRtAsync.cs covers what those extensions would have provided.
set WINMD=C:\Windows\System32\WinMetadata

if not exist "%~dp0bin" mkdir "%~dp0bin"

rem /noconfig is required, not an optimisation.
rem
rem Without it the compiler applies csc.rsp, which auto-references about forty
rem legacy Framework assemblies. Since the February 2026 servicing update at
rem least one of those drags in the .NET Standard shim
rem System.ComponentModel.Primitives.dll, and that assembly and System.dll now
rem hold type forwarders pointing at each other. The compiler rejects the cycle
rem (CS0731) and then cannot resolve ISupportInitialize, which every WPF Window
rem implements - so the build fails on the first line that mentions a Window.
rem Referencing the shim explicitly does not help; it is one half of the cycle.
rem
rem /noconfig skips csc.rsp, so the curated list below is the complete reference
rem set. Anything added here must be a real assembly, never one of the ~106
rem small netstandard shims sitting in the Framework directory.
"%CSC%" /nologo /noconfig /target:winexe /platform:x86 /optimize+ /warn:4 ^
 /out:"%~dp0bin\DesktopWidgets.exe" ^
 /reference:"%FW%\mscorlib.dll" ^
 /reference:"%FW%\System.dll" ^
 /reference:"%FW%\System.Core.dll" ^
 /reference:"%FW%\System.Xml.dll" ^
 /reference:"%FW%\System.Web.Extensions.dll" ^
 /reference:"%GAC%\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll" ^
 /reference:"%GAC%\GAC_32\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll" ^
 /reference:"%GAC%\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" ^
 /reference:"%GAC%\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll" ^
 /reference:"%GAC%\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll" ^
 /reference:"%WINMD%\Windows.Foundation.winmd" ^
 /reference:"%WINMD%\Windows.Media.winmd" ^
 /reference:"%WINMD%\Windows.Storage.winmd" ^
 "%~dp0src\*.cs"

if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK
exit /b 0
