# Imagem da API para o Cloud Run. Compila a solução em uma etapa e leva só o resultado do publish para a imagem final.
#
#   docker build -t frota-online-api .
#   docker run --rm -p 8080:8080 -e ConnectionStrings__Default="..." -e Jwt__SecretKey="..." frota-online-api
#
# No GCP: gcloud run deploy SERVICE --source .   (o Cloud Build usa este arquivo)

# ---- build -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Só os arquivos de projeto primeiro: o restore fica em cache enquanto os pacotes não mudam.
COPY global.json Directory.Build.props ./
COPY src/FrotaOnline.Domain/FrotaOnline.Domain.csproj src/FrotaOnline.Domain/
COPY src/FrotaOnline.Application/FrotaOnline.Application.csproj src/FrotaOnline.Application/
COPY src/FrotaOnline.Infrastructure/FrotaOnline.Infrastructure.csproj src/FrotaOnline.Infrastructure/
COPY src/FrotaOnline.Api/FrotaOnline.Api.csproj src/FrotaOnline.Api/
RUN dotnet restore src/FrotaOnline.Api/FrotaOnline.Api.csproj

COPY src/ src/
RUN dotnet publish src/FrotaOnline.Api/FrotaOnline.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---- runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# O Cloud Run espera o servidor na porta 8080 (valor padrão de PORT).
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

# Usuário sem privilégios que a imagem aspnet já traz.
USER $APP_UID
ENTRYPOINT ["dotnet", "FrotaOnline.Api.dll"]
