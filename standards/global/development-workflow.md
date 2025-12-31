# Development Workflow

> Complete workflow for implementing features, fixing bugs, and maintaining quality.

## Development Order

Follow this sequence for new features:

```
1. src/Shared/Domain.fs     → Define types
2. src/Shared/Api.fs        → Define API contract
3. src/Server/Validation.fs → Input validation
4. src/Server/Domain.fs     → Pure business logic (NO I/O)
5. src/Server/Persistence.fs → Database/file operations
6. src/Server/Api.fs        → Implement API
7. src/Client/State.fs      → Model, Msg, update
8. src/Client/View.fs       → UI components
9. src/Tests/               → Tests
```

## New Feature Checklist

- [ ] Types defined in `src/Shared/Domain.fs`
- [ ] API contract in `src/Shared/Api.fs`
- [ ] Validation in `src/Server/Validation.fs`
- [ ] Domain logic is pure (no I/O) in `src/Server/Domain.fs`
- [ ] Persistence in `src/Server/Persistence.fs`
- [ ] API implementation in `src/Server/Api.fs`
- [ ] Frontend state in `src/Client/State.fs`
- [ ] Frontend view in `src/Client/View.fs`
- [ ] Tests written (at minimum: domain + validation)
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] Development diary updated

## Bug Fix Protocol (MANDATORY)

Every bug fix MUST include a regression test:

1. **Understand root cause** - Don't just fix symptoms
2. **Write failing test FIRST** - Reproduces the bug
3. **Fix the bug** - Make the test pass
4. **Verify no regressions** - Run full test suite
5. **Document in diary** - Include what test was added

### Test Comment Pattern

```fsharp
testCase "amount serializes as number not string" <| fun () ->
    // Prevents regression: Encode.int64 output strings in JS,
    // causing API to silently reject data.
    ...
```

## Development Diary

Update diary after ANY meaningful code changes:

```markdown
## YYYY-MM-DD HH:MM - [Brief Title]

**What I did:**
[Concise description]

**Files Added/Modified/Deleted:**
- `path/to/file.fs` - [What changed]

**Rationale:**
[Why changes were necessary]

**Outcomes:**
- Build: pass/fail
- Tests: X/Y passed
- Issues: [Problems encountered]
```

## Quick Commands

```bash
# Development
cd src/Server && dotnet watch run  # Backend with hot reload
npm run dev                         # Frontend with HMR
dotnet test                         # Run tests

# Build
docker build -t app:latest .        # Build image
docker-compose up -d                # Deploy
```

## Quality Criteria

After every feature/bug fix:

1. Invoke qa-milestone-reviewer agent
2. Address identified test gaps
3. Run full test suite
4. Update development diary

## Code Review Checklist

- [ ] No I/O in Domain.fs
- [ ] Validation at API boundaries
- [ ] Result types handled explicitly
- [ ] React keys on all list renderings
- [ ] Parameterized SQL queries
- [ ] Async for all I/O operations
- [ ] Error messages are user-friendly
- [ ] Mobile-friendly (no hover-only interactions)

## See Also

- `quick-reference.md` - Code templates
- `anti-patterns.md` - Common mistakes
