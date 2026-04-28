# Backlog

Use this file to track ideas, bugs, and next milestones.
Keep items short and clear.

## Current Milestone
- Make core grab/drop/throw loop feel good and stable.

## TODO (Next Up)
- [ ] Tune `ThrowForce`, `ThrowUpForce`, and `ThrowStartOffset`.
- [ ] Decide if throw cooldown is needed.
- [ ] Decide next loop piece: scoring/reset or pass/catch.
- [ ] HUD timing decision: keep temporary debug charge bar until throw feel is stable for a few sessions, then build proper HUD charge panel.

## Bugs / Issues
- [ ] (none currently)

## Ideas (Later)
- [ ] Build proper HUD throw charge bar (single rectangle with red/yellow/green sections and smooth fill).
- [ ] Add animation hooks for hold/throw.
- [ ] Add sound effects for pickup/drop/throw.
- [ ] Add UI feedback for throw cooldown (if added).

## Done
- [x] Split throw logic into `BallThrow`.
- [x] Keep hold-state source of truth in `BallGrab`.
- [x] Add pickup lockout after throw.
- [x] Set up Git + GitHub backup workflow.
