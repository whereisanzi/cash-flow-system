# Makefile para gerenciar o Sistema de Fluxo de Caixa

# Variáveis
DOCKER_COMPOSE = docker-compose
PROJECT_NAME = cash-flow-system

# Cores para output
GREEN = \033[0;32m
YELLOW = \033[0;33m
RED = \033[0;31m
NC = \033[0m # No Color

.PHONY: help build up down restart logs clean test status generate-migrations \
        k6-up-deps k6-run k6-run-host k6-all k6-summary

# Comando padrão
all: help

# Ajuda
help:
	@echo "$(GREEN)Sistema de Gestão de Fluxo de Caixa$(NC)"
	@echo "================================================"
	@echo ""
	@echo "$(YELLOW)Comandos disponíveis:$(NC)"
	@echo "  make build          - Constrói as imagens Docker"
	@echo "  make up             - Inicia todos os serviços"
	@echo "  make down           - Para e remove todos os serviços"
	@echo "  make restart        - Reinicia todos os serviços"
	@echo "  make logs           - Mostra logs de todos os serviços"
	@echo "  make logs-api       - Mostra logs das APIs"
	@echo "  make logs-infra     - Mostra logs da infraestrutura"
	@echo "  make logs-gateway   - Mostra logs do Gateway e Load Balancer"
	@echo "  make status         - Mostra status dos containers"
	@echo "  make generate-migrations - Gera novas migrations do Entity Framework"
	@echo "  make test           - Executa testes unitários"
	@echo "  make clean          - Limpeza completa do ambiente Docker"
	@echo "  make reset          - Reset completo (clean + build + up)"
	@echo "  make dev-infra      - Inicia apenas infraestrutura (DBs + RabbitMQ)"
	@echo "  make dev-apis       - Inicia apenas as APIs"
	@echo "  make health         - Verifica saúde dos serviços"
	@echo ""
	@echo "$(YELLOW)Load Testing (k6 via Docker):$(NC)"
	@echo "  make load-test-comprehensive - Stress test completo (30+ min)"
	@echo "  make load-test-performance  - Monitoramento de performance (15 min)"
	@echo "  make load-test-chaos        - Chaos testing (20 min) - CUIDADO!"
	@echo "  make load-test-all          - Suíte completa de testes"
	@echo "  make load-test-health       - Verifica saúde antes dos testes"
	@echo ""

# Construir imagens
build:
	@echo "$(GREEN)🔨 Construindo imagens Docker...$(NC)"
	$(DOCKER_COMPOSE) build --no-cache

# Iniciar todos os serviços
up:
	@echo "$(GREEN)🚀 Iniciando todos os serviços...$(NC)"
	$(DOCKER_COMPOSE) up -d
	@echo "$(GREEN)✅ Serviços iniciados!$(NC)"
	@echo ""
	@echo "$(YELLOW)📊 Migrations são aplicadas automaticamente na inicialização das APIs$(NC)"
	@echo ""
	@echo "$(YELLOW)URLs disponíveis:$(NC)"
	@echo "  🌐 API Gateway (KrakenD):      http://localhost:8000"
	@echo "  🔐 Keycloak Admin Console:     http://localhost:8080 (admin/admin123)"
	@echo "  📊 HAProxy Stats:              http://localhost:8081"
	@echo "  📈 KrakenD Metrics:            http://localhost:8090"
	@echo "  🐰 RabbitMQ Management:        http://localhost:15672 (guest/guest)"
	@echo ""
	@echo "$(YELLOW)Endpoints via Gateway:$(NC)"
	@echo "  POST /transactions/{merchant_id}/transactions"
	@echo "  GET  /consolidations/{merchant_id}/daily?date=YYYY-MM-DD"
	@echo ""
	@make status

# Parar todos os serviços
down:
	@echo "$(RED)🛑 Parando todos os serviços...$(NC)"
	$(DOCKER_COMPOSE) down
	@echo "$(GREEN)✅ Serviços parados!$(NC)"

# Reiniciar serviços
restart: down up

# Ver logs
logs:
	@echo "$(YELLOW)📋 Logs de todos os serviços:$(NC)"
	$(DOCKER_COMPOSE) logs -f

# Logs apenas das APIs
logs-api:
	@echo "$(YELLOW)📋 Logs das APIs:$(NC)"
	$(DOCKER_COMPOSE) logs -f transactions-api-1 transactions-api-2 consolidations-api-1 consolidations-api-2

# Logs apenas da infraestrutura
logs-infra:
	@echo "$(YELLOW)📋 Logs da infraestrutura:$(NC)"
	$(DOCKER_COMPOSE) logs -f transactions-db consolidations-db rabbitmq

# Logs do Gateway e Load Balancer
logs-gateway:
	@echo "$(YELLOW)📋 Logs Gateway e Load Balancer:$(NC)"
	$(DOCKER_COMPOSE) logs -f krakend haproxy

# Status dos containers
status:
	@echo "$(YELLOW)📊 Status dos containers:$(NC)"
	$(DOCKER_COMPOSE) ps

# Gerar novas migrations do Entity Framework
generate-migrations:
	@echo "$(GREEN)📊 Gerando migrations do Entity Framework...$(NC)"
	@echo "$(YELLOW)Verificando ferramentas do Entity Framework...$(NC)"
	@if ! dotnet tool list -g | grep -q "dotnet-ef"; then \
		echo "Instalando dotnet-ef globalmente..."; \
		dotnet tool install --global dotnet-ef; \
	fi
	@echo "$(YELLOW)Gerando migration para TransactionsApi...$(NC)"
	@cd src/TransactionsApi && dotnet ef migrations add Migration_$(shell date +%Y%m%d_%H%M%S) --verbose
	@echo "$(YELLOW)Gerando migration para ConsolidationsApi...$(NC)"
	@cd src/ConsolidationsApi && dotnet ef migrations add Migration_$(shell date +%Y%m%d_%H%M%S) --verbose
	@echo "$(GREEN)✅ Migrations geradas com sucesso!$(NC)"
	@echo "$(YELLOW)💡 As migrations serão aplicadas automaticamente quando as APIs subirem$(NC)"

# Executar testes
test:
	@echo "$(GREEN)🧪 Executando testes unitários...$(NC)"
	@dotnet test tests/TransactionsApi.Tests/ --verbosity minimal
	@dotnet test tests/ConsolidationsApi.Tests/ --verbosity minimal

# Limpar ambiente
clean: down
	@echo "$(RED)🧹 Limpeza completa do ambiente...$(NC)"
	@echo "$(YELLOW)Parando e removendo containers...$(NC)"
	$(DOCKER_COMPOSE) down -v --remove-orphans --rmi local
	@echo "$(YELLOW)Removendo volumes órfãos...$(NC)"
	@docker volume prune -f
	@echo "$(YELLOW)Removendo imagens não utilizadas...$(NC)"
	@docker image prune -f
	@echo "$(YELLOW)Limpeza geral do sistema Docker...$(NC)"
	@docker system prune -f --volumes
	@echo "$(YELLOW)Removendo networks órfãos...$(NC)"
	@docker network prune -f
	@echo "$(GREEN)✅ Ambiente completamente limpo!$(NC)"

# Reset completo do ambiente
reset: clean build up
	@echo "$(GREEN)🔄 Reset completo concluído!$(NC)"

# Desenvolvimento - apenas infraestrutura
dev-infra:
	@echo "$(GREEN)🔧 Iniciando apenas infraestrutura...$(NC)"
	$(DOCKER_COMPOSE) up -d transactions-db consolidations-db rabbitmq
	@echo "$(GREEN)✅ Infraestrutura iniciada!$(NC)"
	@echo ""
	@echo "$(YELLOW)Para executar as APIs localmente:$(NC)"
	@echo "  cd src/TransactionsApi && dotnet run"
	@echo "  cd src/ConsolidationsApi && dotnet run"

# Desenvolvimento - apenas APIs
dev-apis:
	@echo "$(GREEN)🔧 Iniciando apenas as APIs...$(NC)"
	$(DOCKER_COMPOSE) up -d transactions-api consolidations-api
	@echo "$(GREEN)✅ APIs iniciadas!$(NC)"

# Verificar saúde dos serviços
health:
	@echo "$(YELLOW)🏥 Verificando saúde dos serviços...$(NC)"
	@echo ""
	@echo "TransactionsApi:"
	@curl -s http://localhost:5001/health || echo "❌ TransactionsApi indisponível"
	@echo ""
	@echo "ConsolidationsApi:"
	@curl -s http://localhost:5002/health || echo "❌ ConsolidationsApi indisponível"
	@echo ""

# Comandos avançados

# Backup dos bancos
backup:
	@echo "$(GREEN)💾 Fazendo backup dos bancos...$(NC)"
	@mkdir -p backups
	@docker exec transactions-db pg_dump -U postgres transactions_db > backups/transactions_$(shell date +%Y%m%d_%H%M%S).sql
	@docker exec consolidations-db pg_dump -U postgres consolidations_db > backups/consolidations_$(shell date +%Y%m%d_%H%M%S).sql
	@echo "$(GREEN)✅ Backup concluído!$(NC)"

# Mostrar estatísticas dos containers
stats:
	@echo "$(YELLOW)📈 Estatísticas dos containers:$(NC)"
	@docker stats --no-stream $(shell docker-compose ps -q)

# Limpar apenas containers parados
clean-containers:
	@echo "$(YELLOW)🧹 Removendo containers parados...$(NC)"
	@docker container prune -f

# Rebuild específico de um serviço
rebuild-%:
	@echo "$(GREEN)🔨 Reconstruindo $*...$(NC)"
	$(DOCKER_COMPOSE) build --no-cache $*
	$(DOCKER_COMPOSE) up -d $*

# Exemplo de uso: make curl-create-transaction MERCHANT_ID=merchant-123
curl-create-transaction:
	@echo "$(GREEN)🔗 Criando transação de exemplo...$(NC)"
	@curl -X POST "http://localhost:5001/api/v1/merchants/$(MERCHANT_ID)/transactions" \
		-H "Content-Type: application/json" \
		-d '{"type": "CREDITO", "amount": 100.50, "description": "Transação de teste"}' | jq .

# Exemplo de uso: make curl-get-consolidation MERCHANT_ID=merchant-123 DATE=2024-01-15
curl-get-consolidation:
	@echo "$(GREEN)🔗 Consultando consolidação...$(NC)"
	@curl -X GET "http://localhost:8000/consolidations/$(MERCHANT_ID)/daily?date=$(DATE)" | jq .

# Teste via Gateway - Criar transação
gateway-create-transaction:
	@echo "$(GREEN)🌐 Criando transação via Gateway...$(NC)"
	@curl -X POST "http://localhost:8000/transactions/$(MERCHANT_ID)/transactions" \
		-H "Content-Type: application/json" \
		-d '{"type": "CREDITO", "amount": 100.50, "description": "Teste via Gateway"}' | jq .

# Teste via Gateway - Consultar consolidação
gateway-get-consolidation:
	@echo "$(GREEN)🌐 Consultando consolidação via Gateway...$(NC)"
	@curl -X GET "http://localhost:8000/consolidations/$(MERCHANT_ID)/daily?date=$(DATE)" | jq .

# Teste de Performance/Load Test
load-test:
	@echo "$(GREEN)⚡ Executando teste de carga...$(NC)"
	@echo "Criando 10 transações simultâneas..."
	@for i in {1..10}; do \
		curl -X POST "http://localhost:8000/transactions/load-test-$$i/transactions" \
			-H "Content-Type: application/json" \
			-d '{"type": "CREDITO", "amount": '$$i'0.00, "description": "Load test $$i"}' & \
	done; wait
	@echo "$(GREEN)✅ Teste de carga concluído!$(NC)"

# ==============================
# k6 Load Testing Suite
# ==============================

# Environment configuration
K6_BASE_URL ?= http://krakend:8080
K6_USERNAME ?= merchant1
K6_PASSWORD ?= merchant123
K6_CLIENT_ID ?= cash-flow-api
K6_CLIENT_SECRET ?= cash-flow-secret-2024
K6_MERCHANT_A ?= merchant-001
K6_MERCHANT_B ?= merchant-002
K6_MERCHANT_C ?= merchant-003
K6_DATE ?= 2025-09-20
K6_OUTPUT_DIR ?= ./tests/k6/results

# Load test targets
load-test-comprehensive:
	@echo "$(GREEN)💪 Comprehensive Stress Test (30+ min)$(NC)"
	@mkdir -p $(K6_OUTPUT_DIR)
	@docker run --rm -i --network host \
	  -e K6_BASE_URL=http://localhost:8000 \
	  -e K6_USERNAME=$(K6_USERNAME) \
	  -e K6_PASSWORD=$(K6_PASSWORD) \
	  -e K6_CLIENT_ID=$(K6_CLIENT_ID) \
	  -e K6_CLIENT_SECRET=$(K6_CLIENT_SECRET) \
	  -e K6_MERCHANT_A=$(K6_MERCHANT_A) \
	  -e K6_MERCHANT_B=$(K6_MERCHANT_B) \
	  -e K6_MERCHANT_C=$(K6_MERCHANT_C) \
	  -v $(PWD)/tests/k6:/scripts \
	  -v $(PWD)/$(K6_OUTPUT_DIR):/results \
	  grafana/k6:latest run \
	  --out json=/results/comprehensive-$(shell date +%Y%m%d_%H%M%S).json \
	  /scripts/stress-comprehensive.js

load-test-performance:
	@echo "$(GREEN)📊 Performance Monitoring (15 min)$(NC)"
	@mkdir -p $(K6_OUTPUT_DIR)
	@docker run --rm -i --network host \
	  -e K6_BASE_URL=http://localhost:8000 \
	  -e K6_USERNAME=$(K6_USERNAME) \
	  -e K6_PASSWORD=$(K6_PASSWORD) \
	  -e K6_CLIENT_ID=$(K6_CLIENT_ID) \
	  -e K6_CLIENT_SECRET=$(K6_CLIENT_SECRET) \
	  -v $(PWD)/tests/k6:/scripts \
	  -v $(PWD)/$(K6_OUTPUT_DIR):/results \
	  grafana/k6:latest run \
	  --out json=/results/performance-$(shell date +%Y%m%d_%H%M%S).json \
	  /scripts/performance-monitoring.js

load-test-chaos:
	@echo "$(RED)🔥 Chaos Testing (20 min) - CUIDADO!$(NC)"
	@echo "$(YELLOW)⚠️  Este teste pode causar instabilidade no sistema$(NC)"
	@read -p "Continuar? (y/N) " confirm && [ "$$confirm" = "y" ] || exit 1
	@mkdir -p $(K6_OUTPUT_DIR)
	@docker run --rm -i --network host \
	  -e K6_BASE_URL=http://localhost:8000 \
	  -e K6_USERNAME=$(K6_USERNAME) \
	  -e K6_PASSWORD=$(K6_PASSWORD) \
	  -e K6_CLIENT_ID=$(K6_CLIENT_ID) \
	  -e K6_CLIENT_SECRET=$(K6_CLIENT_SECRET) \
	  -v $(PWD)/tests/k6:/scripts \
	  -v $(PWD)/$(K6_OUTPUT_DIR):/results \
	  grafana/k6:latest run \
	  --out json=/results/chaos-$(shell date +%Y%m%d_%H%M%S).json \
	  /scripts/chaos-testing.js

# Health check before load tests
load-test-health:
	@echo "$(BLUE)🔍 Checking system health...$(NC)"
	@curl -f http://localhost:8000/health || (echo "$(RED)❌ System not healthy$(NC)" && exit 1)
	@echo "$(GREEN)✅ System healthy$(NC)"

# Complete test suite
load-test-all: load-test-health
	@echo "$(YELLOW)🚀 Running complete load test suite$(NC)"
	@$(MAKE) load-test-performance
	@sleep 60
	@$(MAKE) load-test-comprehensive
	@sleep 120
	@$(MAKE) load-test-chaos
	@echo "$(GREEN)🎉 All load tests completed!$(NC)"
	@echo "$(BLUE)Results in: $(K6_OUTPUT_DIR)$(NC)"
