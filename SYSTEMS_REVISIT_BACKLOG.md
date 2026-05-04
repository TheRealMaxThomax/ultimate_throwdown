# Systems Revisit Backlog

Use this file for systems/features that are intentionally postponed.
Keep items concise and implementation-ready for future sessions.

## Status Key
- `parked`: intentionally not doing now
- `ready-to-prototype`: clear next build steps exist
- `in-progress`: currently being built/tested
- `done`: shipped and validated

## Backlog Items

### Trigger-Volume Ball Push (Manual Velocity)
- Status: `parked`
- Priority: Low
- Why parked: Auto-grab solved current gameplay consistency goal with lower networking complexity.
- Revisit condition: only if body-push/dribble feel becomes a design requirement.

#### Build Blueprint (Network-Safe)
1. Keep host authority: clients request/intend push, host applies final velocity.
2. Add a ball child trigger volume (`BallPushZone`) slightly larger than ball collider for detection only.
3. Only allow push while ball is free (`!IsHolding`), never while held.
4. Choose one current pusher deterministically (nearest valid player, stable tie-breaker).
5. Apply push on host at a rate-limited interval (not every frame), with a max speed cap.
6. Derive push direction from player move direction blended with player->ball direction.
7. Keep vertical velocity mostly untouched; adjust horizontal component to avoid pop/bounce artifacts.
8. Use anti-abuse guards: min input threshold, cooldown, optional short post-throw grace.
9. Validate in 2 windows: host push, client push, rapid alternation, wall/jump/post-throw edge cases.
10. If it causes jitter/desync, revert to auto-grab path and return this item to `parked`.

#### First Prototype Test Plan
- Host-only push sanity pass.
- Client push direction check (matches local intent and host view).
- 10-minute spam run: alternate host/client contact repeatedly.
- Edge checks: wall contact, jump contact, post-throw contact window.
