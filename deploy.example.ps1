# =====================================================================
# Fenix Legal OS — Пример скрипта деплоя на VPS
# =====================================================================

$VPS_IP = "YOUR_VPS_IP"
$VPS_USER = "root"
$REMOTE_DIR = "/var/www/fenixlegalos/"

Write-Host "1. Сборка проекта..." -ForegroundColor Yellow
dotnet publish -c Release -o bin/Release/publish

Write-Host "2. Синхронизация файлов..." -ForegroundColor Yellow
scp -r bin/Release/publish/* "$VPS_USER@${VPS_IP}:$REMOTE_DIR"

Write-Host "3. Перезапуск службы..." -ForegroundColor Yellow
ssh "$VPS_USER@$VPS_IP" "systemctl restart fenix; systemctl status fenix --no-pager"
