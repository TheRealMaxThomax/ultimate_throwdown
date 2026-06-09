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

**Goal:** **v1 match flow is done** (slices 1–6). Gameplay polish, longer MP playtests, **map vote** when ready (see [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) → Later).

**Next session (priority order):**
1. **MP join flash** — host sees brief black mesh face when client joins (likely cosmetics load; investigate).
2. Longer soak (15–20 min, two windows); map vote when ready.
3. **Human hold anim** — carry pose on anim graph (bone attach OK; arms don’t curl yet).

**Works today:**
- Ball grab/throw — held ball on **`hold_R`** hand bone (`BallGrab` + `BallClientFeel`); **throw trajectory preview** (`ThrowTrajectoryPreview` + `ThrowReleaseMath`); **`ThrowChargeCamera`** / **`ThrowChargeBar`**; **throw polish 2-window MP OK**; **ball carrier glow** — `BallCarrierOutline` (white ↔ green / red team pulse; thinner default outline); **`BallCompassHud`** — ring + **BALL** hub + ring-edge triangle (player bearing; green/red/white); **`main_ball`** art WIP — emissive gold `ball_v2.vmat` + scroll (neutral vs team cues on glow/compass); tackles/ragdolls; dodge; **crouch disabled**
- **Teams + spawns** (balance on join, ground-snapped spawns)
- **`MatchDirector`** — phases, 10:00 match clock (`M.SS`), goal celebration / intermission, **OVERTIME**, **match over**
- **`GoalZone`** dwell scoring
- **Post-goal reset** — teleport to spawns, ball to `BallSpawn` (ground clearance), ragdoll stand-up, 20s freeze (camera free)
- **Match HUD** on **`MatchHud`** — score, clock, goal banner, intermission countdown, **match over** (10s winner celebration → host **`1`** rematch)
- **Rematch** — same map, fresh 0–0 and 10:00 timer (`HostRequestRematch`)
- **Enemy team outline** — red `HighlightOutline` on opponents (not self/teammates); same look on tackle ragdolls (`PlayerEnemyOutline`, `RagdollEnemyOutline`); tune on player prefab **`HighlightOutline`**
- **Street lamps (Turf Wars)** — `streetlight.vmdl` + warm spots; **`streetlight_broken.vmdl`** for dead poles (no spot/emissive). Optional **`StreetLightFlicker`** on a **per-lamp parent** empty (child model + child spot) — syncs spot + bulb emissive (`goldenearth_streetlight_off.vmat` on **`light.vmat`** slot, auto index `-1`)
- **Petrol station lights** — optional **`StationLightFlicker`** on a parent empty (child `Spot Light` + child block mesh). Keeps mesh visible and flickers via `Spot.Enabled` + mesh `Color` (`VisualOnColor`/`VisualOffColor`)
- **Road traffic (Turf Wars — Road0 + Road1)** — **`TrafficSpawner`** + disabled **`TrafficCarTemplate`**. **3 car models per lane** via **`CarModelVariants`** (red Road0 / blue Road1); host applies random **Body renderer + Model Collider** **after** **`NetworkSpawn`** + **`Network.Refresh`**. **Physics mesh** on each `.vmdl`; **ball bounce** on host. Knockdown via code hit box + **`PlayerTackle.ApplyKnockdownFromHost`**. **Engine sounds** — idle = cruise/slow, drive = accel only. **`Game.IsPlaying`** guard (no editor spawn spam). **2-window MP OK**.

**Before ship (optional):** Uncheck **`Enable Debug Force Goal`** on `MatchDirector` in scene if you don’t want `,` testing in builds (already **off** by default in code).

**Still later:** Tackle tuning, map vote (30s, all players, `Slot1`–`N`), tackle whiff deferred → [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md).

**Avatar (WIP):** On branch **`feature/human-avatar`**, Player **Body** uses **`citizen_human_*`** (`models/citizen/citizen_human/`) + human locomotion graph — **not** `citizen.vmdl` / **`citizen_holdball_test`**. Male/female from account via **`PlayerCosmeticsSync`** / `ClothingContainer`; no dual-body v1. Solo + 2-window MP: cosmetics, tackle ragdoll, launch distance OK vs old citizen. **Leaning human for release** (goofy tone from gameplay, not silhouette); merge TBD.

**Git:** Day-to-day on `main`; avatar scene work on **`feature/human-avatar`** until merged.

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
| `Code/Ball/` | Ball pickup, throw, charge bar, trajectory preview (`ThrowReleaseMath`), **`BallCarrierOutline`**, smooth ball on clients |
| `Code/Player/` | Movement, dodge, tackle, team, class, cosmetics, **no crouch** |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, `MapMatchConfig` |
| `Code/UI/` | Match HUD + placeholder owner HUDs (dodge/ramp) + **`BallCompassHud`** |
| `Code/Map/` | `StartupMapBootstrap` (practice NPC locks); **`StreetLightFlicker`** (decorative lamp flicker); **`StationLightFlicker`** (petrol station spot + mesh color flicker); **`TrafficSpawner`** / **`TrafficCar`** (host lane traffic + knockdown) |

**Scene you play in:** `scenes/throwdown_turf_wars.scene` (Turf Wars WIP). `throwdown_prototype.scene` = older greybox fallback.

**Important:** AI should **not** edit `.scene` files unless you ask — you wire components in the s&box editor.

---

## Multiplayer gotcha (match flow)

`MatchDirector` is on **Main Camera** — each machine has its own copy. **Clients do not** use it for freeze/HUD/score.

**Authoritative on clients:** synced fields on **`PlayerTeam`** (on each network-spawned player). Host pushes via `MatchDirector.PushMatchHudStateToPlayers()`.

---

## How the game is put together (simple rules)

- **One script, one job** — e.g. `BallGrab` = “who holds the ball”, `BallThrow` = “throwing”, `ThrowTrajectoryPreview` = owner aim helper only.
- **Throw trajectory preview:** Owner-only scrolling white dashed arc + **1:1 held-ball clone** landing marker (`TranslucentBallMaterialPath` → `ball_translucent.vmat`), first arc to ground (no bounces). Dash scroll uses simulation-time keys so motion stays visible while charge lengthens the arc. `ThrowReleaseMath` shares release velocity with `BallThrow`; preview pivot = `BallGrab.GetPredictedThrowReleasePivotPosition()`. Use **Translucent** material + `g_flOpacityScale` (not tint-alpha on opaque — grains on clients).
- **Ball carrier glow:** `BallCarrierOutline` auto on `main_ball` — **white ↔ green** (teammate) / **white ↔ red** (enemy) colour pulse; ring width scales with camera distance; emissive breathe for non-carrier viewers; carrier sees nothing; no through walls. Tune on ball: `PulseWhiteColor`, `FriendlyAccentColor`, `EnemyAccentColor`, `EmissiveBrightnessMax`.
- **Ball compass:** `BallCompassHud` — bottom-left panel + ring; white **`LabelText`** hub centered in ring (default **BALL**); small **triangle** orbits ring edge (360°) toward **`main_ball`** (held or loose). **White** loose · **green** teammate · **red** enemy. **Needle hidden** when you carry (label + panel + dim ring stay). Bearing = **player position + `EyeAngles` yaw** (not camera). Auto-added on network spawn (`GameNetworkManager.GetOrCreate`). Tune: `MarginLeft` / `MarginBottom` / `CompassSize` / `NeedleTipRadius` / colours.
- **Throw charge camera:** `ThrowChargeCamera` lerps `PlayerController.CameraOffset` + main-camera FOV with `BallThrow.GetThrowChargeLerp()`; quick smoothstep blend on throw/cancel. **Does not run** while ragdolled or during `PlayerTackle` stand-up camera blend — ragdoll orbit is applied in `PlayerTackle.OnPreRender` last.
- **Walk into the ball = pick it up.** No kick button. While held, ball follows **`hold_R`** on **Body** `SkinnedModelRenderer` (`BallGrab.HoldBoneName`; falls back to `HoldAnchor`). Old `HandHoldPoint` + `citizen_holdball_test` IK was for classic citizen — human uses bone attach.
- **Online: the host is the referee** — clients request; host decides.
- **Tackles:** Only at full charge speed. Host spawns **ragdoll object**; clients **request** via RPC. Launch = pelvis `ApplyImpulse` on host **before** `NetworkSpawn` (poll `RagdollPhysicsInitDelay` max). Juggernaut bonus: owner mirror sent in RPC so client tackles aren’t weaker.
- **Dodge:** Double-tap A or D. Tackle iframe only.
- **Crouch:** Disabled — do not rebind `Duck` without re-enabling intentionally.
- **Test dummies:** Tag `practice_npc` on **dummies only**.
- **Weapons later:** Ball **or** weapon, not both (not implemented).
- **Enemy outlines:** Camera needs **`Highlight`** post-process (`EnemyOutlineCameraSetup` on Main Camera, or add `Highlight` manually). Per-player **`HighlightOutline`** on the prefab is the style source; ragdolls copy it on the host (`NetVictimTeamId` synced for clients).
- **Traffic cars:** Host-only movement + hits. **`TrafficCarTemplate` stays disabled** — clone while disabled, **`ConfigureLane`** → enable → **`NetworkSpawn`** → apply **`CarModelVariants`** (renderer + collider, same `.vmdl`) → **`Network.Refresh`**. **Do not** apply variants before `NetworkSpawn` (spawn resets to template mesh). Template **Body** must reference a **valid** fallback `.vmdl` (not a deleted asset). **`TrafficSpawner`** runs only when **`Game.IsPlaying`**. Per lane: 3 model variants (physics mesh + `solid`). Engine audio on template; never sound lifecycle on template. Clients: proxy pose + **`NetDriveBlend`**; colliders off on client.

More history → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).

---

## Multiplayer testing (do this after network changes)

1. Start Play (host).
2. Network menu → **Join via new instance** (second window = client).
3. Check both windows: grab, throw, tackle (**host→client and client→host**, similar launch distance), dodge, **enemy red outlines** (standing + ragdoll, both directions), **goals, reset, intermission, match over, rematch, HUD**, **traffic** (Road0 + Road1: 3 model variants per lane, knockdown, **ball bounce on host**, engine idle/drive).
4. **Throw polish:** trajectory arc + 1:1 translucent landing marker, charge camera, charge bar; tackle while charging (ragdoll cam OK).
5. **Ball carrier glow:** teammate = white ↔ green; enemy = white ↔ red; **you carry** — no glow; behind wall — no glow.
6. **Ball compass:** triangle orbits ring toward ball; green / red / white by possession; you carry → **BALL** hub + ring, no triangle.
7. **Held ball:** sits on carrier’s **right hand** (`hold_R`), not hip; both windows agree.
8. Spam actions once to probe desync.

**Ball jittery on client only?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

**Client tackle looks short or late?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Ragdoll (technical)”. Don’t re-add `StartAsleep` or mute collision sounds without waking bodies — broke launch (2026-05-18).

---

## Multiplayer gotcha (tackles)

- Physics and impulse are **host-only** on `PlayerRagdoll`.
- Remote attacker: `TryOwnerRequestTackleOnHost` → `RequestTackleApplyOnHost` (owner positions + `ownerTackleChargeBonus`).
- **Do not** require extra host-side charge/distance gates on the RPC — `NetAtChargeSpeed` / host positions lag and tackles feel late.
- Rare: impact sound spam at tackle start (client → host); left alone — not worth breaking launch.

---

## Multiplayer gotcha (traffic)

- **`TrafficSpawner`** only spawns when **`Game.IsPlaying`** — no traffic logic in edit mode (avoids `NetworkSpawn failed` console spam).
- **Variant mesh:** host applies **`CarModelVariants` after `NetworkSpawn`**, then **`Network.Refresh`**. Applying before spawn gets overwritten by template defaults.
- **Ball vs car:** host only (client traffic proxies have colliders disabled). Each variant `.vmdl` needs a **physics mesh**; template **Body** fallback must be a **real** asset.
- Player knockdown uses **code hit box** on `TrafficCar`, not `ModelCollider` — knockdown can work even when ball bounce is broken.

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
- **Street lamps:** steady = `streetlight.vmdl` + spot under `_LIGHTING` or parented to lamp; broken = `streetlight_broken.vmdl` (no spot). **Flicker** = one parent empty per lamp → **`StreetLightFlicker`** + child model + child **`Spot Light`**; **Bulb Material Index** `-1` (auto)
- **Petrol station lights:** one parent empty per fixture → **`StationLightFlicker`** + child **`Spot Light`** + child mesh block; set `VisualOnColor`/`VisualOffColor` (mesh stays enabled)
- **Petrol station signs (mapping blocks):** sign face uses emissive `.vmat` (e.g. `gassymoessign.vmat` on slot **1**; slot **0** = frame wood). Steady glow only — no runtime flicker (removed **`SignFlicker`** attempt).
- **Road0 / Road1 traffic:** see **Traffic cars** subsection below (spawner + car template wiring)

**Player prefab** (clone source for `GameNetworkManager` — all joins inherit these values):
- `PlayerTeam` (auto at spawn), `PlayerTackle`, `PlayerDodge`, `RagdollClientFeel`, `PlayerClass`, `CatchUpSpeedBoost`
- **`PlayerDisableCrouch`** (also auto-added at network spawn — add on prefab for scene NPCs)
- **`Move Mode Walk` → Step Up Height** — global curb step (default was **10**; try **24–32** for 16-unit geo). Tune here only — no code wrapper.
- **Body child** — `SkinnedModelRenderer`: **`citizen_human_*`** + human **Animation Graph** (not `citizen_holdball_test` unless on classic citizen)
- **`HighlightOutline`** — tune colors/width here (ragdoll copies this exact component); optional **`PlayerEnemyOutline`** (auto at spawn)
- `DodgeCooldownHud`, `MovementRampHud`, **`BallCompassHud`**, **`ThrowChargeBar`**, **`ThrowTrajectoryPreview`**, **`ThrowChargeCamera`**
- **`BallGrab`** — **`Hold Bone Name`** = `hold_R` (default); optional **`Body Renderer`** → Body `SkinnedModelRenderer`; tune **`Hold Bone Local Offset`** if grip looks off; **`HoldAnchor`** / `HandHoldPoint` = legacy fallback only
- `PlayerController` camera **X = 185**; **no** `ModelPhysics` on player
- **`BallThrow` → Throw Direction Source** optional; if empty, throw uses **`PlayerController.EyeAngles`** (look while charging)

**`main_ball`:**
- `ModelRenderer` — e.g. **`ball_v2.vmat`** (emissive gold + pattern scroll; team read from glow/compass not ball albedo)
- **`BallCarrierOutline`** — tune `OutlineWidth` (~1–1.5), `PulseWhiteColor`, `FriendlyAccentColor`, `EnemyAccentColor`

**Traffic cars (per lane — Road0 + Road1 wired):**

**`TrafficCarTemplate`** (disabled in scene — clone source only):
- Root: **`TrafficCar`** — **`Mesh Uniform Scale`** **0.6** (client MP fallback; must match Body)
- **Engine sound** (on template): **`Engine Idle Sound`** → `traffic_engine_idle`; **`Engine Drive Sound`** → `traffic_engine_drive` (`Assets/Sounds/Traffic/`). Tune **`Engine Sound Volume`** + **`Engine Sound Max Distance`** on template
- Child **`Body`**: scale **0.6**; **`Model Renderer`** + **`Model Collider`** → any **valid** fallback `.vmdl` (spawn overrides both from **`Car Model Variants`**); **physics mesh** + `solid` in Model Doc; **`Rigidbody`** on Body — gravity off, lock **X/Y/Z**
- **`Facing Yaw Offset Degrees`** on spawner if model nose points backward (**180**)

**`Traffic_Road0`** / **`Traffic_Road1`** (one empty per lane):
- **`TrafficSpawner`** — **`Car Template`** → `TrafficCarTemplate`; **`Waypoints`** in **drive order** (first = spawn, last = exit). 2–3 waypoints per 90° turn (before / in / on bend / after)
- **`Car Model Variants`** — 3 red `.vmdl`s on Road0, 3 blue on Road1 (each: **physics mesh** + `solid`); code picks random + sets **renderer + collider** after spawn
- Tune on spawner **Car** group: **`Car Speed`**, **`Car Acceleration`** / **`Car Deceleration`**, **`Corner Fillet Radius`**, **`Curve Slow Look Ahead`**, **`Curve Min Speed Fraction`**, **`Hit Half Extents`**, **`Hit Box Center Offset`** (Z up if pivot at wheels), **`Car Height Offset`** (usually 0 if Body local Y = 0)
- **`Disable Template On Start`** on; **`Only Spawn While Match Playing`** for ship
- Do **not** save scene with traffic clones in hierarchy — delete `(clone)` leftovers if any

**Car models:** `models/turfwarspoly/*` — Static Prop; **PhysicsMeshFile** (or hull-from-render) per variant; code hit box on spawner for players, **Model Collider** for ball.

---

## Open decisions (not chosen yet)

- **Player body for v1:** **`citizen_human_*`** (branch tested) vs classic **`citizen.vmdl`** — leaning **human** (audience + looks good); citizen fits chaotic meme tone. No custom rig (account cosmetics). **Human hold/throw anim graph** still TBD after body is locked.
- Holding forward + backward while charging — exploit or cool fake-out?
- Closed roof on arena vs open roof + sun for lighting
- Small screen shake on tackle hit — yes or no?
- Map vote: allow changing vote during the 30s window?
- **Traffic knockdown tuning:** **`KnockdownLaunchSpeed`** / hit box vs dodgeability
- **Ball compass polish:** optional distance readout on `BallCompassHud`
- **Hero asset art:** maps/props low poly; **players + ball** may get higher-detail models later — ball on **`ball_v2.vmat`** (emissive gold + scroll) for now; `BallCarrierOutline` still copies ball material for carry breathe
- **Human hold/throw anim graph** — bone attach shipped; posed carry layer still TBD (was `citizen_holdball_test` on classic citizen)

---

## Ball carrier UX — handoff (compass + glow + hold)

**`BallCompassHud` — shipped (solo OK):** Bottom-left **BALL** hub (white) + ring + small **triangle** on ring edge (360° orbit) toward **`main_ball`**. **White** loose · **green** teammate · **red** enemy. Always on; triangle hidden when you carry (hub + dim ring stay). **Player position + `EyeAngles` yaw** bearing. Everyone sees it.

**`BallCarrierOutline` — shipped:** White ↔ **green** (teammate) / white ↔ **red** (enemy) on held ball; hidden for carrier; no wallhack; default **`OutlineWidth`** ~1.5.

**`BallGrab` hold — shipped:** Ball follows **`hold_R`** on Body `SkinnedModelRenderer` (`TryGetBoneTransform`); falls back to `HoldAnchor`. Human avatar — no `citizen_holdball_test` IK.

**Abandoned:** Edge-of-screen compass arrow.

**Future:** B&W HUD chrome; Razor UI; optional compass distance readout; human **carry anim layer**.

**Code:** `Code/UI/BallCompassHud.cs`, `Code/Ball/BallCarrierOutline.cs`, `Code/Ball/BallGrab.cs`

---

## Known issues

- [ ] Throw strength still needs playtest tuning
- [ ] Walk/run animations while charging throw (can’t move)
- [ ] **MP join visual glitch (host)** — brief **black mesh face** flash when client joins; stops when client leaves; likely joining player **`PlayerCosmeticsSync`** / human body before `ClothingContainer.ApplyAsync` (~0.25s delay) — **unconfirmed**; not caused by compass HUD (lines only)
- [ ] Need longer multiplayer playtests (15–20 min, two windows)
- [ ] **Clutter** sometimes missing after **engine reload** — save scene after paint; check clutter **Volume** bounds; verify in **Play** (not only editor flycam)
- [ ] **Traffic engine loops** — seam click in-game vs clean Audacity preview; re-export with DC offset remove + zero-crossing trim if needed

---

## For AI chats

Paste at the start of a new chat:

```
Read SESSION_NOTES.md. Match flow slices 1–6 are done (MATCH_FLOW_PLAN.md). Do not edit .scene files unless I ask.
Prefer inspector / existing engine components (e.g. Move Mode Walk Step Up Height) — do not add code that only mirrors a property Max can set on the player template.
Ball carrier UX: BallCarrierOutline (team pulse glow) + BallCompassHud (compass) + BallGrab hold_R bone — see “Ball carrier UX” section.
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-06-09 (ball carrier UX wrap):** **`BallGrab`** → **`hold_R`** hand bone attach (human; replaces hip/`HandHoldPoint` IK). **`BallCompassHud`** overhaul shipped (ball tracking, ring-edge triangle, **BALL** hub, player bearing, team colors). **`BallCarrierOutline`** team pulse (white ↔ green/red); thinner outline. Ball art: **`ball_v2.vmat`** emissive gold + texture scroll (neutral albedo).
- **2026-06-09 (ball carrier glow):** **`BallCarrierOutline`** — white ↔ **green** (teammate) / white ↔ **red** (enemy) pulse; matches compass semantics.
- **2026-06-09 (ball compass polish):** Player **`EyeAngles`** bearing; ring-edge triangle marker; **BALL** hub centered in ring.
- **2026-06-09 (ball compass overhaul):** Renamed **`BallCarrierOffscreenHud`** → **`BallCompassHud`**. Tracks **`main_ball`**; always-on; white/green/red by possession; needle hidden when local player carries. Ball glow team pulse deferred.
- **2026-06-09 (ball carrier compass):** Edge arrow → bottom-left compass prototype (superseded by overhaul above).
- **2026-06-09 (ball carrier UX):** **`BallCarrierOutline`** — gold colour-pulse outline + emissive breathe; hidden for carrier; no through-wall glow; ring width distance-scaled (no width pulse). **Throw trajectory** — 1:1 held-ball landing marker + `ball_translucent.vmat`; translucent grain fix (no tint-alpha on opaque). **Throw polish 2-window MP OK.**
- **2026-06-08 (throw polish — solo OK, MP test next session):** **`ThrowTrajectoryPreview`** — scrolling white dashed arc (simulation-time dash keys so motion visible while arc grows); ball-colored translucent landing sphere (`ModelRenderer` + ball material/tint). **`ThrowChargeCamera`** — charge-scaled `CameraOffset` pullback + height + mild FOV; smooth release blend; baseline from prefab; skips ragdoll + `IsStandUpCameraBlending`. **`PlayerTackle`** — ragdoll orbit camera moved to **`OnPreRender`** (fixes charge-cam fight). **`BallThrow.GetThrowChargeLerp()`** public. **Next:** 2-window MP pass on all of the above.
- **2026-06-07 (sign flicker — removed):** **`SignFlicker`** + flicker-only assets (`gassymoessignflicker.vmat`, partial illum mask, diffuse overlay mat) **deleted** — mapping-block runtime emissive swap does not render; overlay approach also failed in Play. **`gassymoessign.vmat`** sign art kept for steady emissive on blocks. **Editor:** remove **`SignFlicker`** component from **`Block (4)`** (and any other blocks) if still attached.
- **2026-06-06:** **`TrafficSpawner.SpawnDelaySeconds`** — per-lane wait after Playing starts (e.g. Road1 = 5s fairness). Editor-only on **`Traffic_Road1`** spawner.
- **2026-06-06:** Editor **`CloudLocations` / Asset Browser** console spam — local tools patch (`OnDestroyed` + `EditorEvent.Unregister`); harmless to game; may revert on s&box update.
- **2026-06-05 (traffic wrap):** **Road0 + Road1 verified** — 3 **`CarModelVariants`** per lane (red/blue); renderer + collider synced **after `NetworkSpawn`** + **`Network.Refresh`**; **`Model.Load`** fallback; template needs valid fallback `.vmdl`. **Ball bounce** + engine idle/drive + spawn fixes (`Game.IsPlaying`, no template enable-on-clone). **Do not** apply variants before `NetworkSpawn`.
- **2026-06-05 (wrap):** **Road1 traffic + variants + engine audio** — **`CarModelVariants`** (red/blue per lane); **`TrafficCar`** idle/drive loops; **`NetDriveBlend`**. Spawn/sound lifecycle fixes on template clone.
- **2026-06-05:** **`TrafficSpawner.CarModelVariants`** — random Body `.vmdl` per spawn; per-lane lists in editor.
- **2026-06-03 (wrap):** **Road0 traffic MP shipped** — client visibility, Body scale, knockdown, ball bounce (2-window verified). Code: **`PlayerDodge.cs`** split from **`CatchUpSpeedBoost.cs`**; **`TrafficCar`** client proxy (no mesh hoist).
- **2026-06-03:** **Turf Wars road traffic (Road0)** — **`TrafficSpawner`** + **`TrafficCar`**: filleted paths, corner slow, accel/decel, **`PlayerTackle.ApplyKnockdownFromHost`**, **`Model Collider` + locked `Rigidbody` on `Body`**.
- **2026-06-02:** **`feature/human-avatar`** — Player Body → `citizen_human_*` + human anim graph (drop `citizen_holdball_test` on human or T-pose at run). Cosmetics + male/female from connection; tackle/ragdoll/MP unchanged in code.
- **2026-05-28:** Added **`StationLightFlicker`** (`Code/Map/`) for petrol-station fixtures: flickers child `SpotLight` and tints mesh (`VisualOnColor`/`VisualOffColor`) instead of hiding it.
- **2026-05-28:** Turf Wars `MatchDirector.BallSpawn` wiring confirmed in `throwdown_turf_wars.scene`; goal/intermission ball resets are now correctly configured.
- **2026-05-25:** **Street lamps** — Blender `streetlight` / `streetlight_broken`; emissive `goldenearth_streetlight.vmat`; **`StreetLightFlicker`** (`Code/Map/`) on per-lamp parent empties (spot + bulb off sync). Clutter reload quirk logged under Known issues.
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
