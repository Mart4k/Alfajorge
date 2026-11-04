FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Alfajorge.Api.csproj ./
RUN dotnet restore Alfajorge.Api.csproj
COPY . ./
RUN dotnet publish Alfajorge.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Alfajorge.Api.dll"]
