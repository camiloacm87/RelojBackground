@echo off
rem Compila RelojBackground.exe y su instalador RelojBackground-Setup.exe con el compilador de C#
rem que ya trae Windows (.NET Framework 4). reloj.ico es el icono del programa.
rem El reconocimiento de imágenes usa el OCR de Windows 10/11 (archivos .winmd de System32\WinMetadata).
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WM=%WINDIR%\System32\WinMetadata
set CSC="%FW%\csc.exe"
set REFS=/r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
  /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.WindowsRuntime.dll" ^
  /r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Media.winmd" /r:"%WM%\Windows.Graphics.winmd" ^
  /r:"%WM%\Windows.Storage.winmd" /r:"%WM%\Windows.Globalization.winmd" /r:"%WM%\Windows.Security.winmd"
set FUENTES=RelojBackground.cs Configuracion.cs LectorImagen.cs

%CSC% /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:reloj.ico /resource:reloj.ico,reloj.ico ^
  /out:RelojBackground.exe %REFS% %FUENTES%
if errorlevel 1 (echo Error al compilar la app & pause & exit /b 1)

rem El instalador lleva la app dentro (recurso RelojBackground.exe).
%CSC% /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:reloj.ico /resource:reloj.ico,reloj.ico ^
  /resource:RelojBackground.exe,RelojBackground.exe /main:Instalador ^
  /out:RelojBackground-Setup.exe %REFS% %FUENTES% Instalador.cs
if errorlevel 1 (echo Error al compilar el instalador & pause & exit /b 1)

echo Listo: RelojBackground.exe y RelojBackground-Setup.exe