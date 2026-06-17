#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${POSTGRES_CONNECTION_STRING:-}" ]]; then
  echo "POSTGRES_CONNECTION_STRING is required."
  exit 1
fi

dotnet test tests/SemanticMemory.IntegrationTests/SemanticMemory.IntegrationTests.csproj \
  --filter "FullyQualifiedName~PostgresSupabaseTests" \
  --logger "console;verbosity=minimal"
