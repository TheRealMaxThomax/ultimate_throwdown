# Session Notes — start here

**What this is:** A cheat sheet for you and for AI chats so we don’t forget how the game works.  
**What to read:** This file most of the time. Other files only when you need design detail, exact names, or old history.

| File | Open when… |
|------|------------|
| **This file** | Every session — current goal, checklist, don’t-break rules |
| [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) | Tuning dodge/tackle, or planning weapons / classes |
| [`NAMING_CANON.md`](NAMING_CANON.md) | Exact script/property names — agents read this automatically when adding/renaming under `Code/` |
| [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) | Something broke before and you want the long “why we did it” story |

---

## Right now

**Goal:** Finish match flow — reset after goals, HUD, match over/rematch. Slices 1–3 (teams, phases, real scoring) are **in**.

**Match flow detail:** [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) — full plan + what’s done vs slice 4–6.

**Works today:** Ball grab/throw; tackles/ragdolls; dodge; **team assign + team spawns**; **`MatchDirector`** phases (celebration/intermission/timer/OT); **real goals** via `GoalZone` (hold ball in opponent zone ~0.35s). **Not yet:** teleport/ball reset after goal, input freeze in intermission, score HUD, rematch UI.

**Next up (match flow, in order):**
1. **Slice 4** — reset (teleport to spawns, ball to `BallSpawn`, force stand ragdolls, freeze input in intermission).
2. **Slice 5** — HUD (round score, goal banner, intermission countdown).
3. **Slice 6** — match over + host rematch same map.

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
| `Code/Player/` | Movement speed, dodge, tackle, class stats, cosmetics |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, `MapMatchConfig`, team/match flow |
| `Code/Map/` | Loads `testing_map` when the game starts |

**Scene you play in:** `scenes/throwdown_prototype.scene` (must have `GameNetworkManager` + `MatchDirector` + two `GoalZone`s).

**Important:** AI should **not** edit `.scene` files for you — you wire components in the s&box editor. AI edits `.cs` scripts when you ask.

---

## How the game is put together (simple rules)

- **One script, one job** — e.g. `BallGrab` = “who holds the ball”, `BallThrow` = “throwing”.
- **Walk into the ball = pick it up.** No kick button. No pushing the ball with your body (too hard to sync online).
- **Online: the host is the referee** — clients ask (“I want to throw”); host decides and tells everyone the result.
- **Tackles:** Only at full charge speed. Host spawns a **separate ragdoll object** (not physics on the player prefab).
- **Dodge:** Double-tap A or D (strafe left/right). Short invincibility vs tackles only. Carrier drops to walk after dodge; charge yaw slows a charger’s re-aim for a second pass.
- **Test dummies:** Tag `practice_npc` on **dummies only**, never on real players.
- **Weapons later:** You’ll hold ball **or** weapon, not both (not implemented yet).

More detail on past choices → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).

---

## Multiplayer testing (do this after network changes)

1. Start Play (host).
2. Use the network menu → **Join via new instance** (second window = client).
3. Check both windows see the same thing: grab, drop, throw, tackle, dodge.
4. Try spamming actions once to see if anything desyncs.

**Ball feels jittery on client only?** See the fix steps in [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

---

## Editor checklist (after scripts recompile)

**Match flow (scene root / manager object):**
- `GameNetworkManager` — `PlayerTemplateRoot`, `Team0Spawns` / `Team1Spawns` (6 each), optional `MatchConfig`
- `MatchDirector` — `Enable Match Debug Logs`; debug goal via `DebugForceGoal` (`,` key) until removed
- `MapMatchConfig` — Team A / Team B display names
- Two **`GoalZone`** objects — opposite `Defending Team` (0 and 1), `Box Size` tuned to goal mouth
- **`BallSpawn`** empty at center — wire when slice 4 lands (not required yet)

On the **player** object, confirm these components exist:

- `PlayerTackle`
- `PlayerDodge`
- `RagdollClientFeel`
- `PlayerClass` (with a `.cdata` file assigned)
- `CatchUpSpeedBoost`

Also:

- `PlayerController` → third-person camera distance **X = 185**
- **Do not** add `ModelPhysics` on the player (ragdoll is spawned separately)
- Optional: `MapInstance` with map name `testing_map`; remove duplicate grass floor if using Hammer floor

**Class data (`.cdata`):** If numbers feel wrong, open Speedster / Sniper / Juggernaut assets and check movement speeds (140 / 220 / 320) and dodge distance **260**, iframe **0.14 s**.

---

## Open decisions (not chosen yet)

- Holding forward + backward while charging — exploit or cool fake-out?
- Closed roof on arena vs open roof + sun for lighting
- Small screen shake on tackle hit — yes or no?

---

## Known issues

- [ ] Throw strength still needs playtest tuning (`ThrowForce`, etc. in inspector)
- [ ] While charging a throw, walk/run animations can play even though you can’t move
- [ ] Need longer multiplayer playtests (15–20 min, two windows)
- [ ] Closed indoor map was hard to read; using open roof for now

---

## For AI chats

Paste at the start of a new chat:

`Read SESSION_NOTES.md and MATCH_FLOW_PLAN.md first. Continue from “Next up”. Only open other doc files if the task needs them.`

**Undecided list:** When we postpone a design choice, add one short bullet under **Open decisions**; remove it when we decide.

## Recent session notes

- **2026-05-18:** Match flow slices 1–3 shipped (teams/spawns, `MatchDirector`, `GoalZone` scoring); slice 4+ in [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md).
- **2026-05-18:** Tackle whiff deferred (carrier-sprint shape if needed later); docs split into smaller files.
- **2026-05-13:** Map loads on clients too; keep `Resources: null`.
- **2026-05-08:** Dodge added (double-tap strafe).
- Older log → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
