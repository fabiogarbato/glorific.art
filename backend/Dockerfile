# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia so os manifestos primeiro para aproveitar o cache de restore.
COPY Directory.Build.props NuGet.config ./
COPY src/Domain/Domain.csproj                 src/Domain/
COPY src/Application/Application.csproj       src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/API/API.csproj                       src/API/
RUN dotnet restore src/API/API.csproj

COPY src/ src/
RUN dotnet publish src/API/API.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=America/Sao_Paulo
EXPOSE 8080

# Nao roda como root.
RUN adduser --disabled-password --gecos "" --uid 5001 glorific
USER glorific

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Glorific.Api.dll"]
