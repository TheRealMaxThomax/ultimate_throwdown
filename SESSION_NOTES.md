# Session Notes

Use this file as persistent project memory between chats.
Keep entries short, specific, and current.

## Project Snapshot
- **Current Goal:** Tackle system — ragdoll launch now consistent and working. `TackleLaunchSpeed` is live tuning knob. Needs force/arc tuning in inspector. Remaining polish: camera lerp on stand-up, naked ragdoll (clothing excluded).
- **Current Branch:** `feature/class-system` (pushed to origin).
- **Build/Run Status:** Compiles clean. Grab/drop/throw works. Tackle detection, ragdoll visual, launch force, stand-up, invincibility window all working. Both screens see the ragdoll fly in a consistent direction.
- **Last Updated:** 06/05/26 session 3 (tackle launch consistency + PhysicsGroup.Velocity fix)

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
- **Tackle launch direction uses flat horizontal vector toward victim, not attacker velocity.**
  - Why: Attacker velocity includes vertical Z (jump/slope), causing random launch angles. Victim-direction is always consistent regardless of attacker movement state.
  - Date: 2026-05-06
- **Ragdoll launch uses `PhysicsBody.Velocity` per-body (via `body.Component.PhysicsBody.Velocity`), NOT `Rigidbody.Velocity` or any `PhysicsGroup` path.**
  - Why: `ModelPhysics.PhysicsGroup` is always null on spawned ragdolls. `PhysicsBody.PhysicsGroup` is also null. `Rigidbody.Velocity` and `Rigidbody.ApplyImpulse` are overridden by the constraint solver. `PhysicsBody.Velocity` bypasses the component layer and writes directly to the physics body — velocity reads back correctly and physics simulates it.
  - Date: 2026-05-06 (session 4 — corrected from session 3's now-outdated entry)
- **NetworkSpawn the ragdoll AFTER setting launch velocity, not before.**
  - Why: Setting velocity on a `NetworkSpawn()`-ed ragdoll before the velocity step was causing `PhysicsGroup` to appear null (bodies disconnected from local physics world). Waiting 0.05s locally, applying velocity, then calling `NetworkSpawn()` keeps the physics group live during velocity assignment.
  - Date: 2026-05-06
- **`ModelPhysics.MotionEnabled = true` and `IgnoreRoot = false` must be set explicitly on the spawned ragdoll.**
  - Why: `MotionEnabled` ensures physics drives the renderer (not the other way around). `IgnoreRoot = false` ensures the root physics body drives the GameObject's `WorldPosition` so `NetRagdollPosition` (which reads `ragdollObject.WorldPosition`) actually tracks the ragdoll as it flies.
  - Date: 2026-05-06
- **Victim's `PlayerController` and `Collider` components disabled immediately in `ExecuteTackle` on the host, not just in `ApplyRagdollLocally`.**
  - Why: `ApplyRagdollLocally` runs one frame after the tackle fires. The ragdoll spawns in that same frame, so bones would spawn inside an active capsule and get ejected in random directions before the launch velocity fires. Immediate disable closes that gap.
  - Date: 2026-05-06

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

- [x] `CatchUpSpeedBoost` needs a `public bool IsAtChargeSpeed { get; private set; }` getter so the tackle system can check if the attacker qualifies.
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

## Current Tackle Status (Resume Here Next Chat)

### What works
- Tackle detection: host distance+direction+charge-speed check fires correctly.
- **Ragdoll visual: WORKING on both screens.** Host spawns a separate `PlayerRagdoll` GameObject with the victim's base model + `ModelPhysics`. `NetworkSpawn()` makes it visible to all clients. Physics runs on the host without client transform ownership conflicts.
- Player model hidden during ragdoll: all renderers cached at tackle time (`hiddenRenderers` list) and re-enforced to `Enabled=false` every frame — catches cosmetics added after spawn and prevents anything re-enabling them.
- Camera follows ragdoll: owner sets `WorldPosition = NetRagdollPosition` (synced from host each frame), camera offset computed at tackle time.
- Stand-up: controller re-enables, player snaps to `NetStandUpPosition` (ragdoll's landing position), renderers re-enabled.
- Post-tackle invincibility window working.
- `Bodies[0]` = pelvis confirmed. Launch uses `PhysicsGroup.Velocity` (not per-body), so this is informational only.

### What is still missing / known issues
- **Launch force tuning in progress:** `TackleLaunchSpeed` is confirmed working. At 150 the ragdoll only travels ~35 units (looks like it collapses in place). At 600 it's clearly visible. At 2000 it flies off the map. Good tuning range is probably 400–800 — dial in with in-game feel.
- **Ragdoll is naked:** Only base body model copied to ragdoll. Clothing SkinnedModelRenderers copied to the same GameObject don't get driven by `ModelPhysics` — they sit in T-pose as a ghost. Decision: accept naked ragdoll for now; revisit clothing on ragdoll when a multi-renderer physics approach is found.
- **Camera snap on stand-up:** One-frame camera teleport when `PlayerController` re-enables. Fix is a lerp — low priority.

### Scene setup that must be correct every session (recompile may drop some)
- `PlayerTackle` on root player object (gets dropped on recompile — check every session)
- `PlayerClass` on root player with Speedster/Sniper/Juggernaut `.cdata` asset assigned
- `CatchUpSpeedBoost` on root player
- **Do NOT add `ModelPhysics` to the player prefab** — it is no longer used on the player object. The ragdoll is a separately spawned object.
- All three `.cdata` assets must have values set manually:
  - Movement: StartMoveSpeed=140, SprintMoveSpeed=220, CatchUpMoveSpeed=320, TimeToSprintSpeed=2, TimeToCatchUpSpeed=4
  - Tackle: TriggerSphereRadius=40, RagdollDuration=2, PostTackleInvincibilityDuration=1, BallLaunchForceOnTackle=500, BallPickupLockoutAfterTackle=1.5
  - Mass: Mass=80 (same placeholder for all three for now)

## Current Plan (Top 3)
1. **Tune ragdoll launch force** — `TackleLaunchSpeed` confirmed working. Tested range: 150 (barely moves), 600 (good), 2000 (off the map). Aim for 400–800. Also tune upward arc (`Vector3.Up * 0.35f` in `SpawnRagdollObject`) — reduce toward `0.2f` if you want a flatter, more horizontal fly.
2. **Camera lerp on stand-up** — one-frame snap when controller re-enables. Low priority until ragdoll feel is tuned.
3. **Regression pass** — grab/drop/throw/auto-grab still working after all tackle changes this session.

---

## Ragdoll Visual Research (Findings from 06/05/26)

### Why ragdolling the player object itself fails
All approaches that try to ragdoll the player's own `GameObject` hit the same wall: **the client owns the player's transform and overrides physics every frame.**

- `IgnoreRoot=false` + `ModelPhysics` on player: host physics moves the body, client keeps syncing `WorldPosition` back. Character freezes from client's view.
- `IgnoreRoot=true` + `SimulateRagdoll` (carry bones by delta): bones travel with root on owner, but physics doesn't simulate on proxy machines so host sees no collapse. Also: additional `SkinnedModelRenderer` components (clothing) on the same object don't get driven by `ModelPhysics` and appear as T-pose ghost.
- **Rule: Do not attempt to use `ModelPhysics` on a client-owned networked player object. It will not work.**

### Option D — Spawn a separate host-owned ragdoll object (CURRENT APPROACH, WORKING)
The key insight from GMod's EFT and s&box's sandbox ragdolls: **don't ragdoll the player — spawn a separate ragdoll object that the host owns.**

How it works:
1. Host spawns a new `GameObject` ("PlayerRagdoll") at victim's position with victim's base model + `ModelPhysics`.
2. `NetworkSpawn()` makes it visible on all clients automatically. Physics runs on host, syncs to all.
3. Host sets `PhysicsGroup.Velocity` after 0.05s init delay (wait for physics bodies to initialise). All bones fly as a unit; joints settle naturally.
4. Host syncs ragdoll's `WorldPosition` â†’ `NetRagdollPosition` each frame. Owner sets own `WorldPosition = NetRagdollPosition` to follow for camera.
5. On stand-up: `NetStandUpPosition` = ragdoll's final position. Owner snaps there. Ragdoll destroyed.
6. Player renderers hidden via `hiddenRenderers` list (cached at tackle time, re-enforced every `OnUpdate` frame).

### Key API facts confirmed
- `modelPhysics.Bodies` returns 16 bodies for citizen model — valid for inspecting/identifying bones.
- `Bodies[0].Component.GameObject.Name = "pelvis"` — the root/pelvis body is index 0.
- **`modelPhysics.PhysicsGroup.ApplyImpulse(vel, true)`** — correct API for launching the whole ragdoll. `withMass=true` multiplies impulse by each body's mass so every bone gets the same velocity change. Also wakes sleeping bodies.
- Must wait until `PhysicsWereCreated == true && PhysicsGroup != null` before calling — `PhysicsGroup.BodyCount` can return 0 even when bodies exist (different list from `ModelPhysics.Bodies`). `PhysicsWereCreated` is the definitive ready flag.
- `PhysicsGroup.Velocity = x` does not reliably work — group may have 0 bodies at the time it's set, or may not wake sleeping bodies. Use `ApplyImpulse` instead.
- `body.Component.Velocity = x` per-body does NOT reliably work for ragdoll launch — the constraint solver ignores or overrides it. Do not use for launch.
- Additional `SkinnedModelRenderer` on the same ragdoll `GameObject` as `ModelPhysics` are NOT driven by physics — they appear as T-pose ghost. Only link one renderer to `ModelPhysics`.
- `GetAll<SkinnedModelRenderer>` for hiding must be called at tackle time, not at `OnStart` — cosmetics are added dynamically after spawn.

### Remaining ragdoll polish (not blocking)
- **Naked ragdoll:** Only base model on ragdoll (no clothing). Clothing on a separate physics-driven ragdoll requires investigation of multi-renderer physics approaches.

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
| `TackleChargeRampRate` | float | Juggernaut passive: bonus multiplier per second at charge speed |
| `MaxTackleChargeBonus` | float | Juggernaut passive: cap on the ramp bonus |
| `IgnoreWeaponSpeedPenalty` | bool | If true, class ignores weapon-hold speed slowdown |
| `WeaponSwingSpeedPenaltyDuration` | float | Seconds reset to walking speed after any weapon swing |

**Global (not in ClassData):**
- `TackleLaunchSpeed` — single force tuning knob on `PlayerTackle`, not per-class. Class multipliers applied on top when class system is built.

**Explicitly removed:**
- `ThrowChargeSpeedMultiplier` — dropped; Sniper's identity is `ThrowPower` + dodge-while-charging passive
- `MaxSpeed`, `Acceleration` — dropped; keeping three-tier speed system (`StartMoveSpeed`/`SprintMoveSpeed`/`CatchUpMoveSpeed`)
- `WeaponMissSpeedPenalty` — renamed to `WeaponSwingSpeedPenaltyDuration` (penalty applies on all swings, not miss-only)

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
- *Passive:* Short speed boost on successfully dodging a tackle. Detection: when host fires a tackle attempt and victim's `IsDodging` is true â†’ tackle is nullified â†’ trigger speed boost. If `IsDodging` is false â†’ normal tackle applies.
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
- Tackles only trigger when attacker is at charge speed. `IsAtChargeSpeed` public getter on `CatchUpSpeedBoost` — implemented.
- Velocity + direction check before applying: dot product of (attacker velocity direction) Â· (direction to victim) must exceed a tunable threshold (~0.5, roughly 60Â° forward cone). Threshold is a tunable property.
- Host-authoritative. Trigger detection and ragdoll application on host only.

**Launch force (current implementation):**
- `TackleLaunchSpeed` on `PlayerTackle` is the single global tuning knob. Launch direction is flat horizontal toward victim + fixed upward arc (`Vector3.Up * 0.35f`).
- Class multipliers will be added when the class system is built (e.g. Juggernaut hits harder). Do not hardcode per-class values yet.
- Mass ratio formula is designed for when classes are active:
```
massRatio = clamp(attacker.Mass / victim.Mass, 0.5, 2.5)
finalSpeed = TackleLaunchSpeed Ã— massRatio
```
- Result: Juggernaut â†’ Speedster is strongest (â‰ˆ 2.5Ã—). Speedster â†’ Juggernaut is weakest (â‰ˆ 0.5Ã—). Sniper is middle.

**On tackle hit:**
1. Victim enters full ragdoll — gets sent flying by impulse.
2. Victim drops ball; ball also gets launched (not just dropped). `BallLaunchForceOnTackle` from `ClassData`.
3. Ball pickup is locked out for `BallPickupLockoutAfterTackle` seconds (uses existing `BlockPickupForSeconds`).
4. After `RagdollDuration` (2s default) victim stands back up.
5. Post-tackle invincibility window starts (`PostTackleInvincibilityDuration`).

**Ragdoll init delay:**
- Wait 0.05s after `ModelPhysics` is added before calling `PhysicsGroup.Velocity` — bodies need at least one physics tick to fully initialise or the velocity is ignored.

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
- Tackle system component: `PlayerTackle`
- Tackle inspector properties: `TackleDirectionThreshold`, `TackleCooldown`, `TackleLaunchSpeed`, `RagdollCameraDistance`, `RagdollCameraHeight`, `EnableTackleDebugLogs`
- Tackle synced properties: `NetIsRagdolled`, `NetRagdollPosition`, `NetStandUpPosition`, `NetIsTackleImmune`
- Tackle host-only field: `ragdollObject` (the spawned physics ragdoll GameObject)
- Tackle renderer cache: `hiddenRenderers` (list of SkinnedModelRenderers hidden during ragdoll)
- Tackle collider cache: `disabledColliders` (list of Colliders disabled during ragdoll, re-enabled on stand-up)
- Tackle public getters: `IsRagdolled`, `IsTackleImmune`
- Charge speed public getter: `IsAtChargeSpeed` on `CatchUpSpeedBoost`
- Dodge component (planned): `PlayerDodge`
- Dodge state public getter (planned): `IsDodging` on `PlayerDodge`
- Juggernaut passive ClassData fields: `TackleChargeRampRate`, `MaxTackleChargeBonus`

## Next Chat Kickoff
Paste this at the start of a new session:

`Read SESSION_NOTES.md first, continue from Current Plan, and propose any needed updates before coding.`

## End-of-Session Handoff
- What changed (06/05/26 session 2): Discovered the correct approach for multiplayer ragdoll — spawn a separate host-owned ragdoll GameObject instead of trying to ragdoll the player object. Implemented Option D: host spawns PlayerRagdoll with ModelPhysics, NetworkSpawn makes it visible everywhere, player renderers hidden during ragdoll, camera follows via NetRagdollPosition, stand-up snaps to NetStandUpPosition. Both screens now see the ragdoll. Committed and pushed to feature/class-system.
- What changed (06/05/26 session 3): Fixed three tackle issues: (1) launch direction, (2) capsule collision on ragdoll spawn, (3) attempted PhysicsGroup.Velocity fix (this later turned out to not be the real API — see session 4).
- What changed (06/05/26 session 4): Long debug session. Root cause of "ragdoll not flying" was simply TackleLaunchSpeed set too low (150 = ~35 units of travel, invisible). Confirmed working at 600+. Along the way: discovered PhysicsGroup is always null on ModelPhysics ragdolls (both via ModelPhysics and PhysicsBody); switched to PhysicsBody.Velocity per-body as the launch API; moved NetworkSpawn to AFTER velocity is set (keeps bodies in local physics world during velocity assignment); added explicit ModelPhysics.MotionEnabled=true and IgnoreRoot=false.
- What is still in progress: TackleLaunchSpeed needs in-game tuning (400–800 is the sweet spot range). Ragdoll is naked (clothing excluded). Camera snap on stand-up (lerp pending).
- Exactly what to do next: Open new chat, read SESSION_NOTES, tune TackleLaunchSpeed and upward arc (Vector3.Up * 0.35f in SpawnRagdollObject) until the fly feels right.
