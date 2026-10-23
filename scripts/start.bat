@echo off
setlocal EnableExtensions
cd /d "%~dp0.."

echo.
echo ========================================
echo   Patamar Gateway - inicializacao
echo ========================================
echo.

where docker >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Docker nao encontrado.
  echo.
  echo Instale o Docker Desktop:
  echo   https://www.docker.com/products/docker-desktop/
  echo Depois reinicie o computador e execute este arquivo de novo.
  echo.
  pause
  exit /b 1
)

docker info >nul 2>&1
if errorlevel 1 (
  echo [ERRO] Docker instalado, mas nao esta em execucao.
  echo.
  echo Abra o Docker Desktop e espere ficar "Running".
  echo Depois execute este arquivo de novo.
  echo.
  pause
  exit /b 1
)

if not exist ".env" (
  if exist ".env.example" (
    copy /Y ".env.example" ".env" >nul
    echo [OK] Arquivo .env criado a partir de .env.example
  ) else (
    echo [AVISO] .env.example nao encontrado. Continuando sem .env...
  )
) else (
  echo [OK] Arquivo .env ja existe
)

echo.
echo Baixando imagens e subindo a API + banco...
echo Isso pode demorar alguns minutos na primeira vez.
echo.

docker compose up --build -d
if errorlevel 1 (
  echo.
  echo [ERRO] Falha ao subir os containers.
  echo Verifique se a porta 8080 esta livre e tente de novo.
  echo.
  pause
  exit /b 1
)

echo.
echo Aguardando a API ficar pronta...
set /a tries=0

:wait_health
set /a tries+=1
curl.exe -sf http://localhost:8080/health >nul 2>&1
if not errorlevel 1 goto ready
if %tries% GEQ 60 goto timeout
timeout /t 2 /nobreak >nul
goto wait_health

:timeout
echo.
echo [AVISO] Containers subiram, mas a API ainda nao respondeu em /health.
echo Aguarde mais alguns segundos e abra: http://localhost:8080/health
echo.
goto summary

:ready
echo [OK] API respondendo em http://localhost:8080/health

:summary
echo.
echo ========================================
echo   Pronto!
echo ========================================
echo.
echo   API:     http://localhost:8080
echo   Saude:   http://localhost:8080/health
echo.
echo   Para parar tudo depois, rode: scripts\stop.bat
echo.
echo   Exemplo Curitiba (sync):
echo   POST http://localhost:8080/api/events/sync?lat=-25.4284^&long=-49.2733^&provider=sympla-public
echo.
echo   Exemplo Curitiba (listar):
echo   GET  http://localhost:8080/api/events?lat=-25.4284^&long=-49.2733
echo.
pause
endlocal
