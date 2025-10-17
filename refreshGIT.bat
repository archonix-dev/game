@echo off
chcp 65001 >nul
echo ================================================
echo  Git Sync - Синхронизация с GitHub
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

echo [3/5] Добавление всех файлов...
git add .
echo ✓ Файлы добавлены
echo.

echo [4/5] Создание коммита...
set /p commit_message="Введите описание изменений: "

if "%commit_message%"=="" (
    echo.
    echo ⚠ Описание не может быть пустым!
    pause
    exit /b 1
)

git commit -m "%commit_message%"
echo.

echo [5/5] Отправка на GitHub...
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

