# Code-Reviewer Subagent Architecture

## Overview

The code-reviewer subagent is a RAG-powered (Retrieval-Augmented Generation) code review system that provides contextual analysis by leveraging project-specific code patterns and best practices. It uses Microsoft Agent Framework (MAF) components for embeddings, vector storage, and AI model integration.

## Architecture Components

### Core Components

#### 1. OllamaEmbeddingGenerator
- **Purpose**: Generates text embeddings using Ollama's embedding API
- **Implementation**: `ASXRunTerminal/infra/OllamaEmbeddingGenerator.cs`
- **MAF Compliance**: Implements `IEmbeddingGenerator<string, Embedding<float>>`
- **Default Model**: nomic-embed-text
- **API Endpoint**: `/api/embeddings`

#### 2. SqliteVectorStore
- **Purpose**: Persistent vector storage with similarity search
- **Implementation**: `ASXRunTerminal/infra/SqliteVectorStore.cs`
- **Database**: SQLite with custom embedding storage
- **Similarity Metric**: Cosine similarity
- **Features**:
  - Document CRUD operations
  - Configurable similarity threshold
  - Smart caching and indexing strategies

#### 3. VectorStoreConfiguration
- **Purpose**: Configuration for vector operations
- **Implementation**: `ASXRunTerminal/infra/VectorStoreConfiguration.cs`
- **Key Settings**:
  - Database path (null for in-memory)
  - Embedding model selection
  - Similarity threshold (default: 0.7)
  - Indexing strategy (SmartIncremental, Full, OnDemand, InMemory)
  - Cache expiration (default: 60 minutes)

#### 4. CodeReviewerSubagent
- **Purpose**: Main subagent coordinating RAG operations
- **Implementation**: `ASXRunTerminal/subagents/CodeReviewerSubagent.cs`
- **Interface**: `ICodeReviewerSubagent`
- **Responsibilities**:
  - Code file indexing with embeddings
  - Context retrieval via similarity search
  - Code review execution with project-specific rules
  - Result parsing and metrics calculation

### Data Structures

#### CodeReviewContext
```csharp
- FilePaths: Files to review
- FileContents: Optional pre-loaded content
- MinSeverity: Minimum severity level (Critical to Info)
- Focus: Review focus (Comprehensive, Security, Performance, etc.)
- UseRag: Enable/disable RAG context
- Model: Ollama model selection
- CheckProjectRules: Verify ASXRunTerminal-specific rules
```

#### CodeReviewResult
```csharp
- Status: Completion status
- Issues: List of identified problems
- Metrics: Quality scores and statistics
- Recommendations: General improvement suggestions
- RagContext: RAG operation metadata
- DurationMs: Execution time
```

#### CodeReviewIssue
```csharp
- Id: Unique identifier
- FilePath: File location
- LineNumber: Optional line number
- Severity: Critical, High, Medium, Low, Info
- Category: Security, Performance, Style, etc.
- Title: Issue summary
- Description: Detailed explanation
- Suggestion: Actionable recommendation
- ProjectRule: Violated project rule (if applicable)
```

## RAG Pipeline

### 1. Indexing Phase
```
Code Files → Content Extraction → Embedding Generation → Vector Storage
```

**Features**:
- Smart incremental indexing (index on first use, cache results)
- Force reindex option for fresh analysis
- Document deduplication via hash-based IDs
- Configurable indexing strategies

### 2. Retrieval Phase
```
Query → Embedding Generation → Similarity Search → Context Assembly
```

**Process**:
1. Generate embedding for search query
2. Search vector store for similar code patterns
3. Filter by similarity threshold
4. Return top N results (default: 10)
5. Include context metadata (file paths, similarity scores)

### 3. Review Phase
```
Code + Context → Prompt Construction → AI Analysis → Result Parsing
```

**Prompt Construction**:
- System instructions for code review
- Project-specific rules (implicit operator, test coverage, etc.)
- Focus-specific guidelines (security, performance, etc.)
- RAG context from similar code
- Target code for review
- Output format specifications

## Project-Specific Rules

The code-reviewer enforces ASXRunTerminal project conventions:

1. **Implicit Operator**: Use implicit operators instead of AutoMapper
2. **Test Coverage**: Create tests for new behaviors
3. **Security**: No secret exposure, proper input validation
4. **Performance**: Avoid unnecessary computational costs
5. **Maintainability**: Idiomatic code, clear naming
6. **Error Handling**: Proper exception handling
7. **C#/.NET Standards**: Framework compliance

## CLI Integration

### Command Syntax
```bash
asxrun code-review <files> [options]
```

### Options
- `--severity=<level>`: Minimum severity level
- `--focus=<area>`: Review focus area
- `--no-rag`: Disable RAG context
- `--model=<model>`: Custom Ollama model

### Examples
```bash
# Basic review with RAG
asxrun code-review src/Program.cs

# Security-focused review
asxrun code-review src/**/*.cs --focus security --severity high

# Simple review without RAG
asxrun code-review src/Program.cs --no-rag

# Custom model
asxrun code-review src/Program.cs --model qwen3.5:4b
```

## Integration with Existing Skills

The code-reviewer subagent coexists with the existing `skill code-reviewer`:

- **skill code-reviewer**: Simple prompt-based review, good for quick checks
- **code-review**: Advanced RAG-powered review with project context

Users can choose based on their needs:
- Use `skill code-reviewer` for fast, simple reviews
- Use `code-review` for comprehensive, context-aware analysis

## Vector Storage

### Database Schema
```sql
CREATE TABLE documents (
    id TEXT PRIMARY KEY,
    content TEXT NOT NULL,
    file_path TEXT NOT NULL,
    embedding TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX idx_file_path ON documents(file_path);
CREATE INDEX idx_updated_at ON documents(updated_at);
```

### Embedding Storage
- Embeddings stored as JSON arrays in TEXT column
- Custom serialization/deserialization for performance
- Cosine similarity calculation in-memory

### Persistence
- Default: SQLite in `~/.asxrun/vector-store.db`
- In-memory option for testing
- Automatic schema initialization

## Performance Considerations

### Indexing Strategy
- **SmartIncremental**: Index on first use, cache results (recommended)
- **Full**: Index all files upfront (slow startup, complete context)
- **OnDemand**: Index only when needed (fast startup, potential missing context)
- **InMemory**: No persistence (fastest, data lost on restart)

### Caching
- Embedding cache with configurable expiration (default: 60 minutes)
- Document-level deduplication
- Similarity threshold filtering to reduce noise

### Scalability
- SQLite handles small to medium codebases well
- For large codebases, consider:
  - Partitioning by directory structure
  - Incremental updates only for changed files
  - External vector stores (Qdrant, etc.)

## Testing

### Unit Tests
- `SqliteVectorStoreTests.cs`: Vector storage operations
- `VectorStoreConfigurationTests.cs`: Configuration validation
- `CodeReviewDataStructuresTests.cs`: Data structure integrity

### Integration Tests
- End-to-end RAG pipeline with Ollama
- Code review execution with sample code
- Vector store persistence and retrieval

## Future Enhancements

### Planned Features
1. **Multi-model Support**: Different embedding models for different languages
2. **Hierarchical Indexing**: Repository → package → file structure
3. **Custom Rules**: User-defined project rules via configuration
4. **Diff Reviews**: Review only changed code (git diff integration)
5. **Batch Processing**: Review multiple projects in parallel
6. **Export Formats**: JSON, SARIF, HTML reports

### Optimization Opportunities
1. **Async Embedding**: Parallel embedding generation
2. **Vector Compression**: Reduce storage footprint
3. **Approximate Search**: HNSW indexing for faster similarity search
4. **Hybrid Search**: Combine semantic and keyword search

## Troubleshooting

### Common Issues

1. **Ollama Embedding Model Not Found**
   - Ensure nomic-embed-text is pulled: `ollama pull nomic-embed-text`
   - Configure alternative model in VectorStoreConfiguration

2. **Vector Store Corruption**
   - Delete `~/.asxrun/vector-store.db` to rebuild
   - Check SQLite file permissions

3. **Slow Indexing**
   - Use SmartIncremental strategy
   - Reduce similarity threshold
   - Limit file scope

4. **Poor Review Quality**
   - Ensure sufficient context is indexed
   - Adjust similarity threshold
   - Use more capable Ollama model

## References

- Microsoft Agent Framework: https://devblogs.microsoft.com/ai/
- Ollama Embeddings: https://ollama.com/blog/embedding-models
- SQLite: https://www.sqlite.org/
- RAG Best Practices: https://arxiv.org/abs/2312.10997
