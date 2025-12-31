# Claude Code Tools

> Tool usage guidelines for AI agents working with F# codebases.

## Serena MCP Tools (MANDATORY for F# Code)

Use Serena instead of Read/Grep/Glob for `.fs` files:

| Instead of... | Use Serena... |
|---------------|---------------|
| `Read` on .fs files | `get_symbols_overview` or `find_symbol` with `include_body=True` |
| `Grep` for code search | `search_for_pattern` |
| `Glob` for finding files | `find_file` or `list_dir` |
| Manual `Edit` on symbols | `replace_symbol_body`, `insert_before_symbol`, `insert_after_symbol` |

### Benefits

- **Token-efficient**: Only reads what's needed
- **Semantic accuracy**: Understands code structure
- **Safe refactoring**: `rename_symbol` updates all references

### Example Workflow

```
1. get_symbols_overview("src/Server/Api.fs")     → See file structure
2. find_symbol("myFunction", include_body=True)  → Read specific function
3. replace_symbol_body(...)                       → Edit just that function
```

## Skills Reference

| Skill | When to Use |
|-------|-------------|
| `fsharp-feature` | Complete feature implementation |
| `fsharp-shared` | Types and API contracts in `src/Shared/` |
| `fsharp-backend` | Backend: validation, domain, persistence, API |
| `fsharp-validation` | Input validation patterns |
| `fsharp-persistence` | Database and file operations |
| `fsharp-frontend` | Elmish state and Feliz views |
| `fsharp-tests` | Writing Expecto tests |
| `tailscale-deploy` | Docker + Tailscale deployment |

## Agent Workflows

### qa-milestone-reviewer

Invoke after implementing features:
- Reviews for tautological tests
- Checks for missing coverage
- Defines missing tests (doesn't implement)
- Hands off to red-test-fixer

### red-test-fixer

Fixes failing tests:
- Diagnoses root cause
- Applies minimal fix
- Maintains functional programming principles
- Verifies no regressions

## When NOT to Use Serena

- Reading non-code files (markdown, JSON, config)
- When you need entire file context
- Files Serena doesn't support

## Browser DevTools MCP

For frontend testing:
- `take_snapshot`: Get accessibility tree
- `click`, `fill`: Automate UI interactions
- `list_console_messages`: Check for JS errors
- `list_network_requests`: Verify API calls

## See Also

- `development-workflow.md` - Implementation process
- `../testing/overview.md` - Testing patterns
