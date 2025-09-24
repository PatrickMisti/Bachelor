@echo off
setlocal

echo Starte Akka.NET Cluster Nodes...

:: 1. Node
start "Backend" dotnet run --project .\FormulaOneAkkaNet.csproj --launch-profile "Backend"

:: 2. Node
start "Controller" dotnet run --project .\FormulaOneAkkaNet.csproj --launch-profile "Controller"

:: 3. Node
start "Ingress" dotnet run --project .\FormulaOneAkkaNet.csproj --launch-profile "Ingress"

echo Alle Nodes gestartet.
endlocal