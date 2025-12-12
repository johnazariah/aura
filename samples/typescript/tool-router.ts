/**
 * Tool Router for dispatching tool calls to their implementations.
 *
 * This module provides a central router for AI agent tool calls,
 * with support for validation, logging, and error handling.
 */

/**
 * Definition of a tool parameter.
 */
export interface ToolParameter {
  name: string;
  type: "string" | "number" | "boolean" | "object" | "array";
  description: string;
  required: boolean;
  default?: unknown;
}

/**
 * Definition of a tool that can be called by agents.
 */
export interface ToolDefinition {
  name: string;
  description: string;
  parameters: ToolParameter[];
  handler: ToolHandler;
}

/**
 * Input to a tool execution.
 */
export interface ToolInput {
  toolName: string;
  parameters: Record<string, unknown>;
  context?: ToolContext;
}

/**
 * Context passed to tool handlers.
 */
export interface ToolContext {
  workflowId?: string;
  stepId?: string;
  workspacePath?: string;
  userId?: string;
}

/**
 * Result of a tool execution.
 */
export interface ToolResult {
  success: boolean;
  output?: string;
  error?: string;
  metadata?: Record<string, unknown>;
}

/**
 * Handler function for a tool.
 */
export type ToolHandler = (
  params: Record<string, unknown>,
  context?: ToolContext
) => Promise<ToolResult>;

/**
 * Options for the tool router.
 */
export interface ToolRouterOptions {
  validateParameters?: boolean;
  logExecutions?: boolean;
  timeoutMs?: number;
}

/**
 * Central router for dispatching tool calls to implementations.
 *
 * @example
 * ```typescript
 * const router = new ToolRouter({ validateParameters: true });
 *
 * router.register({
 *   name: "file.read",
 *   description: "Read contents of a file",
 *   parameters: [
 *     { name: "path", type: "string", description: "File path", required: true }
 *   ],
 *   handler: async (params) => {
 *     const content = await fs.readFile(params.path as string, "utf-8");
 *     return { success: true, output: content };
 *   }
 * });
 *
 * const result = await router.execute({
 *   toolName: "file.read",
 *   parameters: { path: "./README.md" }
 * });
 * ```
 */
export class ToolRouter {
  private tools: Map<string, ToolDefinition> = new Map();
  private options: Required<ToolRouterOptions>;

  constructor(options: ToolRouterOptions = {}) {
    this.options = {
      validateParameters: options.validateParameters ?? true,
      logExecutions: options.logExecutions ?? true,
      timeoutMs: options.timeoutMs ?? 30000,
    };
  }

  /**
   * Register a new tool with the router.
   */
  register(tool: ToolDefinition): void {
    if (this.tools.has(tool.name)) {
      throw new Error(`Tool already registered: ${tool.name}`);
    }
    this.tools.set(tool.name, tool);
  }

  /**
   * Unregister a tool from the router.
   */
  unregister(name: string): boolean {
    return this.tools.delete(name);
  }

  /**
   * Execute a tool by name with the given parameters.
   */
  async execute(input: ToolInput): Promise<ToolResult> {
    const tool = this.tools.get(input.toolName);

    if (!tool) {
      return {
        success: false,
        error: `Tool not found: ${input.toolName}`,
      };
    }

    // Validate parameters if enabled
    if (this.options.validateParameters) {
      const validationError = this.validateParameters(tool, input.parameters);
      if (validationError) {
        return {
          success: false,
          error: validationError,
        };
      }
    }

    // Log execution if enabled
    if (this.options.logExecutions) {
      console.log(`[ToolRouter] Executing ${input.toolName}`, {
        parameters: this.sanitizeForLogging(input.parameters),
        context: input.context,
      });
    }

    // Execute with timeout
    try {
      const result = await this.executeWithTimeout(
        tool.handler(input.parameters, input.context),
        this.options.timeoutMs
      );

      if (this.options.logExecutions) {
        console.log(`[ToolRouter] ${input.toolName} completed`, {
          success: result.success,
        });
      }

      return result;
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error";

      if (this.options.logExecutions) {
        console.error(`[ToolRouter] ${input.toolName} failed`, {
          error: errorMessage,
        });
      }

      return {
        success: false,
        error: errorMessage,
      };
    }
  }

  /**
   * Get all registered tool definitions.
   */
  getTools(): ToolDefinition[] {
    return Array.from(this.tools.values());
  }

  /**
   * Get a specific tool definition by name.
   */
  getTool(name: string): ToolDefinition | undefined {
    return this.tools.get(name);
  }

  /**
   * Get tool definitions formatted for LLM function calling.
   */
  getToolsForLlm(): Array<{
    name: string;
    description: string;
    parameters: {
      type: "object";
      properties: Record<string, unknown>;
      required: string[];
    };
  }> {
    return this.getTools().map((tool) => ({
      name: tool.name,
      description: tool.description,
      parameters: {
        type: "object" as const,
        properties: Object.fromEntries(
          tool.parameters.map((p) => [
            p.name,
            {
              type: p.type,
              description: p.description,
            },
          ])
        ),
        required: tool.parameters.filter((p) => p.required).map((p) => p.name),
      },
    }));
  }

  /**
   * Validate parameters against tool definition.
   */
  private validateParameters(
    tool: ToolDefinition,
    params: Record<string, unknown>
  ): string | null {
    for (const param of tool.parameters) {
      const value = params[param.name];

      if (param.required && value === undefined) {
        return `Missing required parameter: ${param.name}`;
      }

      if (value !== undefined && !this.checkType(value, param.type)) {
        return `Invalid type for ${param.name}: expected ${param.type}`;
      }
    }

    return null;
  }

  /**
   * Check if a value matches the expected type.
   */
  private checkType(value: unknown, expectedType: string): boolean {
    switch (expectedType) {
      case "string":
        return typeof value === "string";
      case "number":
        return typeof value === "number";
      case "boolean":
        return typeof value === "boolean";
      case "object":
        return typeof value === "object" && value !== null && !Array.isArray(value);
      case "array":
        return Array.isArray(value);
      default:
        return true;
    }
  }

  /**
   * Execute a promise with a timeout.
   */
  private async executeWithTimeout<T>(
    promise: Promise<T>,
    timeoutMs: number
  ): Promise<T> {
    const timeoutPromise = new Promise<never>((_, reject) => {
      setTimeout(() => reject(new Error("Tool execution timed out")), timeoutMs);
    });

    return Promise.race([promise, timeoutPromise]);
  }

  /**
   * Sanitize parameters for logging (hide sensitive data).
   */
  private sanitizeForLogging(
    params: Record<string, unknown>
  ): Record<string, unknown> {
    const sensitiveKeys = ["password", "apiKey", "token", "secret"];
    const sanitized: Record<string, unknown> = {};

    for (const [key, value] of Object.entries(params)) {
      if (sensitiveKeys.some((k) => key.toLowerCase().includes(k))) {
        sanitized[key] = "[REDACTED]";
      } else {
        sanitized[key] = value;
      }
    }

    return sanitized;
  }
}
