@echo off
setlocal

cd /d "%~dp0"
set "PATH=C:\Program Files\Docker\Docker\resources\bin;%PATH%"

set "DOCKER_EXE=docker"
where docker >nul 2>&1
if errorlevel 1 (
	if exist "C:\Program Files\Docker\Docker\resources\bin\docker.exe" (
		set "DOCKER_EXE=C:\Program Files\Docker\Docker\resources\bin\docker.exe"
	) else (
		echo Docker не е намерен.
		echo Инсталирай Docker Desktop и опитай отново.
		exit /b 1
	)
)

echo Stopping EcoProject containers...
"%DOCKER_EXE%" compose down

endlocal