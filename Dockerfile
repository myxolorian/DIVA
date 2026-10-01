# Build context = repo root:  docker build -t diva .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Only the API project is built here (tests run in CI; BE/tests is left out by .dockerignore).
COPY BE/src/Diva.Api/Diva.Api.csproj BE/src/Diva.Api/
RUN dotnet restore BE/src/Diva.Api/Diva.Api.csproj
COPY BE/src BE/src
RUN dotnet publish BE/src/Diva.Api/Diva.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# The API serves the static frontend from wwwroot.
COPY FE ./wwwroot
# Plain http inside the container; the host (Render) serves https in front of it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Diva.Api.dll"]
