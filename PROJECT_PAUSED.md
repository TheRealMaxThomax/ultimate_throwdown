# Ultimate Throwdown — paused (2026-07-16)

**Status:** Project paused. Moving to Unity for a new singleplayer game. Repo is intact — not abandoned.

**Original intent:** Party sports brawler played **on s&box** with friends (not a Steam standalone release). Success depends on platform population + discoverability, which is still early.

**When you return:** Read this file first, then [`SESSION_NOTES.md`](SESSION_NOTES.md) for day-to-day cheat sheet.

---

## Where we left off

**Last shipped (2026-07-14):**
- `MatchSetup` — 30s pre-match loadout window (round 1 + rematch)
- Ball pickup client feel (owner predict + host RPC)
- **Quake Slam** (Juggernaut ult, slice 5) — aim preview + 2-window MP verified

**Planned next (not started):**
1. **Ult slice 6 — Sniper path zones** (next in ship order)
2. **Quake Slam polish** — ring/launch tune, wind-up anim/SFX (`RingPhaseDelaySeconds` try **0.35** before **0.25**)
3. **Slice 2b remainder** — walkable intermission room (later)

**Ship order after that:** Combat slice 1 (unarmed melee) → Combat slice 2 (parry) → Ult slice 7 (weapons) → Progression (XP/unlocks).

Full table → [`SESSION_NOTES.md`](SESSION_NOTES.md) § Ship order.

---

## What works today

Core **2-window MP** is OK on: match flow (teams, goals, OT, rematch, pre-match setup), ball grab/throw (charge, trajectory preview, OOB), dodge, tackles + ragdolls + comic text, ult charge + Speed Blitz + Quake Slam, per-class prefabs + loadout picker (Q), join sync RPC.

**Scenes:** `throwdown_turf_wars.scene` (main), `practice_arena.scene`, `throwdown_prototype.scene` (greybox).

Detail → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) **Built vs not built**.

---

## Sniper path zones (slice 6 — design stub)

Not built. Most complex of the three first ults.

- Requires **ball** (exception to “no ult while holding ball”)
- Throw creates **ragdoll zones along the ball path** — ties into `BallThrow` / trajectory
- Planned split: `SniperBallPathUlt` + owner predict + feel siblings on `Player_Sniper` (see [`ARCHITECTURE.md`](ARCHITECTURE.md))
- Design stub → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) § Class ultimates; archive checklist → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) § Ult slice 6

**Pre-slice state:** Jugg/Sniper were pickable before their ults shipped; Quake Slam is now in catalog. Sniper still has no ult in catalog — **X inactive** for Sniper until slice 6.

---

## Loose ends (not blockers to resume)

| Item | Notes |
|------|--------|
| Join sync cross-machine | Code shipped — verify at **publish**, not just editor 2-window |
| Speed Blitz join-client spark sprites | Blue squares in editor join — publish smoke test |
| Throw charge 2-window scrub | Solo OK; full MP verify remaining |
| Throw strength / tackle launch | Still tuning |
| Longer MP soak | 15–20 min two-window tests not done |
| Walkable intermission room | MatchSetup timer shipped; physical room not built |
| PlayerTackle A4 | Ragdoll orbit camera extract — deferred |

Known issues list → [`SESSION_NOTES.md`](SESSION_NOTES.md) § Known issues.

---

## Resume checklist (first session back)

1. Open s&box editor, load **`throwdown_turf_wars.scene`**
2. Read [`SESSION_NOTES.md`](SESSION_NOTES.md) — **Don't break multiplayer** section
3. Host Play → **Join via new instance** — quick smoke: grab, throw, tackle, goal, rematch, loadout Q
4. Pick up work: slice 6 design pass **or** Quake Slam polish (your call)
5. Before any new combat/netcode: read [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md)

**MP smoke (minimum):** grab, throw, tackle both directions, dodge, goals, intermission, rematch, outlines, ball glow/compass, ult if touching ults.

---

## Don't break (MP gotchas)

- **`.sbproj`:** keep `"Resources": null` — never `"Resources": "*"` (clients fail to join)
- **Host is referee** for ball, tackles, ults, loadout
- **`MatchDirector`** on Main Camera — does **not** replicate; HUD via **`PlayerTeam`** sync
- **Practice NPCs:** `NetworkMode.Snapshot` — never `NetworkSpawn` player-prefab NPCs
- **AI rule:** do not edit `.scene` / `.vmdl` / `.vanmgrph` unless you explicitly say yes

---

## Code map

| Folder | Contents |
|--------|----------|
| `Code/Ball/` | Grab, throw, OOB, assist, carrier glow |
| `Code/Player/` | Movement, dodge, tackle, loadout |
| `Code/Network/` | `GameNetworkManager` |
| `Code/Match/` | `MatchDirector`, goals, OOB |
| `Code/Ultimates/` | Charge, Speed Blitz, Quake Slam |
| `Code/UI/` | HUD, loadout picker, comic bursts |
| `Code/Map/` | Traffic, practice lane |

Quake Slam siblings → `Code/Ultimates/Juggernaut/` (`JuggernautQuakeSlamUlt`, `QuakeSlamOwnerPredict`, `QuakeSlamAimPreview`, `QuakeSlamFeel`, `QuakeSlamRadiusMath`).

---

## Doc index

| Open when… | File |
|------------|------|
| Every session | [`SESSION_NOTES.md`](SESSION_NOTES.md) |
| Mechanics / tuning | [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) |
| Prefab wiring | [`COMPONENT_CHECKLIST.md`](COMPONENT_CHECKLIST.md) |
| Folder layout / spawn | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| New combat / MP feature | [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) |
| Shipped history / deep fixes | [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) |

---

## Why pause (for future-you)

Burnout + s&box still early (thin docs/community, weak discoverability for party games). Work here is **not wasted** — substantial MP prototype with good docs. Return when the engine and audience feel ready, or port design ideas to another platform.
