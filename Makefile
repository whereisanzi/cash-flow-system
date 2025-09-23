# Makefile for Cash Flow System

# Variables
DOCKER_COMPOSE = docker-compose
PROJECT_NAME = cash-flow-system

.PHONY: help build up down restart logs clean test status

# Default command
all: help

# Help
help:
	@echo "Cash Flow System Management"
	@echo "============================"
	@echo ""
	@echo "Available commands:"
	@echo "  make build          - Build Docker images"
	@echo "  make up             - Start all services"
	@echo "  make down           - Stop and remove all services"
	@echo "  make restart        - Restart all services"
	@echo "  make logs           - Show logs from all services"
	@echo "  make logs-api       - Show API logs"
	@echo "  make logs-infra     - Show infrastructure logs"
	@echo "  make logs-gateway   - Show gateway and load balancer logs"
	@echo "  make status         - Show container status"
	@echo "  make test           - Run unit tests"
	@echo "  make clean          - Complete Docker environment cleanup"
	@echo "  make reset          - Complete reset (clean + build + up)"
	@echo "  make dev-infra      - Start only infrastructure (DBs + RabbitMQ)"
	@echo "  make dev-apis       - Start only APIs"
	@echo "  make health         - Check service health"
	@echo ""

# Build images
build:
	@echo "Building Docker images..."
	$(DOCKER_COMPOSE) build --no-cache

# Start all services
up:
	@echo "Starting all services..."
	$(DOCKER_COMPOSE) up -d
	@echo "Services started successfully!"
	@echo ""
	@echo "Migrations are applied automatically during API startup"
	@echo ""
	@echo "Available URLs:"
	@echo "  API Gateway (KrakenD):          http://localhost:8000"
	@echo "  Keycloak Admin Console:         http://localhost:8080 (admin/admin123)"
	@echo "  HAProxy Stats (Transactions):   http://localhost:8181"
	@echo "  HAProxy Stats (Consolidations): http://localhost:8282"
	@echo "  KrakenD Metrics:                http://localhost:8090"
	@echo "  RabbitMQ Management:            http://localhost:15672 (guest/guest)"
	@echo ""
	@echo "Endpoints via Gateway:"
	@echo "  POST /api/v1/merchants/{merchant_id}/transactions"
	@echo "  GET  /api/v1/merchants/{merchant_id}/consolidations/daily?date=YYYY-MM-DD"
	@echo ""
	@make status

# Stop all services
down:
	@echo "Stopping all services..."
	$(DOCKER_COMPOSE) down
	@echo "Services stopped successfully!"

# Restart services
restart: down up

# Run tests
test:
	@echo "Running unit tests..."
	@dotnet test tests/TransactionsApi.Tests/ --verbosity minimal
	@dotnet test tests/ConsolidationsApi.Tests/ --verbosity minimal

# Clean environment
clean: down
	@echo "Performing complete environment cleanup..."
	@echo "Stopping and removing containers..."
	$(DOCKER_COMPOSE) down -v --remove-orphans --rmi local
	@echo "Removing orphaned volumes..."
	@docker volume prune -f
	@echo "Removing unused images..."
	@docker image prune -f
	@echo "General Docker system cleanup..."
	@docker system prune -f --volumes
	@echo "Removing orphaned networks..."
	@docker network prune -f
	@echo "Environment cleanup completed!"

# Complete environment reset
reset: clean build up
	@echo "Complete reset finished!"

# Show container statistics
stats:
	@echo "Container statistics:"
	@docker stats --no-stream $(shell docker-compose ps -q)

# Show service logs
logs:
	$(DOCKER_COMPOSE) logs -f

# Show API logs only
logs-api:
	$(DOCKER_COMPOSE) logs -f transactions-api-1 transactions-api-2 consolidations-api-1 consolidations-api-2

# Show infrastructure logs only
logs-infra:
	$(DOCKER_COMPOSE) logs -f transactions-db consolidations-db rabbitmq prometheus grafana

# Show gateway and load balancer logs
logs-gateway:
	$(DOCKER_COMPOSE) logs -f krakend haproxy-transactions haproxy-consolidations

# Show container status
status:
	@echo "Container Status:"
	@echo "=================="
	@docker-compose ps

# Start only infrastructure services
dev-infra:
	@echo "Starting infrastructure services only..."
	$(DOCKER_COMPOSE) up -d transactions-db consolidations-db keycloak-db rabbitmq keycloak pgbouncer-transactions pgbouncer-consolidations transactions-migration consolidations-migration

# Start only API services
dev-apis:
	$(DOCKER_COMPOSE) up -d transactions-api-1 transactions-api-2 consolidations-api-1 consolidations-api-2 haproxy-transactions haproxy-consolidations krakend

# Check service health
health:
	@echo "Checking service health..."
	@curl -s http://localhost:8000/health || echo "Gateway: DOWN"
	@curl -s http://localhost:8080/health/ready || echo "Keycloak: DOWN"
	@curl -s http://localhost:15672/api/healthchecks/node || echo "RabbitMQ: DOWN"

