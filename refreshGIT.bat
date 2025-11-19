@echo off
chcp 65001 >nul
echo ================================================
echo  Git Sync - Автоматическая синхронизация с GitHub
echo ================================================
echo.

echo [1/5] Получение изменений с GitHub...
git pull
echo.

if %errorlevel% neq 0 (
    echo ✗ Ошибка при получении изменений!
    pause
    exit /b 1
)

echo [2/5] Проверка статуса...
git status
echo.

echo [3/5] Проверка изменений...
git diff --quiet && git diff --staged --quiet
if %errorlevel% equ 0 (
    echo ✓ Изменений нет, синхронизация не требуется
    echo.
    pause
    exit /b 0
)

echo [4/5] Добавление всех файлов...
git add .
echo ✓ Файлы добавлены
echo.

echo [5/5] Создание коммита...
for /f "tokens=1-3 delims=/" %%a in ('date /t') do set current_date=%%c-%%b-%%a
for /f "tokens=1-2 delims=:" %%a in ('time /t') do set current_time=%%a:%%b

set commit_message=Auto sync: %current_date% %current_time%

echo Сообщение коммита: "%commit_message%"
git commit -m "%commit_message%"
echo.

echo [6/6] Отправка на GitHub...
git push
echo.

if %errorlevel% equ 0 (
    echo ================================================
    echo ✓ Синхронизация завершена успешно!
    echo ================================================
) else (
    echo ================================================
    echo ✗ Ошибка при отправке!
    echo ================================================
)

echo.
pause