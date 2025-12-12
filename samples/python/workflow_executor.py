"""
Workflow execution engine for processing AI agent tasks.

This module provides the core workflow execution logic including
step planning, execution, and result aggregation.
"""

from dataclasses import dataclass
from enum import Enum
from typing import Optional, List, Callable, Any
import asyncio
from datetime import datetime


class StepStatus(Enum):
    """Status of a workflow step."""
    PENDING = "pending"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    FAILED = "failed"
    SKIPPED = "skipped"


@dataclass
class WorkflowStep:
    """Represents a single step in a workflow execution plan."""
    id: str
    title: str
    description: str
    agent_id: str
    status: StepStatus = StepStatus.PENDING
    output: Optional[str] = None
    error: Optional[str] = None
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None


@dataclass
class WorkflowContext:
    """Context passed to each step during execution."""
    workflow_id: str
    repository_path: str
    issue_title: str
    issue_description: str
    previous_outputs: List[str]
    
    def get_combined_context(self) -> str:
        """Combine all previous outputs into a single context string."""
        return "\n\n---\n\n".join(self.previous_outputs)


class WorkflowExecutor:
    """
    Executes workflow steps sequentially with proper error handling.
    
    Each step is executed by its designated agent, with outputs
    accumulated for context in subsequent steps.
    """
    
    def __init__(self, agent_registry: Any, llm_provider: Any):
        """
        Initialize the workflow executor.
        
        Args:
            agent_registry: Registry for looking up agents by ID.
            llm_provider: Provider for LLM completions.
        """
        self.agent_registry = agent_registry
        self.llm_provider = llm_provider
        self._hooks: List[Callable] = []
    
    def register_hook(self, hook: Callable[[WorkflowStep, StepStatus], None]) -> None:
        """Register a callback for step status changes."""
        self._hooks.append(hook)
    
    async def execute_workflow(
        self,
        steps: List[WorkflowStep],
        context: WorkflowContext
    ) -> List[WorkflowStep]:
        """
        Execute all steps in the workflow sequentially.
        
        Args:
            steps: List of steps to execute.
            context: Shared context for all steps.
            
        Returns:
            List of executed steps with updated status and outputs.
        """
        for step in steps:
            try:
                await self._execute_step(step, context)
                if step.output:
                    context.previous_outputs.append(step.output)
            except Exception as e:
                step.status = StepStatus.FAILED
                step.error = str(e)
                self._notify_hooks(step, StepStatus.FAILED)
                break
        
        return steps
    
    async def _execute_step(
        self,
        step: WorkflowStep,
        context: WorkflowContext
    ) -> None:
        """Execute a single workflow step."""
        step.status = StepStatus.IN_PROGRESS
        step.started_at = datetime.utcnow()
        self._notify_hooks(step, StepStatus.IN_PROGRESS)
        
        agent = self.agent_registry.get(step.agent_id)
        if not agent:
            raise ValueError(f"Agent not found: {step.agent_id}")
        
        prompt = self._build_prompt(step, context)
        response = await self.llm_provider.generate(prompt)
        
        step.output = response
        step.status = StepStatus.COMPLETED
        step.completed_at = datetime.utcnow()
        self._notify_hooks(step, StepStatus.COMPLETED)
    
    def _build_prompt(self, step: WorkflowStep, context: WorkflowContext) -> str:
        """Build the prompt for a step execution."""
        return f"""
Task: {step.title}

Description: {step.description}

Repository: {context.repository_path}

Issue: {context.issue_title}
{context.issue_description}

Previous Context:
{context.get_combined_context()}

Please complete this step.
"""
    
    def _notify_hooks(self, step: WorkflowStep, status: StepStatus) -> None:
        """Notify all registered hooks of a status change."""
        for hook in self._hooks:
            try:
                hook(step, status)
            except Exception:
                pass  # Don't let hook errors break execution
