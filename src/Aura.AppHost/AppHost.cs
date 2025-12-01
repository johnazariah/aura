// Aura AppHost - Aspire Orchestration
// One-click startup for all Aura services

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector extension for RAG embeddings
// Using pgvector/pgvector image which has the extension pre-installed
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithDataVolume("aura-postgres-data")
    .WithPgAdmin();

var auraDb = postgres.AddDatabase("auradb");

// Ollama is external (user runs it separately for now)
// Future: Could add container orchestration for Ollama

// Aura API - the main service
var api = builder.AddProject<Projects.Aura_Api>("aura-api")
    .WithReference(auraDb)
    .WaitFor(auraDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
