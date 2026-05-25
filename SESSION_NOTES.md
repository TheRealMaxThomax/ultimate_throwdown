# Session Notes — start here

**What this is:** A cheat sheet for you and for AI chats so we don’t forget how the game works.  
**What to read:** This file most of the time. Other files only when you need design detail, exact names, or old history.

| File | Open when… |
|------|------------|
| **This file** | Every session — current goal, checklist, don’t-break rules |
| [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) | Full match flow design (slices 1–6 **complete**) |
| [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) | Tuning dodge/tackle, or planning weapons / classes |
| [`NAMING_CANON.md`](NAMING_CANON.md) | Exact script/property names — agents read this automatically when adding/renaming under `Code/` |
| [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) | Something broke before and you want the long “why we did it” story |

---

## Right now

**Goal:** **v1 match flow is done** (slices 1–6). Next focus is gameplay polish, longer MP playtests, and **map vote** when you’re ready (see [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) → Later).

**Works today:**
- Ball grab/throw; tackles/ragdolls; dodge; **crouch disabled** (`PlayerDisableCrouch`, Duck unbound in `Input.config`)
- **Teams + spawns** (balance on join, ground-snapped spawns)
- **`MatchDirector`** — phases, 10:00 match clock (`M.SS`), goal celebration / intermission, **OVERTIME**, **match over**
- **`GoalZone`** dwell scoring
- **Post-goal reset** — teleport to spawns, ball to `BallSpawn` (ground clearance), ragdoll stand-up, 20s freeze (camera free)
- **Match HUD** on **`MatchHud`** — score, clock, goal banner, intermission countdown, **match over** (10s winner celebration → host **`1`** rematch)
- **Rematch** — same map, fresh 0–0 and 10:00 timer (`HostRequestRematch`)
- **Enemy team outline** — red `HighlightOutline` on opponents (not self/teammates); same look on tackle ragdolls (`PlayerEnemyOutline`, `RagdollEnemyOutline`); tune on player prefab **`HighlightOutline`**

**Before ship (optional):** Uncheck **`Enable Debug Force Goal`** on `MatchDirector` in scene if you don’t want `,` testing in builds (already **off** by default in code).

**Still later:** Tackle tuning, map vote (30s, all players, `Slot1`–`N`), tackle whiff deferred → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md).

**Git:** Work on branch `main`, commit when a chunk of work is done.

---

## One rule that breaks multiplayer if you ignore it

In `ultimate_throwdown.sbproj`, keep:

```json
"Resources": null
```

**Do not** set `"Resources": "*"` to “fix” missing textures. That has made clients **unable to join** (error about chunk size over 1024).

If join breaks after a change, put `Resources` back to `null` and test again with two windows.

**Map:** Geometry lives in the active **`.scene`** (e.g. `throwdown_turf_wars.scene`). `StartupMapBootstrap` no longer injects Hammer `MapInstance` maps — delete any `MapInstance` in the scene if you still see old compiled geometry.

---

## Where the code lives

| Folder | What’s in it |
|--------|----------------|
| `Code/Ball/` | Ball pickup, throw, charge bar, smooth ball on clients |
| `Code/Player/` | Movement, dodge, tackle, team, class, cosmetics, **no crouch** |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, `MapMatchConfig` |
| `Code/UI/` | Match HUD + placeholder owner HUDs (dodge/ramp) |
| `Code/Map/` | `StartupMapBootstrap` — locks `practice_npc` rigidbodies only (no Hammer map load) |

**Scene you play in:** `scenes/throwdown_turf_wars.scene` (Turf Wars WIP). `throwdown_prototype.scene` = older greybox fallback.

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
- **Tackles:** Only at full charge speed. Host spawns **ragdoll object**; clients **request** via RPC. Launch = pelvis `ApplyImpulse` on host **before** `NetworkSpawn` (poll `RagdollPhysicsInitDelay` max). Juggernaut bonus: owner mirror sent in RPC so client tackles aren’t weaker.
- **Dodge:** Double-tap A or D. Tackle iframe only.
- **Crouch:** Disabled — do not rebind `Duck` without re-enabling intentionally.
- **Test dummies:** Tag `practice_npc` on **dummies only**.
- **Weapons later:** Ball **or** weapon, not both (not implemented).
- **Enemy outlines:** Camera needs **`Highlight`** post-process (`EnemyOutlineCameraSetup` on Main Camera, or add `Highlight` manually). Per-player **`HighlightOutline`** on the prefab is the style source; ragdolls copy it on the host (`NetVictimTeamId` synced for clients).

More history → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).

---

## Multiplayer testing (do this after network changes)

1. Start Play (host).
2. Network menu → **Join via new instance** (second window = client).
3. Check both windows: grab, throw, tackle (**host→client and client→host**, similar launch distance), dodge, **enemy red outlines** (standing + ragdoll, both directions), **goals, reset, intermission, match over, rematch, HUD**.
4. Spam actions once to probe desync.

**Ball jittery on client only?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

**Client tackle looks short or late?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Ragdoll (technical)”. Don’t re-add `StartAsleep` or mute collision sounds without waking bodies — broke launch (2026-05-18).

---

## Multiplayer gotcha (tackles)

- Physics and impulse are **host-only** on `PlayerRagdoll`.
- Remote attacker: `TryOwnerRequestTackleOnHost` → `RequestTackleApplyOnHost` (owner positions + `ownerTackleChargeBonus`).
- **Do not** require extra host-side charge/distance gates on the RPC — `NetAtChargeSpeed` / host positions lag and tackles feel late.
- Rare: impact sound spam at tackle start (client → host); left alone — not worth breaking launch.

---

## Editor checklist

**Main Camera (manager):**
- `GameNetworkManager` — `PlayerTemplateRoot`, `Team0Spawns` / `Team1Spawns` (6 each)
- `MatchDirector` — `BallSpawn` wired; `Enable Match Debug Logs` optional; `Enable Debug Force Goal` off for ship
- `MapMatchConfig` — team display names
- **`EnemyOutlineCameraSetup`** on Main Camera (adds `Highlight` post-process) — **or** add **`Highlight`** (Post Processing) yourself; keep **Enable Post Processing** on the camera

**`MatchHud` empty (scene UI root):**
- `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud`, **`MatchOverHud`**

**Map:**
- Two **`GoalZone`** — opposite `Defending Team`, tuned `Box Size`
- **`BallSpawn`** at center → wired on `MatchDirector`

**Player prefab** (clone source for `GameNetworkManager` — all joins inherit these values):
- `PlayerTeam` (auto at spawn), `PlayerTackle`, `PlayerDodge`, `RagdollClientFeel`, `PlayerClass`, `CatchUpSpeedBoost`
- **`PlayerDisableCrouch`** (also auto-added at network spawn — add on prefab for scene NPCs)
- **`Move Mode Walk` → Step Up Height** — global curb step (default was **10**; try **24–32** for 16-unit geo). Tune here only — no code wrapper.
- **`HighlightOutline`** — tune colors/width here (ragdoll copies this exact component); optional **`PlayerEnemyOutline`** (auto at spawn)
- `DodgeCooldownHud`, `MovementRampHud`, `ThrowChargeBar` (owner HUD)
- `PlayerController` camera **X = 185**; **no** `ModelPhysics` on player
- **`BallThrow` → Throw Direction Source** optional; if empty, throw uses **`PlayerController.EyeAngles`** (look while charging)

---

## Open decisions (not chosen yet)

- Holding forward + backward while charging — exploit or cool fake-out?
- Closed roof on arena vs open roof + sun for lighting
- Small screen shake on tackle hit — yes or no?
- Map vote: allow changing vote during the 30s window?

---

## Known issues

- [ ] Throw strength still needs playtest tuning
- [ ] Walk/run animations while charging throw (can’t move)
- [ ] Need longer multiplayer playtests (15–20 min, two windows)

---

## For AI chats

Paste at the start of a new chat:

```
Read SESSION_NOTES.md. Match flow slices 1–6 are done (MATCH_FLOW_PLAN.md). Do not edit .scene files unless I ask.
Prefer inspector / existing engine components (e.g. Move Mode Walk Step Up Height) — do not add code that only mirrors a property Max can set on the player template.
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-05-25:** **Scene-first map** — Turf Wars in `throwdown_turf_wars.scene` (Mapping **M**, clutter, Blender props). No Hammer `MapInstance` auto-load. Night lighting: **Ambient Light** + **Envmap Probe** + spot lights (not `DirectionalLight.SkyColor`). **`BallThrow`** aims via **`EyeAngles`** when `ThrowDirectionSource` unset. **Step up** = **`Move Mode Walk`** on player template only.
- **2026-05-21:** **Low poly** map art direction (Turf Wars lowpoly vmaps + `turfwars_*` materials). Perimeter walls around map edges — open void caused meshes (map, player, ball) to disappear at some camera angles.
- **2026-05-18:** MP tackle parity — impulse before `NetworkSpawn` + body poll; owner `ownerTackleChargeBonus` in RPC; reverted `StartAsleep` / collision-sound mute (killed launch).
- **2026-05-18:** Enemy team outlines — `Highlight` on camera, `PlayerEnemyOutline` + ragdoll copy via `RagdollEnemyOutline` / `NetVictimTeamId` (2-window MP).
- **2026-05-18:** Match flow **slice 6** — match over celebration, `MatchOverHud`, host **`1`** rematch, ball ground snap fix.
- **2026-05-18:** Match flow slices 4–5 shipped (reset/MP freeze, HUD + `M.SS` clock); OT setup = reset + intermission; crouch disabled.
- **2026-05-18:** Match flow slices 1–3 (teams, `MatchDirector`, `GoalZone`).
- **2026-05-18:** Tackle whiff deferred; docs split.
- **2026-05-13:** Map on clients; keep `Resources: null`.
- Older log → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
