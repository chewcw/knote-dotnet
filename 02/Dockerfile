FROM mcr.microsoft.com/dotnet/core/sdk:latest AS build
WORKDIR /build
COPY . .
RUN dotnet publish -c Release -o dist

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-alpine
WORKDIR /app
COPY --from=build /build/dist /app
CMD ["dotnet", "knote.dll"]
