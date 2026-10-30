# API image for the V2 AWS demo. Local development doesn't use it (see the README).
# The SDK stage runs on the build machine's architecture and cross-compiles for the target, so an
# arm64 image builds on an x64 runner without emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY global.json ./
COPY RoomBooking/RoomBooking.csproj RoomBooking/
RUN dotnet restore RoomBooking/RoomBooking.csproj -a $TARGETARCH
COPY RoomBooking/ RoomBooking/
RUN dotnet publish RoomBooking/RoomBooking.csproj -a $TARGETARCH -c Release --no-restore -o /app

# Chiseled: no shell or package manager, and runs as a non-root user. Listens on port 8080.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "RoomBooking.dll"]
