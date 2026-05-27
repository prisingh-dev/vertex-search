.PHONY: help install lint apply-dev apply-test apply-uat apply-prd create-dev create-test create-uat create-prd

# Resolve paths relative to this Makefile regardless of where make is invoked from
MAKEFILE_DIR := $(dir $(abspath $(lastword $(MAKEFILE_LIST))))
DEPLOY_DIR   := $(MAKEFILE_DIR)deploy
SCRIPT       := $(MAKEFILE_DIR)serving_config_controls.py
PYTHON_RUN   := $(shell if command -v uv >/dev/null 2>&1; then echo "uv run python"; else echo "python3"; fi)

# Default target
help: ## Show this help message
	@echo ""
	@echo "Agent Serving Config — Deployment Makefile"
	@echo "==========================================="
	@echo ""
	@echo "Usage: make <target> [CONFIG_ID=<new-serving-config-id>]"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2}'
	@echo ""

# ----------------------------------------------------------------------------
# Setup
# ----------------------------------------------------------------------------

install: ## Install Python dependencies via uv
	@command -v uv >/dev/null 2>&1 || python3 -m pip install uv
	uv sync

lint: ## Run pre-commit linters (ruff, etc.)
	pre-commit run --all-files

# ----------------------------------------------------------------------------
# Apply serving config per environment
# Each target loads the corresponding config env file and runs the script.
# ----------------------------------------------------------------------------

apply-dev: ## Apply serving config to the Dev environment
	@echo ">>> Applying serving config to DEV (gcp-wd-shopping-agent-dev)..."
	@set -a && . $(DEPLOY_DIR)/config.dev.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$$AGENT_CONFIG_ID \
		$(PYTHON_RUN) $(SCRIPT)

apply-test: ## Apply serving config to the test environment
	@echo ">>> Applying serving config to Test (gcp-wd-shopping-agent-test)..."
	@echo ">>> WARNING: This will modify Test. Press Ctrl+C within 5 seconds to abort."
	@sleep 5
	@set -a && . $(DEPLOY_DIR)/config.test.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$$AGENT_CONFIG_ID \
		$(PYTHON_RUN) $(SCRIPT)

apply-uat: ## Apply serving config to the UAT environment
	@echo ">>> Applying serving config to UAT (gcp-wd-shopping-agent-uat)..."
	@set -a && . $(DEPLOY_DIR)/config.uat.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$$AGENT_CONFIG_ID \
		$(PYTHON_RUN) $(SCRIPT)

apply-prd: ## Apply serving config to the Production environment
	@echo ">>> Applying serving config to PRD (gcp-wd-shopping-agent-prod)..."
	@echo ">>> WARNING: This will modify PRODUCTION. Press Ctrl+C within 5 seconds to abort."
	@sleep 5
	@set -a && . $(DEPLOY_DIR)/config.prd.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$$AGENT_CONFIG_ID \
		$(PYTHON_RUN) $(SCRIPT)

create-dev: ## Create/update a specific serving config in Dev (requires CONFIG_ID)
	@if [ -z "$(CONFIG_ID)" ]; then echo "CONFIG_ID is required. Example: make create-dev CONFIG_ID=test-config-7"; exit 1; fi
	@echo ">>> Creating serving config in DEV with CONFIG_ID=$(CONFIG_ID)..."
	@set -a && . $(DEPLOY_DIR)/config.dev.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$(CONFIG_ID) \
		$(PYTHON_RUN) $(SCRIPT)

create-test: ## Create/update a specific serving config in Test (requires CONFIG_ID)
	@if [ -z "$(CONFIG_ID)" ]; then echo "CONFIG_ID is required. Example: make create-test CONFIG_ID=test-config-7"; exit 1; fi
	@echo ">>> WARNING: This will modify TEST. Press Ctrl+C within 5 seconds to abort."
	@sleep 5
	@echo ">>> Creating serving config in TEST with CONFIG_ID=$(CONFIG_ID)..."
	@set -a && . $(DEPLOY_DIR)/config.test.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$(CONFIG_ID) \
		$(PYTHON_RUN) $(SCRIPT)

create-uat: ## Create/update a specific serving config in UAT (requires CONFIG_ID)
	@if [ -z "$(CONFIG_ID)" ]; then echo "CONFIG_ID is required. Example: make create-uat CONFIG_ID=test-config-7"; exit 1; fi
	@echo ">>> Creating serving config in UAT with CONFIG_ID=$(CONFIG_ID)..."
	@set -a && . $(DEPLOY_DIR)/config.uat.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$(CONFIG_ID) \
		$(PYTHON_RUN) $(SCRIPT)

create-prd: ## Create/update a specific serving config in PRD (requires CONFIG_ID)
	@if [ -z "$(CONFIG_ID)" ]; then echo "CONFIG_ID is required. Example: make create-prd CONFIG_ID=test-config-7"; exit 1; fi
	@echo ">>> WARNING: This will modify PRODUCTION. Press Ctrl+C within 5 seconds to abort."
	@sleep 5
	@echo ">>> Creating serving config in PRD with CONFIG_ID=$(CONFIG_ID)..."
	@set -a && . $(DEPLOY_DIR)/config.prd.env && set +a && \
		PROJECT_ID=$$PROJECT_ID \
		LOCATION=$$LOCATION \
		CATALOG_ID=$$CATALOG_ID \
		AGENT_CONFIG_ID=$(CONFIG_ID) \
		$(PYTHON_RUN) $(SCRIPT)
