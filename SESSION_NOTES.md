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

**Goal:** Polish tackles and movement; test multiplayer more. Dodge works.

**Tackle whiff:** **Not adding now** — charge yaw + dodge tiers are enough unless playtests say otherwise. If we add it later: **carrier keeps sprint** on a committed miss, not attacker → sprint. Details → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md).

**Works today:** Pick up / drop / throw ball; auto-grab when you walk into the ball; tackles and ragdolls in multiplayer; dodge (double-tap left/right strafe).

**Next up (in order):**
1. Playtest multiplayer in **two game windows** (host + join).
2. Tune how hard tackles launch people (`TackleLaunchSpeed` in the editor).
3. Later: nicer stand-up animation.

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
| `Code/Map/` | Loads `testing_map` when the game starts |

**Scene you play in:** `scenes/throwdown_prototype.scene` (must have `GameNetworkManager`).

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

`Read SESSION_NOTES.md first. Continue from “Next up”. Only open other doc files if the task needs them.`

**Undecided list:** When we postpone a design choice, add one short bullet under **Open decisions**; remove it when we decide.

## Recent session notes

- **2026-05-18:** Tackle whiff deferred (carrier-sprint shape if needed later); docs split into smaller files.
- **2026-05-13:** Map loads on clients too; keep `Resources: null`.
- **2026-05-08:** Dodge added (double-tap strafe).
- Older log → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
