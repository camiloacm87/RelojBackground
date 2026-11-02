@echo off
rem Compila RelojBackground.exe y su instalador RelojBackground-Setup.exe con el compilador de C#
rem que ya trae Windows (.NET Framework 4). reloj.ico es el icono del programa.
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set REFS=/r:System.Windows.Forms.dll /r:System.Drawing.dll

%CSC% /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:reloj.ico /resource:reloj.ico,reloj.ico ^
  /out:RelojBackground.exe %REFS% RelojBackground.cs Configuracion.cs
if errorlevel 1 (echo Error al compilar la app & pause & exit /b 1)

rem El instalador lleva la app dentro (recurso RelojBackground.exe).
%CSC% /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:reloj.ico /resource:reloj.ico,reloj.ico ^
  /resource:RelojBackground.exe,RelojBackground.exe /main:Instalador ^
  /out:RelojBackground-Setup.exe %REFS% RelojBackground.cs Configuracion.cs Instalador.cs
if errorlevel 1 (echo Error al compilar el instalador & pause & exit /b 1)

echo Listo: RelojBackground.exe y RelojBackground-Setup.exe