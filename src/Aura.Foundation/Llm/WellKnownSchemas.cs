using System.Text.Json;

namespace Aura.Foundation.Llm;

/// <summary>
/// Common JSON schemas for structured LLM output.
/// These schemas can be used with providers that support schema enforcement
/// (OpenAI, Azure OpenAI) to guarantee valid structured output.
/// </summary>
public static class WellKnownSchemas
{
    /// <summary>
    /// Schema for ReAct agent responses.
    /// Defines the thought-action-input structure used by the ReAct executor.
    /// </summary>
    public static JsonSchema ReActResponse { get; } = new(
        Name: "react_response",
        Schema: JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "thought": { 
                    "type": "string",
                    "description": "The reasoning step explaining the agent's thinking"
                },
                "action": { 
                    "type": "string",
                    "description": "The action to take (tool name or 'finish')"
                },
                "action_input": { 
                    "type": "object",
                    "description": "Parameters for the action (tool arguments or final answer)"
                }
            },
            "required": ["thought", "action"],
            "additionalProperties": false
        }
        """).RootElement,
        Description: "ReAct agent response with thought, action, and parameters",
        Strict: true
    );

    /// <summary>
    /// Schema for workflow plan responses.
    /// Defines the structure for multi-step workflow planning.
    /// </summary>
    public static JsonSchema WorkflowPlan { get; } = new(
        Name: "workflow_plan",
        Schema: JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "steps": {
                    "type": "array",
                    "description": "List of workflow steps to execute in order",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": { 
                                "type": "string",
                                "description": "Short name for the step (e.g. 'Implement UserService')"
                            },
                            "capability": { 
                                "type": "string",
                                "description": "The capability needed: coding, review, documentation, analysis, fixing, or enrichment"
                            },
                            "language": { 
                                "type": "string",
                                "description": "Programming language if applicable (e.g. 'csharp', 'python', 'typescript')"
                            },
                            "description": { 
                                "type": "string",
                                "description": "Detailed description of what this step should accomplish"
                            }
                        },
                        "required": ["name", "capability", "language", "description"],
                        "additionalProperties": false
                    }
                }
            },
            "required": ["steps"],
            "additionalProperties": false
        }
        """).RootElement,
        Description: "Multi-step workflow plan with capability-based steps",
        Strict: true
    );

    /// <summary>
    /// Schema for code modification responses.
    /// Defines the structure for file edit operations.
    /// </summary>
    public static JsonSchema CodeModification { get; } = new(
        Name: "code_modification",
        Schema: JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "files": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "path": { "type": "string" },
                            "operation": { 
                                "type": "string",
                                "enum": ["create", "modify", "delete"]
                            },
                            "content": { "type": "string" },
                            "searchReplace": {
                                "type": "array",
                                "items": {
                                    "type": "object",
                                    "properties": {
                                        "search": { "type": "string" },
                                        "replace": { "type": "string" }
                                    },
                                    "required": ["search", "replace"]
                                }
                            }
                        },
                        "required": ["path", "operation"]
                    }
                },
                "explanation": {
                    "type": "string",
                    "description": "Explanation of the changes being made"
                }
            },
            "required": ["files", "explanation"],
            "additionalProperties": false
        }
        """).RootElement,
        Description: "Code modification with file operations",
        Strict: true
    );
}
