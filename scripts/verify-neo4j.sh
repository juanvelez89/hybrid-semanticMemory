#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${NEO4J_URI:-}" || -z "${NEO4J_PASSWORD:-}" ]]; then
  echo "NEO4J_URI and NEO4J_PASSWORD are required."
  exit 1
fi

if [[ -z "${NEO4J_USERNAME:-}" && -z "${NEO4J_USER:-}" ]]; then
  echo "NEO4J_USERNAME or NEO4J_USER is required."
  exit 1
fi

export NEO4J_DATABASE="${NEO4J_DATABASE:-neo4j}"

dotnet test tests/SemanticMemory.IntegrationTests/SemanticMemory.IntegrationTests.csproj \
  --filter "FullyQualifiedName~Neo4jAuraTests" \
  --logger "console;verbosity=minimal"
