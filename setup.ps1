# Rode na raiz: .\setup.ps1
dotnet new sln -n FrotaOnline --force
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName }
dotnet tool install --global dotnet-ef --version "9.*" 2>$null
docker compose up -d
dotnet build
