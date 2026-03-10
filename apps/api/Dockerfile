FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY RealEstateStar.Api/RealEstateStar.Api.csproj RealEstateStar.Api/
RUN dotnet restore RealEstateStar.Api/RealEstateStar.Api.csproj
COPY . .
WORKDIR /src/RealEstateStar.Api
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser && \
    chown -R appuser:appgroup /app

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -q --spider http://localhost:8080/health/live || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "RealEstateStar.Api.dll"]
