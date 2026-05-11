FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MedicalFeaturePrototype.Api.csproj ./
RUN dotnet restore MedicalFeaturePrototype.Api.csproj

COPY . ./
RUN dotnet publish MedicalFeaturePrototype.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
RUN mkdir -p /app/publish/Data && cp Data/*.xlsx /app/publish/Data/

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "MedicalFeaturePrototype.Api.dll"]
