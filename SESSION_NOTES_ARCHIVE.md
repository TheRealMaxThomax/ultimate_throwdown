# Archive — old notes & deep fixes

**You rarely need this file.** Start with [`SESSION_NOTES.md`](SESSION_NOTES.md).

**What’s here:** Why we chose things, ragdoll debugging history, and copy-paste fixes when something breaks again.

---

## Short history (why the game works this way)

- **Ball:** `BallGrab` owns hold state; `BallThrow` is separate. Online = host approves grab/drop/throw.
- **No kicking / no body-push:** Auto-grab when you touch the ball (like Extreme Football Throwdown). Pushing the ball on all clients was too unreliable.
- **Tackle:** Don’t ragdoll the player object — spawn a **host-owned ragdoll** object. Launch with **pelvis impulse** after a short delay. Disable victim collider **immediately** on host.
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
- **2026-05-06** — Tackle direction toward victim; pelvis `ApplyImpulse`; disable capsule immediately; separate ragdoll GO; `NetworkSpawn` before impulse; ragdoll clothing via `BoneMergeTarget`; client tackle RPC.
- **2026-05-07** — Ragdoll camera + stand-up blend; ball XOR weapon (future); weapon speed penalties (future).
- **2026-05-08** — Dodge + whiff design linked; dodge shove from `EyeAngles.ToRotation().Right`; `PlayerDodge` in `CatchUpSpeedBoost.cs`.
- **2026-05-10** — `practice_npc` tag; `Network.IsOwner` for camera; momentum multiplier; forward intent for charge.
- **2026-06-06** — Stand up when grounded + settled; `RagdollMaxDuration` cap.

</details>

---

## Ragdoll (technical — if tackles look broken)

**Don’t** put `ModelPhysics` on the **player prefab** for tackles — the client owns that object and fights the physics.

**Do:** Host spawns `PlayerRagdoll` with `ModelPhysics`, `NetworkSpawn()`, hide player mesh, sync position for camera.

**Launch:** Wait `RagdollPhysicsInitDelay` (~0.05s), then `ApplyImpulse` on pelvis (`Bodies[0]`). `PhysicsGroup.Velocity` was unreliable on spawned ragdolls.

**Stand-up trace:** Exclude colliders tagged `ragdoll` so limbs don’t count as floor.

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
