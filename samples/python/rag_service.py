"""
RAG (Retrieval-Augmented Generation) service for semantic code search.

Provides vector-based semantic search over indexed code chunks,
enabling context-aware code retrieval for AI agents.
"""

from dataclasses import dataclass
from typing import List, Optional, Tuple
import numpy as np
from abc import ABC, abstractmethod


@dataclass
class CodeChunk:
    """A chunk of code with metadata for RAG retrieval."""
    id: str
    file_path: str
    content: str
    language: str
    symbol_name: Optional[str] = None
    symbol_type: Optional[str] = None  # function, class, method, etc.
    start_line: int = 0
    end_line: int = 0
    embedding: Optional[np.ndarray] = None


@dataclass
class SearchResult:
    """Result from a semantic search query."""
    chunk: CodeChunk
    score: float
    
    @property
    def relevance_percentage(self) -> float:
        """Convert similarity score to percentage."""
        return round(self.score * 100, 2)


class EmbeddingProvider(ABC):
    """Abstract base class for embedding providers."""
    
    @abstractmethod
    async def embed(self, text: str) -> np.ndarray:
        """Generate embedding vector for text."""
        pass
    
    @abstractmethod
    async def embed_batch(self, texts: List[str]) -> List[np.ndarray]:
        """Generate embeddings for multiple texts efficiently."""
        pass


class VectorStore(ABC):
    """Abstract base class for vector storage backends."""
    
    @abstractmethod
    async def upsert(self, chunks: List[CodeChunk]) -> int:
        """Insert or update chunks in the store."""
        pass
    
    @abstractmethod
    async def search(
        self,
        query_embedding: np.ndarray,
        limit: int = 10,
        filter_language: Optional[str] = None
    ) -> List[Tuple[CodeChunk, float]]:
        """Search for similar chunks."""
        pass
    
    @abstractmethod
    async def delete_by_file(self, file_path: str) -> int:
        """Delete all chunks from a specific file."""
        pass


class RagService:
    """
    Main RAG service orchestrating indexing and retrieval.
    
    Combines embedding generation with vector storage to provide
    semantic search over code repositories.
    """
    
    def __init__(
        self,
        embedding_provider: EmbeddingProvider,
        vector_store: VectorStore,
        chunk_size: int = 1000,
        chunk_overlap: int = 200
    ):
        """
        Initialize the RAG service.
        
        Args:
            embedding_provider: Provider for generating embeddings.
            vector_store: Backend for vector storage and search.
            chunk_size: Maximum size of code chunks in characters.
            chunk_overlap: Overlap between adjacent chunks.
        """
        self.embedding_provider = embedding_provider
        self.vector_store = vector_store
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap
    
    async def index_file(self, file_path: str, content: str, language: str) -> int:
        """
        Index a single file into the RAG store.
        
        Args:
            file_path: Path to the file being indexed.
            content: File content to index.
            language: Programming language of the file.
            
        Returns:
            Number of chunks indexed.
        """
        # Delete existing chunks for this file
        await self.vector_store.delete_by_file(file_path)
        
        # Create chunks from content
        chunks = self._create_chunks(file_path, content, language)
        
        if not chunks:
            return 0
        
        # Generate embeddings
        texts = [chunk.content for chunk in chunks]
        embeddings = await self.embedding_provider.embed_batch(texts)
        
        for chunk, embedding in zip(chunks, embeddings):
            chunk.embedding = embedding
        
        # Store in vector database
        return await self.vector_store.upsert(chunks)
    
    async def search(
        self,
        query: str,
        limit: int = 10,
        language: Optional[str] = None
    ) -> List[SearchResult]:
        """
        Search for code chunks semantically similar to the query.
        
        Args:
            query: Natural language query or code snippet.
            limit: Maximum number of results.
            language: Optional filter by programming language.
            
        Returns:
            List of search results ordered by relevance.
        """
        query_embedding = await self.embedding_provider.embed(query)
        
        results = await self.vector_store.search(
            query_embedding,
            limit=limit,
            filter_language=language
        )
        
        return [
            SearchResult(chunk=chunk, score=score)
            for chunk, score in results
        ]
    
    async def build_context(
        self,
        query: str,
        max_tokens: int = 4000,
        language: Optional[str] = None
    ) -> str:
        """
        Build a context string from relevant code chunks.
        
        Args:
            query: The query to find relevant context for.
            max_tokens: Approximate maximum tokens in result.
            language: Optional language filter.
            
        Returns:
            Formatted context string with relevant code.
        """
        results = await self.search(query, limit=20, language=language)
        
        context_parts = []
        total_chars = 0
        char_limit = max_tokens * 4  # Rough chars-to-tokens ratio
        
        for result in results:
            chunk = result.chunk
            formatted = f"// {chunk.file_path}:{chunk.start_line}-{chunk.end_line}\n{chunk.content}"
            
            if total_chars + len(formatted) > char_limit:
                break
            
            context_parts.append(formatted)
            total_chars += len(formatted)
        
        return "\n\n".join(context_parts)
    
    def _create_chunks(
        self,
        file_path: str,
        content: str,
        language: str
    ) -> List[CodeChunk]:
        """Split content into overlapping chunks."""
        chunks = []
        lines = content.split('\n')
        
        current_chunk = []
        current_size = 0
        start_line = 1
        
        for i, line in enumerate(lines, 1):
            current_chunk.append(line)
            current_size += len(line) + 1
            
            if current_size >= self.chunk_size:
                chunk_content = '\n'.join(current_chunk)
                chunks.append(CodeChunk(
                    id=f"{file_path}:{start_line}-{i}",
                    file_path=file_path,
                    content=chunk_content,
                    language=language,
                    start_line=start_line,
                    end_line=i
                ))
                
                # Calculate overlap
                overlap_lines = []
                overlap_size = 0
                for line in reversed(current_chunk):
                    if overlap_size + len(line) > self.chunk_overlap:
                        break
                    overlap_lines.insert(0, line)
                    overlap_size += len(line) + 1
                
                current_chunk = overlap_lines
                current_size = overlap_size
                start_line = i - len(overlap_lines) + 1
        
        # Don't forget the last chunk
        if current_chunk:
            chunks.append(CodeChunk(
                id=f"{file_path}:{start_line}-{len(lines)}",
                file_path=file_path,
                content='\n'.join(current_chunk),
                language=language,
                start_line=start_line,
                end_line=len(lines)
            ))
        
        return chunks
