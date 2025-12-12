//! Agent Registry for managing AI agent definitions and instances.
//!
//! This module provides a thread-safe registry for storing and retrieving
//! agent definitions, with support for hot-reloading and capability-based lookup.

use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use thiserror::Error;

/// Errors that can occur during agent registry operations.
#[derive(Error, Debug)]
pub enum RegistryError {
    #[error("Agent not found: {0}")]
    AgentNotFound(String),
    
    #[error("Agent already exists: {0}")]
    AgentAlreadyExists(String),
    
    #[error("Invalid agent definition: {0}")]
    InvalidDefinition(String),
    
    #[error("Lock poisoned")]
    LockPoisoned,
}

/// Capabilities that an agent can provide.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub enum Capability {
    Coding,
    Testing,
    Review,
    Documentation,
    Planning,
    Research,
    Custom(String),
}

/// Metadata for an agent definition.
#[derive(Debug, Clone)]
pub struct AgentMetadata {
    pub id: String,
    pub name: String,
    pub description: String,
    pub priority: u32,
    pub capabilities: Vec<Capability>,
    pub languages: Vec<String>,
    pub model: Option<String>,
    pub temperature: f32,
}

/// A complete agent definition including system prompt.
#[derive(Debug, Clone)]
pub struct AgentDefinition {
    pub metadata: AgentMetadata,
    pub system_prompt: String,
    pub tools: Vec<String>,
}

impl AgentDefinition {
    /// Create a new agent definition with required fields.
    pub fn new(id: impl Into<String>, name: impl Into<String>, system_prompt: impl Into<String>) -> Self {
        Self {
            metadata: AgentMetadata {
                id: id.into(),
                name: name.into(),
                description: String::new(),
                priority: 50,
                capabilities: Vec::new(),
                languages: Vec::new(),
                model: None,
                temperature: 0.7,
            },
            system_prompt: system_prompt.into(),
            tools: Vec::new(),
        }
    }
    
    /// Builder method to add capabilities.
    pub fn with_capabilities(mut self, caps: Vec<Capability>) -> Self {
        self.metadata.capabilities = caps;
        self
    }
    
    /// Builder method to set priority.
    pub fn with_priority(mut self, priority: u32) -> Self {
        self.metadata.priority = priority;
        self
    }
    
    /// Check if this agent has a specific capability.
    pub fn has_capability(&self, cap: &Capability) -> bool {
        self.metadata.capabilities.contains(cap)
    }
}

/// Thread-safe registry for agent definitions.
pub struct AgentRegistry {
    agents: Arc<RwLock<HashMap<String, AgentDefinition>>>,
    capability_index: Arc<RwLock<HashMap<Capability, Vec<String>>>>,
}

impl AgentRegistry {
    /// Create a new empty registry.
    pub fn new() -> Self {
        Self {
            agents: Arc::new(RwLock::new(HashMap::new())),
            capability_index: Arc::new(RwLock::new(HashMap::new())),
        }
    }
    
    /// Register a new agent definition.
    pub fn register(&self, agent: AgentDefinition) -> Result<(), RegistryError> {
        let id = agent.metadata.id.clone();
        let capabilities = agent.metadata.capabilities.clone();
        
        // Insert into main registry
        {
            let mut agents = self.agents.write().map_err(|_| RegistryError::LockPoisoned)?;
            if agents.contains_key(&id) {
                return Err(RegistryError::AgentAlreadyExists(id));
            }
            agents.insert(id.clone(), agent);
        }
        
        // Update capability index
        {
            let mut index = self.capability_index.write().map_err(|_| RegistryError::LockPoisoned)?;
            for cap in capabilities {
                index.entry(cap).or_insert_with(Vec::new).push(id.clone());
            }
        }
        
        Ok(())
    }
    
    /// Get an agent by ID.
    pub fn get(&self, id: &str) -> Result<AgentDefinition, RegistryError> {
        let agents = self.agents.read().map_err(|_| RegistryError::LockPoisoned)?;
        agents.get(id).cloned().ok_or_else(|| RegistryError::AgentNotFound(id.to_string()))
    }
    
    /// Find agents with a specific capability, ordered by priority.
    pub fn find_by_capability(&self, cap: &Capability) -> Result<Vec<AgentDefinition>, RegistryError> {
        let index = self.capability_index.read().map_err(|_| RegistryError::LockPoisoned)?;
        let agents = self.agents.read().map_err(|_| RegistryError::LockPoisoned)?;
        
        let ids = index.get(cap).cloned().unwrap_or_default();
        let mut result: Vec<_> = ids
            .iter()
            .filter_map(|id| agents.get(id).cloned())
            .collect();
        
        // Sort by priority (lower = higher priority)
        result.sort_by_key(|a| a.metadata.priority);
        
        Ok(result)
    }
    
    /// Get the best agent for a capability (highest priority).
    pub fn get_best_for_capability(&self, cap: &Capability) -> Result<AgentDefinition, RegistryError> {
        self.find_by_capability(cap)?
            .into_iter()
            .next()
            .ok_or_else(|| RegistryError::AgentNotFound(format!("No agent with capability {:?}", cap)))
    }
    
    /// Remove an agent from the registry.
    pub fn unregister(&self, id: &str) -> Result<AgentDefinition, RegistryError> {
        let agent = {
            let mut agents = self.agents.write().map_err(|_| RegistryError::LockPoisoned)?;
            agents.remove(id).ok_or_else(|| RegistryError::AgentNotFound(id.to_string()))?
        };
        
        // Remove from capability index
        {
            let mut index = self.capability_index.write().map_err(|_| RegistryError::LockPoisoned)?;
            for cap in &agent.metadata.capabilities {
                if let Some(ids) = index.get_mut(cap) {
                    ids.retain(|i| i != id);
                }
            }
        }
        
        Ok(agent)
    }
    
    /// List all registered agent IDs.
    pub fn list_ids(&self) -> Result<Vec<String>, RegistryError> {
        let agents = self.agents.read().map_err(|_| RegistryError::LockPoisoned)?;
        Ok(agents.keys().cloned().collect())
    }
    
    /// Get the count of registered agents.
    pub fn count(&self) -> Result<usize, RegistryError> {
        let agents = self.agents.read().map_err(|_| RegistryError::LockPoisoned)?;
        Ok(agents.len())
    }
}

impl Default for AgentRegistry {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    
    #[test]
    fn test_register_and_get() {
        let registry = AgentRegistry::new();
        let agent = AgentDefinition::new("test-agent", "Test Agent", "You are a test agent.");
        
        registry.register(agent).unwrap();
        
        let retrieved = registry.get("test-agent").unwrap();
        assert_eq!(retrieved.metadata.id, "test-agent");
    }
    
    #[test]
    fn test_capability_lookup() {
        let registry = AgentRegistry::new();
        
        let coder = AgentDefinition::new("coder", "Coder", "You write code.")
            .with_capabilities(vec![Capability::Coding])
            .with_priority(10);
        
        let reviewer = AgentDefinition::new("reviewer", "Reviewer", "You review code.")
            .with_capabilities(vec![Capability::Review, Capability::Coding])
            .with_priority(20);
        
        registry.register(coder).unwrap();
        registry.register(reviewer).unwrap();
        
        let coders = registry.find_by_capability(&Capability::Coding).unwrap();
        assert_eq!(coders.len(), 2);
        assert_eq!(coders[0].metadata.id, "coder"); // Lower priority = first
    }
}
