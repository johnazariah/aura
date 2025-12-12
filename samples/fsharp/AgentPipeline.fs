/// Module for composing and executing AI agent pipelines.
///
/// This module provides a functional approach to building agent workflows
/// using railway-oriented programming for error handling.
module Aura.Samples.AgentPipeline

open System
open System.Threading.Tasks

// ============================================================================
// Domain Types
// ============================================================================

/// Represents the result of an operation that can fail.
type Result<'T, 'E> =
    | Success of 'T
    | Failure of 'E

/// Error types that can occur during agent execution.
type AgentError =
    | AgentNotFound of agentId: string
    | ExecutionFailed of message: string
    | Timeout of duration: TimeSpan
    | InvalidInput of field: string * reason: string
    | ProviderUnavailable of providerId: string

/// A message in a conversation.
type ChatMessage = 
    { Role: string
      Content: string }

/// Context passed through the agent pipeline.
type PipelineContext =
    { WorkflowId: string
      RepositoryPath: string
      Messages: ChatMessage list
      Metadata: Map<string, string>
      StartedAt: DateTime }

/// Output from a single agent execution.
type AgentOutput =
    { AgentId: string
      Content: string
      TokensUsed: int
      Duration: TimeSpan }

/// Definition of an agent's capabilities and configuration.
type AgentDefinition =
    { Id: string
      Name: string
      SystemPrompt: string
      Model: string option
      Temperature: float
      Capabilities: string list }

// ============================================================================
// Railway-Oriented Programming Helpers
// ============================================================================

/// Bind operation for Result - enables railway-oriented programming.
let bind f result =
    match result with
    | Success x -> f x
    | Failure e -> Failure e

/// Map operation for Result.
let map f result =
    match result with
    | Success x -> Success (f x)
    | Failure e -> Failure e

/// Apply a function that might fail.
let (>>=) result f = bind f result

/// Pipe success values through a function.
let (<!>) result f = map f result

/// Convert an option to a Result.
let ofOption error opt =
    match opt with
    | Some x -> Success x
    | None -> Failure error

/// Combine two Results, keeping both values on success.
let combine r1 r2 =
    match r1, r2 with
    | Success x, Success y -> Success (x, y)
    | Failure e, _ -> Failure e
    | _, Failure e -> Failure e

// ============================================================================
// Agent Registry
// ============================================================================

/// Thread-safe registry for agent definitions.
type AgentRegistry() =
    let mutable agents = Map.empty<string, AgentDefinition>
    let lockObj = obj()
    
    /// Register a new agent definition.
    member _.Register(agent: AgentDefinition) =
        lock lockObj (fun () ->
            agents <- agents |> Map.add agent.Id agent)
    
    /// Get an agent by ID.
    member _.TryGet(agentId: string) : AgentDefinition option =
        agents |> Map.tryFind agentId
    
    /// Get an agent, returning Result for railway composition.
    member this.Get(agentId: string) : Result<AgentDefinition, AgentError> =
        this.TryGet(agentId)
        |> ofOption (AgentNotFound agentId)
    
    /// Find agents with a specific capability.
    member _.FindByCapability(capability: string) : AgentDefinition list =
        agents
        |> Map.toList
        |> List.map snd
        |> List.filter (fun a -> a.Capabilities |> List.contains capability)
        |> List.sortBy (fun a -> a.Id)
    
    /// List all registered agent IDs.
    member _.ListIds() : string list =
        agents |> Map.toList |> List.map fst

// ============================================================================
// Pipeline Combinators
// ============================================================================

/// A pipeline step that transforms context and produces output.
type PipelineStep = PipelineContext -> Task<Result<PipelineContext * AgentOutput, AgentError>>

/// Create a step that executes an agent.
let executeAgent (registry: AgentRegistry) (llmCall: AgentDefinition -> ChatMessage list -> Task<Result<string, AgentError>>) (agentId: string) : PipelineStep =
    fun ctx -> task {
        match registry.Get(agentId) with
        | Failure e -> return Failure e
        | Success agent ->
            let startTime = DateTime.UtcNow
            let messages = 
                { Role = "system"; Content = agent.SystemPrompt } :: ctx.Messages
            
            match! llmCall agent messages with
            | Failure e -> return Failure e
            | Success content ->
                let output = 
                    { AgentId = agentId
                      Content = content
                      TokensUsed = content.Length / 4  // Rough estimate
                      Duration = DateTime.UtcNow - startTime }
                
                let newCtx = 
                    { ctx with 
                        Messages = ctx.Messages @ [{ Role = "assistant"; Content = content }] }
                
                return Success (newCtx, output)
    }

/// Compose two pipeline steps sequentially.
let andThen (step2: PipelineStep) (step1: PipelineStep) : PipelineStep =
    fun ctx -> task {
        match! step1 ctx with
        | Failure e -> return Failure e
        | Success (ctx1, output1) ->
            match! step2 ctx1 with
            | Failure e -> return Failure e
            | Success (ctx2, output2) ->
                // Combine outputs by keeping the latest context
                return Success (ctx2, output2)
    }

/// Compose steps using the fish operator (Kleisli composition).
let (>=>) step1 step2 = andThen step2 step1

/// Run a step only if a condition is met.
let conditional (predicate: PipelineContext -> bool) (step: PipelineStep) : PipelineStep =
    fun ctx -> task {
        if predicate ctx then
            return! step ctx
        else
            // Skip with empty output
            let output = 
                { AgentId = "skipped"
                  Content = ""
                  TokensUsed = 0
                  Duration = TimeSpan.Zero }
            return Success (ctx, output)
    }

/// Try a step, falling back to another on failure.
let orElse (fallback: PipelineStep) (primary: PipelineStep) : PipelineStep =
    fun ctx -> task {
        match! primary ctx with
        | Success result -> return Success result
        | Failure _ -> return! fallback ctx
    }

/// Retry a step up to n times on failure.
let retry (maxAttempts: int) (step: PipelineStep) : PipelineStep =
    let rec loop attempt ctx = task {
        match! step ctx with
        | Success result -> return Success result
        | Failure e when attempt < maxAttempts ->
            do! Task.Delay(TimeSpan.FromSeconds(float attempt))
            return! loop (attempt + 1) ctx
        | Failure e -> return Failure e
    }
    loop 1

// ============================================================================
// Pipeline Builder (Computation Expression)
// ============================================================================

/// Builder for creating agent pipelines using computation expressions.
type PipelineBuilder() =
    member _.Bind(step: PipelineStep, f: PipelineContext * AgentOutput -> PipelineStep) : PipelineStep =
        fun ctx -> task {
            match! step ctx with
            | Failure e -> return Failure e
            | Success (newCtx, output) -> return! f (newCtx, output) newCtx
        }
    
    member _.Return(value: PipelineContext * AgentOutput) : PipelineStep =
        fun _ -> Task.FromResult(Success value)
    
    member _.ReturnFrom(step: PipelineStep) : PipelineStep = step
    
    member _.Zero() : PipelineStep =
        fun ctx -> 
            let output = { AgentId = ""; Content = ""; TokensUsed = 0; Duration = TimeSpan.Zero }
            Task.FromResult(Success (ctx, output))

/// Pipeline computation expression.
let pipeline = PipelineBuilder()

// ============================================================================
// Example Usage
// ============================================================================

/// Example: Build a code review pipeline.
let buildCodeReviewPipeline (registry: AgentRegistry) llmCall =
    let analyze = executeAgent registry llmCall "code-analyzer"
    let review = executeAgent registry llmCall "code-reviewer"  
    let summarize = executeAgent registry llmCall "summarizer"
    
    // Compose the pipeline
    analyze >=> review >=> summarize

/// Example: Build a conditional pipeline with fallback.
let buildSmartPipeline (registry: AgentRegistry) llmCall =
    let primary = executeAgent registry llmCall "gpt-4-agent"
    let fallback = executeAgent registry llmCall "local-agent"
    let needsReview ctx = ctx.Messages.Length > 5
    let review = executeAgent registry llmCall "reviewer"
    
    // Try primary, fall back to local, optionally review
    (primary |> orElse fallback) 
    >=> (review |> conditional needsReview)
