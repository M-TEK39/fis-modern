#!/bin/bash

# FIS Docker Development Scripts
# Usage: ./docker-dev.sh [command]

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"
MSSQL_HOST_PORT="${MSSQL_HOST_PORT:-1433}"
MSSQL_DB_NAME="${MSSQL_DB_NAME:-legacy}"
MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-Behox@1903}"
DB_HOST="${DB_HOST:-192.0.2.10}"
DB_PORT="${DB_PORT:-1433}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${BLUE}=== $1 ===${NC}"
}

get_compose_cmd() {
    if docker compose version >/dev/null 2>&1; then
        echo "docker compose"
    elif command -v docker-compose >/dev/null 2>&1; then
        echo "docker-compose"
    else
        print_error "Docker Compose is not available. Install Docker Desktop with Compose support."
        exit 1
    fi
}

run_compose() {
    local compose_cmd
    compose_cmd="$(get_compose_cmd)"
    # shellcheck disable=SC2086
    $compose_cmd "$@"
}

# Function to show usage
show_help() {
    cat << EOF
FIS Docker Development Commands

Usage: $0 [COMMAND]

Commands:
  start-db       Start only the SQL Server database for local development
  stop-db        Stop the database container
  restart-db     Restart the database container
  logs-db        Show database container logs
  
  start-full     Start application services using external SQL Server, API, Next.js
  stop-full      Stop all services
  restart-full   Restart all services
  logs-full      Show logs for all services
  
  build-api      Build the API Docker image
  build-web      Build the Next.js Web Docker image
  build-all      Build all Docker images
  
  clean          Clean up all containers and images
  reset          Stop all containers, remove volumes, and rebuild
  
  status         Show status of all containers
  shell-db       Connect to SQL Server database
  
  help           Show this help message

Examples:
  $0 start-db     # Start database for local development
  $0 build-all    # Build all Docker images
  $0 start-full   # Start complete application stack
EOF
}

# Function to check if Docker is running
check_docker() {
    if ! docker info >/dev/null 2>&1; then
        print_error "Docker is not running. Please start Docker and try again."
        exit 1
    fi
}

# Database-only operations
start_db() {
    print_header "Starting SQL Server Database"
    cd "$PROJECT_ROOT"
    run_compose -f docker/docker-compose.dev.yml up -d
    print_status "Database started successfully!"
    print_status "Connection string: Server=localhost,${MSSQL_HOST_PORT};Database=${MSSQL_DB_NAME};User Id=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True;"
}

stop_db() {
    print_header "Stopping SQL Server Database"
    cd "$PROJECT_ROOT"
    run_compose -f docker/docker-compose.dev.yml down
    print_status "Database stopped successfully!"
}

restart_db() {
    print_header "Restarting SQL Server Database"
    stop_db
    start_db
}

logs_db() {
    print_header "Database Logs"
    cd "$PROJECT_ROOT"
    run_compose -f docker/docker-compose.dev.yml logs -f
}

# Full application operations
start_full() {
    print_header "Starting Full Application Stack"
    cd "$PROJECT_ROOT"
    run_compose -f docker/docker-compose.yml up -d
    print_status "Full application started successfully!"
    print_status "API: http://localhost:5000"
    print_status "Web: https://localhost (Next.js via nginx)"
    print_status "Database: ${DB_HOST}:${DB_PORT} (external Windows SQL Server)"
}

stop_full() {
    print_header "Stopping Full Application Stack"
    cd "$PROJECT_ROOT"
    run_compose -f docker/docker-compose.yml down
    print_status "Full application stopped successfully!"
}

restart_full() {
    print_header "Restarting Full Application Stack"
    stop_full
    start_full
}

logs_full() {
    print_header "Application Logs"
    cd "$PROJECT_ROOT"
    run_compose -f docker/docker-compose.yml logs -f
}

# Build operations
build_api() {
    print_header "Building API Docker Image"
    cd "$PROJECT_ROOT"
    docker build -f docker/Dockerfile.api -t fis-api:latest .
    print_status "API image built successfully!"
}

build_web() {
    print_header "Building Next.js Web Docker Image"
    cd "$PROJECT_ROOT"
    docker build -f docker/Dockerfile.web-next -t fis-web-next:latest .
    print_status "Next.js Web image built successfully!"
}

build_all() {
    print_header "Building All Docker Images"
    build_api
    build_web
    print_status "All images built successfully!"
}

# Utility operations
show_status() {
    print_header "Container Status"
    echo "FIS Containers:"
    docker ps -a --filter "name=fis-" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    echo ""
    echo "All SQL Server Containers:"
    docker ps -a --filter "name=mssql" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
}

shell_db() {
    print_header "Connecting to SQL Server Database"
    print_status "Connecting to database... (Use 'exit' to quit)"
    docker exec -it fis-mssql19-dev /bin/sh -c "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '${MSSQL_SA_PASSWORD}' || /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P '${MSSQL_SA_PASSWORD}'"
}

clean_all() {
    print_header "Cleaning Up Docker Resources"
    
    print_warning "This will remove all FIS containers and images. Continue? (y/N)"
    read -r response
    case "$response" in
        [yY][eE][sS]|[yY]) 
            cd "$PROJECT_ROOT"
            
            # Stop and remove containers
            run_compose -f docker/docker-compose.yml down
            run_compose -f docker/docker-compose.dev.yml down
            
            # Remove FIS containers
            docker ps -a --filter "name=fis-" -q | xargs -r docker rm -f
            
            # Remove FIS images
            docker images --filter "reference=fis-*" -q | xargs -r docker rmi -f
            
            print_status "Cleanup completed!"
            ;;
        *)
            print_status "Cleanup cancelled."
            ;;
    esac
}

reset_all() {
    print_header "Resetting Complete Application"
    
    print_warning "This will stop all containers, remove volumes, and rebuild everything. Continue? (y/N)"
    read -r response
    case "$response" in
        [yY][eE][sS]|[yY])
            cd "$PROJECT_ROOT"
            
            # Stop everything
            run_compose -f docker/docker-compose.yml down -v
            run_compose -f docker/docker-compose.dev.yml down -v
            
            # Remove volumes
            docker volume rm -f fis-mssql-dev-data 2>/dev/null || true
            
            # Rebuild and start
            build_all
            start_db
            
            print_status "Reset completed! Database is ready for development."
            ;;
        *)
            print_status "Reset cancelled."
            ;;
    esac
}

# Main script logic
main() {
    check_docker
    
    case "${1:-help}" in
        "start-db"|"db")
            start_db
            ;;
        "stop-db")
            stop_db
            ;;
        "restart-db")
            restart_db
            ;;
        "logs-db")
            logs_db
            ;;
        "start-full"|"start")
            start_full
            ;;
        "stop-full"|"stop")
            stop_full
            ;;
        "restart-full"|"restart")
            restart_full
            ;;
        "logs-full"|"logs")
            logs_full
            ;;
        "build-api")
            build_api
            ;;
        "build-web")
            build_web
            ;;
        "build-all"|"build")
            build_all
            ;;
        "clean")
            clean_all
            ;;
        "reset")
            reset_all
            ;;
        "status")
            show_status
            ;;
        "shell-db"|"sql")
            shell_db
            ;;
        "help"|"-h"|"--help"|"")
            show_help
            ;;
        *)
            print_error "Unknown command: $1"
            echo ""
            show_help
            exit 1
            ;;
    esac
}

# Run main function with all arguments
main "$@"
