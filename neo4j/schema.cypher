CREATE CONSTRAINT semantic_node_identity IF NOT EXISTS
FOR (n:SemanticNode)
REQUIRE (n.tenantId, n.userId, n.type, n.normalizedKey) IS UNIQUE;

CREATE INDEX semantic_node_search IF NOT EXISTS
FOR (n:SemanticNode)
ON (n.tenantId, n.userId, n.status, n.canonicalName);

CREATE INDEX semantic_edge_identity IF NOT EXISTS
FOR ()-[r:SEMANTIC_EDGE]-()
ON (r.tenantId, r.userId, r.status, r.relationType);
