# ── Stage 1: restore ────────────────────────────────────────────────────────
# Copy only the files that affect restore so this layer is cached as long as
# project references and package versions don't change (DK-1).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Repo-level files that MSBuild reads before the source
COPY global.json                  ./
COPY Directory.Build.props        ./
COPY Directory.Packages.props     ./

# Project files only – each on its own line so a single .csproj change only
# invalidates from that COPY line onward, not the whole restore layer.
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj \
     apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj \
     apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj \
     apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
     apps/api/src/SupportDesk.Presentation/

# Restore NuGet packages – this layer is cached until a .csproj or props file changes.
# A subsequent source-only change hits the COPY below and skips this step.
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# ── Stage 2: publish ─────────────────────────────────────────────────────────
# Copy the full source now that restore is cached.
COPY apps/api/src/ apps/api/src/

# Publish in Release mode; no --no-restore so the already-restored packages
# (in the NuGet global-packages cache at ~/.nuget/packages inside this layer)
# are reused without going to the network.
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

# ── Stage 3: runtime ─────────────────────────────────────────────────────────
# Use the smaller ASP.NET runtime image – no SDK tooling in production (DK-1).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# DK-3: Create and switch to a non-root user.
# aspnet:10.0 is Debian-based; use groupadd/useradd (not addgroup/adduser).
RUN groupadd --system appgroup && useradd --system --gid appgroup appuser
USER appuser

COPY --from=build /app/publish ./

# DK-3: HTTP only on port 8080; no HTTPS certificate work in the image.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
