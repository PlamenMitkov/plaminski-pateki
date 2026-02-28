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

"%DOCKER_EXE%" info >nul 2>&1
if errorlevel 1 (
	echo Docker е инсталиран, но Docker Desktop не е стартиран.
	echo Стартирай Docker Desktop и после пусни start.bat отново.
	exit /b 1
)

set "DOCKER_CONFIG=%TEMP%\ecoproject-docker-config"
if not exist "%DOCKER_CONFIG%" mkdir "%DOCKER_CONFIG%"
if not exist "%DOCKER_CONFIG%\config.json" (
	echo {"auths":{}} > "%DOCKER_CONFIG%\config.json"
)

echo Starting EcoProject (API + Client + DB) with Docker Compose...
"%DOCKER_EXE%" compose up --build

endlocal