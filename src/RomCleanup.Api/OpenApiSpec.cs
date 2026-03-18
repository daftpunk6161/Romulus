namespace RomCleanup.Api;

/// <summary>
/// Embedded OpenAPI 3.0.3 specification for the ROM Cleanup API.
/// </summary>
public static class OpenApiSpec
{
    public const string Json = """
{
  "openapi": "3.0.3",
  "info": {
    "title": "ROM Cleanup API",
    "version": "1.0.0",
    "description": "REST API for ROM collection management: region deduplication, junk removal, format conversion."
  },
  "servers": [{ "url": "http://127.0.0.1:7878" }],
  "paths": {
    "/health": {
      "get": {
        "summary": "Health check",
        "responses": {
          "200": { "description": "Server status" }
        }
      }
    },
    "/runs": {
      "post": {
        "summary": "Create and execute a deduplication run",
        "parameters": [
          { "name": "wait", "in": "query", "schema": { "type": "boolean" }, "description": "Wait for completion without cancelling the server-side run on client disconnect" },
          { "name": "waitTimeoutMs", "in": "query", "schema": { "type": "integer", "minimum": 1, "maximum": 1800000 }, "description": "Maximum wait time before returning 202 with the current run state" },
          { "name": "X-Idempotency-Key", "in": "header", "schema": { "type": "string" }, "description": "Reuse the same run for retries of the same request" },
          { "name": "X-Client-Id", "in": "header", "schema": { "type": "string", "maxLength": 64 }, "description": "Optional logical client binding used for run ownership checks" }
        ],
        "responses": {
          "202": { "description": "Run created (async)" },
          "200": { "description": "Run completed or reused" },
          "400": { "description": "Validation error" },
          "403": { "description": "Run belongs to another client" },
          "409": { "description": "Run conflict" }
        }
      }
    },
    "/runs/{runId}": {
      "get": {
        "summary": "Get run status",
        "responses": {
          "200": { "description": "Run status" },
          "403": { "description": "Run belongs to another client" },
          "404": { "description": "Run not found" }
        }
      }
    },
    "/runs/{runId}/result": {
      "get": {
        "summary": "Get completed run result",
        "responses": {
          "200": { "description": "Full result" },
          "403": { "description": "Run belongs to another client" },
          "404": { "description": "Run not found" },
          "409": { "description": "Run still in progress" }
        }
      }
    },
    "/runs/{runId}/cancel": {
      "post": {
        "summary": "Cancel a run idempotently",
        "responses": {
          "200": { "description": "Cancel accepted or no-op for an already terminal run" },
          "403": { "description": "Run belongs to another client" },
          "404": { "description": "Run not found" }
        }
      }
    },
    "/runs/{runId}/stream": {
      "get": {
        "summary": "SSE progress stream",
        "responses": {
          "200": { "description": "Server-Sent Events stream" },
          "403": { "description": "Run belongs to another client" },
          "404": { "description": "Run not found" }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "OperationError": {
        "type": "object",
        "properties": {
          "code": { "type": "string" },
          "message": { "type": "string" },
          "kind": { "type": "string", "enum": ["Transient", "Recoverable", "Critical"] },
          "module": { "type": "string", "nullable": true }
        }
      },
      "ApiRunResult": {
        "type": "object",
        "properties": {
          "preflightWarnings": { "type": "array", "items": { "type": "string" } },
          "phaseMetrics": { "type": "object" },
          "dedupeGroups": { "type": "array", "items": { "type": "object" } },
          "error": { "$ref": "#/components/schemas/OperationError" },
          "conflictPolicy": { "type": "string", "enum": ["Rename", "Skip", "Overwrite"] },
          "convertOnly": { "type": "boolean" },
          "convertFormat": { "type": "string", "enum": ["auto", "chd", "rvz", "zip", "7z"] }
        }
      }
    },
    "securitySchemes": {
      "ApiKey": {
        "type": "apiKey",
        "in": "header",
        "name": "X-Api-Key"
      }
    }
  },
  "security": [{ "ApiKey": [] }]
}
""";
}
