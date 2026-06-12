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
| [`Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md) | Custom citizen human anims — Blender export, ModelDoc, ScaleAndMirror, troubleshooting (throw, wave, hit, stand-up, …) |

**Doc hygiene:** Keep **this file** under ~250 lines — trim **Recent session notes** to the last ~2 weeks; move older bullets to [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md). Shipped handoff blocks (e.g. ball carrier UX) can compress to one line + archive link when stale.

---

## Right now

**Goal:** **v1 match flow is done** (slices 1–6). Gameplay polish, longer MP playtests, **map vote** when ready (see [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) → Later).

**Next session (priority order):**
1. **Throw charge MP + polish** — wind-up **solo OK**; 2-window verify `NetThrowChargeLerp` + release; Blender `throw_windup` / bone mask if wanted (see **Open decisions**).
2. **Tackle comic text polish** — (1) word tilt shipped; (2) per-letter jitter shipped; (3) stagger pop shipped (`EnableLetterPopStagger`); next: (4) impact shake → (5) highlight extrusion + exit anims (see roadmap). **`TackleImpactFeel`** tune + victim **oof/grunt** SFX later.
3. **MP join flash** — host brief black mesh face on client join (cosmetics load?).
4. **Practice / training scene** — moving + charging `practice_npc` dummies for solo tackle/anim regression (MP tests idle-only when you control one pawn).
5. Longer soak (15–20 min, two windows); map vote when ready.
6. **Future human anims** (wave, hit, stand-up) → [`CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md).

**Works today:**
- Ball grab/throw — held ball on **`hold_R`** (`BallGrab` + `BallClientFeel`); **throw trajectory preview** + **`ThrowChargeCamera`** / **`ThrowChargeBar`**; **`BallThrow.ThrowReleaseDelaySeconds`** — anim fires on release, ball stays on hand until delay elapses (tune to release frame); **`PlayerBallHoldAnim`** — built-in `holditem` RH hold + medium throw on release (`b_attack`) + **custom charge wind-up via forked animgraph masked layer** (`throw_charge`/`throw_charge_weight` on `utd_citizen_human_m.vanmgrph` scrub `throw_windup`; body keeps locomotion/look-at — **solo verified 2026-06-11**); **ball carrier glow** (`BallCarrierOutline`); **`BallCompassHud`**; **`main_ball`** art WIP (`ball_v2.vmat`); tackles/ragdolls; dodge; **crouch disabled**
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
- **Movement charge overlay** — **`PlayerChargeRunAnim`** + masked `charge_run` (`charge_run_weight` / `charge_run_cycle`); gates on synced **`CatchUpSpeedBoost.IsAtChargeSpeed`** — remotes see overlay — **2-window MP OK (2026-06-12)**
- **Tackle impact feel** — **`TackleImpactFeel`**: owner camera **hitstop**, **shake** (`ShakeForAttacker` / `ShakeForVictim`), attacker **FOV/offset punch**; traffic/car knockdowns use victim path too; **`PlayerTackle.PreLaunchPauseSeconds`** (~0.05): victim **body frozen visible** (`NetAwaitingRagdollLaunch`) → impulse + ragdoll; **`0`** = legacy — **initial 2-window OK (2026-06-12)**; tune vs moving victims when practice scene exists
- **Traffic knockdown** — no pre-launch pause; **`HazardKnockdownComicPower`** default **1.55** (Chaos/red); **`TriggerAsHazardVictim()`** + **`IsHazardImpact`** car camera path (defer ragdoll cam, orbit shake baseline, enter blend). **Player tackles** use simpler path — hitstop during freeze, ragdoll cam when `isRagdolled`
- **Tackle comic text** — **`TackleComicTextHud`** + **`TackleComicBurst`** (`WorldPanel` + Razor): world-occluded; fixed **`RenderScale`** (no extra distance scale); diagonal **offset shadow** (random corner, host-synced); Sage/Sans/Chaos = yellow/orange/red + Les Flos tiers; **`ComicWords`** list on Main Camera — **2-window verify pending**

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
| `Code/Player/` | Movement, dodge, tackle, team, class, cosmetics, **`PlayerBallHoldAnim`** (hold/throw), **`PlayerChargeRunAnim`** (charge-speed overlay), **no crouch** |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, `MapMatchConfig` |
| `Code/UI/` | Match HUD + placeholder owner HUDs (dodge/ramp) + **`BallCompassHud`** |
| `Code/Map/` | `StartupMapBootstrap` (practice NPC locks); **`StreetLightFlicker`** (decorative lamp flicker); **`StationLightFlicker`** (petrol station spot + mesh color flicker); **`TrafficSpawner`** / **`TrafficCar`** (host lane traffic + knockdown) |

**Scene you play in:** `scenes/throwdown_turf_wars.scene` (Turf Wars WIP). `throwdown_prototype.scene` = older greybox fallback.

**Important:** AI should **not** edit `.scene`, `.vmdl`, `.vanmgrph`, or other editor-owned assets unless you **explicitly give permission** — see `.cursor/rules/editor-asset-ownership.mdc`. Give steps; you wire in the s&box editor.

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
- **Ball carrier hold/throw anim (v1):** **`PlayerBallHoldAnim`** — **`holditem`** + **RH** while holding; on release **`b_attack`** (built-in medium throw). **`ThrowPoseHoldSeconds`** / **`ThrowPlaybackRate`**; **`BallThrow.ThrowReleaseDelaySeconds`** delays ball velocity. Charge = masked **`throw_windup`** layer on forked **`utd_citizen_human_m.vanmgrph`** (`throw_charge` / `throw_charge_weight`; body alive). Sequences on **`utd_citizen_human_throw.vmdl`**. Auto-added on network spawn.
- **Online: the host is the referee** — clients request; host decides.
- **Tackles:** Only at full charge speed (`NetAtChargeSpeed`). Host ragdoll + client **request** RPC. **`PreLaunchPauseSeconds` > 0:** **`NetAwaitingRagdollLaunch`** — victim **visible + frozen**, hidden host ragdoll, then impulse + **`NetworkSpawn`** + **`NetIsRagdolled`**; **`0`** = impulse-then-spawn. **Traffic/hazards** skip pause (attacker-less knockdown). **`TackleImpactFeel`** = owner-only camera juice; ragdoll orbit waits while `IsImpactFeelActive`. Juggernaut bonus in RPC. Built-in ragdoll collision audio; victim grunt SFX later.
- **Charge run overlay:** **`PlayerChargeRunAnim`** drives graph params when **`IsAtChargeSpeed`** (synced) — not owner-only ramp HUD.
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
8. **Hold/throw anim:** **holditem** while carrying; throw motion on release; **ball leaves hand** after **`ThrowReleaseDelaySeconds`** (not on button-up); remote sees anim (`PlayerBallHoldAnim` RPC).
9. **Charge run overlay:** no ball, max ramp — `charge_run` on **both** windows (remote uses **`NetAtChargeSpeed`**).
10. **Tackle juice:** hitstop/shake/punch on connect; victim **visible freeze** then launch when **`PreLaunchPauseSeconds` > 0**; host→client and client→host.
11. Spam actions once to probe desync.

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
- **Body child** — `SkinnedModelRenderer`: **Model** = **`utd_citizen_human_throw`** when using custom sequences (else `citizen_human_*`); **Animation Graph** = **`citizen_human_m.vanmgrph`**
- **`HighlightOutline`** — tune colors/width here (ragdoll copies this exact component); optional **`PlayerEnemyOutline`** (auto at spawn)
- `DodgeCooldownHud`, `MovementRampHud`, **`BallCompassHud`**, **`ThrowChargeBar`**, **`ThrowTrajectoryPreview`**, **`ThrowChargeCamera`**
- **`BallGrab`** — **`Hold Bone Name`** = `hold_R` (default); optional **`Body Renderer`** → Body `SkinnedModelRenderer`; tune **`Hold Bone Local Offset`** if grip looks off; **`HoldAnchor`** / `HandHoldPoint` = legacy fallback only
- **`PlayerBallHoldAnim`** — auto-added on network spawn. Tune `IdleHoldPoseHand` (~0.1), `ThrowAttackStrong`, `ThrowPoseHoldSeconds` (~0.9), `ThrowPlaybackRate` (~0.7). **Throw charge:** `UseAnimGraphChargePose` on — `throw_charge`/`throw_charge_weight` on **`utd_citizen_human_m.vanmgrph`**; tune **`ChargeWindupCycleEnd`** if wind-up finishes before bar is full (or spread keys in Blender ~3 s). Graph re-applied after cosmetics.
- **`PlayerTackle`** — **`PreLaunchPauseSeconds`** (default **0.05**; **0** = legacy launch); tune with **`TackleImpactFeel.HitstopDurationSeconds`**
- **`PlayerChargeRunAnim`** — auto-added on network spawn. **`UseAnimGraphChargeRunPose`** on; **`IsAtChargeSpeed`** (not local HUD tier). Graph → [`CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md)
- **`TackleImpactFeel`** — auto-added on network spawn. Tune **Hitstop** / **Shake** / **Attacker punch**; **`ShakeForAttacker`** + **`ShakeForVictim`**
- `PlayerController` camera **X = 185**; **no** `ModelPhysics` on player
- **`BallThrow`** — tune **`ThrowReleaseDelaySeconds`** (~0.35) to match anim release frame; **`Throw Direction Source`** optional (else **`PlayerController.EyeAngles`**)

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

- **Player body for v1:** **`citizen_human_*`** (branch tested) vs classic **`citizen.vmdl`** — leaning **human** (audience + looks good); citizen fits chaotic meme tone. No custom rig (account cosmetics).
- Holding forward + backward while charging — exploit or cool fake-out?
- Closed roof on arena vs open roof + sun for lighting
- **Tackle victim oof/grunt** — layered on built-in ragdoll collision audio (not shipped)
- **Practice / training scene** — moving + charging `practice_npc` for solo tackle/anim tests (MP idle-only when controlling one pawn)
- Map vote: allow changing vote during the 30s window?
- **Traffic knockdown tuning:** **`KnockdownLaunchSpeed`** / hit box vs dodgeability
- **Ball compass polish:** optional distance readout on `BallCompassHud`
- **Charge wind-up bone mask choice:** `Blend_UpperBody_HalfSpine_FullArms` (arm + some spine lean, smoother) vs `Only_RightArm` (strictly arm) on the graph's Bone Mask node — pick whichever looks better in playtest
- **Hero asset art:** maps/props low poly; **players + ball** may get higher-detail models later — ball on **`ball_v2.vmat`** (emissive gold + scroll) for now; `BallCarrierOutline` still copies ball material for carry breathe

---

## Tackle comic text — shipped + roadmap

**Shipped (2026-06):** **`TackleComicTextHud`** (spawner/settings on Main Camera, auto via **`GameNetworkManager`**) + **`TackleComicBurst`** (`.razor` / `.razor.scss`). Host broadcasts word + tier + **`ComicShadowDirection`**; no scene wiring. Tackle power → tier: Sage (flat/yellow) / Sans (tilted/orange) / Chaos (max tilt/red). Black duplicate layer offset to a random corner — **not** a uniform stroke.

**MP rule:** Host picks random values (word, shadow corner, future tilt/seeds); broadcast to clients — do not re-roll per machine.

**Polish plan (in order — do not reorder):**
1. ~~**Random whole-word rotation**~~ — **shipped:** host-synced ±° per burst; tune `WordTiltMaxDegreesSage` / `Sans` / `Chaos` on Main Camera.
2. ~~**Per-letter size + baseline + spacing**~~ — **shipped:** `<span>` per char; host-synced `LetterJitterSeed`; tier caps `LetterSizeJitter*` / `LetterBaselineJitter*` / `LetterSpacingJitter*`.
3. ~~**Staggered letter pop-in**~~ — **shipped:** `EnableLetterPopStagger` + `LetterPopStaggerMilliseconds` (per-index delay; off = whole-word pop).
4. **Per-letter shake on impact** — keyed wobble while the word lives.
5. **Highlight extrusion + exit animations** — **not either/or:**
   - **Highlight extrusion** = third duplicate layer (white/pale yellow) offset **opposite** the black shadow — thick ink look while the word is visible; can ship on all tiers.
   - **Exit motion** = how the word leaves (host-synced pick per tier or random): **spin-vanish** (first target), scatter, slam/deflate, launch drift, rubber snap, tackle-directed drift, ink puff (art-heavy).
   - A burst can use **both** — extrusion layer for the whole lifetime, then an exit style on fade-out.

**Tune on Main Camera → `TackleComicTextHud`:** `ComicWords`, font family names, `RenderScale`, `ShadowOffsetPixels`, impact thresholds.

---

## Ball carrier UX — shipped (2026-06)

**`BallCompassHud`** + **`BallCarrierOutline`** + **`hold_R`** hold + **`PlayerBallHoldAnim`** / **`BallThrow`** (holditem RH, `throw_windup` masked layer, `ThrowReleaseDelaySeconds`). 2-window MP OK for compass/glow/trajectory; throw charge MP still on checklist. Abandoned: edge arrow, full-arm IK charge. Future: HUD chrome, compass distance, more clips on `utd_citizen_human_throw.vmdl`.

---

## Known issues

- [ ] **Tackle comic text** — Les Flos import + font names on **`TackleComicTextHud`**; 2-window MP verify; then roadmap steps 1–5 above
- [ ] **Tackle juice — moving victims** — pause reads best vs runners; solo MP idle-only so far — revisit after practice scene or live 2P; tune **`PreLaunchPauseSeconds`** vs **`HitstopDurationSeconds`** (set pause **0** if hang feels like delay)
- [ ] **Throw charge wind-up — MP verify + polish** — ✅ **WORKS solo (2026-06-11)**: masked layer in forked graph `utd_citizen_human_m.vanmgrph`; body keeps locomotion/look-at while arm winds up. Remaining: 2-window MP check (remotes scrub via `NetThrowChargeLerp`); improve the wind-up clip in Blender if wanted (overwrite `throw_windup.fbx` — see workflow doc "Iterating on a clip"); pick final bone mask (see Open decisions).
- [ ] Throw strength still needs playtest tuning
- [ ] Walk/run animations while charging throw (legs still locomote in place — `PlayerBallHoldAnim` does not fix; charge blocks move input)
- [ ] **Hold/throw anim MP verify** — solo timing OK (`ThrowReleaseDelaySeconds` + built-in throw); confirm both windows see **custom charge wind-up** (`NetThrowChargeLerp`) + release + ball detach sync
- [ ] **MP join visual glitch (host)** — brief **black mesh face** flash when client joins; stops when client leaves; likely joining player **`PlayerCosmeticsSync`** / human body before `ClothingContainer.ApplyAsync` (~0.25s delay) — **unconfirmed**; not caused by compass HUD (lines only)
- [ ] Need longer multiplayer playtests (15–20 min, two windows)
- [ ] **Clutter** sometimes missing after **engine reload** — save scene after paint; check clutter **Volume** bounds; verify in **Play** (not only editor flycam)
- [ ] **Traffic engine loops** — seam click in-game vs clean Audacity preview; re-export with DC offset remove + zero-crossing trim if needed
- [ ] **Tackle ragdoll after ModelDoc changes** — if victims freeze ~8s with no flop (`ApplyRagdollLocally` in console but no physics ragdoll), try **editor reboot** or `utd_citizen_human_throw.vmdl` Save + Full Compile before chasing code. Stale compiled extension cache suspected (2026-06-11 — reboot fixed). Code fallback (spawn ragdoll on base `citizen_human_*`) deferred unless it returns.

---

## For AI chats

Paste at the start of a new chat:

```
Read SESSION_NOTES.md. Match flow slices 1–6 done. Do not edit .scene / .vmdl / .vanmgrph / ModelDoc unless I explicitly say yes.
Ball carrier: BallGrab hold_R + BallCompassHud + BallCarrierOutline + PlayerBallHoldAnim + BallThrow.ThrowReleaseDelaySeconds.
Throw charge: `PlayerBallHoldAnim` → `throw_charge`/`throw_charge_weight` + `throw_windup` (MP verify pending). Charge run: `PlayerChargeRunAnim` → `charge_run_*` via `IsAtChargeSpeed` — 2-window OK.
Tackle juice: `TackleImpactFeel` + `PreLaunchPauseSeconds` + `NetAwaitingRagdollLaunch`. Comic text: `TackleComicTextHud` + `TackleComicBurst` (WorldPanel/Razor, offset shadow, fixed RenderScale) — see **Tackle comic text — shipped + roadmap**.
`utd_citizen_human_throw.vmdl` — `throw_windup` + `charge_run` only.
Next: comic text polish steps 1–5; throw charge MP; practice scene; MP join flash; soak.
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-06-12 (comic text WorldPanel):** **`TackleComicBurst`** Razor world panels — offset shadow (random corner), tier colors, fixed scale; camera restore fix in **`TackleImpactFeel`** / **`ThrowChargeCamera`**. Roadmap steps 1–5 in **Tackle comic text** section.
- **2026-06-12 (tackle juice + charge_run MP):** **`TackleImpactFeel`** + **`PreLaunchPauseSeconds`** / **`NetAwaitingRagdollLaunch`**; **`PlayerChargeRunAnim`** remote fix — 2-window initial OK.
- **2026-06-11 (human anim graph):** Extension `.vmdl` = **`throw_windup`** + **`charge_run`** only; **`charge_run`** masked layer in **`utd_citizen_human_m.vanmgrph`**; throw wind-up masked layer solo OK; editor-asset ownership rule. Details → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) (2026-06 chronicle).

Older dated bullets → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
