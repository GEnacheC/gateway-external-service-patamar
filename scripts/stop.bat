@echo off
setlocal EnableExtensions
cd /d "%~dp0.."

echo.
echo ========================================
echo   Patamar Gateway - encerramento
echo ========================================
echo.

where docker >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Docker nao encontrado.
  pause
  exit /b 1
)

docker compose down
if errorlevel 1 (
  echo [ERRO] Falha ao parar os containers.
  pause
  exit /b 1
)

echo.
echo [OK] API e banco parados.
echo.
pause
endlocal
