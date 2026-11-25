FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Vairas.SchoolManagement/Vairas.SchoolManagement.csproj
RUN dotnet publish Vairas.SchoolManagement/Vairas.SchoolManagement.csproj --no-restore -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vairas.SchoolManagement.dll"]
