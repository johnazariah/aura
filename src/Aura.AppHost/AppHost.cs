// Aura AppHost - Aspire Orchestration
// One-click startup for all Aura services

using Aura.Foundation;

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector extension for RAG embeddings
// Using pgvector/pgvector image which has the extension pre-installed
// Fixed port 5432 ensures dev and production use the same database
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithImage("pgvector/pgvector", "pg17")
    .WithContainerName("aura-postgres")
    .WithDataVolume("aura-postgres-data")
    .WithHostPort(5432)
    .WithPgAdmin();

var auraDb = postgres.AddDatabase(ResourceNames.AuraDb);

// Ollama is external (user runs it separately for now)
// Future: Could add container orchestration for Ollama

// Aura API - the main service
var api = builder.AddProject<Projects.Aura_Api>(ResourceNames.AuraApi)
    .WithReference(auraDb)
    .WaitFor(auraDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
