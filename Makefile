.PHONY: help dev test coverage lint typecheck check build docker run infra-up infra-down clean sonar

help:
	@echo "Job Service C# — available targets:"
	@echo "  make dev          Run with file watcher (hot reload)"
	@echo "  make test         Run unit tests"
	@echo "  make coverage     Run tests with coverage report"
	@echo "  make lint         Lint C# code (dotnet format --verify-no-changes)"
	@echo "  make typecheck    Run mypy-equivalent (no-op; C# is compiled)"
	@echo "  make check        Run lint + test + coverage gate"
	@echo "  make build        Build the project (Release)"
	@echo "  make docker       Build Docker image"
	@echo "  make run          Run the application"
	@echo "  make infra-up     Start SQL Server + Redis + RabbitMQ via docker compose"
	@echo "  make infra-down   Stop the dev infrastructure"
	@echo "  make sonar        Run SonarQube scan"
	@echo "  make clean        Remove build artifacts"

dev:
	DOTNET_USE_POLLING_FILE_WATCHER=1 dotnet watch --project src/MageBackend.csproj run

test:
	dotnet test tests/MageBackend.Tests.csproj --logger "console;verbosity=normal"

coverage:
	dotnet test tests/MageBackend.Tests.csproj --collect:"XPlat Code Coverage" --logger "console;verbosity=minimal" --results-directory ./TestResults
	@echo ""
	@echo "Coverage report: tests/TestResults/*/coverage.cobertura.xml"

lint:
	dotnet format src/MageBackend.csproj --verify-no-changes --no-restore

typecheck:
	@echo "(C# is compiled; no separate typecheck step)"

check: lint test coverage
	@echo "✅ All checks passed"

build:
	dotnet build src/MageBackend.csproj -c Release

docker:
	docker build -t job-service-c-sharp:latest .

run:
	dotnet run --project src/MageBackend.csproj

infra-up:
	docker compose -f docker-compose.infra.yml up -d

infra-down:
	docker compose -f docker-compose.infra.yml down

sonar:
	./scripts/sonar-scan.sh "job-service-c-sharp" "job-service-c-sharp"

clean:
	rm -rf src/bin src/obj tests/bin tests/obj tests/TestResults
