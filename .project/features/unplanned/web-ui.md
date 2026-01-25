# Web UI Interface

**Status:** 📋 Backlog  
**Priority:** Medium  
**Source:** Gap Analysis vs Birdlet/Agent Orchestrator  
**Estimated Effort:** 4-6 weeks

## Overview

Add a browser-based web interface for Aura, complementing the existing VS Code extension. This enables users who don't use VS Code (or prefer a standalone interface) to access Aura's workflow management, chat, and monitoring capabilities.

## Strategic Context

Birdlet has a detailed web UI concept that Aura lacks. A web interface would:
- Enable access without VS Code installed
- Support team dashboards and monitoring
- Provide a familiar ChatGPT-style interface for chat
- Allow project managers and non-developers to interact with workflows
- Enable mobile/tablet access for monitoring

## Use Cases

1. **Standalone Access** — Developers using other IDEs (JetBrains, Vim, etc.)
2. **Team Dashboard** — View all team workflows and their status
3. **Chat Interface** — ChatGPT-style conversation with agents
4. **Monitoring** — Index health, agent activity, system status
5. **Mobile Check-in** — Quick workflow status on phone/tablet
6. **Demos & Presentations** — Browser-based demos without IDE setup

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Aura Web UI                                 │
│                   (React/Vite SPA)                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP/SSE
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Aura.Api                                   │
│              (Existing ASP.NET Core API)                        │
│                                                                 │
│  Existing Endpoints:           New Endpoints:                   │
│  • /api/developer/workflows    • /api/ui/dashboard              │
│  • /api/agents                 • /api/ui/preferences            │
│  • /api/rag/search            • /api/ui/notifications          │
│  • /api/workspaces                                              │
└─────────────────────────────────────────────────────────────────┘
```

## Technology Choice

**React + Vite + Tailwind CSS**

Rationale:
- React: Wide adoption, large ecosystem, familiar to most developers
- Vite: Fast builds, modern tooling, excellent DX
- Tailwind: Rapid UI development, consistent design system
- TypeScript: Type safety, matches existing extension code

Alternative considered: Blazor (C# full-stack) — rejected due to larger bundle size and less mature component ecosystem.

## Implementation

### Phase 1: Project Setup & Core Layout (Week 1)

**Project Structure:**

```
src/
├── Aura.Web/                    # New ASP.NET Core project (SPA host)
│   ├── Program.cs              # Static file serving + API proxy
│   └── wwwroot/                # Built React app output
│
├── web/                        # React application
│   ├── src/
│   │   ├── components/
│   │   │   ├── layout/
│   │   │   │   ├── Sidebar.tsx
│   │   │   │   ├── Header.tsx
│   │   │   │   └── MainLayout.tsx
│   │   │   ├── workflows/
│   │   │   ├── chat/
│   │   │   ├── agents/
│   │   │   └── common/
│   │   ├── pages/
│   │   │   ├── Dashboard.tsx
│   │   │   ├── Workflows.tsx
│   │   │   ├── Chat.tsx
│   │   │   ├── Agents.tsx
│   │   │   └── Settings.tsx
│   │   ├── hooks/
│   │   ├── services/
│   │   │   └── api.ts
│   │   ├── store/
│   │   ├── types/
│   │   └── App.tsx
│   ├── package.json
│   ├── vite.config.ts
│   └── tailwind.config.js
```

**Main Layout:**

```tsx
// web/src/components/layout/MainLayout.tsx
import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { Header } from './Header';

export function MainLayout() {
    return (
        <div className="flex h-screen bg-gray-900 text-gray-100">
            <Sidebar />
            <div className="flex flex-col flex-1 overflow-hidden">
                <Header />
                <main className="flex-1 overflow-auto p-6">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}
```

**Sidebar Navigation:**

```tsx
// web/src/components/layout/Sidebar.tsx
import { NavLink } from 'react-router-dom';
import { 
    HomeIcon, 
    DocumentTextIcon, 
    ChatBubbleLeftRightIcon,
    CpuChipIcon,
    MagnifyingGlassIcon,
    Cog6ToothIcon 
} from '@heroicons/react/24/outline';

const navigation = [
    { name: 'Dashboard', href: '/', icon: HomeIcon },
    { name: 'Workflows', href: '/workflows', icon: DocumentTextIcon },
    { name: 'Chat', href: '/chat', icon: ChatBubbleLeftRightIcon },
    { name: 'Agents', href: '/agents', icon: CpuChipIcon },
    { name: 'Search', href: '/search', icon: MagnifyingGlassIcon },
    { name: 'Settings', href: '/settings', icon: Cog6ToothIcon },
];

export function Sidebar() {
    return (
        <div className="flex flex-col w-64 bg-gray-800 border-r border-gray-700">
            <div className="flex items-center h-16 px-4 border-b border-gray-700">
                <img src="/aura-logo.svg" alt="Aura" className="h-8 w-8" />
                <span className="ml-2 text-xl font-semibold">Aura</span>
            </div>
            <nav className="flex-1 px-2 py-4 space-y-1">
                {navigation.map((item) => (
                    <NavLink
                        key={item.name}
                        to={item.href}
                        className={({ isActive }) =>
                            `flex items-center px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                                isActive
                                    ? 'bg-blue-600 text-white'
                                    : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                            }`
                        }
                    >
                        <item.icon className="h-5 w-5 mr-3" />
                        {item.name}
                    </NavLink>
                ))}
            </nav>
            <div className="p-4 border-t border-gray-700">
                <SystemStatus />
            </div>
        </div>
    );
}
```

### Phase 2: Dashboard & Workflows (Week 2)

**Dashboard Page:**

```tsx
// web/src/pages/Dashboard.tsx
import { useQuery } from '@tanstack/react-query';
import { api } from '../services/api';
import { WorkflowCard } from '../components/workflows/WorkflowCard';
import { StatsCard } from '../components/common/StatsCard';
import { ActivityFeed } from '../components/common/ActivityFeed';

export function Dashboard() {
    const { data: workflows } = useQuery({
        queryKey: ['workflows', 'recent'],
        queryFn: () => api.getWorkflows({ limit: 5, sort: 'updated' }),
    });
    
    const { data: stats } = useQuery({
        queryKey: ['stats'],
        queryFn: () => api.getDashboardStats(),
    });
    
    return (
        <div className="space-y-6">
            <h1 className="text-2xl font-bold">Dashboard</h1>
            
            {/* Stats Row */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <StatsCard
                    title="Active Workflows"
                    value={stats?.activeWorkflows ?? 0}
                    icon="document"
                    trend={stats?.workflowTrend}
                />
                <StatsCard
                    title="Indexed Files"
                    value={stats?.indexedFiles ?? 0}
                    icon="database"
                />
                <StatsCard
                    title="RAG Chunks"
                    value={stats?.ragChunks ?? 0}
                    icon="cube"
                />
                <StatsCard
                    title="Agent Calls (24h)"
                    value={stats?.agentCalls24h ?? 0}
                    icon="cpu"
                />
            </div>
            
            {/* Recent Workflows */}
            <section>
                <h2 className="text-lg font-semibold mb-4">Recent Workflows</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {workflows?.map((workflow) => (
                        <WorkflowCard key={workflow.id} workflow={workflow} />
                    ))}
                </div>
            </section>
            
            {/* Activity Feed */}
            <section>
                <h2 className="text-lg font-semibold mb-4">Recent Activity</h2>
                <ActivityFeed />
            </section>
        </div>
    );
}
```

**Workflow List Page:**

```tsx
// web/src/pages/Workflows.tsx
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import { WorkflowTable } from '../components/workflows/WorkflowTable';
import { CreateWorkflowModal } from '../components/workflows/CreateWorkflowModal';

export function Workflows() {
    const [filter, setFilter] = useState<'all' | 'active' | 'completed'>('all');
    const [showCreateModal, setShowCreateModal] = useState(false);
    
    const { data: workflows, isLoading } = useQuery({
        queryKey: ['workflows', filter],
        queryFn: () => api.getWorkflows({ status: filter === 'all' ? undefined : filter }),
    });
    
    const queryClient = useQueryClient();
    const createMutation = useMutation({
        mutationFn: api.createWorkflow,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['workflows'] });
            setShowCreateModal(false);
        },
    });
    
    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <h1 className="text-2xl font-bold">Workflows</h1>
                <button
                    onClick={() => setShowCreateModal(true)}
                    className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
                >
                    New Workflow
                </button>
            </div>
            
            {/* Filter Tabs */}
            <div className="flex space-x-4 border-b border-gray-700">
                {(['all', 'active', 'completed'] as const).map((status) => (
                    <button
                        key={status}
                        onClick={() => setFilter(status)}
                        className={`px-4 py-2 border-b-2 transition-colors ${
                            filter === status
                                ? 'border-blue-500 text-blue-400'
                                : 'border-transparent text-gray-400 hover:text-gray-200'
                        }`}
                    >
                        {status.charAt(0).toUpperCase() + status.slice(1)}
                    </button>
                ))}
            </div>
            
            {/* Workflow Table */}
            {isLoading ? (
                <div className="text-center py-12">Loading...</div>
            ) : (
                <WorkflowTable workflows={workflows ?? []} />
            )}
            
            {/* Create Modal */}
            {showCreateModal && (
                <CreateWorkflowModal
                    onClose={() => setShowCreateModal(false)}
                    onSubmit={(data) => createMutation.mutate(data)}
                    isLoading={createMutation.isPending}
                />
            )}
        </div>
    );
}
```

### Phase 3: Chat Interface (Week 3-4)

**ChatGPT-Style Chat Page:**

```tsx
// web/src/pages/Chat.tsx
import { useState, useRef, useEffect } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { api } from '../services/api';
import { ChatMessage } from '../components/chat/ChatMessage';
import { ChatInput } from '../components/chat/ChatInput';
import { AgentSelector } from '../components/chat/AgentSelector';

export function Chat() {
    const [messages, setMessages] = useState<Message[]>([]);
    const [selectedAgent, setSelectedAgent] = useState('chat-agent');
    const [isStreaming, setIsStreaming] = useState(false);
    const messagesEndRef = useRef<HTMLDivElement>(null);
    
    const { data: agents } = useQuery({
        queryKey: ['agents'],
        queryFn: () => api.getAgents(),
    });
    
    const sendMessage = async (content: string) => {
        // Add user message
        const userMessage: Message = { role: 'user', content, timestamp: new Date() };
        setMessages((prev) => [...prev, userMessage]);
        
        // Add placeholder for assistant
        const assistantMessage: Message = { role: 'assistant', content: '', timestamp: new Date() };
        setMessages((prev) => [...prev, assistantMessage]);
        
        setIsStreaming(true);
        
        try {
            // Stream response using SSE
            const response = await fetch(`/api/agents/${selectedAgent}/chat/stream`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: content }),
            });
            
            const reader = response.body?.getReader();
            const decoder = new TextDecoder();
            
            while (reader) {
                const { done, value } = await reader.read();
                if (done) break;
                
                const chunk = decoder.decode(value);
                const lines = chunk.split('\n').filter((line) => line.startsWith('data: '));
                
                for (const line of lines) {
                    const data = JSON.parse(line.slice(6));
                    if (data.type === 'token') {
                        setMessages((prev) => {
                            const updated = [...prev];
                            updated[updated.length - 1].content += data.content;
                            return updated;
                        });
                    }
                }
            }
        } catch (error) {
            console.error('Chat error:', error);
            setMessages((prev) => {
                const updated = [...prev];
                updated[updated.length - 1].content = 'Error: Failed to get response';
                updated[updated.length - 1].error = true;
                return updated;
            });
        } finally {
            setIsStreaming(false);
        }
    };
    
    // Auto-scroll to bottom
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);
    
    return (
        <div className="flex flex-col h-full">
            {/* Agent Selector */}
            <div className="flex items-center justify-between p-4 border-b border-gray-700">
                <h1 className="text-xl font-bold">Chat</h1>
                <AgentSelector
                    agents={agents ?? []}
                    selected={selectedAgent}
                    onChange={setSelectedAgent}
                />
            </div>
            
            {/* Messages */}
            <div className="flex-1 overflow-auto p-4 space-y-4">
                {messages.length === 0 ? (
                    <div className="text-center text-gray-500 py-12">
                        <p className="text-lg">Start a conversation</p>
                        <p className="text-sm mt-2">Ask questions about your code or request development tasks</p>
                    </div>
                ) : (
                    messages.map((message, i) => (
                        <ChatMessage key={i} message={message} isStreaming={isStreaming && i === messages.length - 1} />
                    ))
                )}
                <div ref={messagesEndRef} />
            </div>
            
            {/* Input */}
            <ChatInput onSend={sendMessage} disabled={isStreaming} />
        </div>
    );
}
```

**Chat Message Component:**

```tsx
// web/src/components/chat/ChatMessage.tsx
import ReactMarkdown from 'react-markdown';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';

interface ChatMessageProps {
    message: Message;
    isStreaming?: boolean;
}

export function ChatMessage({ message, isStreaming }: ChatMessageProps) {
    const isUser = message.role === 'user';
    
    return (
        <div className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}>
            <div
                className={`max-w-3xl rounded-lg px-4 py-3 ${
                    isUser
                        ? 'bg-blue-600 text-white'
                        : 'bg-gray-800 text-gray-100'
                }`}
            >
                <ReactMarkdown
                    components={{
                        code({ node, inline, className, children, ...props }) {
                            const match = /language-(\w+)/.exec(className || '');
                            return !inline && match ? (
                                <SyntaxHighlighter
                                    style={vscDarkPlus}
                                    language={match[1]}
                                    PreTag="div"
                                    {...props}
                                >
                                    {String(children).replace(/\n$/, '')}
                                </SyntaxHighlighter>
                            ) : (
                                <code className="bg-gray-700 px-1 rounded" {...props}>
                                    {children}
                                </code>
                            );
                        },
                    }}
                >
                    {message.content}
                </ReactMarkdown>
                {isStreaming && <span className="animate-pulse">▋</span>}
            </div>
        </div>
    );
}
```

### Phase 4: Search & Agents (Week 5)

**RAG Search Page:**

```tsx
// web/src/pages/Search.tsx
import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { api } from '../services/api';
import { SearchResult } from '../components/search/SearchResult';

export function Search() {
    const [query, setQuery] = useState('');
    
    const searchMutation = useMutation({
        mutationFn: (q: string) => api.search({ query: q, limit: 20 }),
    });
    
    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        if (query.trim()) {
            searchMutation.mutate(query);
        }
    };
    
    return (
        <div className="space-y-6">
            <h1 className="text-2xl font-bold">Search</h1>
            
            {/* Search Input */}
            <form onSubmit={handleSearch} className="flex gap-4">
                <input
                    type="text"
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="Search code, documentation, and knowledge..."
                    className="flex-1 px-4 py-3 bg-gray-800 border border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <button
                    type="submit"
                    disabled={searchMutation.isPending}
                    className="px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
                >
                    {searchMutation.isPending ? 'Searching...' : 'Search'}
                </button>
            </form>
            
            {/* Results */}
            {searchMutation.data && (
                <div className="space-y-4">
                    <p className="text-gray-400">
                        Found {searchMutation.data.length} results
                    </p>
                    {searchMutation.data.map((result, i) => (
                        <SearchResult key={i} result={result} query={query} />
                    ))}
                </div>
            )}
        </div>
    );
}
```

### Phase 5: API Integration & Build (Week 6)

**API Client:**

```typescript
// web/src/services/api.ts
const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5300';

class AuraApiClient {
    private async fetch<T>(path: string, options?: RequestInit): Promise<T> {
        const response = await fetch(`${BASE_URL}${path}`, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...options?.headers,
            },
        });
        
        if (!response.ok) {
            throw new Error(`API error: ${response.status}`);
        }
        
        return response.json();
    }
    
    // Workflows
    getWorkflows(params?: { status?: string; limit?: number; sort?: string }) {
        const query = new URLSearchParams(params as any).toString();
        return this.fetch<Workflow[]>(`/api/developer/workflows?${query}`);
    }
    
    getWorkflow(id: string) {
        return this.fetch<Workflow>(`/api/developer/workflows/${id}`);
    }
    
    createWorkflow(data: CreateWorkflowRequest) {
        return this.fetch<Workflow>('/api/developer/workflows', {
            method: 'POST',
            body: JSON.stringify(data),
        });
    }
    
    // Agents
    getAgents() {
        return this.fetch<Agent[]>('/api/agents');
    }
    
    // Search
    search(params: { query: string; limit?: number }) {
        return this.fetch<SearchResult[]>('/api/rag/search', {
            method: 'POST',
            body: JSON.stringify(params),
        });
    }
    
    // Dashboard
    getDashboardStats() {
        return this.fetch<DashboardStats>('/api/ui/dashboard/stats');
    }
    
    // Workspaces
    getWorkspaces() {
        return this.fetch<Workspace[]>('/api/workspaces');
    }
}

export const api = new AuraApiClient();
```

**Vite Configuration:**

```typescript
// web/vite.config.ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    build: {
        outDir: '../src/Aura.Web/wwwroot',
        emptyOutDir: true,
    },
    server: {
        proxy: {
            '/api': {
                target: 'http://localhost:5300',
                changeOrigin: true,
            },
        },
    },
});
```

**ASP.NET Host:**

```csharp
// src/Aura.Web/Program.cs
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Serve static files from wwwroot (React build output)
app.UseDefaultFiles();
app.UseStaticFiles();

// Proxy API requests to Aura.Api
app.MapForwarder("/api/{**catch-all}", "http://localhost:5300");

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
```

**Build Script:**

```powershell
# scripts/Build-WebUI.ps1
param(
    [switch]$Watch
)

Push-Location $PSScriptRoot\..\web

if ($Watch) {
    npm run dev
} else {
    npm run build
}

Pop-Location

Write-Host "Web UI built successfully" -ForegroundColor Green
```

## New API Endpoints

```csharp
// Dashboard stats endpoint
app.MapGet("/api/ui/dashboard/stats", async (
    OrchestratorDbContext db,
    CancellationToken ct) =>
{
    var stats = new
    {
        ActiveWorkflows = await db.Workflows.CountAsync(w => w.Status == "active", ct),
        CompletedWorkflows = await db.Workflows.CountAsync(w => w.Status == "completed", ct),
        IndexedFiles = await db.RagChunks.Select(c => c.FilePath).Distinct().CountAsync(ct),
        RagChunks = await db.RagChunks.CountAsync(ct),
        AgentCalls24h = await db.AgentExecutions
            .CountAsync(e => e.StartedAt > DateTime.UtcNow.AddHours(-24), ct),
    };
    return Results.Ok(stats);
})
.WithName("GetDashboardStats")
.WithTags("UI");
```

## Configuration

```json
{
    "WebUI": {
        "Enabled": true,
        "Port": 5301,
        "CorsOrigins": ["http://localhost:5173"],  // Vite dev server
        "EnableDevProxy": true
    }
}
```

## Success Criteria

- [ ] Dashboard shows workflow stats and recent activity
- [ ] Workflow list with filtering and creation
- [ ] ChatGPT-style chat with streaming responses
- [ ] RAG search with highlighted results
- [ ] Agent list and details
- [ ] Responsive design (works on mobile)
- [ ] Dark theme consistent with VS Code extension
- [ ] Build integrates with existing `dotnet run`

## Dependencies

```json
{
    "dependencies": {
        "react": "^18.2.0",
        "react-dom": "^18.2.0",
        "react-router-dom": "^6.20.0",
        "@tanstack/react-query": "^5.8.0",
        "react-markdown": "^9.0.0",
        "react-syntax-highlighter": "^15.5.0",
        "@heroicons/react": "^2.0.18"
    },
    "devDependencies": {
        "vite": "^5.0.0",
        "@vitejs/plugin-react": "^4.2.0",
        "typescript": "^5.3.0",
        "tailwindcss": "^3.3.0",
        "autoprefixer": "^10.4.0",
        "postcss": "^8.4.0"
    }
}
```

## Testing

```typescript
// web/src/__tests__/Dashboard.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Dashboard } from '../pages/Dashboard';

test('renders dashboard stats', async () => {
    const queryClient = new QueryClient();
    
    render(
        <QueryClientProvider client={queryClient}>
            <Dashboard />
        </QueryClientProvider>
    );
    
    await waitFor(() => {
        expect(screen.getByText('Active Workflows')).toBeInTheDocument();
    });
});
```

## Future Enhancements

1. **PWA Support** — Offline access, push notifications
2. **Real-time Updates** — WebSocket for live workflow status
3. **Authentication** — User accounts, API keys
4. **Team Features** — Shared workflows, comments
5. **Theming** — Light mode, custom themes
6. **Keyboard Shortcuts** — Power user navigation
7. **Mobile App** — React Native for iOS/Android
