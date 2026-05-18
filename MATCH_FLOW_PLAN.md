# Match Flow Plan — scoring, teams, rounds (planning doc)

**What this is:** Full plan for goals, teams, rounds, intermission, and match end. Decided in chat, mid-implementation.
**When to read:** Before implementing any of: `GoalZone`, team assignment, round/match flow, score HUD. Once shipped, fold the short version into [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) / [`SESSION_NOTES.md`](SESSION_NOTES.md) / [`NAMING_CANON.md`](NAMING_CANON.md) and trim this file.
**Status:** Slices 1–2 shipped. Slices 3–6 pending.

---

## Progress

| Slice | What | Status |
|------|------|--------|
| 1 | Teams + spawns (`PlayerTeam`, balance-on-join, team spawn points, `MapMatchConfig`) | **Done** |
| 2 | `MatchDirector` skeleton (phases, paused match timer, OT, debug F9 force goal) | **Done** |
| 3 | `GoalZone` + dwell scoring (host-side) | Pending |
| 4 | Reset orchestration (teleport to team spawn, ball to `BallSpawn`, force stand-up, release carrier) | Pending |
| 5 | HUD (round score top bar, goal banner, intermission countdown) | Pending |
| 6 | Match over + rematch (host action, client waiting state) | Pending |

---

## TL;DR

Hold the ball in the opponent's `GoalZone` for ~0.35s → goal → +1 round win → 5s celebration → reset → 20s intermission → next round. First team to 5 round wins = match winner. 10 min match timer (paused in celebration + intermission); tie at time → overtime golden goal.

---

## Match structure

| Term | Meaning |
|------|--------|
| **Round** | One possession cycle ending in a valid goal — scoring team gets **1 round win** |
| **Match** | First team to **5 round wins** wins the match |
| **Match timer** | **10 minutes**, **paused** during celebration + intermission |
| **Tie at time** | **Overtime** — next valid goal wins the match (golden goal) |
| **Match over** | For now: **rematch same map** (host restarts). Map vote later. |

---

## Phase timeline (after a valid goal)

```text
Playing
  → GoalCelebration (~5s)   // full movement, on field, force stand ragdolls, banner UI, clock paused
  → Reset                   // teleport players → team spawns, ball → BallSpawn, release carrier
  → Intermission (~20s)     // freeze gameplay except camera, countdown UI, clock paused
  → Playing                 // clock runs again
```

**Goal cooldown:** Scoring is locked from goal confirm until next `Playing` (cooldown covers the whole block, ~25s+ before live play resumes).

**Per-step detail:**
1. **Goal confirmed (host)** — +1 round win; if either team has 5 → `MatchOver`. Show banner ("TEAM A SCORED!"). Phase → `GoalCelebration`. Do **not** reset positions yet.
2. **GoalCelebration (~5s)** — Players keep full movement. Force stand-up on any ragdolls **at the start** so the 5s isn't spent on the ground. No new goals can score.
3. **Reset** — Teleport all players to their team spawn; ball to `BallSpawn`; release from any carrier; apply existing pickup block if needed.
4. **Intermission (~20s)** — Freeze input except camera. Countdown UI ("Resuming in 12…"). Everyone standing. Joiners spawn at their team spawn, frozen until `Playing`.
5. **RoundStart → Playing** — Unfreeze input; unpause match clock.

---

## Scoring rules

- Carrier must **be holding** the ball the entire dwell.
- Carrier must be inside a `GoalZone` whose **`DefendingTeam` ≠ carrier's team**.
- **Dwell:** start at **0.35s**, tune within **0.25–0.5s**.
- Drop ball or leave zone → dwell timer resets.
- **Free ball** in the zone does **not** score.
- **Own goals impossible by construction** — defender zone never awards to defending team.
- Practice NPCs: ignored for scoring on v1 (`testing_map` only).

---

## Teams

### Ids vs display names
- **Logic uses team id** (`0` / `1` — enum or int). Goals, spawns, sync, HUD score numbers.
- **Display name comes from map config** ("Team A" / "Team B" on `testing_map`; custom on `turf_wars`).
- Code never compares team **strings** — only ids.

### Auto-assign on join (host)
- Count active players per team id.
- Put new player on the **smaller** team.
- **Random** if tied (coin flip).
- **Reconnect (same Steam id):** prefer **same team** if still present.
- **Disconnect:** do not reshuffle mid-round; counts update for next join only.

### Join mid-match
- Spawn at **their team's spawn point**.
- If phase is `Playing` → play immediately.
- If celebration / intermission / match over → stand at spawn, frozen until next `Playing`.

### Parties (later, not v1)
- Optional `PartyId` per connection.
- **New match start:** try to keep party on the same team id while still respecting balance (greedy: place party on team that needs players most).
- **Mid-match join:** balance-only — ignore party until you explicitly opt in.

### 2v2 / 3v3+ scaling
- Same `TeamId` values; balance rule scales naturally.
- Goals unchanged — any carrier on team 0 can score in team 1's zone.
- Display names still from map config; HUD reads ids, never branches on the string.

---

## Map setup (per map, in editor)

Each map needs:

| Object | Purpose |
|--------|---------|
| `MapMatchConfig` (one) | `Team0DisplayName`, `Team1DisplayName`, optional match timer length override |
| `Team0Spawns` ×6 | Team 0 spawn empties (6 max for 6v6); wired into `GameNetworkManager.Team0Spawns` |
| `Team1Spawns` ×6 | Team 1 spawn empties; wired into `GameNetworkManager.Team1Spawns` |
| `BallSpawn` | Ball reset position (center) |
| `GoalZone` ×2 | Each with `DefendingTeam = 0` or `1` and a trigger collider |

`GameNetworkManager`'s legacy single `SpawnPoint` + `JoinSpawnSpacing` is only used if the spawn lists are empty.

---

## Components (status per [`NAMING_CANON.md`](NAMING_CANON.md))

| Component | Folder | Job | Status |
|-----------|--------|-----|--------|
| `MapMatchConfig` | `Code/Match/` | Per-map team display names | **Built** |
| `MatchTeamIds` | `Code/Match/` | Team id constants (`Team0`, `Team1`) | **Built** |
| `PlayerTeam` | `Code/Player/` | Synced `TeamId`; host-assigned at spawn | **Built** |
| `GoalZone` | `Code/Match/` | Defended end + trigger volume; reports valid carrier-in-zone to host | Planned |
| `MatchDirector` | `Code/Match/` | Phase state machine, round wins, match timer, OT, reset orchestration | **Built** (reset still stub) |
| `MatchPhase` | `Code/Match/` | `Playing`, `GoalCelebration`, `Intermission`, `MatchOver` | **Built** |
| `MatchScoreHud` | `Code/UI/` | Round wins top bar | Planned |
| `GoalBannerHud` | `Code/UI/` | "TEAM A SCORED!" banner | Planned |
| `IntermissionHud` | `Code/UI/` | Countdown during intermission | Planned |

**Folder decision:** `Code/Match/` exists and is used for match-flow components.

---

## Networking / authority

- **Host only:** Evaluates `GoalZone` dwell, decides goals, assigns teams, runs `MatchDirector` phases, teleports for reset, runs match timer.
- **Clients:** Receive synced `TeamId`, round wins, phase, banner / countdown events. Render HUD only — no client-side score state.
- Same pattern as ball / tackle authority — clients can request actions (movement, throw, tackle) but never report goals.

**Trigger API:** Use `@sbox docs` when implementing `GoalZone` — don't assume Unity-style `OnTriggerEnter`. Polling carrier position inside bounds on host is a valid fallback if triggers are awkward (similar to `PlayerTackle`'s range check).

---

## UI (v1 vs later)

### v1 (ship with scoring)
- **Top bar:** round wins per team (e.g. "Team A 2 — 1 Team B"), team display names from `MapMatchConfig`.
- **Goal banner:** "TEAM A SCORED!" during celebration.
- **Intermission countdown:** "Resuming in 12…".
- **Match over screen:** final score + winner; host has rematch action, others see "Waiting for host…".

### Later
- **Scoreboard** — two columns by team id, Steam/display names, per-player stats (goals, tackles, assists, …). Host tracks stats.
- **Map vote** at match end.
- **Match MVP popup** with winner's citizen / cosmetics + name.
- **Clutch feel** — global slow-mo + dark vignette ring when carrier in opponent zone with dwell progressing (or close to goal line).

---

## Implementation slices (suggested order)

Build in this order so each slice is testable before the next:

1. **Teams + spawns** — `PlayerTeam`, host balance-on-join, team spawn points, `MapMatchConfig` skeleton. **Done.**
2. **`MatchDirector` skeleton** — phases (`Playing` / `GoalCelebration` / `Intermission` / `MatchOver`), input freeze in intermission, match timer with pause, manual "force goal" debug input to drive flow without zones yet. **Done.**
3. **`GoalZone` + dwell** — host-side trigger / poll, valid carrier check, dwell timer, call `MatchDirector.RegisterGoal`.
4. **Reset orchestration** — teleport players to team spawn, ball to `BallSpawn`, force ragdoll stand-up, release carrier.
5. **HUD** — round score bar, goal banner, intermission countdown.
6. **Match over + rematch** — host action, client waiting state.

Each slice ends with a 2-window MP playtest (per [`PLAYTEST_CHECKLIST.md`](PLAYTEST_CHECKLIST.md)) before moving on.

### Slice 1 — shipped notes

**Files added/changed:**
- `Code/Player/PlayerTeam.cs` — `[Sync( SyncFlags.FromHost )] TeamId`.
- `Code/Match/MapMatchConfig.cs` — `Team0DisplayName` / `Team1DisplayName`; `FindInScene` auto-find.
- `Code/Match/MatchTeamIds.cs` — `Team0` / `Team1` / `TeamCount` constants + `IsValid`.
- `Code/Network/GameNetworkManager.cs` — balance-on-join (smaller team, `Game.Random.Int` if tied); reconnect reuses prior team via `preferredTeamBySteamId`; `GetOrCreate<PlayerTeam>` on spawn; spawn transforms strip scale (`WithoutScale`) so editor-scaled empties don't grow the player.

**Spawn point lists (post-slice-1 tweak, before slice 2):**
- `Team0Spawns: List<GameObject>` and `Team1Spawns: List<GameObject>` on `GameNetworkManager` (intended capacity: 6 per team for max 6v6).
- Host picker walks the list and uses the **first point with no same-team player within `SpawnPointOccupiedRadius` (default 60)**; falls back to a random entry if all are occupied.
- Old `Team0Spawn` / `Team1Spawn` singles kept as legacy fallback (used only when the list is empty). `JoinSpawnSpacing` line offset is now only used in the legacy single-spawn fallback path.

**Editor setup that's now expected:**
- `MapMatchConfig` somewhere in the scene (auto-find), or wired explicitly on `GameNetworkManager.MatchConfig`.
- **6 team spawn empties per team** wired into `Team0Spawns` / `Team1Spawns` on `GameNetworkManager`. Falls back to single `Team0Spawn` / `Team1Spawn`, then to `SpawnPoint`, if the lists are empty.
- `PlayerTeam` is auto-added at spawn (no manual editor work needed on the template).

**Verified behavior:**
- Solo play hits the tied case → random team → spawns at `Spawn_1` of that team (expected — only player, slot 1 always free).
- 2-window join: second player goes to the opposite team (smaller-team rule); two players forced onto same team take **different spawn points** thanks to occupied-radius picker.
- Player no longer scales up to the spawn empty's editor scale.

---

## Decisions to confirm at implementation time

- `Code/Match/` as the new system folder for `MatchDirector` / `GoalZone` / `MapMatchConfig`.
- Exact dwell value to start at (0.35s suggested).
- Whether intermission-frozen players keep their **gravity / collider** as-is, or are pinned in place.
- Whether match-over auto-rematches after N seconds, or waits for explicit host input.
- How to handle a player **disconnecting** during celebration / intermission (likely: remove from team count, no reshuffle).

---

## Open (still undecided)

These belong in `SESSION_NOTES.md` "Open decisions" once we start building:

- Tackles / dodges allowed during 5s celebration? (default: full movement = yes; might feel chaotic — playtest)
- Intermission: strict freeze (v1) vs walk-near-spawn (later)
- Series rules beyond 5 (best-of-3 matches? just one match for now? — currently: one match, restart on rematch)
- Practice NPCs on non-`testing_map` maps — out of scope, but flag if they ever ship to real maps

---

## When ready to build

Say "go" + which slice. Next default: **slice 3 (`GoalZone` + dwell)**.

### Slice 2 — shipped notes

**Files added:**
- `Code/Match/MatchPhase.cs` — phase enum.
- `Code/Match/MatchDirector.cs` — host phase machine, synced round wins / timer / phase, `RegisterGoal()`, OT on timer tie, `IsGameplayInputAllowed` (for slice 4 wiring).

**Editor setup:**
- Add **`MatchDirector`** component to the same object as `GameNetworkManager` (e.g. Main Camera).
- Turn on **`Enable Match Debug Logs`** to see `[Match]` lines in console.
- **`Enable Debug Force Goal`** stays on for now; **`Debug Force Goal Action`** = `DebugForceGoal` (F9 in `ProjectSettings/Input.config`). Host only. Remove or disable before shipping.

**Test (host):**
1. Play → timer counts down from 10:00 in `Playing`.
2. Press **F9** → celebration 5s → intermission 20s → `Playing` (timer paused during both).
3. F9 five times for one team (or until 5 round wins) → `MatchOver`.
4. Timer hits 0 with tied round wins → overtime log; next F9 ends match.

Once the full system is shipped:
- Trim this file or delete it; move durable rules into [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md).
- Add new component names to [`NAMING_CANON.md`](NAMING_CANON.md). (Slice 1 names already added.)
- Update [`SESSION_NOTES.md`](SESSION_NOTES.md) "Right now" and editor checklist.
