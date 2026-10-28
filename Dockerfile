FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ScienceLine.Web/ScienceLine.Web.csproj src/ScienceLine.Web/
RUN dotnet restore src/ScienceLine.Web/ScienceLine.Web.csproj
COPY src/ src/
RUN dotnet publish src/ScienceLine.Web/ScienceLine.Web.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
RUN mkdir -p /app/keys && chown app:app /app/keys
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet","ScienceLine.Web.dll"]
