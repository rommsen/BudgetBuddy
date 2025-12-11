# BudgetBuddy - Task Completion Checklist

## After Completing Any Task

### 1. Build Verification
```bash
dotnet build BudgetBuddy.sln
```
Must succeed with no errors.

### 2. Test Verification
```bash
dotnet test
```
All tests must pass.

### 3. Development Diary
Update `diary/development.md` with:
- Date and timestamp
- What was changed
- Files added/modified/deleted
- Rationale
- Build/test outcomes

### 4. QA Review (for features/milestones)
Invoke the `qa-milestone-reviewer` agent to verify:
- Tests are meaningful (not tautological)
- All important behavior is covered
- No missing test coverage

### 5. Backlog/Milestone Update
- Mark completed items in `/backlog.md` with date
- Update `/docs/MILESTONE-PLAN.md` if working on milestones

## Bug Fix Protocol (MANDATORY)
Every bug fix MUST include a regression test:
1. Write a failing test that reproduces the bug
2. Fix the bug (test should now pass)
3. Run full test suite
4. Document in diary

## Verification Checklist
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] Development diary updated
- [ ] No security vulnerabilities introduced
- [ ] Code follows project conventions
