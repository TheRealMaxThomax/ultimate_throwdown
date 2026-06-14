# Multiplayer netcode & feel — plan

**What this is:** Long-lived design doc for **how Ultimate Throwdown handles multiplayer feel** — host authority, client-side prediction (cosmetic), reconciliation, lag compensation, and what to do **now**, **per new feature**, **ongoing tuning**, and **late dev**.

**Read this when:** Adding or changing anything that touches **host RPCs**, **`[Sync]`**, **owner-driven movement**, **combat hits**, **ragdolls**, or **ball replication**. Agents and Max should refer here before inventing a one-off networking approach.

**Companion docs:** [`SESSION_NOTES.md`](SESSION_NOTES.md) (day-to-day checklist, MP test steps), [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) (mechanics), [`NAMING_CANON.md`](NAMING_CANON.md) (script names).

**Status:** **Active.** Authority fixes for Speed Blitz MP done (2026-06-14). **Tier 0 shipped (2026-06-14):** client-owner Speed Blitz dasher predict + attacker feel dedupe. **Next:** Tier A (tackle attacker predict, victim feel timing, shared dedupe helper).

---

## Core philosophy

1. **Host is the referee** — knockdowns, scores, ult spend, ragdoll impulses, ball ownership, match phase. Clients **request**; host **decides**.
2. **Owner moves immediately** — dash, dodge, walk, throw wind-up feel local on the acting player’s machine.
3. **Predict feel, not truth** — the acting client (and sometimes the victim client) may show **early** stop, camera punch, SFX. Gameplay outcome still comes from host.
4. **Reconcile when host disagrees** — rare corrections; prefer policies that don’t rewind the whole match (e.g. accept early stop on false-positive predict rather than full rollback).
5. **Prioritize the two players involved** — dasher + victim feel first; spectators / third-player proxies later.

This is how most action sports games ship without a dedicated netcode team: **thin prediction + host authority + soak tuning**, not full fighting-game rollback.

---

## Why host and client feel different today

| Role | What happens |
|------|----------------|
| **Host (acting player)** | Input, hit test, and result on same machine → contact feels instant. |
| **Client (acting player)** | Moves locally → RPC/report to host → host confirms → sync/RPC back → stop/feel. Gap ≈ **RTT/2+** (often 30–120ms+). |
| **Other clients** | Interpolated copies of remote players; ragdoll/ball smoothed from host snapshots. |

Common symptoms in this project (expected until predict/tuning):

- Client ult/tackle **connect feels late** on the actor.
- **Ragdoll jitter** on non-host viewers (`RagdollClientFeel` interpolation).
- **Ball jitter** on clients (`BallClientFeel`).
- Remote players ** slightly behind** where the owner sees them.

These are **not unfixable** — they need layered techniques (below), not one magic flag.

---

## Three techniques (know the difference)

### 1. Client-side prediction (feel)

**Who:** Usually the **owner** of the acting pawn (dasher, tackler).

**What:** Assume the likely host outcome and show it **immediately** (stop dash, hitstop, camera punch). Host still decides knockdown/score later.

**When to use:** Any **contact moment** where waiting one RTT feels bad.

### 2. Reconciliation (fix-up)

**Who:** Acting client after host responds.

**What:** If host **confirms** → dedupe (don’t double-play feel). If host **rejects** local predict (false positive) → policy: stay stopped vs resume (prefer **stay stopped** for dash/tackle v1).

### 3. Lag compensation (fairness — host-side)

**Who:** **Host only.**

**What:** Rewind other players to **where they were at the owner’s timestamp** when running hit tests. Improves “I saw him there on my screen”; does **not** by itself make local stop instant.

**When:** Tier C / late dev if 2-window still feels unfair at normal ping **after** predict is in.

---

## Current architecture (this repo)

### Authority model

- **s&box host** runs simulation truth for physics-heavy outcomes.
- **`[Sync( SyncFlags.FromHost )]`** — phase, charge, ragdoll flags, match HUD fields on `PlayerTeam`, etc.
- **`[Rpc.Host]`** — owner requests (tackle, dodge, ult commit, dash position reports).
- **`[Rpc.Owner]`** — host pushes owner-only feel (`TackleImpactFeel`, `NotifyOwnerDashEndedRpc` on Speed Blitz).

### Feature-specific today

| System | Owner local | Host authority | Feel RPC / sync |
|--------|-------------|----------------|-----------------|
| **Tackle** | Input, movement | `RequestTackleApplyOnHost`, `ExecuteTackle`, ragdoll spawn | `TriggerTackleImpactFeelAsAttackerRpc` / `AsVictimRpc` **after** host hit |
| **Speed Blitz** | Dash velocity, aim preview | Commit, phase, hit sweep, `ApplyKnockdownFromHost`, walk ramp | `NotifyOwnerDashEndedRpc`; impact feel via tackle path when attacker passed |
| **Dodge** | Shove on owner when `NetDodgeApplyId` bumps | `RequestDodgeOnHostRpc` | Synced apply id (dedupe pattern exists) |
| **Ball throw** | Trajectory preview, charge camera | Host ball / grab | `BallClientFeel` smooths for viewers |
| **Match flow** | HUD reads `PlayerTeam` sync | `MatchDirector` on host pushes to players | Not on local MatchDirector |
| **Traffic** | Proxy pose | Host movement + knockdown | Hazard victim feel path |

### Speed Blitz MP fixes already shipped (authority — not predict)

- Client owners **report dash position** to host each fixed tick.
- Host **caps sweep step** per tick (`DashSweepStepMultiplier`).
- Knockdown direction = **`NetCommittedDirection`** (not desynced victim−dasher vector).
- **Pre-launch pause** on blitz hits (attacker passed to `ApplyKnockdownFromHost`).
- **Zero velocity** on dasher/victim before knockdown on host.
- **`NotifyOwnerDashEndedRpc`** + `ownerDashMovementBlocked` so client owner stops without waiting only on `[Sync]`.

**Remaining gap:** acting client still waits for host to **confirm** hit before stop/feel — predict closes that.

---

## Priority roadmap

### Tier 0 — Speed Blitz owner predict + dedupe ✅ **SHIPPED (2026-06-14)**

**Speed Blitz — owner dasher predict + dedupe**

Scope (agreed):

- **Predict (owner only):** local corridor sweep during dash → on first valid enemy overlap: block dash movement, trigger **`TackleImpactFeel.TriggerAsAttacker`**, optional local SFX.
- **Do not predict:** knockdown, ragdoll, `NetPhase`, walk ramp, charge, victim state.
- **Dedupe:** when host confirms (`NotifyOwnerDashEndedRpc` / impact RPC), skip second attacker feel if already played this dash.
- **Victim:** unchanged **or** one RPC timing tweak so victim owner feel aligns with pre-launch pause (small pass).
- **Spectators:** no work in this slice.

**False-positive policy (v1):** if owner predicted hit but host says dash continues → **do not resume dash**; wait for host end. Tune geometry so false positives are rare.

**Acceptance:** 2-window — client dasher stop + punch feels same frame as visual contact; launch distance still host-authoritative; no mega-launches regression.

---

### Tier A — Soon after Tier 0

| # | Item | Effort | Notes |
|---|------|--------|-------|
| A1 | **Tackle attacker predict** | Medium (easier than blitz) | Local cone/radius when tacker owner presses; early attacker `TackleImpactFeel`; dedupe on host confirm. Reuse blitz pattern. |
| A2 | **Victim-owner feel timing** | Small | Blitz + tackle: victim `TriggerAsVictim` same frame as host commit / pre-launch pause. |
| A3 | **Shared dedupe helper** | Small–medium | e.g. host combat event id + “owner consumed predict for id”. Model after `NetDodgeApplyId`. Document names in `NAMING_CANON.md` when added. |

---

### Tier B — Ongoing (every session / feature)

| # | Item | When |
|---|------|------|
| B1 | **2-window MP test** | After any net/combat change — see [`SESSION_NOTES.md`](SESSION_NOTES.md) → Multiplayer testing. |
| B2 | **Ragdoll interpolation tuning** | `RagdollClientFeel`, `PreLaunchPauseSeconds` vs hitstop — soak when victims look jittery. |
| B3 | **Ball client smoothing** | `BallClientFeel`, held-ball sync — when ball jitter reported. |
| B4 | **New feature netcode checklist** | Before merging any combat feature — see below. |

---

### Tier C — Late dev / if still mushy at normal ping

| # | Item | Effort | Notes |
|---|------|--------|-------|
| C1 | **Lag-comp rewind (host)** | High | Position history buffer; hit tests at owner timestamp. Shared for blitz + tackle. |
| C2 | **Spectator / third-player polish** | Medium | Better proxy interpolation; optional predicted anims on non-owners. Lowest priority. |
| C3 | **Long MP soak** | Time | 15–20 min 2-window sessions; note ping-like delay patterns. |

---

### Per future feature (day one — not end of project)

When adding **Juggernaut stomp**, **Sniper zones**, **weapons**, or any **owner input → host hit → shared outcome**:

1. Implement **host path first** (MP-safe, 2-window).
2. Add **owner predict feel** if contact moment matters (same session or immediate follow-up).
3. Add **victim owner feel** via existing RPC patterns.
4. Add **dedupe** before shipping wide playtests.
5. Record any new **sync/RPC names** in `NAMING_CANON.md`.

| Future feature | Predict on | Host decides | Pattern |
|----------------|------------|--------------|---------|
| **Juggernaut stomp** | Caster AOE telegraph + local impact punch | AOE knockdown list | Blitz/tackle hybrid |
| **Sniper path zones** | Path preview (likely already) | Zone placement + ragdoll rules | Ball + zone sync |
| **Melee weapon** | Attacker hit marker + stop | Damage/KO | Tackle-like |
| **Ranged weapon** | Tracer / impact VFX on shooter | Hit scan or projectile hit | Shooter-style + host validate |

**Do not** wait until “everything is done” to add predict hooks — retrofitting 10 systems costs more than **one extra session per feature** up front.

---

## Speed Blitz predict — implementation sketch (Tier 0)

### Predict locally (owner)

- Each dash fixed tick: segment `lastLocalSample → current WorldPosition`.
- Same filters as host: `IsValidDashTarget`, `HitHalfWidth`, victim `BodyRadius`, committed direction corridor.
- First hit → set `ownerPredictedHitThisDash`, `ownerDashMovementBlocked = true`, zero velocity, `TackleImpactFeel.TriggerAsAttacker()`.

### Stay host-only

- `HostDashHitCheck`, `HostApplyDashKnockdown`, `EndBlitzOnHost`, walk ramp, charge block, victim ragdoll, comic text from host rules.

### Reconcile

| Host | Owner predicted? | Action |
|------|------------------|--------|
| Hit | Yes | Keep stop; skip duplicate attacker feel |
| Hit | No | Normal path today (RPC stop + feel) |
| Miss / timeout | Yes | Stay stopped; no resume (v1) |
| Miss / timeout | No | Normal |

### Code touchpoints (existing)

- `Code/Ultimates/SpeedsterSpeedBlitzUlt.cs` — owner fixed update, `ownerDashMovementBlocked`, `NotifyOwnerDashEndedRpc`.
- `Code/Player/TackleImpactFeel.cs` — attacker/victim feel.
- `Code/Player/PlayerTackle.cs` — `ApplyKnockdownFromHost`, impact RPCs.

**Optional extract:** shared static corridor test used by host + owner (keeps preview/hit/predict aligned).

---

## Tackle predict — sketch (Tier A1)

- **Owner:** on tackle request (or fixed tick while charge-tackling if ever continuous), local `TryFindTackleVictim` with **same** radius/threshold as host RPC.
- On local find → `TackleImpactFeel.TriggerAsAttacker()` once; set predict id flag.
- **Host:** unchanged validation; on confirm RPC victim feel as today.
- **Dedupe:** if `TriggerTackleImpactFeelAsAttackerRpc` arrives and predict already fired for this tackle attempt id, skip.

Existing gotcha (keep): **do not** add extra host-side charge gates on tackle RPC — owner/host position lag (`SESSION_NOTES` → Multiplayer gotcha tackles).

---

## New combat feature checklist (copy for PRs / sessions)

- [ ] Host authoritative outcome implemented and tested 2-window.
- [ ] Owner can act without waiting for host for **input** (not outcome).
- [ ] Acting owner: **predict feel** for contact? (if yes → dedupe on confirm)
- [ ] Victim owner: impact feel RPC on host commit?
- [ ] No gameplay state predicted (HP, score, ragdoll spawn, inventory).
- [ ] New `[Sync]` / RPC names in `NAMING_CANON.md`.
- [ ] `SESSION_NOTES.md` “Works today” or ult roadmap updated if shipped.
- [ ] Debug logs behind a bool for reject/false-positive tuning.

---

## What NOT to do (unless scope explicitly changes)

- Full world rollback / fighting-game netcode.
- Client-authoritative knockdown or damage.
- Predicting victim ragdoll physics on clients.
- Lag-comp before basic owner predict on contact features (lower ROI).
- Blocking host RPCs with strict owner-derived gates that fail under latency (tackle lesson).
- Setting `"Resources": "*"` in `ultimate_throwdown.sbproj` (breaks join — see `SESSION_NOTES`).

---

## Testing

**Minimum:** [`SESSION_NOTES.md`](SESSION_NOTES.md) → **Multiplayer testing** (2-window).

**After predict work, add:**

- Client dasher → client victim / host victim: stop on same frame as contact (dasher screen).
- Host dasher unchanged (no double feel).
- No return of super-far launches.
- Spam ults/tackles once to probe desync.

**Optional:** enable `EnableSpeedBlitzDebugLogs` / tackle debug on prefab when tuning false positives.

---

## Related known issues

See [`SESSION_NOTES.md`](SESSION_NOTES.md) → **Known issues** (ragdoll jitter, throw MP verify, long soak, client join flash, etc.). Archive detail: [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) (client free-ball jitter, ragdoll technical).

---

## Revision log

| Date | Change |
|------|--------|
| 2026-06-14 | Tier 0 shipped — client-owner Speed Blitz predict + attacker feel dedupe (`TryFindBestDashHitInSegment`, `ShouldSkipHostAttackerFeelBecauseOwnerPredicted`). |
| 2026-06-14 | Initial doc — philosophy, tiers 0–C, Speed Blitz predict scope, tackle follow-up, per-feature checklist, current architecture snapshot. |
