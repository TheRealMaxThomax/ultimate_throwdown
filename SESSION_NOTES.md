# Session Notes

Use this file as persistent project memory between chats.
Keep entries short, specific, and current.

## Project Snapshot
- **Current Goal:** Core ball gameplay first: stable grab/drop/throw before animation polish.
- **Current Branch:** `main` (tracking `origin/main`).
- **Build/Run Status:** s&box opens and scripts compile. Multiplayer grab/drop/throw still works. Ball size was increased (`main_ball` scale `0.4`) and passive push anti-abuse tuning is active in `CatchUpSpeedBoost` (`BallPushBlockRadius=30`, `BallPushApproachDot=0.1`). MUST-SOLVE first-contact client solidity bug was fixed by keeping colliders enabled during owning-client free-ball visual proxy in `BallClientFeel`.
- **Last Updated:** 30/04/26 (solidity fix landed)

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
- **Use host-approved RPC flow for grab/drop/throw in multiplayer.**
  - Why: Prevents client-side state divergence and keeps one source of gameplay truth.
  - Date: 2026-04-29
- **Use startup scene `scenes/throwdown_prototype.scene` with `GameNetworkManager` active.**
  - Why: Local multi-window networking tests require the correct gameplay scene and per-connection player spawning.
  - Date: 2026-04-29
- **Keep cosmetics sync in a dedicated visual component (`PlayerCosmeticsSync`) instead of `GameNetworkManager`.**
  - Why: Isolates visual/avatar timing logic from network spawn authority to avoid multiplayer spawn regressions.
  - Date: 2026-04-30
- **Use `ClothingContainer.CreateFromConnection(ownerConnection, false)` for remote cosmetics apply.**
  - Why: Remote clients may not have ownership verification for other players; `removeUnowned=true` can strip valid cosmetics.
  - Date: 2026-04-30
- **Lock player/clothing LOD after cosmetics apply (`LodOverride = 0`).**
  - Why: Prevents camera-distance/angle skin tone and cosmetic visual glitches caused by LOD transitions.
  - Date: 2026-04-30
- **Keep experimental ball push/lock systems scoped and removable.**
  - Why: Several anti-push experiments caused regressions; keep core grab/throw stable while iterating.
  - Date: 2026-04-30

## Constraints and Rules
Only include constraints that are easy to forget and expensive to violate.

- Engine/version constraints: s&box component workflow; compile errors can hide components from inspector list.
- Performance constraints: Keep gameplay logic simple for now; no heavy systems needed yet.
- Code style/conventions: Beginner-readable names, short methods, one source of truth for state.
- Tooling/workflow constraints: After script edits, allow recompile/restart editor if component does not appear.
- Networking constraints: Ball gameplay state is host-authoritative; avoid client-only gameplay mutation paths.
- Cosmetics/visual constraints: Keep avatar/cosmetics apply logic visual-only and isolated from spawn authority components.
- Refactor safety constraint: Mark cleanup chats as `refactor-only, no behavior change intended`, keep scope to 1-2 files, and run quick regression checks after.

## Network-Safe Rules (Always Apply)
Treat these as mandatory implementation rules for all new gameplay features.

1. **Authority first (before coding):**
   - Decide who owns truth for gameplay outcomes (default: host/server).
   - Do not let clients finalize gameplay outcomes.
2. **Input is a request, not a result:**
   - Client sends action request to host (for example tackle, grab, throw).
   - Host validates and applies final result.
3. **Sync final gameplay state:**
   - Sync authoritative end states (`IsHolding`, `IsRagdolled`, cooldown state, etc.).
   - Do not rely on local-only gameplay booleans for shared truth.
4. **Separate gameplay and visuals:**
   - Gameplay logic must be authoritative and replicated.
   - Client-only effects/animations are fine, but must not decide outcomes.
5. **2-window validation is required before done:**
   - Host action visible correctly on client.
   - Client action visible correctly on host.
   - Rapid repeat/spam test.
   - At least one edge case (jump/mid-air/moving fast/timing overlap).

### Feature Build Pattern (Use Every Time)
1. Define authority and synced state fields first.
2. Implement host RPC request flow for actions.
3. Apply outcomes on host only, then replicate/sync.
4. Add minimal visual handling after gameplay sync is stable.
5. Run multiplayer checklist before marking feature complete.

## Collaboration Preferences
- Experience level: Beginner (new to development).
- Communication style: Explain steps in simple language, avoid jargon where possible, and include quick "what/why" context for commands and changes.
- Reminder: If naming confusion/repeated renames starts happening, propose expanding Naming Canon (especially before team size or system count grows).

## Known Issues / Risks
- [ ] TOP PRIORITY (next session): keep full ball physics feel, but remove reliable passive dribble from player body contact.
  - What Max wants: `grab`, `throw`, and `kick` are the only intentional/reliable ways to move the ball.
  - Must preserve: ball still collides/bounces off players and world, rolls naturally, and does not feel frozen/dead.
  - Problem to prevent: players should not be able to effectively "walk the ball" with capsule contact as a strong movement mechanic.
  - Recommended solution:
    1. Keep player-ball collisions enabled (for believable bounce/contact), but treat them as low-authority movement.
    2. Add host-authoritative anti-dribble dampening on the ball during recent player-contact windows.
    3. In that window, clamp/dampen horizontal velocity in low/mid "dribble band" speeds, while allowing higher throw/kick speeds to carry.
    4. Add a short post-kick/throw grace window where heavy dampening is reduced (so intentional actions still feel strong).
    5. Tune physics materials (lower player-vs-ball friction, keep ball bounce reasonable) to reduce sticky rubbing/dribble.
  - Suggested implementation shape: small dedicated ball component (for example `BallPlayerContactDampener`) with inspector tuning values and host-only velocity correction.
  - Owner: Max
  - Next action: Implement minimal host-only dampener, run 2-3 window tests, and tune until passive dribble is weak but bounce/roll still feels alive.
- [x] MUST SOLVED: client first-contact non-solid bug (could walk/jump into ball until first grab+drop).
  - Impact: Previously high exploit risk and inconsistent feel; now mitigated with deterministic collider-on behavior.
  - Owner: Max
  - Fix landed: `BallClientFeel` free-ball owner proxy now disables rigidbodies only and keeps colliders enabled.
  - Validation to keep: Fresh host + 2 clients, no pickup/drop first, confirm immediate solid contact on all windows.
- [ ] Throw tuning still placeholder (force/arc may need balancing).
  - Impact: Throw feel may be too strong/weak depending on map and movement speed.
  - Owner: Max
  - Next action: Tune `ThrowForce`, `ThrowUpForce`, and `ThrowStartOffset` in inspector playtests.
- [ ] While charging throw, walk/run animations can still play in place even though movement is locked.
  - Impact: Mechanics are correct, but visual polish is rough during charge.
  - Owner: Max
  - Next action: Handle with explicit charge/throw animation states in animation pass.
- [ ] Future animation integration not done yet.
  - Impact: Visual quality lower for now, but mechanics are playable.
  - Owner: Max
  - Next action: Add throw/run-holding animations after mechanics lock-in.
- [ ] Multiplayer still needs broader stress testing (rapid pickup/drop/throw spam, jump-drop edge cases, longer sessions).
  - Impact: Core behavior is much better, but edge desync risk remains until stress tests pass repeatedly.
  - Owner: Max
  - Next action: Run 2-window multiplayer consistency pass for 15-20 minutes and log any repro steps.
- [ ] Free-ball collision feel on client still not equal to host feel.
  - Impact: Ball is largely consistent across screens, but client contact feels less direct/smooth than host contact.
  - Owner: Max
  - Next action: Tune/iterate free-ball client visual follow settings and validate with repeatable push tests from multiple angles.
- [ ] Need one fresh regression pass after cosmetics + LOD stabilization commit.
  - Impact: Main flows are passing, but one clean verification pass helps catch any side effects before moving to new features.
  - Owner: Max
  - Next action: Run 2-3 player session and validate pickup/drop/throw, cosmetics visibility, and camera-distance render stability for 10+ minutes.

## Current Plan (Top 3)
1. TOP PRIORITY: implement anti-dribble behavior so passive player body-push is not a reliable ball movement mechanic, while preserving natural bounce/roll physics.
2. Run focused multiplayer regression/tuning pass: fresh startup no-warmup first-contact test, then passive push attempts, then rapid pickup/drop/throw spam.
3. Add intentional kick mechanic as the primary free-ball movement action and tune dampener around kick/throw windows.

## Proven Fix Recipes (Reuse)
Use these when the same symptom appears again.

### Client Free-Ball Jitter (Host looks good, client looks floaty/jittery)
**Symptom**
- Host sees natural bounces, client sees rapid up/down jitter or floaty correction (especially jump-apex drop tests).

**What actually fixed it**
1. Keep host as full gameplay/physics authority (`BallGrab` remains source of truth).
2. In `BallClientFeel`, disable local free-ball rigidbody simulation on owning client during free-ball visual follow, but keep colliders enabled (prevents local physics fighting host corrections while preserving first-contact solidity).
3. Buffer recent host snapshots on client (`position`, `rotation`, timestamp) and render from delayed buffered target (`InterpolationDelay`) instead of chasing latest transform each frame.
4. Avoid double smoothing: when buffered target is available, render directly from buffered interpolation result.
5. Add velocity-aware interpolation path (snapshot linear velocity + Hermite position interpolation) to better preserve bounce/throw arc shape.

**What did NOT help enough**
- Tuning only `FreeBallVisualFollowSharpness` / `ContactBoostSharpness` / `ContactBoostDuration` without changing interpolation approach.
- Hard snap thresholds for normal motion (reduced one jitter pattern but made roll/push look framey/teleporty).

**Validation path**
- 2+ windows.
- Repeat exact apex jump-drop test and normal run/push test from same start point.
- Confirm host/client ball path similarity improved without introducing framey motion.

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
- Client feel helper component: `BallClientFeel`
- Throw helper component: `BallThrow`
- Catch-up movement component: `CatchUpSpeedBoost`
- Throw charge display component: `ThrowChargeBar`
- Hold-state public getter: `IsHolding`
- Held-ball public getter: `HeldBall`
- Ball selection properties: `MainBall`, `MainBallName`
- Interaction properties: `InteractDistance`, `InteractAction`, `HoldAnchor`, `PromptText`
- Internal ball references in `BallGrab`: `ballObject`, `ballOriginalParent`, `ballCollidersToRestore`, `ballBodiesToRestore`
- Multiplayer manager component: `GameNetworkManager`
- Sync property in `BallGrab`: `NetIsHolding`
- Host-synced ball transform properties in `BallGrab`: `NetHeldBallWorldPosition`, `NetHeldBallWorldRotation`
- Host-synced ball velocity properties in `BallGrab`: `NetHeldBallLinearVelocity`, `NetHeldBallAngularVelocity`
- Multiplayer debug property: `EnableNetDebugLogs`
- Free-ball feel properties in `BallClientFeel`: `FreeBallVisualFollowSharpness`, `ContactBoostSharpness`, `ContactBoostDuration`
- Snapshot interpolation properties in `BallClientFeel`: `InterpolationDelay`, `MaxSnapshots`
- Throw properties in `BallThrow`: `ThrowAction`, `ThrowForce`, `ThrowUpForce`, `ThrowStartOffset`, `PickupDelayAfterThrow`, `ThrowDirectionSource`
- Charge tuning properties in `BallThrow`: `MinThrowChargeTime`, `MaxThrowChargeTime`, `MinThrowForceMultiplier`, `MinThrowUpForceMultiplier`
- Charge-state public getter in `BallThrow`: `IsChargingThrow`
- Catch-up movement properties in `CatchUpSpeedBoost`: `ForwardAction`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`, `MinForwardInput`
- Anti-dribble properties in `CatchUpSpeedBoost`: `BallPushBlockRadius`, `BallPushApproachDot`
- Throw charge bar property in `ThrowChargeBar`: `ChargeBarOffset`
- Core release method in `BallGrab`: `ReleaseHeldBall()`
- Pickup lockout method in `BallGrab`: `BlockPickupForSeconds(float seconds)`
- Cosmetics sync component: `PlayerCosmeticsSync`
- Cosmetics sync properties: `FirstApplyDelay`, `RetryInterval`, `MaxApplyAttempts`, `LockHighestLodAfterApply`, `EnableDebugLogs`

## Next Chat Kickoff
Paste this at the start of a new session:

`Read SESSION_NOTES.md first, continue from Current Plan, and propose any needed updates before coding.`

## End-of-Session Handoff
Update this checklist before ending a chat:

- What changed: Fixed MUST-SOLVE first-contact solidity bug by updating `BallClientFeel` free-ball owner proxy to keep colliders enabled while disabling rigidbody simulation. Also set next-session top priority: keep natural ball physics but remove reliable passive dribble/body-push.
- What is still blocked: Passive body-push/dribble still needs host-authoritative dampening/tuning so contact remains believable but not an effective movement exploit.
- Exactly what to do next: Build a small host-only contact dampener component on the ball (recent player-contact window, dribble-band horizontal damping/clamp, lighter damping during post-kick/throw grace window), then run 2-3 window tuning tests until bounce/roll feel is preserved and passive dribble is weak.
