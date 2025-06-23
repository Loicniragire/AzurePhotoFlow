# AzurePhotoFlow Development Makefile
.PHONY: help setup models check-models dev build clean test

# Default target
help: ## Show this help message
	@echo "AzurePhotoFlow Development Commands"
	@echo "=================================="
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

setup: ## Complete setup including models
	@echo "🚀 Running complete AzurePhotoFlow setup..."
	@chmod +x scripts/setup/auto-setup.sh
	@./scripts/setup/auto-setup.sh

models: ## Export/update CLIP models based on .env configuration
	@echo "🤖 Updating CLIP models..."
	@python3 scripts/ai-ml/auto_export_models.py --update-env

check-models: ## Check if models match .env configuration
	@echo "🔍 Checking model configuration..."
	@python3 scripts/ai-ml/auto_export_models.py --check-only

dev: models ## Start development environment
	@echo "🐳 Starting development environment..."
	@docker-compose up --build

dev-detached: models ## Start development environment in background
	@echo "🐳 Starting development environment (detached)..."
	@docker-compose up --build -d

build: ## Build Docker images
	@echo "🔨 Building Docker images..."
	@docker-compose build

clean: ## Clean up Docker containers and images
	@echo "🧹 Cleaning up..."
	@docker-compose down --volumes --remove-orphans
	@docker system prune -f

test-backend: ## Run backend tests
	@echo "🧪 Running backend tests..."
	@cd tests/backend && dotnet test

test-frontend: ## Run frontend tests
	@echo "🧪 Running frontend tests..."
	@cd tests/frontend && npm test

logs: ## Show application logs
	@docker-compose logs -f

stop: ## Stop all services
	@docker-compose down

restart: ## Restart all services
	@docker-compose restart

config: ## Show current configuration from .env
	@echo "📋 Current Configuration:"
	@echo "========================"
	@grep -E "^[^#].*=" .env | head -20

# Model dimension shortcuts
models-512: ## Export base model (512 dimensions)
	@echo "📦 Setting up base model (512D)..."
	@sed -i '' 's/EMBEDDING_DIMENSION=.*/EMBEDDING_DIMENSION=512/' .env
	@sed -i '' 's/EMBEDDING_MODEL_VARIANT=.*/EMBEDDING_MODEL_VARIANT=base/' .env
	@python3 scripts/ai-ml/auto_export_models.py --force

models-768: ## Export large model (768 dimensions)
	@echo "📦 Setting up large model (768D)..."
	@sed -i '' 's/EMBEDDING_DIMENSION=.*/EMBEDDING_DIMENSION=768/' .env
	@sed -i '' 's/EMBEDDING_MODEL_VARIANT=.*/EMBEDDING_MODEL_VARIANT=large/' .env
	@python3 scripts/ai-ml/auto_export_models.py --force

models-1024: ## Export huge model (1024 dimensions)
	@echo "📦 Setting up huge model (1024D)..."
	@sed -i '' 's/EMBEDDING_DIMENSION=.*/EMBEDDING_DIMENSION=1024/' .env
	@sed -i '' 's/EMBEDDING_MODEL_VARIANT=.*/EMBEDDING_MODEL_VARIANT=huge/' .env
	@python3 scripts/ai-ml/auto_export_models.py --force

# Quick deployment
deploy-512: models-512 dev ## Deploy with 512D model
deploy-768: models-768 dev ## Deploy with 768D model  
deploy-1024: models-1024 dev ## Deploy with 1024D model