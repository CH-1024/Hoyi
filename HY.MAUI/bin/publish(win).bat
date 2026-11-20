@echo off
setlocal

rem 切换到 bat 所在目录的上一级（项目根目录）
pushd "%~dp0.." || (
    echo Failed to enter project directory.
    pause
    exit /b 1
)

rem 自动查找当前目录下的 .csproj
set "PROJ="
for %%f in (*.csproj) do (
    set "PROJ=%%f"
    goto :found
)
:found

if not defined PROJ (
    echo No .csproj file found in "%CD%".
    popd
    pause
    exit /b 1
)

echo.
echo 请选择发布模式:
echo   [1] 依赖框架发布  - 体积小，但目标机需安装 .NET 10 Desktop Runtime 和 Windows App SDK Runtime
echo   [2] 自包含发布    - 体积大，但目标机无需安装任何运行时（推荐）
echo.
choice /C 12 /N /D 2 /T 30 /M "请选择 [1/2]（30 秒内未选择默认 2）: "
set "MODE=%ERRORLEVEL%"

if "%MODE%"=="1" (
    set "MODE_NAME=依赖框架"
) else (
    set "MODE_NAME=自包含"
)

set "OUTDIR=bin\Hoyi"

echo.
echo Project : %PROJ%
echo Output  : %CD%\%OUTDIR%
echo 模式    : %MODE_NAME%
echo.

rem 发布前清空 bin\Hoyi
if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"

echo Publishing...

if "%MODE%"=="1" (
    dotnet publish "%PROJ%" ^
        -f net10.0-windows10.0.19041.0 ^
        -c Release ^
        -p:RuntimeIdentifierOverride=win10-x64 ^
        -p:WindowsPackageType=None ^
        -p:SelfContained=false ^
        -p:WindowsAppSDKSelfContained=false ^
        -o "%OUTDIR%"
) else (
    dotnet publish "%PROJ%" ^
        -f net10.0-windows10.0.19041.0 ^
        -c Release ^
        -p:RuntimeIdentifierOverride=win10-x64 ^
        -p:WindowsPackageType=None ^
        -p:SelfContained=true ^
        -p:WindowsAppSDKSelfContained=true ^
        -o "%OUTDIR%"
)

set "EXITCODE=%ERRORLEVEL%"
popd

if not "%EXITCODE%"=="0" (
    echo.
    echo Publish failed. Exit code: %EXITCODE%
) else (
    echo.
    echo Publish succeeded. Files are in: %OUTDIR%
)

pause
exit /b %EXITCODE%