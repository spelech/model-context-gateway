# Stage 1: Build the React frontend SPA
FROM node:25-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: Build the C# application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
ENV CI=true

# Copy csproj, props and restore dependencies
COPY ModelContextGateway.csproj Directory.Build.props ./
RUN dotnet restore ModelContextGateway.csproj

# Copy source and publish
COPY . ./
# Copy built frontend assets to wwwroot
COPY --from=frontend-build /wwwroot ./wwwroot
RUN dotnet publish ModelContextGateway.csproj -c Release -o /app

# Stage 3: Standard runtime image
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app

LABEL org.opencontainers.image.title="Model Context Gateway (MCG)"
LABEL org.opencontainers.image.description="High-performance ASP.NET Core gateway router for the Model Context Protocol (MCP)"
LABEL org.opencontainers.image.source="https://github.com/spelech/model-context-gateway"
LABEL org.opencontainers.image.licenses="MIT"

# Install native dependencies for SQLite / SQLCipher bundle if any
RUN apt-get update && apt-get install -y --no-install-recommends \
    libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy the published app
COPY --from=build /app .

# Expose port
EXPOSE 8080

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

# Run the app
ENTRYPOINT ["./mcg"]

# Stage 4: "Batteries-Included" full runtime image for STDIO backends
FROM runtime AS runtime-full

# Install Python 3, Node.js, npm, ca-certificates, and curl
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    python3-pip \
    python3-venv \
    nodejs \
    npm \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy pre-compiled uv and bun binaries from official images
COPY --from=ghcr.io/astral-sh/uv:latest /uv /usr/local/bin/uv
COPY --from=oven/bun:latest /usr/local/bin/bun /usr/local/bin/bun
