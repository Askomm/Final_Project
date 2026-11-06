@echo off
setlocal
cd /d "%~dp0"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (echo Install Visual Studio C++ build tools. & exit /b 1)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSROOT=%%i"
if not defined VSROOT (echo C++ tools not found. & exit /b 1)
call "%VSROOT%\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 exit /b 1
if not exist "..\runtimes\win-x64\native" mkdir "..\runtimes\win-x64\native"
if not exist "build" mkdir "build"
cl /nologo /W4 /WX /O2 /MT /LD rental_native.c /Fobuild\rental_native.obj /link /OUT:..\runtimes\win-x64\native\rental_native.dll /IMPLIB:build\rental_native.lib
exit /b %errorlevel%
