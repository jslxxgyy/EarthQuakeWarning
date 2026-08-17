@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
cd /d "%~dp0"
REM 项目根目录的绝对路径
set "BASE=%CD%"

REM 输出 zip 的目录
set "OUT_ROOT=dist"
REM 临时发布目录（会重建）
set "BUILD_ROOT=dist-build"
REM zip 文件前缀
set "ZIP_NAME_PREFIX=EarthquakeWaring.App"
REM true=自包含 false=框架依赖
set "SELF_CONTAINED=false"
REM 7z.exe 完整路径
set "SEVENZIP=C:\Program Files\7-Zip\7z.exe"
REM 项目文件
set "PROJECT=EarthquakeWaring.App\EarthquakeWaring.App.csproj"

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
        echo win-%%A 编译失败，跳过打包。
    ) else (
        set "ARCH_NAME=%%A"
        if "%%A"=="x86" set "ARCH_NAME=win_x86"
        if "%%A"=="x64" set "ARCH_NAME=win_x64"
        if "%%A"=="arm64" set "ARCH_NAME=win_arm64"

        set "ZIPNAME=%ZIP_NAME_PREFIX%_%VERSION%_!ARCH_NAME!.zip"
        echo 正在打包 !ZIPNAME! ...
        pushd "%BASE%\%BUILD_ROOT%\win-%%A"
        "%SEVENZIP%" a -tzip -mx=9 "%BASE%\%OUT_ROOT%\!ZIPNAME!" "*" >nul
        set "ZIPEXIT=%ERRORLEVEL%"
        popd
        if not "!ZIPEXIT!"=="0" (
            echo 打包 !ZIPNAME! 失败。
        ) else (
            echo 已生成 %OUT_ROOT%\!ZIPNAME!
        )
    )
)

if exist "%BUILD_ROOT%" rd /s /q "%BUILD_ROOT%"

echo.
echo 全部完成！
endlocal
exit /b 0
