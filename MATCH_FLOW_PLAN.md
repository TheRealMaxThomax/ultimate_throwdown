# Match Flow Plan — scoring, teams, rounds

**What this is:** Design + implementation tracker for match flow. **Start here** for goals/rounds/intermission work.  
**Status:** **Slices 1–5 shipped and playtested (2-window MP).** Slice 6 not started.  
**Daily cheat sheet:** [`SESSION_NOTES.md`](SESSION_NOTES.md) · **Names:** [`NAMING_CANON.md`](NAMING_CANON.md)

---

## New chat handoff

Paste:

`Read SESSION_NOTES.md and MATCH_FLOW_PLAN.md. Continue from slice 6 (match over + rematch). Do not edit .scene files unless I ask.`

---

## Progress

| Slice | What | Status |
|------|------|--------|
| 1 | Teams + spawns | **Done** |
| 2 | `MatchDirector` (phases, timer, OT, debug goal) | **Done** |
| 3 | `GoalZone` + dwell scoring | **Done** |
| 4 | Reset orchestration + input freeze | **Done** |
| 5 | Match HUD (score, clock, banner, countdown) | **Done** |
| 6 | Match over + rematch | **Not started** |

---

## TL;DR (full match when complete)

Hold ball in opponent `GoalZone` ~0.35s → goal → +1 round win → **5s celebration** (move on field) → **reset** (teleport + ball center) → **20s intermission** (freeze except camera) → `Playing`. First to **5** round wins. **10 min** timer (paused in celebration + intermission). Tie at time → **OVERTIME** golden goal. Match over → host **rematch same map** (map vote later).

---

# Implemented (slices 1–5)

## Slice 1 — Teams + spawns

**Code:**
- `Code/Player/PlayerTeam.cs` — synced `TeamId` (host-assigned).
- `Code/Match/MapMatchConfig.cs` — `Team0DisplayName` / `Team1DisplayName`; `FindInScene`.
- `Code/Match/MatchTeamIds.cs` — `Team0` / `Team1` constants.
- `Code/Network/GameNetworkManager.cs` — balance-on-join (smaller team, random if tied); reconnect keeps team; `Team0Spawns` / `Team1Spawns` lists (6 per team); occupied-radius picker; `WithoutScale` on spawn so scaled editor empties don’t resize players; **`SnapPositionToGround()`** on all team spawns (join + reset).

**Editor (`throwdown_prototype.scene`):**
- `MapMatchConfig` on scene (or wire `MatchConfig` on `GameNetworkManager`).
- Six spawn empties per team in `Team0Spawns` / `Team1Spawns`.
- `PlayerTeam` added automatically at spawn.

**Verified:** 2-window join → opposite teams, different spawn slots; solo → random team, always `Spawn_1`.

---

## Slice 2 — MatchDirector

**Code:**
- `Code/Match/MatchPhase.cs` — `Playing`, `GoalCelebration`, `Intermission`, `MatchOver`.
- `Code/Match/MatchDirector.cs` — host phase machine; synced round wins, match timer, phase timer, OT flag; `RegisterGoal()`; `IsGameplayInputAllowed`; `PushMatchHudStateToPlayers()`; `PerformRoundReset()`; `HostRequestRematch()` stub.

**Editor:**
- `MatchDirector` on same object as `GameNetworkManager` (e.g. Main Camera).
- `Enable Match Debug Logs` for `[Match]` console lines.

**Debug (remove before ship):**
- `Enable Debug Force Goal` + action `DebugForceGoal` in `ProjectSettings/Input.config` (bound to **`,`** — F9 is eaten by editor).
- Host only; random team per press.

**Verified:** `,` drives celebration → intermission → `Playing`; 5 round wins → `MatchOver`; timer tie → OVERTIME → next goal ends match.

---

## Slice 3 — GoalZone + scoring

**Code:**
- `Code/Match/GoalZone.cs` — host polls oriented box (`BoxSize` on object center/rotation); ball position when held; dwell → `MatchDirector.RegisterGoal()`; ignores `practice_npc`.

**Important fixes during bring-up:**
- Do **not** gate gameplay with `Scene.IsEditor` — it stays true during editor Play and blocked all scoring.
- Use `Scene.GetAllComponents<BallGrab>()` and **held ball world position**, not player root only.

**Editor (per map, two goals):**
- Empty objects with `GoalZone` (e.g. `GoalZoneA`, `GoalZoneB`).
- **`Defending Team`** — team that defends this end (0 or 1). Opposite team scores here.
- **`Box Size`** — full size of scoring volume; tune until editor gizmo wraps the goal mouth.
- **`Enable Goal Zone Debug Logs`** — `diag:` lines every second while holding ball.

**`testing_map` layout (current scene):**
| Object | ~Position | Defending team | Who scores here |
|--------|-----------|----------------|-----------------|
| GoalZoneA (west) | -1119, 40, 351 | 1 | Team **0** |
| GoalZoneB (east) | 1697, 40, 351 | 0 | Team **1** |
| TeamSpawnsA | 1695, 40, 351 | — | Team **0** spawns (own goal = Zone B) |
| TeamSpawnsB | -1117, 40, 351 | — | Team **1** spawns (own goal = Zone A) |

**Verified:** Real goals fire `[Match] GOAL` + phase flow; own defended end does nothing; drop ball resets dwell.

---

## Slice 4 — Reset orchestration + input freeze

**Goal:** After 5s celebration, reset the round; freeze gameplay during 20s intermission (camera still free). Works on **host and clients** in 2-window MP.

**Code:**
- `MatchDirector.PerformRoundReset()` — called from `OnGoalCelebrationEnded()`: `PlayerTackle.ForceStandUpFromHost()` on all players → release ball → ball to `BallSpawn` (ground-snapped) → `GameNetworkManager.ApplyRoundResetToAllPlayers()` → pickup block for intermission duration.
- `MatchDirector.BallSpawn` — inspector ref to center kickoff empty.
- `MatchDirector.PushMatchHudStateToPlayers()` — copies phase + HUD fields onto every networked `PlayerTeam` (see networking note below).
- `GameNetworkManager.ApplyRoundResetToPlayer()` — host sets `PlayerTeam.NetRoundResetPosition` / `NetRoundResetRotation`, bumps `NetRoundResetSequence`; all peers apply via `PlayerTeam.ApplyRoundResetTransform()`.
- `GameNetworkManager.SnapPositionToGround()` — trace down from spawn / ball reset point (fixes floating spawn empties).
- `PlayerTeam` — `NetMatchPhase`, `NetRoundResetSequence`, `IsMatchGameplayInputAllowed` (Playing + GoalCelebration only).
- **Input freeze** — `CatchUpSpeedBoost`, `BallGrab`, `BallThrow`, `PlayerTackle`, `PlayerDodge` consult **`PlayerTeam.IsMatchGameplayInputAllowed`**, not `MatchDirector` on Main Camera.
- `PlayerTackle.ForceStandUpFromHost()` — host ends ragdoll before intermission; async recovery bails if already stood.
- Join mid-intermission — `SyncMatchHudStateToPlayer()` + pickup block for remaining `NetPhaseTimeRemaining`.

**Networking (important):**
- `MatchDirector` lives on **local** Main Camera per machine — its `[Sync]` fields do **not** replicate to clients by themselves.
- **Authoritative for clients:** `PlayerTeam` on each **network-spawned player** (phase, reset pose, score, timers for HUD).

**Editor (you):**
- Create **`BallSpawn`** empty at center kickoff; wire on `MatchDirector` → **Ball Spawn**.

**Verified (2 windows):**
- Goal → 5s celebration (move OK) → both at team spawns on ground, ball at center → 20s frozen except look → `Playing` resumes.
- No ragdolls left down entering intermission.
- Client + host see same reset positions and freeze.

---

## Slice 5 — Match HUD

**Goal:** On-screen score, match clock, goal banner, intermission countdown. Placeholder style (`Scene.Camera.Hud` draw calls, same as dodge/ramp HUDs).

**Code:**
- `Code/UI/MatchHudDraw.cs` — `TryGetHudState()`, `FormatMatchClock()` (`10.00`, `9.59`, `9.58`…), banner text helper.
- `Code/UI/MatchScoreHud.cs` — top center: `Team A  2 — 1  Team B`.
- `Code/UI/MatchClockHud.cs` — match timer under score; **`OVERTIME`** when tied at 0:00; hidden during `MatchOver`.
- `Code/UI/GoalBannerHud.cs` — `TEAM A SCORED!` during `GoalCelebration`.
- `Code/UI/IntermissionHud.cs` — `Resuming in 12…` during `Intermission`.
- `PlayerTeam` — host-pushed HUD mirrors: `NetTeam0RoundWins`, `NetTeam1RoundWins`, `NetMatchTimeRemaining`, `NetPhaseTimeRemaining`, `NetLastGoalScoringTeamId`, `NetIsOvertime` (plus existing `NetMatchPhase`).
- Host calls `PushMatchHudStateToPlayers()` every frame + on phase/score changes.

**Editor (you):**
- Scene empty **`MatchHud`** (or `UI/MatchHud`) — add all four match HUD components there (not on Main Camera).
- Keep **Main Camera** for `MatchDirector`, `GameNetworkManager`, `MapMatchConfig`.
- Keep **player** HUD on player prefab: `DodgeCooldownHud`, `MovementRampHud`, `ThrowChargeBar` (owner-only).

**Verified (2 windows):**
- Score + clock on host and client; clock ticks `10.00` → `9.59`… while `Playing`; pauses in celebration/intermission.
- Goal banner correct team name on both screens.
- Intermission countdown on both screens.
- Tied timer expiry → clock shows **OVERTIME**.

---

# Upcoming (slice 6)

## Slice 6 — Match over + rematch

**Goal:** End-of-match screen; host restarts same map.

### Code tasks
- [ ] **`MatchOverHud`** — winner + final round score; host **Rematch** button → `MatchDirector.HostRequestRematch()`; clients show **"Waiting for host…"**.
- [ ] **`HostRequestRematch()`** — reset round wins, timer, phase to `Playing`, ball + player positions (reuse slice 4 `PerformRoundReset` / `ApplyRoundResetToAllPlayers`).
- [ ] Disable or remove **`EnableDebugForceGoal`** before considering match flow "shipped".

### Test
- [ ] Play to 5 round wins → match over UI → host rematch → fresh 0–0, timer 10:00.

---

# Later (post v1 match flow)

| Feature | Notes |
|---------|--------|
| **Map vote** | After match over, pick next map (replaces rematch-only) |
| **Scoreboard** | Per-team columns: player names, goals, tackles, assists |
| **Match MVP popup** | Winner cosmetics + name |
| **Clutch slow-mo + vignette** | When carrier nearing goal / dwell progressing |
| **Spawn cube containment** | Walk in spawn area during intermission, can’t leave |
| **Party system** | Same team on new match start; balance-only mid-match join |
| **Per-map timer override** | Optional on `MapMatchConfig` |
| **Razor / styled UI** | Replace placeholder HUD draw with proper UI panels |

---

# Design reference (unchanged rules)

## Match structure

| Term | Meaning |
|------|--------|
| **Round** | Valid goal → +1 round win for scoring team |
| **Match** | First to **5** round wins |
| **Match timer** | **600s**, paused in celebration + intermission; HUD shows `M.SS` |
| **OVERTIME** | Timer expires tied → clock shows `OVERTIME`; next goal wins match |

## Phase timeline

```text
Playing
  → GoalCelebration (5s)   // move freely; goal banner
  → Reset                  // teleport + ball center
  → Intermission (20s)     // freeze except camera; countdown HUD
  → Playing
```

## Scoring rules

- Carrier **holding** ball entire dwell (~**0.35s**, tune 0.25–0.5).
- In `GoalZone` where `DefendingTeam ≠ carrier.TeamId`.
- Drop or leave zone → dwell resets.
- Host authority only; `MatchDirector.IsScoringAllowed` only in `Playing`.

## Teams

- Logic: `TeamId` **0** / **1**. Display names from `MapMatchConfig`.
- Join: smaller team, random if tied; reconnect → same team.
- 2v2+: same rules; six spawns per team.

## Map setup checklist

| Object | Purpose |
|--------|---------|
| `MapMatchConfig` | Team display names |
| `GameNetworkManager` | `Team0Spawns` ×6, `Team1Spawns` ×6 |
| `MatchDirector` | Phase machine + `BallSpawn` ref (Main Camera) |
| `GoalZone` ×2 | `DefendingTeam` 0 and 1, `BoxSize` tuned |
| `BallSpawn` | Center reset — wire on `MatchDirector` |
| **`MatchHud`** empty | `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud` |

## Networking

- Host: goals, phases, timer, teams, resets, HUD state push.
- Clients: gameplay + HUD read **`PlayerTeam`** synced fields (not local `MatchDirector`).

## Open decisions

- Tackles/dodges during 5s celebration? (currently full movement allowed)
- Intermission: strict freeze (v1) vs walk-near-spawn (later)
- Disconnect during celebration/intermission handling
- Remove `DebugForceGoal` when?

---

## Components (built vs planned)

| Component | Status |
|-----------|--------|
| `MapMatchConfig`, `MatchTeamIds`, `PlayerTeam` | Built (phase, reset, HUD sync) |
| `MatchDirector`, `MatchPhase` | Built |
| `GoalZone` | Built |
| `MatchHudDraw`, `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud` | Built |
| `MatchOverHud` | Planned (slice 6) |

See [`NAMING_CANON.md`](NAMING_CANON.md) for property names.
