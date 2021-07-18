rem @echo off

setlocal EnableDelayedExpansion

set "vs_folder=Common7\IDE"
set "versions[0]=2019"
set "versions[1]=2017"
set "versions[2]=2015"
set "versions[3]=2013"
set "versions[4]=2012"
set "versions[5]=2010"
set "i=-1"

:loop
set /a "i+=1"
if defined versions[%i%] (
	call echo checking for version %%versions[%i%]%%
	call vs_qversion "!versions[%i%]!"
	goto :checkResult
)

goto :fail

:checkResult
if not "%VSPATH%"=="" (
	goto :success
)
goto :loop

:success
set "vs_folder=%VSPATH%%vs_folder%"
echo SUCCESS: set '%vs_folder%' into environment variable "PATH"
goto :end

:fail
echo FAILED: 'vs_folder=%devenv_exec%'
goto :end


:end
endlocal & (
	set "PATH=%vs_folder%"
	exit /b
)