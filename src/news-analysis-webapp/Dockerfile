# --- Stage 1: Build React client ---
FROM node:22-alpine AS client-build
WORKDIR /app/client
COPY news-analysis-webapp/client/package.json news-analysis-webapp/client/package-lock.json ./
RUN npm ci
COPY news-analysis-webapp/client/ ./
RUN npm run build

# --- Stage 2: Build .NET API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY news-analysis-agent/src/NewsAnalysisAgent.Models/ news-analysis-agent/src/NewsAnalysisAgent.Models/
COPY news-analysis-agent/src/NewsAnalysisAgent.Agents/ news-analysis-agent/src/NewsAnalysisAgent.Agents/
COPY news-analysis-agent/src/NewsAnalysisAgent.Orchestration/ news-analysis-agent/src/NewsAnalysisAgent.Orchestration/
COPY news-analysis-agent/src/NewsAnalysisAgent.Tools/ news-analysis-agent/src/NewsAnalysisAgent.Tools/
COPY news-analysis-webapp/api/ news-analysis-webapp/api/
WORKDIR /src/news-analysis-webapp/api
RUN dotnet publish -c Release -o /app/publish

# --- Stage 3: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api-build /app/publish .
COPY --from=client-build /app/client/dist ./wwwroot
COPY news-portal/ ./news-portal
COPY news-analysis-webapp/api/domain-config.json ./domain-config.json
COPY news-analysis-webapp/api/news-scenarios.json ./news-scenarios.json
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "NewsAnalysisWebApp.Api.dll"]
