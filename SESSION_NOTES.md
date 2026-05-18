# Session Notes — start here

**What this is:** A cheat sheet for you and for AI chats so we don’t forget how the game works.  
**What to read:** This file most of the time. Other files only when you need design detail, exact names, or old history.

| File | Open when… |
|------|------------|
| **This file** | Every session — current goal, checklist, don’t-break rules |
| [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) | Match flow slices, networking gotchas, slice 6 tasks |
| [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) | Tuning dodge/tackle, or planning weapons / classes |
| [`NAMING_CANON.md`](NAMING_CANON.md) | Exact script/property names — agents read this automatically when adding/renaming under `Code/` |
| [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) | Something broke before and you want the long “why we did it” story |

---

## Right now

**Goal:** **Slice 6 only** — match over screen + host rematch. Match flow slices 1–5 are **in** and playtested (2-window MP).

**Match flow detail:** [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md)

**Works today:**
- Ball grab/throw; tackles/ragdolls; dodge; **crouch disabled** (`PlayerDisableCrouch`, Duck unbound in `Input.config`)
- **Teams + spawns** (balance on join, ground-snapped spawns)
- **`MatchDirector`** — phases, 10:00 match clock (`M.SS`), celebration / intermission, **OVERTIME** (tied at 0:00 → reset + 20s intermission, then golden goal)
- **`GoalZone`** dwell scoring
- **Post-goal reset** — teleport to spawns, ball to `BallSpawn`, ragdoll stand-up, 20s freeze (camera free)
- **Match HUD** on scene **`MatchHud`** root — score, clock, goal banner, intermission countdown

**Not yet:** `MatchOverHud`, working `HostRequestRematch()`, remove debug force-goal before ship.

**Next up:** **Slice 6** — match over UI + rematch (reuse slice 4 reset).

**Still later:** Tackle tuning, longer MP playtests, tackle whiff deferred → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md).

**Git:** Work on branch `main`, commit when a chunk of work is done.

---

## One rule that breaks multiplayer if you ignore it

In `ultimate_throwdown.sbproj`, keep:

```json
"Resources": null
```

**Do not** set `"Resources": "*"` to “fix” missing textures. That has made clients **unable to join** (error about chunk size over 1024).

If join breaks after a change, put `Resources` back to `null` and test again with two windows.

**Map:** The game loads `testing_map` via code (`StartupMapBootstrap`), not only from the project map list. Edit the map in Hammer: `Assets/Maps/testing_map.vmap`, then compile.

---

## Where the code lives

| Folder | What’s in it |
|--------|----------------|
| `Code/Ball/` | Ball pickup, throw, charge bar, smooth ball on clients |
| `Code/Player/` | Movement, dodge, tackle, team, class, cosmetics, **no crouch** |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, `MapMatchConfig` |
| `Code/UI/` | Match HUD + placeholder owner HUDs (dodge/ramp) |
| `Code/Map/` | Loads `testing_map` when the game starts |

**Scene you play in:** `scenes/throwdown_prototype.scene`

**Important:** AI should **not** edit `.scene` files unless you ask — you wire components in the s&box editor.

---

## Multiplayer gotcha (match flow)

`MatchDirector` is on **Main Camera** — each machine has its own copy. **Clients do not** use it for freeze/HUD/score.

**Authoritative on clients:** synced fields on **`PlayerTeam`** (on each network-spawned player). Host pushes via `MatchDirector.PushMatchHudStateToPlayers()`.

---

## How the game is put together (simple rules)

- **One script, one job** — e.g. `BallGrab` = “who holds the ball”, `BallThrow` = “throwing”.
- **Walk into the ball = pick it up.** No kick button.
- **Online: the host is the referee** — clients request; host decides.
- **Tackles:** Only at full charge speed. Separate **ragdoll object** spawned on host.
- **Dodge:** Double-tap A or D. Tackle iframe only.
- **Crouch:** Disabled — do not rebind `Duck` without re-enabling intentionally.
- **Test dummies:** Tag `practice_npc` on **dummies only**.
- **Weapons later:** Ball **or** weapon, not both (not implemented).

More history → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).

---

## Multiplayer testing (do this after network changes)

1. Start Play (host).
2. Network menu → **Join via new instance** (second window = client).
3. Check both windows: grab, throw, tackle, dodge, **goals, reset, intermission freeze, HUD**.
4. Spam actions once to probe desync.

**Ball jittery on client only?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

---

## Editor checklist

**Main Camera (manager):**
- `GameNetworkManager` — `PlayerTemplateRoot`, `Team0Spawns` / `Team1Spawns` (6 each)
- `MatchDirector` — `BallSpawn` wired; `Enable Match Debug Logs` optional; remove `EnableDebugForceGoal` before ship
- `MapMatchConfig` — team display names

**`MatchHud` empty (scene UI root):**
- `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud`

**Map:**
- Two **`GoalZone`** — opposite `Defending Team`, tuned `Box Size`
- **`BallSpawn`** at center → wired on `MatchDirector`

**Player prefab:**
- `PlayerTeam` (auto at spawn), `PlayerTackle`, `PlayerDodge`, `RagdollClientFeel`, `PlayerClass`, `CatchUpSpeedBoost`
- **`PlayerDisableCrouch`** (also auto-added at network spawn — add on prefab for scene NPCs)
- `DodgeCooldownHud`, `MovementRampHud`, `ThrowChargeBar` (owner HUD)
- `PlayerController` camera **X = 185**; **no** `ModelPhysics` on player

---

## Open decisions (not chosen yet)

- Holding forward + backward while charging — exploit or cool fake-out?
- Closed roof on arena vs open roof + sun for lighting
- Small screen shake on tackle hit — yes or no?

---

## Known issues

- [ ] Throw strength still needs playtest tuning
- [ ] Walk/run animations while charging throw (can’t move)
- [ ] Need longer multiplayer playtests (15–20 min, two windows)
- [ ] Match over is phase-only today — no rematch UI until slice 6

---

## For AI chats

Paste at the start of a new chat:

```
Read SESSION_NOTES.md and MATCH_FLOW_PLAN.md. Continue from slice 6 (match over + rematch). Do not edit .scene files unless I ask.
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-05-18:** Match flow slices 4–5 shipped (reset/MP freeze, HUD + `M.SS` clock); OT setup = reset + intermission; crouch disabled.
- **2026-05-18:** Match flow slices 1–3 (teams, `MatchDirector`, `GoalZone`).
- **2026-05-18:** Tackle whiff deferred; docs split.
- **2026-05-13:** Map on clients; keep `Resources: null`.
- Older log → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
