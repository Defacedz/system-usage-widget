@echo off
rem Relaunches the installed System Widget - after closing it by hand, for
rem instance. Not installed yet? It offers to run Installer.bat for you.

set "EXE=%ProgramFiles%\SystemWidget\SystemWidget.exe"
if not exist "%EXE%" (
    echo System Widget is not installed on this machine yet.
    choice /C YN /M "Install it now"
    if errorlevel 2 exit /b 1
    call "%~dp0Installer.bat"
    exit /b
)

rem Through its scheduled task when there is one: that is what grants the
rem administrator rights the CPU thermometer needs, with no UAC prompt.
rem Without the task ("Start with Windows" unticked) the widget starts
rem directly and only the CPU thermometer stays blank.
schtasks /Run /TN "SystemWidget" >nul 2>&1
if errorlevel 1 start "" "%EXE%"
