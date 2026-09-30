# Build context = repo root:  docker build -t diva .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BE/Diva.slnx BE/
COPY BE/src/Diva.Api/Diva.Api.csproj BE/src/Diva.Api/
COPY BE/tests/Diva.Tests/Diva.Tests.csproj BE/tests/Diva.Tests/
RUN dotnet restore BE/src/Diva.Api/Diva.Api.csproj
COPY BE/src BE/src
RUN dotnet publish BE/src/Diva.Api/Diva.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# The API serves the static frontend from wwwroot.
COPY FE ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Diva.Api.dll"]
