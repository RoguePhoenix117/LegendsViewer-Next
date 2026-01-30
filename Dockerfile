# Stage 1: Build frontend
FROM node:18-alpine AS frontend-build
WORKDIR /src
COPY LegendsViewer.Frontend/legends-viewer-frontend/package*.json ./
RUN npm ci
COPY LegendsViewer.Frontend/legends-viewer-frontend/ ./
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

# Copy solution and project files
COPY LegendsViewer.sln ./
COPY LegendsViewer.Backend/*.csproj ./LegendsViewer.Backend/
COPY LegendsViewer.Frontend/*.csproj ./LegendsViewer.Frontend/
COPY LegendsViewer.Backend.Tests/*.csproj ./LegendsViewer.Backend.Tests/

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY . .

# Copy frontend build output
COPY --from=frontend-build /src/dist ./LegendsViewer.Frontend/legends-viewer-frontend/dist

# Build backend (skip frontend build since we already built it in Stage 1)
WORKDIR /src/LegendsViewer.Backend
RUN dotnet build -c Release -o /app/build -p:BuildFrontend=false

# Publish backend (skip frontend build since we already built it in Stage 1)
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false /p:BuildFrontend=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install SkiaSharp native dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libfreetype6 \
    libharfbuzz0b \
    libjpeg62-turbo \
    libpng16-16 \
    libx11-6 \
    libxcb1 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=backend-build /app/publish .

# Create data directory for world export files
RUN mkdir -p /app/data && chmod 755 /app/data

# Expose port
EXPOSE 8080

# Set environment
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the app
ENTRYPOINT ["dotnet", "LegendsViewer.dll"]
