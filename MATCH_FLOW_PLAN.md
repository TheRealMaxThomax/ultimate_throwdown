# Match Flow Plan — scoring, teams, rounds

**What this is:** Design + implementation tracker for match flow. **Start here** for goals/rounds/intermission/match-over work.  
**Status:** **v1 match flow complete** — slices **1–6** shipped and playtested (2-window MP).  
**Daily cheat sheet:** [`SESSION_NOTES.md`](SESSION_NOTES.md) · **Names:** [`NAMING_CANON.md`](NAMING_CANON.md)

---

## New chat handoff

Paste:

`Read SESSION_NOTES.md. Match flow slices 1–6 are done (see MATCH_FLOW_PLAN.md). Do not edit .scene files unless I ask.`

---

## Progress

| Slice | What | Status |
|------|------|--------|
| 1 | Teams + spawns | **Done** |
| 2 | `MatchDirector` (phases, timer, OT, debug goal) | **Done** |
| 3 | `GoalZone` + dwell scoring | **Done** |
| 4 | Reset orchestration + input freeze | **Done** |
| 5 | Match HUD (score, clock, banner, countdown) | **Done** |
| 6 | Match over + rematch | **Done** |

---

## TL;DR (full match)

Hold ball in opponent `GoalZone` ~0.35s → goal → +1 round win → **5s celebration** (move on field) → **reset** (teleport + ball center) → **20s intermission** (freeze except camera) → `Playing`. First to **5** round wins. **10 min** timer (paused in celebration + intermission; HUD `10.00` → `9.59`…). Tie at 0:00 → **OVERTIME** (reset + 20s intermission, then golden goal). Match over → **10s winner celebration** (move freely, `TEAM WINS!` banner) → rematch panel → host presses **`1`** (`Slot1`) → same map, fresh **0–0**, timer **10:00**. **Map vote** (all players, 30s) is planned later.

---

# Implemented (slices 1–6)

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
- `Code/Match/MatchDirector.cs` — host phase machine; synced round wins, match timer, phase timer, OT flag; `RegisterGoal()`; `PushMatchHudStateToPlayers()`; `PerformRoundReset()`; `HostRequestRematch()`; `MatchOverCelebrationSeconds`.

**Editor:**
- `MatchDirector` on same object as `GameNetworkManager` (e.g. Main Camera).
- `Enable Match Debug Logs` for `[Match]` console lines.

**Debug (optional — off by default):**
- `Enable Debug Force Goal` + action `DebugForceGoal` in `ProjectSettings/Input.config` (bound to **`,`** — F9 is eaten by editor).
- Host only; random team per press. Uncheck before ship if you don’t want it in builds.

**Verified:** `,` drives celebration → intermission → `Playing`; 5 round wins → `MatchOver`.

**OVERTIME (tied at 0:00, equal round wins):** `BeginOvertimeSetup()` — full round reset + 20s intermission (no celebration) → `Playing` with `NetIsOvertime`; clock shows **OVERTIME**; next goal ends match.

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
- `MatchDirector.PerformRoundReset( pickupBlockSeconds )` — called from `OnGoalCelebrationEnded()`: `PlayerTackle.ForceStandUpFromHost()` on all players → release ball → ball to `BallSpawn` via **`GameNetworkManager.SnapBallToGround()`** → `GameNetworkManager.ApplyRoundResetToAllPlayers()` → pickup block for intermission duration.
- `MatchDirector.BallSpawn` — inspector ref to center kickoff empty.
- `MatchDirector.PushMatchHudStateToPlayers()` — copies phase + HUD fields onto every networked `PlayerTeam` (see networking note below).
- `GameNetworkManager.ApplyRoundResetToPlayer()` — host sets `PlayerTeam.NetRoundResetPosition` / `NetRoundResetRotation`, bumps `NetRoundResetSequence`; all peers apply via `PlayerTeam.ApplyRoundResetTransform()`.
- `GameNetworkManager.SnapPositionToGround()` — trace down from spawn point (players).
- `PlayerTeam` — `NetMatchPhase`, `NetRoundResetSequence`, `IsMatchGameplayInputAllowed` (Playing, goal celebration, match-over celebration).
- **Input freeze** — `CatchUpSpeedBoost`, `BallGrab`, `BallThrow`, `PlayerTackle`, `PlayerDodge` consult **`PlayerTeam.IsMatchGameplayInputAllowed`**, not `MatchDirector` on Main Camera.
- `PlayerTackle.ForceStandUpFromHost()` — host ends ragdoll before intermission; async recovery bails if already stood.
- Join mid-intermission — `SyncMatchHudStateToPlayer()` + pickup block for remaining `NetPhaseTimeRemaining`.

**Networking (important):**
- `MatchDirector` lives on **local** Main Camera per machine — its `[Sync]` fields do **not** replicate to clients by themselves.
- **Authoritative for clients:** `PlayerTeam` on each **network-spawned player** (phase, reset pose, score, timers, winner for HUD).

**Editor (you):**
- Create **`BallSpawn`** empty at center kickoff; wire on `MatchDirector` → **Ball Spawn**.

**Verified (2 windows):**
- Goal → 5s celebration (move OK) → both at team spawns on ground, ball at center (not buried) → 20s frozen except look → `Playing` resumes.
- No ragdolls left down entering intermission.
- Client + host see same reset positions and freeze.

---

## Slice 5 — Match HUD

**Goal:** On-screen score, match clock, goal banner, intermission countdown. Placeholder style (`Scene.Camera.Hud` draw calls, same as dodge/ramp HUDs).

**Code:**
- `Code/UI/MatchHudDraw.cs` — `TryGetHudState()`, `FormatMatchClock()`, banner helpers, `IsMatchOverCelebrating()`.
- `Code/UI/MatchScoreHud.cs` — top center: `Team A  2 — 1  Team B` (hidden during `MatchOver`).
- `Code/UI/MatchClockHud.cs` — match timer under score; **`OVERTIME`** when tied at 0:00; hidden during `MatchOver`.
- `Code/UI/GoalBannerHud.cs` — `TEAM A SCORED!` during `GoalCelebration`.
- `Code/UI/IntermissionHud.cs` — `Resuming in 12…` during `Intermission`.
- `PlayerTeam` — host-pushed HUD mirrors: round wins, match timer, phase timer, last goal team, OT, winner team id.
- Host calls `PushMatchHudStateToPlayers()` every frame + on phase/score changes.

**Editor (you):**
- Scene empty **`MatchHud`** — add all match HUD components (see checklist below).
- Keep **Main Camera** for `MatchDirector`, `GameNetworkManager`, `MapMatchConfig`.
- Keep **player** HUD on player prefab: `DodgeCooldownHud`, `MovementRampHud`, `ThrowChargeBar` (owner-only).

**Verified (2 windows):**
- Score + clock on host and client; clock ticks `10.00` → `9.59`… while `Playing`; pauses in celebration/intermission.
- Goal banner correct team name on both screens.
- Intermission countdown on both screens.
- Tied timer expiry → clock shows **OVERTIME**.

---

## Slice 6 — Match over + rematch

**Goal:** End-of-match flow; host restarts same map. Numbered vote keys (`Slot1`–`Slot9`) ready for future map vote.

**Code:**
- `Code/UI/MatchOverHud.cs` — during `MatchOver` with `NetPhaseTimeRemaining > 0`: **`TEAM WINS!`** banner only; movement allowed (`MatchOverCelebrationSeconds`, default **10s**). After timer: panel with final score; host **`1`** (`RematchVoteSlot` → `Slot1`) → `HostRequestRematch()`; clients **"Waiting for host…"**.
- `MatchDirector.HostRequestRematch()` — `PerformRoundReset(0)` + `ResetMatchState()` (0–0, timer 10:00, `Playing`).
- `PlayerTeam.NetMatchWinnerTeamId` — synced for client HUD.
- `EnableDebugForceGoal` — **default off** in code; enable in inspector only for testing.

**Editor (you):**
- Add **`MatchOverHud`** on **`MatchHud`** root (with the other four HUD components).

**Verified (2 windows):**
- First to 5 (or OT golden goal / timer lead) → winner banner → ~10s free movement → rematch panel → host **`1`** → fresh match on same map.

---

# Later (post v1 match flow)

| Feature | Notes |
|---------|--------|
| **Map vote** | After match-over celebration: **30s** timer; **every player** votes with **`1`–`N`** (`Slot1`…); numbered map previews; **most votes wins**; tie for first place → **random** among tied maps. Slot **1** = rematch same map today. Replaces host-only rematch. |
| **Scoreboard** | Per-team columns: player names, goals, tackles, assists |
| **Match MVP popup** | Winner cosmetics + name |
| **Clutch slow-mo + vignette** | When carrier nearing goal / dwell progressing |
| **Spawn cube containment** | Walk in spawn area during intermission, can’t leave |
| **Party system** | Same team on new match start; balance-only mid-match join |
| **Per-map timer override** | Optional on `MapMatchConfig` |
| **Razor / styled UI** | Replace placeholder HUD draw with proper UI panels |

---

# Design reference

## Match structure

| Term | Meaning |
|------|--------|
| **Round** | Valid goal → +1 round win for scoring team |
| **Match** | First to **5** round wins |
| **Match timer** | **600s**, paused in celebration + intermission; HUD shows `M.SS` |
| **OVERTIME** | Timer expires tied (equal round wins) → reset + 20s intermission → clock shows `OVERTIME`; next goal wins match |

## Phase timeline

```text
Playing
  → GoalCelebration (5s)   // move freely; goal banner
  → Reset                  // teleport + ball center
  → Intermission (20s)     // freeze except camera; countdown HUD
  → Playing

Tied at match timer 0:00 (equal round wins)
  → Reset + Intermission (20s)   // BeginOvertimeSetup — no celebration
  → Playing (OVERTIME)           // golden goal

Match ends (5 round wins / OT goal / timer lead)
  → MatchOver celebration (10s)  // TEAM WINS! banner; move freely
  → MatchOver rematch panel      // frozen; host presses 1 to rematch same map
  → Playing (after rematch)
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
| **`MatchHud`** empty | `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud`, **`MatchOverHud`** |

## Networking

- Host: goals, phases, timer, teams, resets, HUD state push, rematch.
- Clients: gameplay + HUD read **`PlayerTeam`** synced fields (not local `MatchDirector`).

## Open decisions

- Tackles/dodges during 5s goal celebration? (currently full movement allowed)
- Tackles/dodges during 10s match-over celebration? (currently full movement allowed)
- Intermission: strict freeze (v1) vs walk-near-spawn (later)
- Disconnect during celebration/intermission/match-over handling
- Map vote: change vote after casting, or lock on first press?

---

## Components (built vs planned)

| Component | Status |
|-----------|--------|
| `MapMatchConfig`, `MatchTeamIds`, `PlayerTeam` | Built (phase, reset, HUD sync, winner) |
| `MatchDirector`, `MatchPhase` | Built |
| `GoalZone` | Built |
| `MatchHudDraw`, `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud`, `MatchOverHud` | Built |
| Map vote UI / tally | Planned |

See [`NAMING_CANON.md`](NAMING_CANON.md) for property names.
