// Package llm provides abstractions for Large Language Model providers.
//
// This package defines interfaces and implementations for interacting with
// various LLM backends including OpenAI, Azure OpenAI, and Ollama.
package llm

import (
	"context"
	"errors"
	"sync"
	"time"
)

// Common errors returned by LLM providers.
var (
	ErrProviderNotFound  = errors.New("provider not found")
	ErrModelNotAvailable = errors.New("model not available")
	ErrRateLimited       = errors.New("rate limited")
	ErrContextCanceled   = errors.New("context canceled")
	ErrInvalidResponse   = errors.New("invalid response from provider")
)

// Message represents a single message in a chat conversation.
type Message struct {
	Role    string `json:"role"`    // "system", "user", or "assistant"
	Content string `json:"content"` // The message content
}

// ChatRequest contains parameters for a chat completion request.
type ChatRequest struct {
	Model       string    `json:"model"`
	Messages    []Message `json:"messages"`
	Temperature float64   `json:"temperature,omitempty"`
	MaxTokens   int       `json:"max_tokens,omitempty"`
}

// ChatResponse contains the result of a chat completion.
type ChatResponse struct {
	Content      string        `json:"content"`
	Model        string        `json:"model"`
	FinishReason string        `json:"finish_reason"`
	Usage        *UsageStats   `json:"usage,omitempty"`
	Latency      time.Duration `json:"-"`
}

// UsageStats tracks token usage for a request.
type UsageStats struct {
	PromptTokens     int `json:"prompt_tokens"`
	CompletionTokens int `json:"completion_tokens"`
	TotalTokens      int `json:"total_tokens"`
}

// Provider defines the interface for LLM providers.
type Provider interface {
	// ID returns the unique identifier for this provider.
	ID() string

	// Chat sends a chat completion request and returns the response.
	Chat(ctx context.Context, req *ChatRequest) (*ChatResponse, error)

	// IsModelAvailable checks if a model is available on this provider.
	IsModelAvailable(ctx context.Context, model string) (bool, error)

	// ListModels returns all available models on this provider.
	ListModels(ctx context.Context) ([]string, error)
}

// ProviderRegistry manages multiple LLM providers with fallback support.
type ProviderRegistry struct {
	mu        sync.RWMutex
	providers map[string]Provider
	defaultID string
}

// NewProviderRegistry creates a new provider registry.
func NewProviderRegistry() *ProviderRegistry {
	return &ProviderRegistry{
		providers: make(map[string]Provider),
	}
}

// Register adds a provider to the registry.
func (r *ProviderRegistry) Register(provider Provider) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.providers[provider.ID()] = provider
}

// SetDefault sets the default provider by ID.
func (r *ProviderRegistry) SetDefault(id string) error {
	r.mu.Lock()
	defer r.mu.Unlock()

	if _, ok := r.providers[id]; !ok {
		return ErrProviderNotFound
	}
	r.defaultID = id
	return nil
}

// Get retrieves a provider by ID.
func (r *ProviderRegistry) Get(id string) (Provider, error) {
	r.mu.RLock()
	defer r.mu.RUnlock()

	provider, ok := r.providers[id]
	if !ok {
		return nil, ErrProviderNotFound
	}
	return provider, nil
}

// GetDefault retrieves the default provider.
func (r *ProviderRegistry) GetDefault() (Provider, error) {
	r.mu.RLock()
	defer r.mu.RUnlock()

	if r.defaultID == "" {
		return nil, ErrProviderNotFound
	}
	return r.providers[r.defaultID], nil
}

// Chat sends a request to the default provider.
func (r *ProviderRegistry) Chat(ctx context.Context, req *ChatRequest) (*ChatResponse, error) {
	provider, err := r.GetDefault()
	if err != nil {
		return nil, err
	}
	return provider.Chat(ctx, req)
}

// ChatWithFallback tries multiple providers in order until one succeeds.
func (r *ProviderRegistry) ChatWithFallback(ctx context.Context, req *ChatRequest, providerIDs []string) (*ChatResponse, error) {
	var lastErr error

	for _, id := range providerIDs {
		provider, err := r.Get(id)
		if err != nil {
			lastErr = err
			continue
		}

		resp, err := provider.Chat(ctx, req)
		if err == nil {
			return resp, nil
		}
		lastErr = err

		// Don't try other providers if context was canceled
		if errors.Is(err, context.Canceled) || errors.Is(err, context.DeadlineExceeded) {
			return nil, ErrContextCanceled
		}
	}

	if lastErr != nil {
		return nil, lastErr
	}
	return nil, ErrProviderNotFound
}

// ListProviders returns IDs of all registered providers.
func (r *ProviderRegistry) ListProviders() []string {
	r.mu.RLock()
	defer r.mu.RUnlock()

	ids := make([]string, 0, len(r.providers))
	for id := range r.providers {
		ids = append(ids, id)
	}
	return ids
}

// HealthCheck verifies all providers are operational.
func (r *ProviderRegistry) HealthCheck(ctx context.Context) map[string]error {
	r.mu.RLock()
	providers := make(map[string]Provider, len(r.providers))
	for id, p := range r.providers {
		providers[id] = p
	}
	r.mu.RUnlock()

	results := make(map[string]error, len(providers))
	var wg sync.WaitGroup
	var mu sync.Mutex

	for id, provider := range providers {
		wg.Add(1)
		go func(id string, p Provider) {
			defer wg.Done()

			_, err := p.ListModels(ctx)

			mu.Lock()
			results[id] = err
			mu.Unlock()
		}(id, provider)
	}

	wg.Wait()
	return results
}
