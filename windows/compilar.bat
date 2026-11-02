@echo off
rem Compila RelojBackground.exe con el compilador de C# que ya trae Windows (.NET Framework 4).
rem reloj.ico es el icono del programa (y se incrusta tambien para la bandeja y las ventanas).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 ^
  /win32icon:reloj.ico /resource:reloj.ico,reloj.ico ^
  /out:RelojBackground.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll RelojBackground.cs Configuracion.cs
if errorlevel 1 (echo Error al compilar & pause & exit /b 1)
echo Listo: RelojBackground.exe