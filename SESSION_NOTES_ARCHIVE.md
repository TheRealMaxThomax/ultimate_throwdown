# Archive — old notes & deep fixes

**You rarely need this file.** Start with [`SESSION_NOTES.md`](SESSION_NOTES.md).

**What’s here:** Why we chose things, ragdoll debugging history, and copy-paste fixes when something breaks again.

---

## Short history (why the game works this way)

- **Ball:** `BallGrab` owns hold state; `BallThrow` is separate. Online = host approves grab/drop/throw.
- **No kicking / no body-push:** Auto-grab when you touch the ball (like Extreme Football Throwdown). Pushing the ball on all clients was too unreliable.
- **Tackle:** Don’t ragdoll the player object — spawn a **host-owned ragdoll** object. Host applies **pelvis `ApplyImpulse`**, then **`NetworkSpawn`** (poll until bodies exist). Disable victim collider **immediately** on host. Client tackles send owner charge bonus in RPC. **`PreLaunchPauseSeconds` > 0:** **`NetAwaitingRagdollLaunch`** — victim body **visible + frozen**, hidden host ragdoll, then impulse + spawn + **`NetIsRagdolled`**; **`0`** = legacy impulse-then-spawn. **`TackleImpactFeel`** = owner-only camera juice.
- **Dodge:** Double-tap strafe; shove uses look direction (`EyeAngles`), not body rotation.
- **Cosmetics:** Own component; don’t mix with spawn logic. Use `CreateFromConnection(..., false)` for other players’ clothes.
- **Classes:** Stats in `.cdata` / `ClassData` in `PlayerClass.cs` — don’t split into extra files (caused compile errors before).

---

## Full decision log (with dates)

<details>
<summary>Expand — every dated decision (for AI / archaeology)</summary>

- **2026-04-28** — `BallGrab` = source of truth for hold; `BallThrow` separate; `MainBall` + name fallback.
- **2026-04-29** — Host RPC for ball actions; scene `throwdown_prototype` + `GameNetworkManager`.
- **2026-04-30** — `PlayerCosmeticsSync` separate; clothing API `removeUnowned=false`; LOD lock after apply.
- **2026-05-04** — Auto-grab; no kick; composition not “god object”; drop uses player facing.
- **2026-05-06** — Tackle direction toward victim; pelvis `ApplyImpulse`; disable capsule immediately; separate ragdoll GO; ragdoll clothing via `BoneMergeTarget`; client tackle RPC.
- **2026-05-18** — MP launch parity: poll bodies → impulse → `NetworkSpawn` (not spawn-then-fixed-delay). Owner `ownerTackleChargeBonus` in `RequestTackleApplyOnHost`. **Don’t** use `StartAsleep` or collision-sound mute without explicit wake — zero launch.
- **2026-05-07** — Ragdoll camera + stand-up blend; ball XOR weapon (future); weapon speed penalties (future).
- **2026-05-08** — Dodge + whiff design linked; dodge shove from `EyeAngles.ToRotation().Right`; `PlayerDodge` in `CatchUpSpeedBoost.cs`.
- **2026-05-10** — `practice_npc` tag; `Network.IsOwner` for camera; momentum multiplier; forward intent for charge.
- **2026-06-06** — Stand up when grounded + settled; `RagdollMaxDuration` cap.

</details>

---

## Ragdoll (technical — if tackles look broken)

**Don’t** put `ModelPhysics` on the **player prefab** for tackles — the client owns that object and fights the physics.

**Do:** Host spawns local `PlayerRagdoll` with `ModelPhysics`, hide player mesh, sync pelvis for camera (`NetRagdollPosition` / `RagdollClientFeel` on victim owner).

**Launch (current):**
1. `CopyBonesFrom` victim renderer.
2. Poll up to `RagdollPhysicsInitDelay` (default **0.08s**) until `Bodies.Count > 0`.
3. `ApplyImpulse` on pelvis (`Bodies[0]`) — `launchVelocity * mp.Mass` (not `PhysicsGroup.Velocity`).
4. **`NetworkSpawn()` after impulse** so clients don’t see a stationary ragdoll then a late launch.

**Client attacker:** `RequestTackleApplyOnHost` sends owner positions + `ownerTackleChargeBonus` (Juggernaut ramp mirror). Host uses `max(hostBonus, ownerBonus)`. Validate hit on **owner** snapshot only — extra host charge/distance gates feel laggy.

**Dead ends (2026-05-18):** `StartAsleep = true` before impulse → no launch. Muting `EnableCollisionSounds` alone was fine in theory but shipped with sleep; reverted both.

**Stand-up trace:** Exclude colliders tagged `ragdoll` so limbs don’t count as floor.

**Symptom: client tackle much shorter than host** — usually was spawn-then-delay-then-impulse + weak host-side Juggernaut bonus, not `TackleLaunchSpeed` alone.

---

## Fix: client free-ball jitter

**Symptom:** Host ball looks fine; client ball bounces or jitters.

**Fix (already in code — don’t undo without reason):**
1. Host still owns real ball physics.
2. On client, `BallClientFeel` doesn’t run local rigidbody sim on free ball (colliders stay on).
3. Client follows **delayed snapshots** (`InterpolationDelay`), not every frame’s latest position.

**Test:** Two windows, same jump-drop test from same spot.

---

## Fix: component missing in editor

1. Save scripts, wait for compile.
2. Search Add Component again.
3. Recompile or restart s&box if still missing.
4. Fix any compile errors first — errors hide components.

---

## Older handoff log

<details>
<summary>Expand — 2025 ragdoll debug sessions</summary>

- **06/05** — Switched to separate ragdoll object; both screens see ragdoll.
- **06/05** — Launch direction, capsule disable, `TackleLaunchSpeed` too low (150) vs working (~600+).
- **06/05** — Stand-up hover fixed with `ragdoll` tag on limb colliders.

</details>

---

## 2026-06 session chronicle (moved from SESSION_NOTES)

<details>
<summary>Expand — throw anim, ball carrier UX, traffic, charge_run dev log</summary>

- **2026-06-11** — `utd_citizen_human_throw.vmdl` trimmed to `throw_windup` + `charge_run`; graph `1D Blendspace B` wiring; `PlayerBallHoldAnim` animgraph-only charge; `EnsureCustomAnimGraph()` force re-assign after cosmetics; ModelDoc stale cache → editor reboot (ragdoll flop missing).
- **2026-06-10** — `PlayerBallHoldAnim` hold/throw + `ThrowReleaseDelaySeconds`; FBX pipeline in `CITIZEN_ANIMATION_WORKFLOW.md` (ScaleAndMirror 0.3937).
- **2026-06-09** — Ball carrier UX: `hold_R`, `BallCompassHud`, `BallCarrierOutline`, `ball_v2.vmat`; throw polish 2-window MP OK.
- **2026-06-08** — `ThrowTrajectoryPreview`, `ThrowChargeCamera`; ragdoll cam → `PlayerTackle.OnPreRender`.
- **2026-06-05–07** — Road0/Road1 traffic MP; sign flicker removed.
- **2026-06-02** — `feature/human-avatar` branch; citizen_human body.

</details>
