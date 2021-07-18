REM @echo off
setlocal 

call:vs%~1 2>nul
if "%n%" == "" (
    echo Visual studio is not supported.
    exit /b
)

set "vs_registry=HKLM\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\SxS\VS7"
set "reg_query_cmd='reg query "%vs_registry%" /v "%n%.0" 2^>nul'"

for /f "tokens=1,2*" %%a in (%reg_query_cmd%) do (set "VSPATH=%%c")
if "%VSPATH%" == "" (
    echo Visual studio %~1 is not installed on this machine
    exit /b
)

echo Visual studio %1 path is "%VSPATH%"
endlocal &(
	set "VSPATH=%VSPATH%"
	exit /b
	REM exit /b
)

:vs2019
	set /a "n=%n%+1"
:vs2017
    set /a "n=%n%+1"
:vs2015
    set /a "n=%n%+2"
:vs2013
    set /a "n=%n%+1"
:vs2012
    set /a "n=%n%+1"
:vs2010
    set /a "n=%n%+10"
    exit /b
	