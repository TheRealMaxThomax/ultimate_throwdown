# Ultimate Throwdown Workflow

This is the simple process to follow each time you work on the game.

## 1) Start of Session
- Pull latest changes:
  - `git pull`
- Read `SESSION_NOTES.md`.
- Confirm startup scene is correct:
  - `scenes/throwdown_prototype.scene`
- Pick one small goal for this session (example: tune throw force).

## 2) Build in Small Steps
- Make one small change at a time.
- Test after each small change.
- If something breaks, stop and fix before adding more features.
- For multiplayer logic, always run a quick 2-window validation after core changes.

## 3) Definition of Done (Before Commit)
Only commit when all are true:
- Code compiles.
- Feature works in a quick playtest.
- Multiplayer behavior is checked if gameplay state/networking changed.
- No obvious new bug introduced.
- `SESSION_NOTES.md` updated if plan/state changed.

## 4) Save Your Progress (Git)
- `git add .`
- `git commit -m "clear message about what changed"`
- `git push`

Good commit message examples:
- `tune throw force and throw up force`
- `fix pickup delay after throw`
- `add throw cooldown timer`

## 5) End of Session
- In chat, type: `session wrap-up`.
- Update `SESSION_NOTES.md` handoff section:
  - What changed
  - What is blocked
  - Exactly what to do next
- Update `BACKLOG.md` status (move completed items to Done).
- Update `PLAYTEST_CHECKLIST.md` only if test steps changed.
- Push your latest commit so GitHub has backup.

## Multiplayer Quick Loop (Required for Network Changes)
- Start host play session.
- Use network icon -> `Join via new instance`.
- Validate: both players visible, client pickup visible to both, client drop visible to both, client throw visible to both.
- If behavior differs by window, stop feature work and fix networking consistency first.

## Optional Safety Rule (Recommended)
If work feels risky or experimental, create a branch first:
- `git checkout -b feature/short-description`
