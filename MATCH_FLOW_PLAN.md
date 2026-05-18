# Match Flow Plan — scoring, teams, rounds

**What this is:** Design + implementation tracker for match flow. **Start here** for goals/rounds/intermission work.  
**Status:** **Slices 1–3 shipped and playtested.** Slices 4–6 not started.  
**Daily cheat sheet:** [`SESSION_NOTES.md`](SESSION_NOTES.md) · **Names:** [`NAMING_CANON.md`](NAMING_CANON.md)

---

## New chat handoff

Paste:

`Read SESSION_NOTES.md and MATCH_FLOW_PLAN.md. Continue from slice 4 (reset orchestration). Do not edit .scene files unless I ask.`

---

## Progress

| Slice | What | Status |
|------|------|--------|
| 1 | Teams + spawns | **Done** |
| 2 | `MatchDirector` (phases, timer, OT, debug goal) | **Done** |
| 3 | `GoalZone` + dwell scoring | **Done** |
| 4 | Reset orchestration + input freeze | **Not started** |
| 5 | Match HUD (score, banner, countdown) | **Not started** |
| 6 | Match over + rematch | **Not started** |

---

## TL;DR (full match when complete)

Hold ball in opponent `GoalZone` ~0.35s → goal → +1 round win → **5s celebration** (move on field) → **reset** (teleport + ball center) → **20s intermission** (freeze except camera) → `Playing`. First to **5** round wins. **10 min** timer (paused in celebration + intermission). Tie at time → OT golden goal. Match over → host **rematch same map** (map vote later).

---

# Implemented (slices 1–3)

## Slice 1 — Teams + spawns

**Code:**
- `Code/Player/PlayerTeam.cs` — synced `TeamId` (host-assigned).
- `Code/Match/MapMatchConfig.cs` — `Team0DisplayName` / `Team1DisplayName`; `FindInScene`.
- `Code/Match/MatchTeamIds.cs` — `Team0` / `Team1` constants.
- `Code/Network/GameNetworkManager.cs` — balance-on-join (smaller team, random if tied); reconnect keeps team; `Team0Spawns` / `Team1Spawns` lists (6 per team); occupied-radius picker; `WithoutScale` on spawn so scaled editor empties don’t resize players.

**Editor (`throwdown_prototype.scene`):**
- `MapMatchConfig` on scene (or wire `MatchConfig` on `GameNetworkManager`).
- Six spawn empties per team in `Team0Spawns` / `Team1Spawns`.
- `PlayerTeam` added automatically at spawn.

**Verified:** 2-window join → opposite teams, different spawn slots; solo → random team, always `Spawn_1`.

---

## Slice 2 — MatchDirector

**Code:**
- `Code/Match/MatchPhase.cs` — `Playing`, `GoalCelebration`, `Intermission`, `MatchOver`.
- `Code/Match/MatchDirector.cs` — host phase machine; synced round wins, match timer, phase timer, OT flag; `RegisterGoal()`; `IsGameplayInputAllowed` (not wired to movement/ball yet); `HostRequestRematch()` stub.

**Editor:**
- `MatchDirector` on same object as `GameNetworkManager` (e.g. Main Camera).
- `Enable Match Debug Logs` for `[Match]` console lines.

**Debug (remove before ship):**
- `Enable Debug Force Goal` + action `DebugForceGoal` in `ProjectSettings/Input.config` (bound to **`,`** — F9 is eaten by editor).
- Host only; random team per press.

**Verified:** `,` drives celebration → intermission → `Playing`; 5 round wins → `MatchOver`; timer tie → OT → next goal ends match.

**Not done yet:** Reset after celebration is a **log stub** only (`"reset (stub until slice 4)"`).

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

# Upcoming (slices 4–6)

## Slice 4 — Reset orchestration + input freeze

**Goal:** After 5s celebration, actually reset the round; freeze gameplay during 20s intermission (camera still free).

### Code tasks
- [ ] **`BallSpawn`** — `GameObject` ref on `MatchDirector` or `GameNetworkManager` (map center ball reset point).
- [ ] **`PerformRoundReset()`** — call from `MatchDirector.OnGoalCelebrationEnded()` (replace stub log).
  - [ ] Force **ragdoll stand-up** on all players (`PlayerTackle` host path — everyone standing before intermission).
  - [ ] **Release ball** from any carrier (`BallGrab.ReleaseHeldBall()`).
  - [ ] **Teleport ball** to `BallSpawn` (zero/low velocity).
  - [ ] **Teleport each player** to a free team spawn (reuse `GameNetworkManager` spawn picker / expose `GetSpawnTransformForTeam`).
  - [ ] **`BallGrab.BlockPickupForSeconds()`** after reset (reuse existing pickup block).
- [ ] **Input freeze** — consult `MatchDirector.IsGameplayInputAllowed` from:
  - [ ] `CatchUpSpeedBoost` / movement
  - [ ] `BallGrab` / `BallThrow`
  - [ ] `PlayerTackle` / `PlayerDodge`
  - Allowed: `Playing` + `GoalCelebration`. Blocked: `Intermission` + `MatchOver`. Camera always allowed.
- [ ] **Join mid-intermission** — new players spawn at team spawn, frozen until `Playing` (may need spawn hook in `GameNetworkManager` reading `MatchDirector.CurrentPhase`).

### Editor tasks (you)
- [ ] Create **`BallSpawn`** empty at center kickoff spot; wire on `MatchDirector` (or agreed component).

### Test (2 windows)
- [ ] Score a goal → celebration 5s (can still move) → everyone at team spawns, ball at center → 20s frozen (except look) → play resumes.
- [ ] No one ragdolled when intermission starts.
- [ ] Client + host see same positions after reset.

---

## Slice 5 — Match HUD

**Goal:** Visible UI for score, goal callout, intermission timer (console-only today).

### Code tasks
- [ ] **`MatchScoreHud`** (`Code/UI/`) — top bar: `Team0DisplayName` + round wins vs `Team1DisplayName` (from `MapMatchConfig` + synced `NetTeam0RoundWins` / `NetTeam1RoundWins`).
- [ ] **`GoalBannerHud`** — e.g. `"TEAM A SCORED!"` during `GoalCelebration` (use `NetLastGoalScoringTeamId` + display names).
- [ ] **`IntermissionHud`** — countdown from `NetPhaseTimeRemaining` during `Intermission` ("Resuming in 12…").
- [ ] Wire HUD components on scene UI root (you place in editor; AI does not edit `.scene` unless asked).

### Test
- [ ] Score updates on goal; banner shows correct team name; countdown ticks during intermission.

---

## Slice 6 — Match over + rematch

**Goal:** End-of-match screen; host restarts same map.

### Code tasks
- [ ] **`MatchOverHud`** — winner + final round score; host **Rematch** button → `MatchDirector.HostRequestRematch()`; clients show **"Waiting for host…"**.
- [ ] **`HostRequestRematch()`** — reset round wins, timer, phase to `Playing`, ball + player positions (reuse slice 4 reset).
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

---

# Design reference (unchanged rules)

## Match structure

| Term | Meaning |
|------|--------|
| **Round** | Valid goal → +1 round win for scoring team |
| **Match** | First to **5** round wins |
| **Match timer** | **600s**, paused in celebration + intermission |
| **OT** | Timer expires tied → next goal wins match |

## Phase timeline

```text
Playing
  → GoalCelebration (5s)   // move freely; force stand ragdolls at start
  → Reset                  // teleport + ball center  [slice 4]
  → Intermission (20s)     // freeze except camera   [slice 4]
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
| `MatchDirector` | Phase machine |
| `GoalZone` ×2 | `DefendingTeam` 0 and 1, `BoxSize` tuned |
| `BallSpawn` | Center reset (**slice 4** — wire in editor) |

## Networking

- Host: goals, phases, timer, teams, resets.
- Clients: synced state + HUD only.

## Open decisions

- Tackles/dodges during 5s celebration? (currently full movement allowed)
- Intermission: strict freeze (v1) vs walk-near-spawn (later)
- Disconnect during celebration/intermission handling
- Remove `DebugForceGoal` when?

---

## Components (built vs planned)

| Component | Status |
|-----------|--------|
| `MapMatchConfig`, `MatchTeamIds`, `PlayerTeam` | Built |
| `MatchDirector`, `MatchPhase` | Built (reset stub) |
| `GoalZone` | Built |
| `MatchScoreHud`, `GoalBannerHud`, `IntermissionHud`, `MatchOverHud` | Planned |

See [`NAMING_CANON.md`](NAMING_CANON.md) for property names.
