# Ragdoll Launch Debug Notes

**RESOLVED — 06/05/26 session 4. See SESSION_NOTES.md for final API decisions.**

**Current MP flow (2026-05-18):** Poll for bodies → `ApplyImpulse` on host → `NetworkSpawn`. Details in [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → Ragdoll (technical). Old “spawn then fixed delay then impulse” caused client tackles to look shorter/late.

Root cause: `TackleLaunchSpeed` was set to 150 in the inspector. At that speed the ragdoll only travels ~35 units which looks like it collapses in place. At 600 it visibly flies. Physics was working the whole time.

Secondary fixes made during this session (all kept):
- `PhysicsBody.Velocity` per-body is the correct launch API (PhysicsGroup always null on ModelPhysics ragdolls)
- `NetworkSpawn()` moved to AFTER velocity is set (keeps bodies in local physics world)
- `ModelPhysics.MotionEnabled = true` and `IgnoreRoot = false` set explicitly

---

## Original Problem Statement (archived)

---

## What We Know For Certain (from debug logs)

```
Post-delay | mp=True PhysicsWereCreated=True PhysicsGroup=False Bodies=16
Velocity set on 16 bodies | speed=150
Bodies[0] velocity after set: -6.4303, 141.4326, 49.5526
```

- `mp` (ModelPhysics component re-fetched from ragdollGo) — valid ✓
- `PhysicsWereCreated` — true ✓
- `PhysicsGroup` — **always null** on network-spawned ModelPhysics ✗ (cannot use this API path)
- `Bodies` — 16 bodies available ✓
- Velocity set on all 16 bodies ✓
- `Bodies[0].Component.Velocity` reads back as the correct launch vector ✓ (not zero)
- Despite this, the ragdoll does NOT visually launch

---

## Confirmed Dead Ends (do not retry)

| Approach | Why It Failed |
|---|---|
| `Bodies[0].Component.Velocity = x` (single body) | Velocity set but joints resisted; also `IsValid()` was silently false sometimes |
| `PhysicsGroup.Velocity = x` | `PhysicsGroup` is **always null** on network-spawned `ModelPhysics` |
| `PhysicsGroup.ApplyImpulse(x, true)` | Same — `PhysicsGroup` is null |
| `body.Velocity = x` (on `ModelPhysics.Body` struct directly) | Compile error — `ModelPhysics.Body` has no `Velocity` property |
| Polling on `PhysicsGroup.BodyCount > 0` | `BodyCount` always returned 0 even when bodies existed |
| Polling on `PhysicsWereCreated` with 1s cap | `PhysicsWereCreated` is true, but exit guard also checked `PhysicsGroup != null` which was always false, so ApplyImpulse was never reached |
| Flat 0.3s delay + `PhysicsGroup.ApplyImpulse` | PhysicsGroup null — same as above. Also 0.3s is too long (ragdoll falls into floor) |

---

## Current Code Path (PlayerTackle.cs, SpawnRagdollObject)

```
1. Create ragdollGo, add SkinnedModelRenderer + ModelPhysics
2. NetworkSpawn()
3. await 0.05s
4. Re-fetch mp = ragdollGo.Components.Get<ModelPhysics>()   <-- key fix (original local ref was stale)
5. Guard: mp == null || mp.Bodies.Count == 0 → return
6. foreach body in mp.Bodies: body.Component.Velocity = launchVelocity
7. Log confirms velocity reads back correctly
```

Velocity IS set. Something overrides it or MotionEnabled is preventing it from taking effect.

---

## Most Likely Remaining Causes (investigate in this order)

### 1. `Rigidbody.MotionEnabled` is false on body components
`ModelPhysics` individual body `Rigidbody` components have a `MotionEnabled` property.
If `false` on the individual Rigidbody, physics simulation might be disabled for that body (kinematic),
meaning velocity assignments are ignored. But gravity also wouldn't work if truly kinematic...
**Test:** log `body.Component.MotionEnabled` for bodies[0] before and after setting velocity.
**Fix attempt:** `body.Component.MotionEnabled = true` before setting velocity.

### 2. Something overrides velocity the next frame
Network sync, ModelPhysics internal update, or animation system may reset velocity to 0
in the physics tick immediately following our assignment.
**Test:** log `Bodies[0].Component.Velocity` in `OnUpdate` (1 frame after setting) to see if it persists.
**Fix attempt:** Use `ApplyImpulse` on `Rigidbody` instead of `Velocity=`:
```csharp
body.Component.ApplyImpulse( launchVelocity * body.Component.Mass );
```
`ApplyImpulse` adds momentum directly into the physics solver rather than setting state.

### 3. `CopyBonesFrom` needed before physics activates
The ragdoll spawns in T-pose. ModelPhysics may not drive the renderer until `CopyBonesFrom`
has been called to initialize bone positions from the victim's SkinnedModelRenderer.
Without correct bone initialisation, joints may be in invalid states that fight any applied velocity.
**Fix attempt:** call `ragdollPhysics.CopyBonesFrom(baseVictimRenderer, true)` immediately after
adding ModelPhysics, BEFORE NetworkSpawn.

### 4. Ragdoll falls into floor during 0.05s delay
Even 0.05s may be enough for the ragdoll to fall into the floor depending on the map.
Check if spawning the ragdoll slightly elevated (e.g. `victim.WorldPosition + Vector3.Up * 20f`)
prevents it from landing before velocity fires.

---

## Key API Facts (confirmed via sbox API docs)

- `ModelPhysics.PhysicsGroup` — always null on network-spawned objects. Do not use.
- `ModelPhysics.Bodies` — List of `ModelPhysics.Body` structs. Count=16 for citizen model. Available immediately after spawn.
- `ModelPhysics.Body.Component` — typed as `Rigidbody` (confirmed from API: `public Rigidbody Component`)
- `ModelPhysics.PhysicsWereCreated` — bool, true when bodies are ready. Use as readiness guard.
- `Rigidbody.Velocity` — get/set. Reads back correctly, but may not affect simulation (see above).
- `Rigidbody.ApplyImpulse(Vector3 force)` — applies instant impulse. Has not been tried on per-body yet.
- `Rigidbody.MotionEnabled` — bool. Unknown default for ModelPhysics-managed bodies.
- `ModelPhysics.CopyBonesFrom(source, teleport)` — copies bone positions from SkinnedModelRenderer.
- Use `@sbox docs` in chat when unsure about any API.

---

## Suggested First Steps for New Chat

1. Read SESSION_NOTES.md
2. Read this file
3. Add `body.Component.MotionEnabled` to debug log, check if it's false
4. Try `body.Component.MotionEnabled = true` before setting velocity
5. If still not working, try `ApplyImpulse(launchVelocity * body.Component.Mass)` on each body
6. If still not working, try `CopyBonesFrom` before NetworkSpawn
