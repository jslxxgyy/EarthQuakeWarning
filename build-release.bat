@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
cd /d "%~dp0"

set "PROJECT=EarthquakeWaring.App\EarthquakeWaring.App.csproj"
set "OUT_ROOT=dist"                      REM 输出 zip 的目录
set "BUILD_ROOT=dist-build"              REM 临时发布目录（会重建）
set "ZIP_NAME_PREFIX=EarthquakeWaring.App"   REM zip 文件前缀（如需改名在此修改）
set "SELF_CONTAINED=false"               REM true=自包含(带运行时,体积大) false=框架依赖(需安装 .NET 10 桌面运行时)
set "SEVENZIP=C:\Program Files\7-Zip\7z.exe" 

set "VERSION="
for /f "tokens=2 delims=<> " %%i in ('findstr /c:"<PackageVersion>" "%PROJECT%"') do set "VERSION=%%i"
if "%VERSION%"=="" set "VERSION=0.0.0"
echo 版本: %VERSION%

if exist "%OUT_ROOT%" rd /s /q "%OUT_ROOT%"
if exist "%BUILD_ROOT%" rd /s /q "%BUILD_ROOT%"
mkdir "%OUT_ROOT%"

REM 逐个架构编译并打包
set "ARCHES=x86 x64 arm64"
for %%A in (%ARCHES%) do (
    echo.
    echo 正在编译 win-%%A ...

    call dotnet publish "%PROJECT%" -c Release -r win-%%A --self-contained %SELF_CONTAINED% -o "%BUILD_ROOT%\win-%%A"
    if errorlevel 1 (
        echo [错误] win-%%A 编译失败，跳过打包。
    ) else (
        set "ARCH_NAME=%%A"
        if "%%A"=="x86" set "ARCH_NAME=x86"
        if "%%A"=="x64" set "ARCH_NAME=x64"
        if "%%A"=="arm64" set "ARCH_NAME=arm64"

        set "ZIPNAME=%ZIP_NAME_PREFIX%_%VERSION%_!ARCH_NAME!.zip"
        echo 正在打包 !ZIPNAME! ...
        "%SEVENZIP%" a -tzip -mx=9 "%OUT_ROOT%\!ZIPNAME!" "%BUILD_ROOT%\win-%%A\*" >nul
        if errorlevel 1 (
            echo [错误] 打包 !ZIPNAME! 失败。
        ) else (
            echo [完成] 已生成 %OUT_ROOT%\!ZIPNAME!
        )
    )
)

if exist "%BUILD_ROOT%" rd /s /q "%BUILD_ROOT%"

echo.
echo 全部完成！产物位于：%OUT_ROOT%\
endlocal
exit /b 0
