# Session Notes

Use this file as persistent project memory between chats.
Keep entries short, specific, and current.

## Project Snapshot
- **Current Goal:** Core ball gameplay first: stable grab/drop/throw before animation polish.
- **Current Branch:** `main` (tracking `origin/main`).
- **Build/Run Status:** s&box opens and gameplay scripts compile; `BallThrow` works with camera-based aim source; occasional inspector component-list stale state fixed by recompile/restart.
- **Last Updated:** 2026-04-28

## Important Decisions
- **Keep `BallGrab` as the source of truth for hold state.**
  - Why: One owner of state avoids desync bugs between multiple scripts.
  - Date: 2026-04-28
- **Split throw into a separate `BallThrow` component.**
  - Why: Better organization, easier debugging, cleaner future animation integration.
  - Date: 2026-04-28
- **Use optional direct ball reference (`MainBall`) with name fallback (`MainBallName`).**
  - Why: Direct reference is safer; name fallback keeps flexibility.
  - Date: 2026-04-28

## Constraints and Rules
Only include constraints that are easy to forget and expensive to violate.

- Engine/version constraints: s&box component workflow; compile errors can hide components from inspector list.
- Performance constraints: Keep gameplay logic simple for now; no heavy systems needed yet.
- Code style/conventions: Beginner-readable names, short methods, one source of truth for state.
- Tooling/workflow constraints: After script edits, allow recompile/restart editor if component does not appear.

## Collaboration Preferences
- Experience level: Beginner (new to development).
- Communication style: Explain steps in simple language, avoid jargon where possible, and include quick "what/why" context for commands and changes.

## Known Issues / Risks
- [ ] Throw tuning still placeholder (force/arc may need balancing).
  - Impact: Throw feel may be too strong/weak depending on map and movement speed.
  - Owner: Max
  - Next action: Tune `ThrowForce`, `ThrowUpForce`, and `ThrowStartOffset` in inspector playtests.
- [ ] Future animation integration not done yet.
  - Impact: Visual quality lower for now, but mechanics are playable.
  - Owner: Max
  - Next action: Add throw/run-holding animations after mechanics lock-in.

## Current Plan (Top 3)
1. Playtest throw feel and tune values in `BallThrow`.
2. Optionally add throw cooldown / animation hooks (events/timers) if needed.
3. Build next core mechanic loop piece (e.g., scoring/reset or pass/catch behavior).

## Component Missing Recovery (s&box)
If a component (for example `BallThrow`) does not appear in Add Component:

1. Save all scripts.
2. Wait for script compile to finish.
3. Try Add Component search again with exact name.
4. Recompile scripts once (or trigger project refresh).
5. If still missing, restart s&box/editor.
6. After restart, confirm no compile errors before adding component.

## Naming Canon
Use these exact names unless explicitly changed in chat.

- Core state owner component: `BallGrab`
- Throw helper component: `BallThrow`
- Hold-state public getter: `IsHolding`
- Held-ball public getter: `HeldBall`
- Ball selection properties: `MainBall`, `MainBallName`
- Interaction properties: `InteractDistance`, `InteractAction`, `HoldAnchor`, `PromptText`
- Internal ball references in `BallGrab`: `ballObject`, `ballBody`, `ballOriginalParent`, `ballCollidersToRestore`
- Throw properties in `BallThrow`: `ThrowAction`, `ThrowForce`, `ThrowUpForce`, `ThrowStartOffset`, `PickupDelayAfterThrow`, `ThrowDirectionSource`
- Core release method in `BallGrab`: `ReleaseHeldBall()`
- Pickup lockout method in `BallGrab`: `BlockPickupForSeconds(float seconds)`

## Next Chat Kickoff
Paste this at the start of a new session:

`Read SESSION_NOTES.md first, continue from Current Plan, and propose any needed updates before coding.`

## End-of-Session Handoff
Update this checklist before ending a chat:

- What changed: Added safety fixes to `BallGrab`; created `BallThrow`; fixed aim direction using `ThrowDirectionSource`; fixed third-person spawn issue by using camera for direction only; added short pickup block after throw; added component-missing recovery checklist; initialized Git, made baseline + guide commits, and connected/pushed to GitHub (`TheRealMaxThomax/ultimate_throwdown`).
- What is still blocked: No major blocker. Remaining work is throw feel tuning and future animation pass.
- Exactly what to do next: Keep `ThrowDirectionSource` set to main camera, run 10-15 throw tests, tune `ThrowForce`/`ThrowUpForce`/`ThrowStartOffset`, then choose next mechanic milestone (cooldown hooks vs scoring/reset loop).
