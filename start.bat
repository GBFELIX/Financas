@echo off
title Sistema Acoes e FIIs
echo Iniciando o servidor web da sua Carteira...
echo.

:: Define o ambiente como Desenvolvimento
set ASPNETCORE_ENVIRONMENT=Development

:: Abre a aba do Chrome automaticamente na URL do seu projeto
start chrome "http://localhost:5207"

:: Roda o projeto
dotnet run

pause