@rem Ordinaryunus - (c) 2026 Yunus Emre Canoglu - GPL-3.0 + ek sartlar (EK-SARTLAR.md) - https://github.com/canogluy06-byte/Ordinaryunus
@echo off
rem Uygulamayi acar: yayin klasorundeki tek dosyalik Ordinaryunus.exe.
rem Ilk acilista exe, yakinindaki ornek-kasa klasorunu kasa olarak kullanir.
if exist "%~dp0yayin\Ordinaryunus.exe" goto ac
echo Uygulama bulunamadi: yayin\Ordinaryunus.exe
echo.
echo Hazir surumu kullaniyorsan zip dosyasinin tamamini ayni klasore cikar.
echo Kaynaktan calisiyorsan once derle. Depo kokunde, .NET 10 SDK ile:
echo   dotnet publish Ordinaryunus\Ordinaryunus.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o yayin
echo.
pause
exit /b 1

:ac
start "" "%~dp0yayin\Ordinaryunus.exe"
