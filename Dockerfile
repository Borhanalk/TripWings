# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY *.csproj ./
RUN dotnet restore

# Copy everything and publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Render uses port 10000
ENV ASPNETCORE_URLS=http://+:10000

COPY --from=build /app/publish .

# Change TripWings.dll إذا اسم المشروع مختلف
ENTRYPOINT ["dotnet", "TripWings.dll"]
