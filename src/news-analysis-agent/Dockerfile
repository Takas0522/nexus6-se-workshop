FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NewsAnalysisAgent.sln ./
COPY src/NewsAnalysisAgent.Host/NewsAnalysisAgent.Host.csproj src/NewsAnalysisAgent.Host/
COPY src/NewsAnalysisAgent.Orchestration/NewsAnalysisAgent.Orchestration.csproj src/NewsAnalysisAgent.Orchestration/
COPY src/NewsAnalysisAgent.Agents/NewsAnalysisAgent.Agents.csproj src/NewsAnalysisAgent.Agents/
COPY src/NewsAnalysisAgent.Tools/NewsAnalysisAgent.Tools.csproj src/NewsAnalysisAgent.Tools/
COPY src/NewsAnalysisAgent.Models/NewsAnalysisAgent.Models.csproj src/NewsAnalysisAgent.Models/
RUN dotnet restore src/NewsAnalysisAgent.Host/NewsAnalysisAgent.Host.csproj

COPY src/ ./src/
RUN dotnet publish src/NewsAnalysisAgent.Host/NewsAnalysisAgent.Host.csproj \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    Urls=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "NewsAnalysisAgent.Host.dll"]
