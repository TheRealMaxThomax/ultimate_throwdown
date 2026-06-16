# Session Notes — start here

**What this is:** A cheat sheet for you and for AI chats so we don’t forget how the game works.  
**What to read:** This file most of the time. Other files only when you need design detail, exact names, or old history.

| File | Open when… |
|------|------------|
| **This file** | Every session — current goal, checklist, don’t-break rules |
| [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md) | Full match flow design (slices 1–6 **complete**) |
| [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) | Tuning dodge/tackle, classes, **ultimates** (permanent charge + ult rules), future weapons |
| [`NAMING_CANON.md`](NAMING_CANON.md) | Exact script/property names — agents read this automatically when adding/renaming under `Code/` |
| [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) | **MP feel & netcode** — host authority, client predict, reconciliation, priority roadmap, **checklist for new combat features** |
| [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) | Something broke before and you want the long “why we did it” story |
| [`Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md) | Custom citizen human anims — Blender export, ModelDoc, ScaleAndMirror, troubleshooting (throw, wave, hit, stand-up, …) |

**Doc hygiene:** Keep **this file** under ~250 lines — trim **Recent session notes** to the last ~2 weeks; move older bullets to [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md). Shipped handoff blocks (e.g. ball carrier UX) can compress to one line + archive link when stale.

---

## Right now

**Goal:** **Slice 2d** (Speed Blitz **wind-up** polish — particles → SFX → Olympic pose). **2c ✅ (2026-06-16)** — dash feel, tuning, ult comic, MP remote anims signed off.

**Next session (priority order):**
1. **Slice 2d** — wind-up blue energy particles (editor asset first), then buildup + dash-start SFX, then Olympic pose + animgraph layer
2. **Practice scene** — moving/charging dummies before C1 lag-comp
3. **Optional later:** aim preview art v3 (custom blue `.vmat` telegraph — not blocking 2d)

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
- **W+S movement mutex** — **`CatchUpSpeedBoost.ApplyMutuallyExclusiveForwardBackwardInput`** — W and S cannot counteract; W wins when both held; walk/run/charge — **solo OK (2026-06-13)**
- **Tackle impact feel** — **`TackleImpactFeel`**: owner camera **hitstop**, **shake** (`ShakeForAttacker` / `ShakeForVictim`), attacker **FOV/offset punch**; traffic/car knockdowns use victim path too; **`PlayerTackle.PreLaunchPauseSeconds`** (~0.05): victim **body frozen visible** (`NetAwaitingRagdollLaunch`) → impulse + ragdoll; **`0`** = legacy — **initial 2-window OK (2026-06-12)**; tune vs moving victims when practice scene exists
- **Traffic knockdown** — no pre-launch pause; **`HazardKnockdownComicPower`** default **1.55** (Chaos/red); **`TriggerAsHazardVictim()`** + **`IsHazardImpact`** car camera path (defer ragdoll cam, orbit shake baseline, enter blend). **Player tackles** use simpler path — hitstop during freeze, ragdoll cam when `isRagdolled`
- **Tackle comic text** — **`TackleComicTextHud`** + **`TackleComicBurst`** + **`ComicLetterExitMotion`**: entrance polish + **14 exit styles** (5 CSS + 7 letter C#); timing via `LifetimeSeconds` / `ExitFadeStartFraction` / `ExitFadeDurationFraction` / `ExitTailSeconds` — **good enough for v1**; MP verify + Les Flos optional
- **Ult charge (slice 1)** — **`PlayerUltCharge`** + **`UltChargeHud`** on **player prefab** (manual — **not** auto-spawned). Passive regen **`Playing` only**; goal (scorer) + tackle (attacker, **enemy only**); FF tackle **no** charge; % **persists** across rounds; **rematch → 0%**. HUD: floored **%**, white → blue after **`ReadyHighlightDelaySeconds`** at 100%. **`Ultimate`** bound to **X** (ability slice 2).
- **Speed Blitz (slice 2a/2b/2c ✅)** — **`SpeedsterSpeedBlitzUlt`** + owner **`SpeedBlitzAimPreview`** (blue `#24b0ff` tint); hold X → corridor preview; release → commit; **2c shipped (2026-06-16):** hang, dash cam, connect/launch SFX, **`ComicBurstPalette.Ult`** on launch, ball-strip on carrier connect (intentional), MP remote wind-up/throw anim fix; dash range/speed/feel signed off at current prefab values
- **Owner cameras (2026-06-15)** — **`PostCameraSetup`** for all owner FOV (PC resets preference FOV every frame). **`ThrowChargeCamera`** `[Order(10002)]`: charge offset + release blend after ball leaves hand (transition-frame hold — no pop). **`SpeedBlitzDashCamera`** `[Order(10012)]`: idle must **not** stomp **`CameraOffset`** (throw owns offset). **`TackleImpactFeel`**: blitz attacker uses overrides — hitstop freezes **world pose only**; dash cam eases during freeze; no blitz attacker offset/FOV punch (recovery blend owns it). Player tackles unchanged.
- **MP combat feel predict** — **`CombatFeelPredictDedupe`** (auto on join): client-owner early **`TackleImpactFeel`** for blitz dash, tackle connect, victim freeze (tackle/blitz), traffic ragdoll; host **`NetCombatFeelApplyId`** dedupe. Details → [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md). **2–3 window idle-target soak OK (2026-06-14)**; moving-target fairness → practice scene + Tier C1 later.

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
| `Code/Player/` | Movement, dodge, tackle, **`CombatFeelPredictDedupe`**, team, class, cosmetics, **`PlayerBallHoldAnim`**, **`PlayerChargeRunAnim`**, **no crouch** |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, `MapMatchConfig` |
| `Code/Ultimates/` | **`PlayerUltCharge`** (slice 1); **`SpeedsterSpeedBlitzUlt`** (slice 2a/2b) |
| `Code/UI/` | Match HUD + owner HUDs + **`UltChargeHud`** + **`BallCompassHud`** + **`TackleComicTextHud`** / **`TackleComicBurst`** |
| `Code/Map/` | `StartupMapBootstrap` (practice NPC locks); **`StreetLightFlicker`** (decorative lamp flicker); **`StationLightFlicker`** (petrol station spot + mesh color flicker); **`TrafficSpawner`** / **`TrafficCar`** (host lane traffic + knockdown) |

**Scene you play in:** `scenes/throwdown_turf_wars.scene` (Turf Wars WIP). `throwdown_prototype.scene` = older greybox fallback.

**Important:** AI should **not** edit `.scene`, `.vmdl`, `.vanmgrph`, or other editor-owned assets unless you **explicitly give permission** — see `.cursor/rules/editor-asset-ownership.mdc`. Give steps; you wire in the s&box editor.

---

## Multiplayer gotcha (match flow)

`MatchDirector` is on **Main Camera** — each machine has its own copy. **Clients do not** use it for freeze/HUD/score.

**Authoritative on clients:** synced fields on **`PlayerTeam`** (on each network-spawned player). Host pushes via `MatchDirector.PushMatchHudStateToPlayers()`.

---

## Multiplayer feel & netcode

**Read [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md)** when changing host RPCs, `[Sync]`, owner-driven movement, combat hits, ragdolls, or **adding new combat features**. That doc covers: host authority vs client-side prediction (feel only), reconciliation, priority order (now → per-feature → tuning → late dev), and the **new feature checklist**.

**Tier 0–A3 + A2b ✅ (2026-06-14):** Client predict for blitz dasher, tackle attacker, victim freeze, traffic ragdoll; **`CombatFeelPredictDedupe`**. **Next netcode:** Tier B tuning / Tier C1 lag-comp if moving targets feel unfair — see [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md).

---

## How the game is put together (simple rules)

- **One script, one job** — e.g. `BallGrab` = “who holds the ball”, `BallThrow` = “throwing”, `ThrowTrajectoryPreview` = owner aim helper only.
- **Throw trajectory preview:** Owner-only scrolling white dashed arc + **1:1 held-ball clone** landing marker (`TranslucentBallMaterialPath` → `ball_translucent.vmat`), first arc to ground (no bounces). Dash scroll uses simulation-time keys so motion stays visible while charge lengthens the arc. `ThrowReleaseMath` shares release velocity with `BallThrow`; preview pivot = `BallGrab.GetPredictedThrowReleasePivotPosition()`. Use **Translucent** material + `g_flOpacityScale` (not tint-alpha on opaque — grains on clients).
- **Ball carrier glow:** `BallCarrierOutline` auto on `main_ball` — **white ↔ green** (teammate) / **white ↔ red** (enemy) colour pulse; ring width scales with camera distance; emissive breathe for non-carrier viewers; carrier sees nothing; no through walls. Tune on ball: `PulseWhiteColor`, `FriendlyAccentColor`, `EnemyAccentColor`, `EmissiveBrightnessMax`.
- **Ball compass:** `BallCompassHud` — bottom-left panel + ring; white **`LabelText`** hub centered in ring (default **BALL**); small **triangle** orbits ring edge (360°) toward **`main_ball`** (held or loose). **White** loose · **green** teammate · **red** enemy. **Needle hidden** when you carry (label + panel + dim ring stay). Bearing = **player position + `EyeAngles` yaw** (not camera). Auto-added on network spawn (`GameNetworkManager.GetOrCreate`). Tune: `MarginLeft` / `MarginBottom` / `CompassSize` / `NeedleTipRadius` / colours.
- **Throw charge camera:** **`ThrowChargeCamera`** — `[Order(10002)]` after **`BallThrow`**; offset in `OnUpdate`; FOV in **`PostCameraSetup`**. Holds through **`IsPendingThrowRelease`**, then **`ReleaseCameraBlendDuration`** ease (transition FOV hold — no frame pop). Idle: does **not** touch **`CameraOffset`**. Yields to ragdoll, **`TackleImpactFeel`**, active Speed Blitz.
- **Speed Blitz dash camera:** **`SpeedBlitzDashCamera`** — wind-up lerp → blended dash spike (`WindUpToDashBlendDurationSeconds`) → on hit **`BeginHitRecoveryBlend()`** (`DashEndBlendDurationSeconds` to baseline at contact). Auto-added by ult. Tune on Speedster prefab.
- **Speed Blitz connect SFX:** Host picks random **`ConnectImpactSoundA/B`** at dash stop → **`PlaySpeedBlitzConnectImpactSoundRpc`** (all clients). **`LaunchSound`** at ragdoll impulse after hang — same broadcast pattern. Drag-drop **`SoundEvent`** on Speedster prefab.
- **Speed Blitz body freeze:** **`BlitzConnectPoseFreeze`** — attacker + victim **`PlaybackRate = 0`** during blitz hang only (`IsConnectPoseFrozen` / `IsAwaitingSpeedBlitzRagdollLaunch`). Optional **`ConnectImpactChargeRunCycle`** snap on dasher.
- **Owner camera FOV rule:** Never set **`CameraComponent.FieldOfView`** only in `OnUpdate` — use **`PlayerController.IEvents.PostCameraSetup`**. **`CameraOffset`**: set in `OnUpdate` before PC setup; respect **`[Order]`** — idle ult cam must not stomp throw offset.
- **Walk into the ball = pick it up.** No kick button. While held, ball follows **`hold_R`** on **Body** `SkinnedModelRenderer` (`BallGrab.HoldBoneName`; falls back to `HoldAnchor`). Old `HandHoldPoint` + `citizen_holdball_test` IK was for classic citizen — human uses bone attach.
- **Ball carrier hold/throw anim (v1):** **`PlayerBallHoldAnim`** — **`holditem`** + **RH** while holding; on release **`b_attack`** (built-in medium throw). **`ThrowPoseHoldSeconds`** / **`ThrowPlaybackRate`**; **`BallThrow.ThrowReleaseDelaySeconds`** delays ball velocity. Charge = masked **`throw_windup`** layer on forked **`utd_citizen_human_m.vanmgrph`** (`throw_charge` / `throw_charge_weight`; body alive). Sequences on **`utd_citizen_human_throw.vmdl`**. Auto-added on network spawn.
- **Online: the host is the referee** — clients request; host decides.
- **Tackles:** Only at full charge speed (`NetAtChargeSpeed`). Host ragdoll + client **request** RPC. **`PreLaunchPauseSeconds` > 0:** **`NetAwaitingRagdollLaunch`** — victim **visible + frozen**, then impulse + ragdoll. **Client victim/attacker feel predict** (Tier A) — host still owns knockdown. **`CombatFeelPredictDedupe`** dedupes host feel RPCs.
- **Charge run overlay:** **`PlayerChargeRunAnim`** drives graph params when **`IsAtChargeSpeed`** (synced) — not owner-only ramp HUD.
- **Dodge:** Double-tap A or D. Tackle iframe only.
- **Ragdoll / knockdown:** **Walk** ramp resets **on knockdown** (`TriggerForceWalkRampOnHost` + local snap of `smoothedMoveSpeedCap` in `CatchUpSpeedBoost`); ramp timers frozen while down. **✅ Working.**
- **Charge tier + W+S:** **✅ Fixed** — `ApplyMutuallyExclusiveForwardBackwardInput` patches `AnalogMove.x` (not `.y`); `[Order(-100)]` + `OnFixedUpdate` so `PlayerController` sees mutex before movement.
- **Crouch:** Disabled — do not rebind `Duck` without re-enabling intentionally.
- **Test dummies:** Tag `practice_npc` on **dummies only**.
- **Weapons later:** Ball **or** weapon, not both (not implemented).
- **Ultimates:** Shared **`PlayerUltCharge`** (0–100%); class ult components in `Code/Ultimates/`. Host authority. **Do not** put ult logic in `MatchDirector`. Prefab components **manual** — not `GameNetworkManager` auto-add.
- **Enemy outlines:** Camera needs **`Highlight`** post-process (`EnemyOutlineCameraSetup` on Main Camera, or add `Highlight` manually). Per-player **`HighlightOutline`** on the prefab is the style source; ragdolls copy it on the host (`NetVictimTeamId` synced for clients).
- **Traffic cars:** Host-only movement + hits. **`TrafficCarTemplate` stays disabled** — clone while disabled, **`ConfigureLane`** → enable → **`NetworkSpawn`** → apply **`CarModelVariants`** (renderer + collider, same `.vmdl`) → **`Network.Refresh`**. **Do not** apply variants before `NetworkSpawn` (spawn resets to template mesh). Template **Body** must reference a **valid** fallback `.vmdl` (not a deleted asset). **`TrafficSpawner`** runs only when **`Game.IsPlaying`**. Per lane: 3 model variants (physics mesh + `solid`). Engine audio on template; never sound lifecycle on template. Clients: proxy pose + **`NetDriveBlend`**; colliders off on client.

More history → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).

---

## Multiplayer testing (do this after network changes)

See also [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) → **Testing** after predict/lag-comp work.

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
12. **Ult charge:** % creeps in **Playing** only; frozen in celebration/intermission; goal/tackle bumps; FF tackle no bump; persists across rounds; rematch clears; HUD floored % + blue flash at 100%.
13. **Combat feel predict:** client tackler / dasher / victim / car-hit — juice on contact frame, no double feel; idle targets OK (2026-06-14).

**Ball jittery on client only?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

**Client tackle looks short or late?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Ragdoll (technical)”. Don’t re-add `StartAsleep` or mute collision sounds without waking bodies — broke launch (2026-05-18).

---

## Multiplayer gotcha (tackles)

- Physics and impulse are **host-only** on `PlayerRagdoll`.
- Remote attacker: `TryOwnerRequestTackleOnHost` → `RequestTackleApplyOnHost` (owner positions + `ownerTackleChargeBonus`). **Attacker feel predict** on RPC send; **`CombatFeelPredictDedupe`** dedupes host RPC.
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
- `DodgeCooldownHud`, `MovementRampHud`, **`UltChargeHud`**, **`BallCompassHud`**, **`ThrowChargeBar`**, **`ThrowTrajectoryPreview`**
- **`ThrowChargeCamera`** — tune `ExtraFieldOfViewAtFullCharge`, pullback/height, **`ReleaseCameraBlendDuration`** (~0.35; ease after ball leaves hand). FOV extras **additive** on preference FOV.
- **`SpeedBlitzDashCamera`** — auto on Speedster (from ult). **Wind-up** / **Dash** FOV + pullback groups; **`WindUpToDashBlendDurationSeconds`** (~0.15); **`DashEndBlendDurationSeconds`** (~0.18) for miss end + **hit recovery** on connect.
- **`PlayerUltCharge`** — ult % meter (host sync); tune `PassivePointsPerSecond`, `GoalChargePoints`, `TackleChargePoints`. **Add on prefab** (not auto-spawned).
- **`SpeedsterSpeedBlitzUlt`** — **Speedster only** (class gate). **Add on Speedster prefab** (not auto-spawned). Tune `WindUpDurationSeconds` (default **2**), `DashRange`, `DashSpeed`, `HitHalfWidth`, `DefaultTargetBodyRadius`, `KnockdownLaunchSpeed`, `KnockdownLaunchArc`. **Knockdown feel:** `KnockdownPreLaunchPauseSeconds` (**0.65**), **`ConnectImpactChargeRunCycle`** (**-1** = freeze at contact; ≥ **0** = snap dasher impact stride), **`LaunchSound`** + **`ConnectImpactSoundA`** / **`ConnectImpactSoundB`** (drag-drop **`SoundEvent`** — random connect crunch), **`LaunchSoundVolume`** / **`ConnectImpactSoundVolume`**, `ImpactHitstopDurationSeconds`, shake/punch fields. Optional **`Enable Speed Blitz Debug Logs`**. Auto-adds **`SpeedBlitzDashCamera`** on start.
- **`SpeedBlitzAimPreview`** — **Speedster only**, same prefab as ult (manual). Owner corridor while holding X; tune `CorridorTint` / `CorridorAlpha`, `SegmentSpacing`, `MarkerModelBaseSize` (dev box native size ≈ **50**).
- **`UltChargeHud`** — floored **%** centered (left of `MovementRampHud`); **`ReadyHighlightDelaySeconds`** (~0.4s white at 100% then blue). **Add on prefab** with `PlayerUltCharge`.
- **`BallGrab`** — **`Hold Bone Name`** = `hold_R` (default); optional **`Body Renderer`** → Body `SkinnedModelRenderer`; tune **`Hold Bone Local Offset`** if grip looks off; **`HoldAnchor`** / `HandHoldPoint` = legacy fallback only
- **`PlayerBallHoldAnim`** — auto-added on network spawn. Tune `IdleHoldPoseHand` (~0.1), `ThrowAttackStrong`, `ThrowPoseHoldSeconds` (~0.9), `ThrowPlaybackRate` (~0.7). **Throw charge:** `UseAnimGraphChargePose` on — `throw_charge`/`throw_charge_weight` on **`utd_citizen_human_m.vanmgrph`**; tune **`ChargeWindupCycleEnd`** if wind-up finishes before bar is full (or spread keys in Blender ~3 s). Graph re-applied after cosmetics.
- **`PlayerTackle`** — **`PreLaunchPauseSeconds`** (default **0.05**; **0** = legacy launch); tune with **`TackleImpactFeel.HitstopDurationSeconds`**
- **`PlayerChargeRunAnim`** — auto-added on network spawn. **`UseAnimGraphChargeRunPose`** on; **`IsAtChargeSpeed`** (not local HUD tier). **`SpeedBlitzChargeRunBlendInSeconds`** (default **0.03** — charge_run builds faster during dash). Graph → [`CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md)
- **`TackleImpactFeel`** — auto-added on network spawn. Tune **Hitstop** / **Shake** / **Attacker punch**; **`ShakeForAttacker`** + **`ShakeForVictim`**
- **`CombatFeelPredictDedupe`** — auto-added on network spawn (with **`TackleImpactFeel`**). No inspector tuning.
- **`BlitzConnectPoseFreeze`** — auto-added on network spawn. No inspector tuning (optional **`ConnectImpactChargeRunCycle`** on **`SpeedsterSpeedBlitzUlt`**).
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
- Closed roof on arena vs open roof + sun for lighting
- **Tackle victim oof/grunt** — layered on built-in ragdoll collision audio (not shipped)
- **Practice / training scene** — moving + charging `practice_npc` for solo tackle/anim tests (MP idle-only when controlling one pawn)
- Map vote: allow changing vote during the 30s window?
- **Traffic knockdown tuning:** **`KnockdownLaunchSpeed`** / hit box vs dodgeability
- **Ball compass polish:** optional distance readout on `BallCompassHud`
- **Charge wind-up bone mask choice:** `Blend_UpperBody_HalfSpine_FullArms` (arm + some spine lean, smoother) vs `Only_RightArm` (strictly arm) on the graph's Bone Mask node — pick whichever looks better in playtest
- **Hero asset art:** maps/props low poly; **players + ball** may get higher-detail models later — ball on **`ball_v2.vmat`** (emissive gold + scroll) for now; **leaning white ball** later (fits blue ult VFX). `BallCarrierOutline` still copies ball material for carry breathe
- **Comic word scope:** tackles/knockdowns only for v1; **ults** (+ weapon KOs later) get own burst — not throws/dodges. **Ult palette ✅:** `ComicBurstPalette.Ult` — blue fill (`#24b0ff`) + pale cyan highlight; Speed Blitz spawns **after ragdoll launch** (not at connect hang). Future ults pass `ComicBurstPalette.Ult` to `NotifyHostKnockdown`.
- **Ult charge point values** — goal / tackle / passive rates TBD in playtest (`PlayerUltCharge` inspector defaults are placeholders).
- **Charge tier + backward (S) while W held:** **✅ Fixed** — mutex was writing forward/back on `AnalogMove.y` (strafe axis); s&box uses `.x` for forward/back. W wins when both held.
- **Speed Blitz ball strip on connect:** During blitz connect hang, **`BallGrab`** on the victim can lose the ball to the dasher if pickup overlap runs — **intentional** (reward for hitting carriers); not a bug to remove without design pass.
- **Speed Blitz aim preview v3 (2c+):** replace dev-box corridor/end with **custom translucent blue `.vmat`** + **`Model.Plane`** or **`DecalRenderer`** (SMITE-like ground telegraph); Max authors material in editor — not blocking current segmented-box preview.
- **Speed Blitz impact stride `charge_run_cycle`:** snap dasher to a fixed cycle at connect (shoulder-in frame) vs freeze whatever pose contact landed on — scrub `charge_run` in ModelDoc; inspector default TBD in playtest.
- **Speed Blitz victim flinch (later):** optional masked hit-react clip + graph layer during hang (same pattern as `throw_windup` / `charge_run`) — polish on top of body freeze v1; ship or skip after playtest
- **Player prefab component count (3 classes):** **✅ Chosen: Option A — per-class prefab variants** before slice 5/6 (`Player_Speedster` / `Player_Juggernaut` / `Player_Sniper`; `GameNetworkManager` picks template by class). Not doing yet — see roadmap note before slice 5. Move **`BlitzConnectPoseFreeze`** off global auto-add when splitting.
- **Post-tackle attacker ramp (attacker only, on connect — no whiff penalty):** leaning **walk** reset for all classes; **Juggernaut** exception **run** (sprint tier, not charge) for “unstoppable” feel — **bundle with loadout UI** (pick passive / ult / etc.), not a standalone tweak. **Future:** one passive slot per class → Juggernaut pick **tackle ramp bonus** *or* **post-tackle run recovery** (not both).

---

## Tackle comic text — shipped + roadmap

**Shipped (2026-06):** **`TackleComicTextHud`** (spawner/settings on Main Camera, auto via **`GameNetworkManager`**) + **`TackleComicBurst`** (`.razor` / `.razor.scss`). Host broadcasts word + tier + **`ComicShadowDirection`**; no scene wiring. Tackle power → tier: Sage (flat/yellow) / Sans (tilted/orange) / Chaos (max tilt/red). Black duplicate layer offset to a random corner — **not** a uniform stroke.

**MP rule:** Host picks random values (word, shadow corner, word tilt, `LetterJitterSeed`); broadcast to clients — do not re-roll per machine.

**s&box UI gotchas (comic bursts):** `display: flex` only; text in `div.letter` (not `<span>`); **no inline `animation:`** — names/timing in `.razor.scss`, inline **`animation-duration`** + **`animation-delay`** only; baseline via **`margin-top`** (not per-letter `transform` on shadow layer); `ApplySpawnData` after create; `Game.Random.Int` max must stay below `int.MaxValue`.

**Polish plan (in order — do not reorder):**
1. ~~**Random whole-word rotation**~~ — **shipped:** host-synced ±° per burst; tune `WordTiltMaxDegreesSage` / `Sans` / `Chaos` on Main Camera.
2. ~~**Per-letter size + baseline + spacing**~~ — **shipped:** `div.letter` per char; host-synced `LetterJitterSeed`; tier caps `LetterSizeJitter*` / `LetterBaselineJitter*` / `LetterSpacingJitter*`; `BurstPanelPadding` for clip headroom.
3. ~~**Staggered letter pop-in**~~ — **shipped:** `EnableLetterPopStagger` + `LetterPopStaggerMilliseconds` (per-index delay; off = whole-word pop).
4. ~~**Per-letter shake on impact**~~ — **shipped:** `EnableLetterImpactShake` + `LetterImpactShakeDurationSeconds`; tier `tackle-letter-pop-shake-*` / `tackle-letter-shake-*` keyframes (`margin-left` wobble); off = whole-word `.word-stack` shake only.
5. ~~**Highlight extrusion + exit animations**~~ — **shipped** (2026-06-13 good enough). Whole-word CSS + C# letter exits in `ComicLetterExitMotion`; **`LetterSnake` cut**. Remaining: Les Flos fonts + optional MP verify.

**Tune on Main Camera → `TackleComicTextHud`:** `ComicWords`, fonts, `RenderScale`, `LifetimeSeconds`, `ExitFadeStartFraction` (hold before exit), `ExitFadeDurationFraction`, `ExitTailSeconds`, `ExitStylePick`, `BurstPanelPadding` / `ExitAnimationPaddingPixels`.

---

## Ult implementation roadmap (temporary)

**Permanent design** (charge rules, Speed Blitz spec, voided ideas, ship order) lives in [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) → **Ultimates**, **Speed Blitz**, **Other class ults**. **Names:** [`NAMING_CANON.md`](NAMING_CANON.md) → `Code/Ultimates/`.

**When all slices below are shipped:** delete **this whole roadmap section** (checkboxes + MP table only). Before deleting, confirm `GAMEPLAY_DESIGN.md` is up to date. Keep ult bullets under **Works today** + editor checklist in this file.

---

### Implementation slices (do in this order)

#### Slice 1 — shared charge + HUD ✅ **SHIPPED**

- [x] `Code/Ultimates/PlayerUltCharge.cs` — passive, goal/tackle bumps, rematch reset, sync `NetChargePercent`
- [x] `Code/UI/UltChargeHud.cs`
- [x] Hooks: `GoalZone` scorer, `PlayerTackle.ExecuteTackle` attacker, `MatchDirector.ResetMatchState`
- [x] `Ultimate` → **X** in `Input.config` (binding only until slice 2)
- [x] Prefab manual wiring (no `GameNetworkManager` auto-add)

#### Slice 2a — Speed Blitz **core** (no ground preview yet) ✅ **SHIPPED (solo 2026-06-13)**

- [x] `Code/Ultimates/SpeedsterSpeedBlitzUlt.cs` on **Speedster** prefab (class gate by `PlayerClass` / `ClassName`)
- [x] **100%** + not holding ball + allowed phase → **tap X** commit (hold/release preview is 2b)
- [x] **Commit:** `TrySpendFullChargeOnHost()` → **0%** immediately; **no charge gain during ult** (`SetHostChargeGainBlocked` — passive + tackle + goal blocked until ult ends)
- [x] **3 s wind-up:** `UseInputControls` off + planted; look-lock; **vulnerable** (knockdown wastes spent ult); **no cancel**
- [x] **Dash:** **invulnerable** (`SetHostTackleImmune`); **owner-driven through `PlayerController`** (Rigidbody velocity) → wall-slide, step-up, stick-to-ground, charge_run anim. **Time-based** (duration = `DashRange`/`DashSpeed`)
- [x] **Hit:** first **enemy** in corridor; **`ApplyKnockdownFromHost`**; **dash stops** on contact (hitstop freeze later)
- [x] **End penalty:** hit or miss → **`TriggerForceWalkRampOnHost`** (forced walk — rebuild to charge)
- [x] **Walls:** slide along; no tunnel-through
- [x] No ball pickup / dodge during ult; **enemies-only** dash hit
- [x] Cancelled on round reset / rematch (`CancelAllInScene`)
- [x] **MP 2-window** — ✅ **OK (2026-06-14)** commit/dash/knockdown + predict feel

**Prefab:** **`SpeedsterSpeedBlitzUlt`** on Speedster player prefab (manual). Tune `DashRange` / `DashSpeed` / `HitHalfWidth` / `KnockdownLaunchSpeed` / `WindUpDurationSeconds`.

#### Slice 2b — Speed Blitz **hold/release + owner preview** ✅ **SHIPPED (2026-06-14)**

- [x] **Hold X** at 100% → owner-only preview: segmented corridor + end marker (dev boxes; blue tint)
- [x] **Release X** → commit (same as 2a wind-up → dash)
- [x] **Right-click** cancel while aiming (release X before re-aim)
- [x] Host hit test uses victim **body radius** so preview side lines = outer knockdown edge (`lateral + BodyRadius ≤ HitHalfWidth`)
- [x] Aim locked on **release** (yaw snapped to committed dir before wind-up)
- [x] Preview vs knockdown **playtest sign-off** (solo + optional MP) — max-range corridor hit fix same session

#### Slice 2c — Speed Blitz **polish** ✅ **SHIPPED (2026-06-16)**

- [x] Blitz-only victim pre-launch hang (**0.65s** default) + stronger connect impact feel (hitstop / shake / punch)
- [x] Owner dash camera (`SpeedBlitzDashCamera` — wind-up → blended dash → **hit recovery at contact**)
- [x] Dasher contact snap (`HitStopContactGap`) — no stopping inside victim
- [x] Owner camera transitions — **`PostCameraSetup`** + release/wind-up→dash/hit-recovery blends (**2026-06-15** sign-off)
- [x] Blitz connect **hit** SFX — **`ConnectImpactSoundA/B`** random crunch at dash stop (**2026-06-15** playtest OK)
- [x] Blitz connect body freeze — **`BlitzConnectPoseFreeze`** (`PlaybackRate = 0`); optional **`ConnectImpactChargeRunCycle`**
- [x] Blitz victim **launch SFX** — **`LaunchSound`** at ragdoll impulse (all clients)
- [x] Dash **`charge_run`** faster blend-in — **`SpeedBlitzChargeRunBlendInSeconds`** on **`PlayerChargeRunAnim`**
- [x] Tune range, speed, hit width, wind-up, launch force, wall-slide feel in playtest — **signed off (2026-06-16)** at current Speedster prefab values
- [x] ~~Optional: yaw-only camera lock or wider hit cone~~ — **signed off (2026-06-15):** keep **full camera lock** + current lane hit test (“in the corridor = hit”); no yaw-only or wider cone
- [x] Ult **blue** comic burst — `ComicBurstPalette.Ult`; Speed Blitz spawns on launch (not connect)

#### Slice 2d — Speed Blitz **wind-up** polish (energy fantasy + Olympic pose)

**Design locked (2026-06-15):** **Energy-anime** read — blue/cyan particles pulling inward during wind-up; **Olympic sprinter blocks** pose (full-body mask — planted anyway). **Telegraph:** enemies should **hear** wind-up; particles **visible from distance** (~1500–2500 units — tune in playtest; profile before shortening). **All clients** see + hear. **Only after release X** (commit) — no buildup during hold/preview. **Not** during hold aim corridor.

**Ship order:** 1) particles → 2) SFX → 3) pose/anim.

- [ ] **Wind-up VFX** — blue energy streaks/sparks **inward** toward dasher; intensity scales with **`GetWindUpLerp()`**; keyed off synced **`IsWindUp`**; **stop on interrupt** (tackle) and on dash start. Max: author **particle asset in editor** first (project has no particles yet — use **`@sbox docs`** for spawn API when wiring code). New helper component TBD (e.g. wind-up VFX on Speedster / spawned by ult).
- [ ] **Wind-up SFX** — rising pitch/volume **energy build** over **`WindUpDurationSeconds`** (~2s); separate **dash-start burst** at wind-up → dash transition. **`SoundEvent`** on **`SpeedsterSpeedBlitzUlt`** (same drag-drop pattern as connect/launch); **`[Rpc.Broadcast]`** or follow-player 3D so all clients hear; cut/fade on interrupt.
- [ ] **Wind-up pose** — Blender: **single-frame** Olympic blocks, all bones keyed → `blitz_windup.fbx` → AnimFile on **`utd_citizen_human_throw.vmdl`**. Animgraph: **third masked stack** on **`utd_citizen_human_m.vanmgrph`** (`blitz_windup` / `blitz_windup_weight`); **full-body bone mask**. Code: blend **weight 0→1** (~0.25–0.4s) at wind-up start — **no** in-clip transition required unless weight-only blend pops. **`PlayerChargeRunAnim`:** force **`charge_run_weight` off during `IsWindUp`** (commit at charge speed otherwise fights pose). See [`CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md).
- [ ] **2-window MP** — remote sees/hears wind-up; interrupt + dash-start sync OK

**Audio arc (full ult):** wind-up build → dash-start burst → (dash) → connect crunch (existing) → launch boom (existing).

#### Slice 3 — assist charge

- [ ] Ball possession / pass history on host
- [ ] On goal: credit assist passer per void rules (enemy touch, chain pass)
- [ ] Point value **TBD**

#### Slice 4 — per-class charge max (balance pass)

- [ ] `maxPoints` per class/ult (e.g. Juggernaut stomp 150, Speed Blitz 100)
- [ ] Display still 0–100%; same universal event point awards

**Before slice 5/6 (prefab split — not blocking 2d–4):** Split shared player into **per-class prefab variants** (Option A). Duplicate shared components (`PlayerTackle`, `BallGrab`, `PlayerUltCharge`, HUDs, …) on each; **class-only** ult + preview + wind-up VFX live only on that class prefab. Update **`GameNetworkManager`** to spawn the template matching **`PlayerClass`**. Remove Speedster-only **`BlitzConnectPoseFreeze`** from global auto-add — keep on Speedster prefab or `GetOrCreate` from blitz ult. Do this **before Juggernaut + Sniper** land, not required for Speed Blitz 2d.

#### Slice 5 — **Juggernaut** ult (ground stomp)

- [ ] New component `Code/Ultimates/` — AOE knockdown around self
- [ ] Reuse `ApplyKnockdownFromHost`; MOBA preview pattern as needed
- [ ] Same `PlayerUltCharge` gate + commit rules

#### Slice 6 — **Sniper** ult (ball path ragdoll zones)

- [ ] Requires **ball**; exception to “no ult while holding”
- [ ] Zones along throw path — ties into `BallThrow` / trajectory
- [ ] Most complex of the three first ults

#### Slice 7 — **Weapons** (after all three first ults)

- [ ] Per [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) → Weapons

---

### MP test checklist (ults — delete with this section when shipped)

| After slice | Verify |
|-------------|--------|
| **1** ✅ | % creeps Playing only; frozen celebration/intermission; goal/tackle bumps; FF tackle no bump; persists rounds; rematch 0%; HUD floor % + blue at 100% |
| **2a** ✅ | Commit, dash, knockdown, walk ramp; **2-window MP OK (2026-06-14)** |
| **2b** ✅ | Preview owner-only; release aim = dash direction; preview matches hit incl. max range (**2026-06-14**) |
| **2c** ✅ | Camera + hit recovery ✅; connect crunch + launch boom ✅; body freeze ✅; dash charge_run blend ✅; ult blue comic ✅; MP remote anims ✅; dash tuning signed off (**2026-06-16**) |
| **2d** | Wind-up particles + build SFX + dash-start burst + Olympic pose; all clients; audible/visible telegraph; interrupt clean; 2-window MP |

---

## Ball carrier UX — shipped (2026-06)

`BallCompassHud` + `BallCarrierOutline` + `hold_R` + throw charge wind-up. Details → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) if needed.

---

## Known issues

- [ ] **Tackle comic text** — Les Flos import + **2-window MP verify** (optional; exits good enough for v1)
- [ ] **Tackle juice — moving victims** — predict tested on **idle** targets only (2026-06-14); moving fairness → practice scene + Tier C1 lag-comp
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
Read SESSION_NOTES.md → Ult roadmap 2d (wind-up VFX/SFX/pose), MULTIPLAYER_NETCODE.md (any net/combat work).
Match flow slices 1–6 done. MP combat predict Tier 0–A3 + A2b shipped. Speed Blitz 2c ✅ (2026-06-16); next = 2d wind-up polish (particles first).
Owner FOV: PlayerController.IEvents.PostCameraSetup — not OnUpdate alone. ThrowChargeCamera [10002]; SpeedBlitzDashCamera idle must not stomp CameraOffset.
Blitz SFX today: ConnectImpactSoundA/B (host random) at dash stop; LaunchSound at ragdoll launch — Rpc.Broadcast from host. 2d adds wind-up build + dash-start burst.
Wind-up: all clients; only after release X; energy particles + Olympic full-body pose; @sbox docs for particles.
Do not edit .scene / .vmdl / .vanmgrph unless I explicitly say yes.
No GameNetworkManager auto-add for ult components — player prefab manual (consider per-class prefab variants before 3 ults on one root).
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-06-16 (Speed Blitz 2c signed off):** Dash range/speed/feel OK at prefab values; ult blue comic + aim preview tint; MP remote wind-up plant + throw hold clear; ball strip on carrier connect kept intentional.
- **2026-06-16 (MP remote anim):** Blitz wind-up plants velocity on **all** clients (was owner-only); charge_run snaps off during wind-up. Throw hold clears **`holdtype_handedness`** / **`b_attack`** on remotes; throw RPC shares owner **`throwPoseEndTime`**.
- **2026-06-16 (Speed Blitz 2c comic):** Ult knockdowns use **`ComicBurstPalette.Ult`** (blue fill); Speed Blitz word spawns **after ragdoll launch** (with launch SFX), not during connect hang.
- **2026-06-15 (Speed Blitz 2d planned):** Wind-up polish scoped — energy particles (first), buildup + dash-start SFX, Olympic full-body pose; all-client telegraph; release-only. Full camera lock + lane hit signed off in 2c.
- **2026-06-14 (MP combat feel + Blitz 2b):** Tier 0–A3 + A2b predict; Blitz preview vs knockdown sign-off.

Older detail → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
