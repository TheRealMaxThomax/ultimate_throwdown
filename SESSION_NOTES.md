# Session Notes

Use this file as persistent project memory between chats.
Keep entries short, specific, and current.

## Project Snapshot
- **Current Goal:** Tackle system — ragdoll fly-back working on client, stand-up and impulse need final fixes.
- **Current Branch:** `feature/class-system` (pushed to origin).
- **Build/Run Status:** Compiles clean. Grab/drop/throw works. ClassData wired to CatchUpSpeedBoost and BallThrow. PlayerTackle detection works. Client ragdolls on tackle. Stand-up broken (see Current Tackle Debug Status below).
- **Last Updated:** 05/05/26 (class system + tackle system session)

## Code Folder Structure
- `Code/Ball/` — BallGrab, BallThrow, BallClientFeel, ThrowChargeBar
- `Code/Player/` — CatchUpSpeedBoost, PlayerCosmeticsSync, ClassData, PlayerClass, PlayerTackle
- `Code/Network/` — GameNetworkManager
- New systems get their own folder (e.g. `Code/Ultimates/`, `Code/UI/`)

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
- **Auto-grab on contact: walking into the ball picks it up automatically.**
  - Why: Client-side prediction required to make body-push feel consistent across host/client is too complex for current stage. Auto-grab is the same compromise made by Extreme Football Throwdown (Garry's Mod). Intentional ball interactions are grab, drop, and throw only (no separate kick).
  - Date: 2026-05-04
- **Removed `BallPlayerPushLock` entirely.**
  - Why: Auto-grab intercepts players before they reach push range; the component was redundant.
  - Date: 2026-05-04
- **Composition over inheritance; flat components over Core/Hub architecture.**
  - Why: s&box is built for component composition. A central "Core data" file creates God Object risks. Each component owns one job.
  - Date: 2026-05-04
- **No separate kick mechanic.**
  - Why: Auto-grab on contact already handles moving the free ball into play; adding kick would duplicate that role. Focus stays on grab/drop/throw feel and tuning.
  - Date: 2026-05-04
- **Drop placement uses player-facing yaw (not hold anchor forward).**
  - Why: Keeps drop side consistent relative to current movement/facing instead of depending on anchor orientation.
  - Date: 2026-05-04
- **Dropped ball inherits scaled player velocity via inspector slider.**
  - Why: Makes drop feel responsive while keeping tuning control (`DropVelocityScale`) and preserving no-push guardrails.
  - Date: 2026-05-04

## Constraints and Rules
Only include constraints that are easy to forget and expensive to violate.

- Engine/version constraints: s&box component workflow; compile errors can hide components from inspector list.
- Performance constraints: Keep gameplay logic simple for now; no heavy systems needed yet.
- Code style/conventions: Beginner-readable names, short methods, one source of truth for state.
- Tooling/workflow constraints: After script edits, allow recompile/restart editor if component does not appear.
- Networking constraints: Ball gameplay state is host-authoritative; avoid client-only gameplay mutation paths.
- Cosmetics/visual constraints: Keep avatar/cosmetics apply logic visual-only and isolated from spawn authority components.
- Refactor safety constraint: Mark cleanup chats as `refactor-only, no behavior change intended`, keep scope to 1-2 files, and run quick regression checks after.
- Scene/inspector constraint: Agent does not edit `.scene` or prefab files. All in-engine setup is done by Max.

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

## Prerequisites for Planned Systems
These are small gaps in existing code that must be filled before the planned systems can be built. Do not build these proactively — add them at the time they're needed.

- [ ] `CatchUpSpeedBoost` needs a `public bool IsAtChargeSpeed { get; private set; }` getter so the tackle system can check if the attacker qualifies.
- [ ] `CatchUpSpeedBoost` speed/timing properties need to be driven from `ClassData` instead of inspector values.
- [ ] `BallThrow` force and charge timing need to accept per-class multipliers from `ClassData`.
- [ ] Player capsule size (height/radius) needs to be set from `ClassData.ModelScale` when class system is built.

## Known Issues / Risks
- [x] Anti-dribble / passive push resolved via auto-grab on contact.
  - Decided: body-push is too complex to make consistent across host/client. Auto-grab is the EF Throwdown compromise and fits the game design.
- [x] MUST SOLVED: client first-contact non-solid bug (could walk/jump into ball until first grab+drop).
  - Fix landed: `BallClientFeel` free-ball owner proxy now disables rigidbodies only and keeps colliders enabled.
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
- [ ] Need one fresh regression pass after this session's changes.
  - Impact: Auto-grab + cleanup touches several files; one clean 2-window pass recommended.
  - Owner: Max
  - Next action: Fresh host + client session, test grab/drop/throw/auto-grab, confirm no regressions.

## Current Tackle Debug Status (Resume Here Next Chat)

### What works
- Tackle detection: host distance+direction+charge-speed check fires correctly. Debug log `[Tackle] Player_Thomax → Player_Thomax (2)` appears.
- Sync: `NetIsRagdolled` and `NetRagdollImpulse` sync from host to client correctly. Both machines detect state change.
- Client ragdolls visually when tackled. `ApplyRagdollLocally` disables PlayerController and enables ModelPhysics on the client.

### What is broken
- **Stand-up broken:** `StandUpLocally` was previously failing to re-enable `PlayerController` because `Components.Get<PlayerController>()` returns null for disabled components. Fixed in last code change by switching to cached `playerController` field. **Not yet tested** — this was the last code change before running out of context.
- **Impulse not applying on host:** `PhysicsGroup` is always null on the host for the client's Body child. This is expected — the client owns the physics. Impulse should apply on client. Client logs for PhysicsGroup status not yet seen.
- **Host visual:** On host screen the victim stays standing (host can't simulate client-owned ragdoll physics). This may be acceptable — victim ragdolls on their own screen which is what matters.

### Exact last code change (not yet tested)
Cached `modelPhysics` in `OnStart` using `FindMode.EverythingInSelfAndDescendants`. Both `ApplyRagdollLocally` and `StandUpLocally` now use cached `playerController` and `modelPhysics` fields instead of re-fetching. This should fix stand-up.

### Next test steps
1. Compile and test — host runs into client at charge speed
2. Check client console for: `PhysicsGroup ready | Found=True` and `StandUpLocally | Controller re-enabled=True`
3. Check if client ragdolls AND stands back up after 2 seconds
4. If `PhysicsGroup=False` on client, the issue is ModelPhysics not being found — check `modelPhysics` is not null in `OnStart` log

### Scene setup that must be correct
- `PlayerTackle` on root player object (gets dropped on recompile — check every session)
- `Model Physics` on the **Body child** (the child with `SkinnedModelRenderer`), NOT on root player
- `Model Physics` must be **disabled** (unchecked) by default
- `PlayerClass` on root player with Speedster/Sniper/Juggernaut `.cdata` asset assigned
- `CatchUpSpeedBoost` on root player
- All three `.cdata` assets must have values set manually (s&box doesn't auto-apply C# defaults):
  - Movement: StartMoveSpeed=140, SprintMoveSpeed=220, CatchUpMoveSpeed=320, TimeToSprintSpeed=2, TimeToCatchUpSpeed=4
  - Tackle: TriggerSphereRadius=40, RagdollDuration=2, PostTackleInvincibilityDuration=1, BallLaunchForceOnTackle=500, BallPickupLockoutAfterTackle=1.5
  - Mass: Mass=80 (same placeholder for all three for now)

## Current Plan (Top 3)
1. Finish tackle system: confirm stand-up works, confirm impulse applies (client flies on tackle), tune force values.
2. Regression pass: grab/drop/throw/auto-grab still working after class system wiring.
3. Throw tuning pass and broader multiplayer stress testing.

---

## Planned Systems (Design Locked, Not Yet Built)

### Class System

Three player classes. **All player stats read from `ClassData` — never hardcoded on components.**

| Class | Mass | Size | Speed Ramp | Throw |
|---|---|---|---|---|
| Speedster | Lightest | Smallest | Reaches charge speed fastest | Default |
| Sniper | Middle | Middle | Middle ramp | Faster charge, higher force |
| Juggernaut | Heaviest | Biggest | Slowest ramp | Default |

**`ClassData` fields (s&box `[GameResource]`):**

| Field | Type | Notes |
|---|---|---|
| `ClassName` | string | Display name |
| `Mass` | float | Gameplay-only — used in tackle formula, not a physics property |
| `CapsuleHeight` | float | Player capsule height |
| `CapsuleRadius` | float | Player capsule radius |
| `ModelScale` | float | Visual scale |
| `TriggerSphereRadius` | float | Tackle detection sphere radius; scales with class size |
| `StartMoveSpeed` | float | Replaces hardcoded value in `CatchUpSpeedBoost` |
| `SprintMoveSpeed` | float | Replaces hardcoded value in `CatchUpSpeedBoost` |
| `CatchUpMoveSpeed` | float | Replaces hardcoded value in `CatchUpSpeedBoost` |
| `TimeToSprintSpeed` | float | Speedster lowest, Juggernaut highest |
| `TimeToCatchUpSpeed` | float | Speedster lowest, Juggernaut highest |
| `WalkTurnSpeed` | float | Turn rate while walking |
| `RunTurnSpeed` | float | Turn rate while sprinting |
| `ChargeTurnSpeed` | float | Turn rate at charge speed |
| `MomentumMultiplier` | float | How much momentum carries on direction change/stop |
| `ThrowPower` | float | Sniper > 1.0, others 1.0 |
| `DodgeCooldown` | float | Cooldown between dodges |
| `DodgeDistance` | float | How far a dodge travels |
| `DodgeInvincibilityWindow` | float | Invincibility frames during dodge |
| `RagdollDuration` | float | Seconds down after being tackled (default 2s) |
| `PostTackleInvincibilityDuration` | float | Short invincibility after standing back up |
| `BallLaunchForceOnTackle` | float | How hard ball flies when tackle-dropped |
| `BallPickupLockoutAfterTackle` | float | Seconds ball can't be grabbed after tackle drop |
| `TackleChargeRampRate` | float | Juggernaut passive: bonus multiplier per second at charge speed |
| `MaxTackleChargeBonus` | float | Juggernaut passive: cap on the ramp bonus |
| `IgnoreWeaponSpeedPenalty` | bool | If true, class ignores weapon-hold speed slowdown |
| `WeaponSwingSpeedPenaltyDuration` | float | Seconds reset to walking speed after any weapon swing |

**Global (not in ClassData):**
- `TackleImpulseMultiplier` — single tuning knob on the tackle component, not per-class

**Explicitly removed:**
- `ThrowChargeSpeedMultiplier` — dropped; Sniper's identity is `ThrowPower` + dodge-while-charging passive
- `MaxSpeed`, `Acceleration` — dropped; keeping three-tier speed system (`StartMoveSpeed`/`SprintMoveSpeed`/`CatchUpMoveSpeed`)
- `WeaponMissSpeedPenalty` — renamed to `WeaponSwingSpeedPenaltyDuration` (penalty applies on all swings, not miss-only)
| `PostTackleInvincibilityDuration` | float | Short post-ragdoll window, exact value TBD via playtesting |
| `BallLaunchForceOnTackle` | float | How hard ball flies when tackle-dropped |
| `BallPickupLockoutAfterTackle` | float | Seconds ball can't be grabbed after a tackle drops it |

**Notes:**
- `Mass` is gameplay-only. `PlayerController` is a character controller with no physics mass. The tackle formula uses this float directly.
- Class size affects model scale, capsule dimensions, and trigger sphere radius — all live in `ClassData`.
- `CatchUpSpeedBoost` will need to read from `ClassData` instead of its own inspector properties when this is built. Ask before modifying it.
- `BallThrow` force will need to read from `ClassData` (`ThrowPower`). Ask before modifying it.
- Weapon system is future work. `IgnoreWeaponSpeedPenalty` and `WeaponSwingSpeedPenaltyDuration` are in ClassData now so the per-class tuning is ready when weapons get built.
- Weapon rules: holding a weapon forces all classes to Juggernaut's `TimeToCatchUpSpeed` (unless `IgnoreWeaponSpeedPenalty = true`). Any weapon swing resets speed to walking speed for `WeaponSwingSpeedPenaltyDuration` seconds. Weapons are one-use: ranged/explosive consumed on use; melee only breaks on a successful hit.
- Passives and ults are designed (see below) but not being built yet. They will be selectable at match/round start.

---

### Dodge Mechanic (Planned, Not Built Yet)

- Players can dodge left or right on input.
- Has a cooldown.
- Lives in its own component: `PlayerDodge`.
- Must expose `public bool IsDodging` so the tackle system can query it on the host.
- Passives interact with dodge state — build `PlayerDodge` as a standalone system before wiring any passive logic.

---

### Passives and Ults (Designed, Not Being Built Yet)

Selectable per class at match/round start.

**Speedster**
- *Passive:* Short speed boost on successfully dodging a tackle. Detection: when host fires a tackle attempt and victim's `IsDodging` is true → tackle is nullified → trigger speed boost. If `IsDodging` is false → normal tackle applies.
- *Ult:* Lightning dash. Long-distance charge in a fixed direction. Charge-up time required. **Facing direction is snapshotted at the moment the charge begins — player cannot adjust aim during charge-up.** On contact with a player, delivers a powerful tackle.

**Sniper**
- *Passive:* Can dodge while charging throw. Other classes cannot dodge during charge. (`PlayerDodge` must check if player is Sniper before allowing dodge during charge state.)
- *Ult:* Sonic boom throw. Ball travels its normal arc, but creates an AOE ragdoll zone along the flight path each frame. Requires a flag (`IsUltThrow` or a projectile mode) on the ball so it knows to run sweep logic. Most complex system of the four — build last.

**Juggernaut**
- *Passive:* The longer they stay at charge speed, the more powerful their tackle. Bonus accumulates at `TackleChargeRampRate` per second, capped at `MaxTackleChargeBonus`. Resets immediately when they drop below charge speed (counterplay: slow them down to reset their stack).
- *Ult:* AOE circular stomp. Centered on the Juggernaut. All players within radius are sent flying as ragdolls.

---

### Tackle System

**Core rules:**
- Trigger sphere on each player (radius from `ClassData.TriggerSphereRadius`), slightly larger than capsule. **Never `OnPhysicsCollision`** — unreliable in multiplayer.
- Tackles only trigger when attacker is at charge speed. Need `IsAtChargeSpeed` public getter on `CatchUpSpeedBoost` (doesn't exist yet).
- Velocity + direction check before applying: dot product of (attacker velocity direction) · (direction to victim) must exceed a tunable threshold (~0.5, roughly 60° forward cone). Threshold is a tunable property.
- Host-authoritative. Trigger detection and ragdoll application on host only.

**Impulse formula:**
```
massRatio = clamp(attacker.Mass / victim.Mass, 0.5, 2.5)
impulse = attackerVelocity × baseTackleForce × massRatio × TackleImpulseMultiplier
```
- `TackleImpulseMultiplier` is **global** (not per-class). Mass ratio already handles class differentiation. Add per-class only if playtesting proves it's needed.
- Result: Juggernaut → Speedster is strongest hit (high mass / low mass ≈ 2.5). Speedster → Juggernaut is weakest (low / high ≈ 0.5). Sniper is middle.

**On tackle hit:**
1. Victim enters full ragdoll — gets sent flying by impulse.
2. Victim drops ball; ball also gets launched (not just dropped). `BallLaunchForceOnTackle` from `ClassData`.
3. Ball pickup is locked out for `BallPickupLockoutAfterTackle` seconds (uses existing `BlockPickupForSeconds`).
4. After `RagdollDuration` (2s default) victim stands back up.
5. Post-tackle invincibility window starts (`PostTackleInvincibilityDuration`).

**Ghost collider bug (s&box known issue):**
- Add a 0.05s delay after calling `BecomeRagdoll` before applying the physics impulse.
- Without this, the collider isn't fully initialized and the impulse is ignored.

**Auto-grab stays as distance check (not trigger sphere):**
- `OnPhysicsCollision` unreliability is the reason tackles need trigger spheres. Auto-grab doesn't have that problem.
- Distance check runs every frame and only targets one known object. Switching to a trigger sphere adds editor setup and collider filtering for no gain.

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

### Body-Push Consistency (Host can push ball, client cannot)
**What was tried and failed:**
- Gain-detection (per-frame horizontal speed increase near player) — per-frame impulses too small to catch reliably.
- Grace-period after throw — ball still pushable during grace window.
- `BallPush` RPC component — correct pattern but adds complexity and round-trip delay makes it feel different to host.

**Decision:** Don't solve body-push. Use auto-grab instead. Same compromise as Extreme Football Throwdown (Garry's Mod).

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
- Interaction properties: `InteractDistance`, `InteractAction`, `HoldAnchor`
- BallGrab pickup/drop timing properties: `PickupDelayAfterDrop`, `DropperNoPushWindow`
- BallGrab drop no-push tuning properties: `DropperNoPushRadius`, `DropperMaxHorizontalSpeed`
- Drop tuning properties in `BallGrab`: `DropSideOffset`, `DropVelocityScale`
- BallGrab prompt property: `PromptText`
- Auto-grab rate limiter in `BallGrab`: `nextAutoGrabAttemptAt`
- Internal ball references in `BallGrab`: `ballObject`, `ballOriginalParent`, `ballCollidersToRestore`, `ballBodiesToRestore`
- Multiplayer manager component: `GameNetworkManager`
- Network manager properties: `PlayerTemplateName`, `DisableTemplateOnStart`
- Sync property in `BallGrab`: `NetIsHolding`
- Host-synced ball transform properties in `BallGrab`: `NetHeldBallWorldPosition`, `NetHeldBallWorldRotation`
- Host-synced ball velocity properties in `BallGrab`: `NetHeldBallLinearVelocity`, `NetHeldBallAngularVelocity`
- Client-read synced ball transform getters in `BallGrab`: `SyncedBallWorldPosition`, `SyncedBallWorldRotation`
- Client-read synced ball velocity getters in `BallGrab`: `SyncedBallLinearVelocity`, `SyncedBallAngularVelocity`
- Multiplayer debug property: `EnableNetDebugLogs`
- Free-ball feel properties in `BallClientFeel`: `FreeBallVisualFollowSharpness`, `ContactBoostSharpness`, `ContactBoostDuration`
- Snapshot interpolation properties in `BallClientFeel`: `InterpolationDelay`, `MaxSnapshots`
- Throw properties in `BallThrow`: `ThrowAction`, `ThrowForce`, `ThrowUpForce`, `ThrowStartOffset`, `PickupDelayAfterThrow`, `ThrowDirectionSource`
- Charge tuning properties in `BallThrow`: `MinThrowChargeTime`, `MaxThrowChargeTime`, `MinThrowForceMultiplier`, `MinThrowUpForceMultiplier`
- Charge-state public getter in `BallThrow`: `IsChargingThrow`
- Catch-up movement properties in `CatchUpSpeedBoost`: `ForwardAction`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`, `MinForwardInput`
- Throw charge bar property in `ThrowChargeBar`: `ChargeBarOffset`
- Core release method in `BallGrab`: `ReleaseHeldBall()`
- Ball ownership handoff method in `BallGrab`: `TransferBallOwnershipToHost()`
- Pickup lockout method in `BallGrab`: `BlockPickupForSeconds(float seconds)`
- Cosmetics sync component: `PlayerCosmeticsSync`
- Cosmetics sync properties: `FirstApplyDelay`, `RetryInterval`, `MaxApplyAttempts`, `LockHighestLodAfterApply`, `EnableDebugLogs`
- Class data resource: `ClassData`
- Class data fields: `ClassName`, `Mass`, `CapsuleHeight`, `CapsuleRadius`, `ModelScale`, `TriggerSphereRadius`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`, `WalkTurnSpeed`, `RunTurnSpeed`, `ChargeTurnSpeed`, `MomentumMultiplier`, `ThrowPower`, `DodgeCooldown`, `DodgeDistance`, `DodgeInvincibilityWindow`, `RagdollDuration`, `PostTackleInvincibilityDuration`, `BallLaunchForceOnTackle`, `BallPickupLockoutAfterTackle`, `TackleChargeRampRate`, `MaxTackleChargeBonus`, `IgnoreWeaponSpeedPenalty`, `WeaponSwingSpeedPenaltyDuration`
- Tackle system component (planned): `PlayerTackle`
- Tackle tuning properties (planned): `BaseTackleForce`, `TackleImpulseMultiplier` (global), `TackleDirectionThreshold`
- Charge speed public getter (planned): `IsAtChargeSpeed` on `CatchUpSpeedBoost`
- Dodge component (planned): `PlayerDodge`
- Dodge state public getter (planned): `IsDodging` on `PlayerDodge`
- Juggernaut passive ClassData fields: `TackleChargeRampRate`, `MaxTackleChargeBonus`

## Next Chat Kickoff
Paste this at the start of a new session:

`Read SESSION_NOTES.md first, continue from Current Plan, and propose any needed updates before coding.`

## End-of-Session Handoff
- What changed (05/05/26): ClassData [GameResource] built. PlayerClass component built. CatchUpSpeedBoost reads movement stats from ClassData (with inspector fallback). BallThrow reads ThrowPower from ClassData. IsAtChargeSpeed public getter added to CatchUpSpeedBoost. PlayerTackle built: host distance+direction+charge-speed detection, synced NetIsRagdolled/NetRagdollImpulse, ApplyRagdollLocally/StandUpLocally run on all machines. Last fix: switched to cached playerController/modelPhysics fields to fix stand-up. Not yet retested after last fix.
- What is still blocked: Tackle stand-up and impulse application not confirmed working. See Current Tackle Debug Status above.
- Exactly what to do next: Open new chat, read SESSION_NOTES, compile, test tackle, check client console logs for PhysicsGroup and StandUpLocally results.
