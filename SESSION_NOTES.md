# Session Notes — start here

**What this is:** A cheat sheet for you and for AI chats — current goal, ship order, don't-break rules.  
**Detail lives elsewhere:** mechanics → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) · wiring → [`ARCHITECTURE.md`](ARCHITECTURE.md) · names → [`NAMING_CANON.md`](NAMING_CANON.md) · MP/netcode → [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) · history → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md)

| File | Open when… |
|------|------------|
| **This file** | Every session |
| [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) | Dodge/tackle/classes/ultimates/loadout design, tuning |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Folder layout, spawn wiring, prefab checklist |
| [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) | Full match flow (slices 1–6 complete) |
| [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) | **MP / netcode** — host authority, client predict, **checklist for new combat features**, Testing |
| [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) | Shipped slice checklists, editor wiring, deep fixes |

**Doc hygiene:** Keep this file under ~250 lines. Trim **Recent session notes** to ~2 weeks; move older bullets to archive.

---

## Right now

**Goal:** **Quake Slam slice 5 — solo playable ✅ (2026-07-09).** **Next:** verify aim-preview scale + color in play, tune rings/launch, 2-window MP verify.

**Next session:**
1. **Quake Slam aim preview** — code fix: unparented rings + `LocalScale` + `materials/oob_drop_ring.vmat`. **Playtest:** inner (70) should be ~78% of OOB drop ring (180 dia); re-save Jugg prefab if inspector still shows old `speed_blitz_preview` material path
2. Tune `JuggernautQuakeSlamUlt` radii/launch after preview matches hit zones; 2-window MP smoke
3. Polish: wind-up anim, slam SFX, ring pulse VFX (later)

---

## Ship order

| Order | Slice | Status |
|-------|-------|--------|
| **1** | Map slice 1 — ball OOB | ✅ Shipped 2026-07-04 |
| **2** | Prefab split + loadout v1 | ✅ Shipped 2026-07-06 |
| **3** | Ult slice 5 — Juggernaut Quake Slam | **Solo OK — tune preview + MP verify** |
| **4** | Ult slice 6 — Sniper path zones | Planned |
| **2b** | `MatchSetup` + walkable intermission room | After slice 5 |
| **5** | Combat slice 1 — unarmed melee | Before weapons |
| **6** | Combat slice 2 — parry | Melee only |
| **7** | Ult slice 7 — Weapons | After combat 1 |
| **8** | Progression — XP, unlocks, server ledger | After weapons |

Full combat/progression specs → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md). Join sync: **code shipped — cross-machine verify at publish.**

---

## Works today (summary)

Match flow, teams, goals, OT, rematch, Turf Wars map (traffic, lamps, OOB), practice arena, ball grab/throw (planted charge, RMB cancel, trajectory preview), dodge channel, tackles + ragdolls + comic text, ult charge + assist + Speed Blitz 2a–2d + **Quake Slam (Jugg — hold/release X, wind-up, 3-ring knockdown; aim preview WIP scale/color)**, per-class prefabs + loadout picker (Q, intermission/practice), join sync RPC. **2-window MP OK** on core flows; **Quake Slam MP not verified yet**.

Mechanics detail → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) **Built vs not built**.

---

## Don't break multiplayer

**`.sbproj` — keep `"Resources": null`.** Never set `"Resources": "*"` to fix missing textures — clients fail to join (chunk size > 1024).

**MatchDirector** is on **Main Camera** — it does **not** replicate. Clients use synced fields on **`PlayerTeam`** (host pushes via `MatchDirector.PushMatchHudStateToPlayers()`).

**Host is referee** for ball, tackles, ults, loadout apply. Owner sends RPCs; host decides. Feel predict + **`CombatFeelPredictDedupe`** — see [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md).

**Editor join quirk:** "Join via new instance" may not mount local sprites/textures on the client (blue squares). Not a game bug — publish test for VFX. **Do not** fix with `"Resources": "*"` ([Facepunch #5177](https://github.com/Facepunch/sbox-public/issues/5177)).

**Practice NPCs:** scene dummies = **`NetworkMode.Snapshot`** — **never `NetworkSpawn` player-prefab NPCs**. Knockdown = host **`PracticeNpcClient*Rpc`**; patrol pose = **`PracticeNpcPatrolPoseRelay`**. Details → archive.

**AI:** Do not edit `.scene` / `.vmdl` / `.vanmgrph` unless Max explicitly says yes — give editor steps instead.

---

## Where the code lives

| Folder | What's in it |
|--------|----------------|
| `Code/Ball/` | Grab, throw, OOB, assist, carrier glow, client feel |
| `Code/Player/` | Movement, dodge, tackle, loadout, cosmetics, anim overlays |
| `Code/Network/` | `GameNetworkManager` — spawn, teams, loadout apply |
| `Code/Match/` | `MatchDirector`, goals, OOB zones, audio bootstrap |
| `Code/Ultimates/` | `PlayerUltCharge`, Speed Blitz + Quake Slam + feel |
| `Code/UI/` | Match HUD, owner HUDs, loadout picker, comic bursts |
| `Code/Map/` | Traffic, practice lane, lights, bootstrap |

**Scenes:** `throwdown_turf_wars.scene` · `practice_arena.scene` (`PracticeArenaMode`) · `throwdown_prototype.scene` (greybox).

Loadout architecture + spawn wiring → [`ARCHITECTURE.md`](ARCHITECTURE.md) § Loadout & spawn.

---

## Simple rules (don't forget)

- **One script, one job** — e.g. `BallGrab` = hold state, `BallThrow` = release flow.
- **Walk into ball = pick up.** Cylinder pickup; ball follows **`hold_R`** while held.
- **Tackle** = charge speed only. Host ragdoll object, not physics on player prefab.
- **Dodge** = double-tap A/D; capped slide; hard stop at channel end.
- **Ult charge** on prefab manually — **not** GNM auto-add. **`Ultimate`** = **X**.
- **Online:** host authority; clients request via `[Rpc.Host]`.

More implementation notes → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) § Gameplay implementation notes.

---

## MP smoke test (after network changes)

**Before any new combat feature or netcode change:** read [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) (authority rules + **Testing** + new-feature checklist).

1. Host Play → **Join via new instance**.
2. Both windows: grab, throw, tackle (both directions), dodge, goals, intermission, rematch, HUD.
3. Enemy outlines (standing + ragdoll). Ball carrier glow + compass.
4. OOB flow if touching ball code. Ult charge bumps + Speed Blitz commit if touching ults.
5. Loadout: intermission Q picker, class respawn if changed.

**Full 17-step checklist** + per-slice verify table → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) § Multiplayer testing (full). Ball jitter / ragdoll fixes → archive.

---

## Editor essentials

Full prefab/scene wiring → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) § Editor checklist.

**Every session:**
- Main Camera: `GameNetworkManager` (class templates + spawns), `MatchDirector` (`BallSpawn`), `Highlight` post-process
- Player prefab(s): core gameplay on prefab; GNM auto-adds feel/HUD only — see [`ARCHITECTURE.md`](ARCHITECTURE.md)
- `main_ball`: `BallGrab`, OOB components (or GNM auto-add)
- Turf Wars: `GoalZone` ×2, OOB zones, `playerclip` walls, traffic spawners

**Before ship:** `Enable Debug Force Goal` off on `MatchDirector`.

---

## Open decisions

- **Competitive vs casual FF (later):** v1 = FF on tackles/melee; ults enemies-only. Future mode flag.
- **Competitive loadout rules (later):** ranked may lock class or cap swaps.
- **Loadout unlocks / XP (later):** server-trusted ledger; picker filters unlocked only.
- **Sumo / shrink-ring mode (later):** FFA, match-start pick + force-commit.
- **Player body for v1:** leaning **`citizen_human_*`** vs classic citizen — human likely for release.
- Closed vs open roof arena lighting
- **Tackle oof/grunt** — not shipped
- **Practice patrol zigzag** — straight A↔B only for now
- **Practice arena ball OOB (later):** instant-respawn volumes, not match OOB flow
- Map vote: change vote during 30s window?
- **Traffic knockdown tuning**
- **Ball compass:** optional distance readout
- **Throw charge bone mask:** half-spine+arms vs right-arm only — playtest pick
- **Hero asset art:** low-poly maps; players/ball may get higher detail later
- **Comic scope:** tackles/knockdowns v1; ults get own burst palette
- **Speed Blitz impact stride:** snap `charge_run_cycle` at connect vs freeze pose — TBD
- **Speed Blitz victim flinch (later):** optional hit-react during hang
- **Juggernaut post-tackle run recovery (passive):** keep sprint after tackle vs tackle ramp — mutually exclusive
- **Speed Blitz join-client spark sprites:** deferred to publish smoke test
- **UI font pass:** Les Flos (comic/OOB) + Barlow Condensed (HUD/menus) — not wired yet
- **Quake Slam aim preview color (v1):** warm orange quake (default tints on white `oob_drop_ring.vmat`) vs ult blue — settled orange unless playtest says otherwise
- **Quake Slam default radii (70/135/200):** playtest may retune after preview scale verify
- **Quake Slam aim preview annulus bands (later polish):** replace nested filled discs with true donut geometry or shader mask — matches gameplay annuli, no alpha overlap

Settled items removed 2026-07-06 cleanup → archive decision log.

---

## Known issues

- **Ragdoll arms z-fight** — left as-is; theory = overlapping arm meshes on ragdoll clone. Detail → archive.
- **Tackle comic** — Les Flos import + optional 2-window MP verify (exits good enough for v1).
- **Throw charge MP verify** — plant/cancel solo OK; 2-window scrub check remaining; bone mask open.
- **Throw strength** — still needs tuning.
- **MP join black face flash (host)** — likely cosmetics async; unconfirmed.
- **Longer MP soak** — 15–20 min two-window tests still needed.
- **Spot-light shadow lines** — engine (#10960); defer or shadows off on non-hero lamps.
- **Clutter missing after engine reload** — save scene; check volume bounds.
- **Traffic engine loop seam** — re-export audio if needed.
- **Quake Slam aim preview scale** — uses `Model.Bounds` for mesh diameter + `WorldScale`; playtest outer (200 r → 400 dia)
- **Quake Slam 2-window MP** — not verified yet

---

## For AI chats

```
Read SESSION_NOTES.md → Quake Slam slice 5 solo OK → next: aim preview scale + material color, tune rings, 2-window MP.
Quake Slam: Code/Ultimates/Juggernaut/ — JuggernautQuakeSlamUlt + QuakeSlamOwnerPredict + QuakeSlamAimPreview + QuakeSlamFeel + QuakeSlamRadiusMath. Catalog quake_slam. Design: GAMEPLAY_DESIGN.md § Quake Slam.
PlayerTackle Track A ✅ (A4 deferred). Speed Blitz Track B ✅ (B3 optional).
Wiring: ARCHITECTURE.md § Juggernaut prefab siblings. Names: NAMING_CANON.md. MP: MULTIPLAYER_NETCODE.md.
Do not edit .scene / .vmdl / .vanmgrph unless I explicitly say yes.
```

**Undecided:** add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-07-09 (Quake Slam — solo playable ✅):** Max wired Jugg prefab; hold/release X, wind-up, slam, rings work. **Follow-up:** aim preview switched from dev plane → `oob_drop_ring.vmdl` filled discs; **preview too small** vs tuned radii; **green tint** = bad material fallback + orange tint on blue blitz preview mat — fix to `materials/oob_drop_ring.vmat`. `ThrowChargeCamera` missing `quakeSlamUlt` field fixed (CS0103).
- **2026-07-09 (Quake Slam slice 5 — code ✅):** `JuggernautQuakeSlamUlt` + siblings; catalog `quake_slam`; loadout enable; `CatchUpSpeedBoost` wind-up plant; design locked in `GAMEPLAY_DESIGN.md`.
- **2026-07-07 (PlayerTackle Track A ✅):** A1 `TackleRagdollLifecycle`, A2 `TackleImpactRelay`, A3 `PracticeNpcTackleClientRelay` — 2-window MP practice dummy mirroring signed off. Orchestrator ~1,175 lines. **A4** (ragdoll orbit camera extract) **deferred**.
- **2026-07-06 (join sync editor smoke ✅):** Host double `[PlayerLoadout]` on joiner spawn, no errors. Same SteamId = not cross-host proof. **Code shipped — verify at publish.**
- **2026-07-06 (loadout v1 ✅):** Intermission Q picker, class respawn, join sync RPC, force-commit. Prefab split + per-class spawn.
- **2026-07-06 (SESSION_NOTES cleanup):** Trimmed to ~250 lines; loadout design → GAMEPLAY_DESIGN; wiring → ARCHITECTURE; shipped slices → archive.

Older bullets → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
