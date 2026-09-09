# FIS Docker Configuration

This directory contains Docker configuration files for the Fleet Information System (FIS) modernization project.

## 🐳 Available Configurations

### 1. **docker-compose.yml** - Full Application Stack
Complete containerized application with:
- SQL Server 2019 database
- FIS API (.NET 8)
- FIS Web Application (Next.js)
- Automatic health checks and dependencies

### 2. **docker-compose.dev.yml** - Database Only
Just the SQL Server database for local development:
- SQL Server 2019 with FIS database
- Pre-configured with IFMS database and users
- Perfect for running .NET applications locally while using containerized database

### 3. **Dockerfile.api** - API Service Container
Multi-stage build for the FIS API:
- .NET 8 SDK for building
- .NET 8 runtime for execution
- Security hardened (non-root user)
- Health check endpoint included

### 4. **Dockerfile.web-next** - Next.js Web Application Container
Multi-stage build for the FIS Web application:
- Node.js 22 runtime
- pnpm dependency installation with a frozen lockfile
- Optimized for the Next.js standalone production server

## 🚀 Quick Start

### Prerequisites
- Docker Desktop installed and running
- .NET 8 SDK (for local development)

### Option 1: Database Only (Recommended for Development)
```bash
# Start just the database
./scripts/docker-dev.sh start-db

# Your connection string will be:
# Server=localhost,1433;Database=IFMS;User Id=sa;Password=******;Encrypt=True;TrustServerCertificate=True;

# Run your .NET applications locally
cd /home/dev/work/fis-modern
dotnet run --project src/Services/FIS.Api
```

### Option 2: Full Application Stack
```bash
# Build and start everything
./scripts/docker-dev.sh build-all
./scripts/docker-dev.sh start-full

# Access your application:
# API: http://localhost:5000
# Web: https://localhost
# Database: localhost:1433
```

#### Windows/macOS (no scripts)
```bash
# Build and start everything
docker compose -f docker/docker-compose.yml up -d --build

# Access your application:
# API: http://localhost:5000
# Web: http://localhost:5001
# Database: localhost:1433
```

### On-demand database tools (migration + seeding)
```bash
# Run schema sync (idempotent)
docker compose -f docker/docker-compose.yml --profile tools run --rm db-migrate

# Seed demo data
docker compose -f docker/docker-compose.yml --profile tools run --rm db-seed
```

### Cloudflare Tunnel (public access)
```bash
# Set your tunnel token (PowerShell)
$env:CLOUDFLARED_TOKEN="<your-token>"

# Start the tunnel
docker compose -f docker/docker-compose.yml --profile tunnel up -d cloudflared
```
Configure your tunnel public hostnames in Cloudflare to point to:
- `http://fis-web-next:3000` for fis.irisgroup.co.za
- `http://fis-api:8080` for api.irisgroup.co.za

## 🛠️ Development Commands

Use the convenient script for common operations:

```bash
# Database operations
./scripts/docker-dev.sh start-db     # Start database only
./scripts/docker-dev.sh stop-db      # Stop database
./scripts/docker-dev.sh logs-db      # View database logs
./scripts/docker-dev.sh shell-db     # Connect to SQL Server

# Full application operations
./scripts/docker-dev.sh start-full   # Start all services
./scripts/docker-dev.sh stop-full    # Stop all services
./scripts/docker-dev.sh logs-full    # View all service logs

# Build operations
./scripts/docker-dev.sh build-api    # Build API image
./scripts/docker-dev.sh build-web    # Build Web image
./scripts/docker-dev.sh build-all    # Build all images

# Utility operations
./scripts/docker-dev.sh status       # Show container status
./scripts/docker-dev.sh clean        # Clean up containers/images
./scripts/docker-dev.sh reset        # Complete reset with rebuild
```

## 📊 Service Configuration

### SQL Server Database
- **Image**: mcr.microsoft.com/mssql/server:2019-latest
- **Port**: 1433
- **Database**: IFMS
- **SA Password**: ******
- **Additional application user**: configured through environment variables (db_owner in local development)
- **Timezone**: Africa/Johannesburg
- **Health Check**: Every 10s with 30s startup period

### FIS API
- **Port**: 5000 (external) → 8080 (internal)
- **Health Check**: http://localhost:8080/health
- **Environment**: Development
- **Auto-restart**: Unless stopped manually

### FIS Web
- **Port**: 5001 (external) → 8080 (internal)
- **Depends on**: FIS API
- **Auto-restart**: Unless stopped manually

## 🔒 Security Features

### Container Security
- **Non-root users** for API and Web containers
- **Minimal attack surface** with multi-stage builds
- **No sensitive data** in container layers
- **Health checks** for service monitoring

### Database Security
- **Strong passwords** with complexity requirements
- **Separate application user** (ifms) with limited privileges
- **Encrypted connections** with TrustServerCertificate for development
- **Volume persistence** for data safety

## 📁 Volume Management

### Persistent Volumes
- `fis-mssql-data`: Production database data
- `fis-mssql-dev-data`: Development database data

### Volume Commands
```bash
# List FIS volumes
docker volume ls | grep fis

# Backup database volume
docker run --rm -v fis-mssql-dev-data:/data -v $(pwd):/backup alpine tar czf /backup/fis-database-backup.tar.gz /data

# Restore database volume
docker run --rm -v fis-mssql-dev-data:/data -v $(pwd):/backup alpine tar xzf /backup/fis-database-backup.tar.gz -C /
```

## 🔧 Troubleshooting

### Common Issues

1. **Port 1433 already in use**
   ```bash
   # Check what's using the port
   sudo netstat -tulpn | grep 1433

   # Stop local SQL Server if running
   sudo systemctl stop mssql-server
   ```

2. **Docker permission denied**
   ```bash
   # Add user to docker group
   sudo usermod -aG docker $USER
   # Log out and back in
   ```

3. **Database connection fails**
   ```bash
   # Check container health
   ./scripts/docker-dev.sh status

   # View database logs
   ./scripts/docker-dev.sh logs-db

   # Test connection manually
   ./scripts/docker-dev.sh shell-db
   ```

4. **Build fails**
   ```bash
   # Clean Docker cache
   docker system prune -f

   # Rebuild from scratch
   ./scripts/docker-dev.sh reset
   ```

### Log Locations
```bash
# Container logs
docker logs fis-mssql19-dev
docker logs fis-api
docker logs fis-web-next

# Follow logs in real-time
docker logs -f fis-mssql19-dev
```

## 🌐 Network Configuration

### Default Network
All services communicate through the `fis-network` bridge network:
- Database: `mssql:1433` (internal)
- API: `fis-api:8080` (internal)
- Web: `fis-web-next:3000` (internal)

### External Access
- Database: `localhost:1433`
- API: `localhost:5000`
- Web: `https://localhost` (ports 80/443 through nginx)

## 📈 Production Considerations

### Before Production Deployment
1. **Change default passwords** in all configuration files
2. **Use proper SSL certificates** instead of TrustServerCertificate
3. **Configure proper logging** and monitoring
4. **Set up database backups** and disaster recovery
5. **Review security settings** for production environment
6. **Configure reverse proxy** (nginx/Apache) for HTTPS termination
7. **Set resource limits** for containers

### Environment Variables for Production
```bash
# Override in production docker-compose.override.yml
ASPNETCORE_ENVIRONMENT=Production
MSSQL_SA_PASSWORD=<strong-production-password>
ConnectionStrings__Default=<production-connection-string>
```

---

This Docker setup matches the configuration from your fleet TypeScript application but adapted for .NET 8, providing a solid foundation for developing and running your modernized FIS application.
