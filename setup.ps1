# Run from the repository root:  .\setup.ps1
$ErrorActionPreference = "Stop"

if (-not (Test-Path "FrotaOnline.sln")) {
    dotnet new sln -n FrotaOnline
}
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName 2>$null }

dotnet tool update --global dotnet-ef --version "9.*"

docker compose up -d
dotnet build
dotnet test --no-build
