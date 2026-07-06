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
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | **Read before slice 5/6** — folder layout, god components, spawn wiring, prefab split checklist, what to refactor first |
| [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) | Something broke before and you want the long “why we did it” story |
| [`Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md) | Custom citizen human anims — Blender export, ModelDoc, ScaleAndMirror, troubleshooting (throw, wave, hit, stand-up, …) |

**Doc hygiene:** Keep **this file** under ~250 lines — trim **Recent session notes** to the last ~2 weeks; move older bullets to [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md). Shipped handoff blocks (e.g. ball carrier UX) can compress to one line + archive link when stale.

---

## Right now

**Goal:** **Map slice 1 — ball OOB ✅ SHIPPED (2026-07-04)** — solo + **2-window MP OK**. **Next:** **prefab split + basic loadout** (spec ✅ **2026-07-06**, refined same day — [§ Prefab split + loadout](#prefab-split--loadout--decided-2026-07-06)) → ult slice 5. **Code steps 1–3 not started** — Max does editor prefab split **after** those land.

**Next session (priority order):**
1. **Prefab split + loadout v1** — spec below + [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6
2. **Ult slice 5** — Juggernaut stomp

**Pre–prefab split (ball throw ✅ 2026-07-04):** planted charge (`IsThrowPlantLocked` — no move/jump/air steer); **RMB** (`CancelChargeAction` / `attack2`) cancels charge; **`NotifyThrowChargeCancelled`** smooth blend back to idle hold (`ChargeCancelBlendOutSeconds`); **`BallGrab`** host-authoritative auto-grab (OOB sky-drop MP).

**Works today:**
- Ball grab/throw — held ball on **`hold_R`** (`BallGrab` + `BallClientFeel`); **throw trajectory preview** + **`ThrowChargeCamera`** / **`ThrowChargeBar`**; **`BallThrow`** — planted charge (**`IsThrowPlantLocked`**: no WASD/jump/air steer; release-delay plant too), **RMB cancel** (`CancelChargeAction`), **`CancelActiveThrowCharge()`**; **`PlayerBallHoldAnim`** — `holditem` RH + masked **`throw_charge`** wind-up + **`NotifyThrowChargeCancelled`** ease to idle hold; **`ThrowReleaseDelaySeconds`** — anim on release, ball detaches after delay; **`BallGrab`** host-authoritative pickup; **ball carrier glow** (`BallCarrierOutline`); **`BallCompassHud`**; tackles/ragdolls; **crouch disabled**
- **Dodge (channel ✅ 2026-07-04)** — **`PlayerDodge`** capped lateral slide; class **`DodgeDistance`** = literal travel; **`DodgeChannelDurationSeconds`** on prefab (lower = snappier); **air dodge OK**; **hard horizontal stop** at channel end (no dodge+jump cannonball). Tier penalty + iframe + cooldown unchanged. **Solo playtest OK**
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
- **Tackle impact feel** — **`TackleImpactFeel`**: owner camera **hitstop**, **shake** (`ShakeForAttacker` / `ShakeForVictim`), attacker **FOV/offset punch**; **player tackle connect crunch** — host random **`TackleConnectImpactSoundA/B`** (`speed_blitz_connect_crunch_a/b.sound` defaults) + owner predict dedupe; traffic/car knockdowns use victim path too; **`PlayerTackle.PreLaunchPauseSeconds`** (~0.05): victim **body frozen visible** (`NetAwaitingRagdollLaunch`) → impulse + ragdoll; **`0`** = legacy — **initial 2-window OK (2026-06-12)**; tune vs moving victims when practice scene exists
- **Traffic knockdown** — no pre-launch pause; **`HazardKnockdownComicPower`** default **1.55** (Chaos/red); **`TriggerAsHazardVictim()`** + **`IsHazardImpact`** car camera path (defer ragdoll cam, orbit shake baseline, enter blend). **Player tackles** use simpler path — hitstop during freeze, ragdoll cam when `isRagdolled`
- **Tackle comic text** — **`TackleComicTextHud`** + **`TackleComicBurst`** + **`ComicLetterExitMotion`**: entrance polish + **14 exit styles** (5 CSS + 7 letter C#); timing via `LifetimeSeconds` / `ExitFadeStartFraction` / `ExitFadeDurationFraction` / `ExitTailSeconds` — **good enough for v1**; MP verify + Les Flos optional
- **Ult charge (slice 1 + 3 + 4 ✅ playtest OK 2026-07-02)** — **`PlayerUltCharge`** + **`UltChargeHud`** on **player prefab** (manual — **not** auto-spawned). Passive regen **`Playing` only**; goal (scorer) **40** + assist (throw passer) **25** + tackle (attacker, **enemy only**) **10**; **`IPlayerUlt.MaxChargePoints`** per ult (Speed Blitz default **100** on **`SpeedsterSpeedBlitzUlt`**); FF tackle **no** charge; **`BallPassAssistState`** on **`main_ball`** (host); ult swap = raw points carry over (**no penalty**); % **persists** across rounds; **rematch → 0%**. HUD: floored **%**, white → blue at 100%. **`Ultimate`** → **X**.
- **Ball OOB (map slice 1 ✅ 2026-07-04)** — dwell → whistle → **`OUT OF BOUNDS!`** banner → **10s** drop marker (disc + **`oob_drop_ring.vmat`** + black outline) + world stack (**DROP ZONE** pulse / countdown / ▼ bob, **Les Flos Sage** + black shadow only). Sky-drop at **player feet** ground (feet-level trace, skip OOB roofs, ceiling-capped height). **`BallCompassHud`** → drop anchor during countdown. Throw preview ignores **`playerclip`**. **`BallGrab`** host-authoritative auto-grab (reliable client sky-drop pickup). **`BallOutOfBoundsHost`** + **`BallLastTouchLedger`** on **`main_ball`**. **Off in `PracticeArenaMode`**. **2-window MP OK.**
- **Speed Blitz (slice 2a–2d ✅)** — **`SpeedsterSpeedBlitzUlt`** + owner **`SpeedBlitzAimPreview`** (segmented **`plane.vmdl`** strips, `speed_blitz_preview.vmat`, ult blue `#24b0ff`; tune **`PlaneWidthBaseSize`** for hit-width read, **`PlaneLengthBaseSize`** **100** for segment length); hold X → preview → release commit; dash hits = **contact + LOS** + **`TryValidateContactCylinder`** (jump-over = tackle); wind-up 2d shipped; spark sprites deferred (editor/publish).
- **Speed Blitz wind-up feel (slice 2d — solo ✅ 2026-06-18)** — **`SpeedBlitzWindUpFeel`**: **`speedblitzwindupvfx`** **wind-up only** (off dash / connect hang); **`speedblitzdischargevfx`** on **dasher chest** at ragdoll launch (hit only). **`SpeedBlitzBodyGlow`** + render system: tint + point light (`GetWindUpLerp()` ramp → peak → discharge); **point light destroyed on end** (remote host-dasher fix **2026-06-22**). **`PlayerSpeedBlitzWindUpAnim`**: masked **`speedblitz_windup`** via `blitz_windup` / `blitz_windup_weight` while **`IsWindUp`** (synced). **SFX:** electric hard stop at connect, windup rise, dash woosh — **`PlayFeelSoundAt`**; **client dasher connect crunch on predict** (host broadcast dedupes — **2026-06-22**). **Connect hang timing:** pre-launch pause runs **parallel** with ragdoll body init — launch aligns with pose unfreeze (**2026-06-22**). **MP:** owner/host wind-up looks OK; **joining client spark sprites = blue squares** (engine limitation — publish only)
- **Owner cameras (2026-06-15)** — **`PostCameraSetup`** for all owner FOV (PC resets preference FOV every frame). **`ThrowChargeCamera`** `[Order(10002)]`: charge offset + release blend after ball leaves hand (transition-frame hold — no pop). **`SpeedBlitzDashCamera`** `[Order(10012)]`: idle must **not** stomp **`CameraOffset`** (throw owns offset). **`TackleImpactFeel`**: blitz attacker uses overrides — hitstop freezes **world pose only**; dash cam eases during freeze; no blitz attacker offset/FOV punch (recovery blend owns it). Player tackles unchanged.
- **MP combat feel predict** — **`CombatFeelPredictDedupe`** (auto on join): client-owner early **`TackleImpactFeel`** for blitz dash, tackle connect, victim freeze (tackle/blitz), traffic ragdoll; host **`NetCombatFeelApplyId`** dedupe. Details → [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md). **2–3 window idle-target soak OK (2026-06-14)**; **practice patrol runner MP ✅ (2026-06-23)** — Tier C1 only if normal-ping misses still feel wrong.
- **Practice arena (`practice_arena.scene`) ✅ (2026-06-22)** — **`MapMatchConfig.PracticeArenaMode`**: unlimited clock/goals, all joiners team **0** + team-0 spawns only, **no top score/clock HUD**. **`PracticeLaunchMeasure`** on **`PracticeLaunchLane`** (origin = first line at NPC feet; local **Y** down lane; **`BandPitch` 128** → score **1, 2, 3…** from max pelvis **`along`**). **`PracticeLaunchReadout`** on **`LaunchReadoutSign`** TV. Three static **`practice_npc`** dummies + ruler art (editor). **`PracticeNpcPatrol` + `PracticeNpcPatrolHostState` ✅ (2026-06-23):** host ping-pong **Point A ↔ Point B** at charge speed, instant 180°, knockdown pause + pre-hit snap-back resume; **can tackle player** (`TryGetHostTackleMove`) and **be tackled**; **`PlayerBallHoldAnim`** required for forked graph + **`charge_run`** overlay. **Run legs ✅ (2026-06-23):** animgraph needs **`move_x`** (local forward, positive) + **`move_groundspeed`**. **Practice NPC MP ✅ (2026-06-23):** scene dummies stay **`NetworkMode.Snapshot`** — **do not `NetworkSpawn` player-prefab NPCs**; knockdown hide/show = host **`PracticeNpcClient*Rpc`**; patrol runner pose = host **`PracticeNpcPatrolPoseRelay`** fixed-tick **`PracticeNpcPatrolPoseRpc`** (auto on network spawn); client blitz contact freeze pins at visual contact (no host rewind); **`CatchUpSpeedBoost`** ignores global Input on tag; host tackle detect patrol-only; **`PlayerCosmeticsSync`** off on tag. PC **disabled** on runner.
- **Global dry audio ✅ (2026-07-05)** — **`MatchAudioBootstrap`** (auto Main Camera): **`DisableRoomSimulation`** default **on** — Master **`Reverb = 0`** + **`BlockingTags`** whitelist `audio_room_sim` (no map geo uses it → no room echo). **`PlayerFootstepAudio`** auto on network spawn (owner → Master footsteps). Gameplay **`.sound`** assets + **`PlayWorldSoundDry`** for code one-shots. **Indoor/tunnel map later:** uncheck **`DisableRoomSimulation`** on bootstrap. Optional editor: **`passaudio`** on canopy/tree props if reverb re-enabled.

**Before ship (optional):** Uncheck **`Enable Debug Force Goal`** on `MatchDirector` in scene if you don’t want `,` testing in builds (already **off** by default in code).

**Still later:** Tackle tuning, map vote (30s, all players, `Slot1`–`N`). **UI font pass** → [UI typography](#ui-typography-deferred-pass) (Barlow Condensed on HUD/menus — not started).

### UI typography (deferred pass)

**Decided (2026-07-04):** two-font split — **not wired in HUD yet** (still Poppins in code defaults).

| Role | Font | Where |
|------|------|--------|
| **Display / comic** | **Les Flos** Sage (± Sans/Chaos on tackle tiers) | Comic bursts, OOB world stack, future menu **titles** |
| **HUD / UI body** | **Barlow Condensed** (SIL OFL) | Score, clock, ult %, menus, loadout body — **migration later** |

**Assets:** `Assets/fonts/BarlowCondensed-*.ttf` — **keep** Regular / Medium / SemiBold / Bold for v1; rest optional. **CSS family name:** `Barlow Condensed`. Copy **OFL.txt** into `Assets/fonts/` when convenient.

**Style rules:** Comic bursts = shadow + highlight extrusion. **Lingering UI** (OOB stack, menus) = **shadow only** on Sage; no highlight extrusion. HUD numbers = **flat** Barlow.

**When we do the pass (order):** (1) `PracticeLaunchReadout.ScoreFontFamily` smoke test → (2) match HUD → (3) owner HUDs → (4) menus/loadout. **Do not** put Barlow on OOB stack or comic bursts.

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

**Editor MP (join via new instance):** Local/custom assets (e.g. **`vfx/spark_01.sprite`**) may **not mount** on the joining instance — client console `sprite_c` / `ERROR_FILEOPEN`, particles show **error-material squares**. Host/owner often fine. Known s&box editor quirk ([Facepunch #5177](https://github.com/Facepunch/sbox-public/issues/5177)). **Do not** “fix” with `"Resources": "*"` in `.sbproj`. Deferred fixes to try later: disabled wind-up prefab in scene (mount list), published/local-server test, asset path bundling. **Not blocking solo / owner play.**

---

## Where the code lives

| Folder | What’s in it |
|--------|----------------|
| `Code/Ball/` | Ball pickup, throw, charge bar, trajectory preview (`ThrowReleaseMath`), **`BallCarrierOutline`**, **`BallPassAssistState`**, **`BallLastTouchLedger`**, **`BallOutOfBoundsHost`**, smooth ball on clients |
| `Code/Player/` | Movement, dodge, tackle, **`CombatFeelPredictDedupe`**, **`PlayerFootstepAudio`**, team, class, cosmetics, anim overlays; *(planned)* **`PlayerMelee`** (unarmed + shared melee pipeline for weapons) |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, **`OutOfBoundsZone`**, **`MapMatchConfig`**, **`MatchAudioBootstrap`** |
| `Code/Ultimates/` | **`PlayerUltCharge`** (slice 1); **`SpeedsterSpeedBlitzUlt`** (2a–2c); **`SpeedBlitzWindUpFeel`**, **`SpeedBlitzBodyGlow`** (2d) |
| `Code/UI/` | Match HUD + owner HUDs + **`UltChargeHud`** + **`BallCompassHud`** + **`OutOfBoundsBannerHud`** + **`BallOobDropZoneHud`** / **`BallOobDropZoneMarker`** + **`TackleComicTextHud`** / **`TackleComicBurst`** + **`PracticeLaunchReadoutRoot`** / **`PracticeLaunchScorePanel`** |
| `Code/Map/` | `StartupMapBootstrap` (practice NPC locks); **`PracticeLaunchMeasure`** / **`PracticeLaunchReadout`**; **`PracticeNpcPatrol`** / **`PracticeNpcPatrolPoseRelay`**; **`StreetLightFlicker`**; **`StationLightFlicker`**; **`TrafficSpawner`** / **`TrafficCar`** |

**Scenes:** `scenes/throwdown_turf_wars.scene` (Turf Wars WIP) · **`scenes/practice_arena.scene`** (training — enable **`PracticeArenaMode`**) · `throwdown_prototype.scene` = greybox fallback.

**Important:** AI should **not** edit `.scene`, `.vmdl`, `.vanmgrph`, or other editor-owned assets unless you **explicitly give permission** — see `.cursor/rules/editor-asset-ownership.mdc`. Give steps; you wire in the s&box editor.

---

## Multiplayer gotcha (match flow)

`MatchDirector` is on **Main Camera** — each machine has its own copy. **Clients do not** use it for freeze/HUD/score.

**Authoritative on clients:** synced fields on **`PlayerTeam`** (on each network-spawned player). Host pushes via `MatchDirector.PushMatchHudStateToPlayers()`.

---

## Multiplayer feel & netcode

**Read [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md)** when changing host RPCs, `[Sync]`, owner-driven movement, combat hits, ragdolls, or **adding new combat features**. That doc covers: host authority vs client-side prediction (feel only), reconciliation, priority order (now → per-feature → tuning → late dev), and the **new feature checklist**.

**Tier 0–A3 + A2b ✅ (2026-06-14):** Client predict for blitz dasher, tackle attacker, victim freeze, traffic ragdoll; **`CombatFeelPredictDedupe`**. **Practice arena moving targets ✅ (2026-06-23).** **Next netcode:** Tier B ongoing; Tier C1 lag-comp only if normal-ping misses still feel unfair — see [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md).

---

## How the game is put together (simple rules)

- **One script, one job** — e.g. `BallGrab` = “who holds the ball”, `BallThrow` = “throwing”, `ThrowTrajectoryPreview` = owner aim helper only.
- **Throw trajectory preview:** Owner-only scrolling white dashed arc + **1:1 held-ball clone** landing marker (`TranslucentBallMaterialPath` → `ball_translucent.vmat`), first arc to ground (no bounces). Dash scroll uses simulation-time keys so motion stays visible while charge lengthens the arc. `ThrowReleaseMath` shares release velocity with `BallThrow`; preview pivot = `BallGrab.GetPredictedThrowReleasePivotPosition()`. Use **Translucent** material + `g_flOpacityScale` (not tint-alpha on opaque — grains on clients).
- **Ball carrier glow:** `BallCarrierOutline` auto on `main_ball` — **white ↔ green** (teammate) / **white ↔ red** (enemy) colour pulse; ring width scales with camera distance; emissive breathe for non-carrier viewers; carrier sees nothing; no through walls. Tune on ball: `PulseWhiteColor`, `FriendlyAccentColor`, `EnemyAccentColor`, `EmissiveBrightnessMax`.
- **Ball compass:** `BallCompassHud` — bottom-left panel + ring; white **`LabelText`** hub centered in ring (default **BALL**); small **triangle** orbits ring edge (360°) toward **`main_ball`** (held or loose). **White** loose · **green** teammate · **red** enemy. **Needle hidden** when you carry (label + panel + dim ring stay). Bearing = **player position + `EyeAngles` yaw** (not camera). Auto-added on network spawn (`GameNetworkManager.GetOrCreate`). Tune: `MarginLeft` / `MarginBottom` / `CompassSize` / `NeedleTipRadius` / colours.
- **Throw charge camera:** **`ThrowChargeCamera`** — `[Order(10002)]` after **`BallThrow`**; offset in `OnUpdate`; FOV in **`PostCameraSetup`**. Holds through **`IsPendingThrowRelease`**, then **`ReleaseCameraBlendDuration`** ease (transition FOV hold — no frame pop). Idle: does **not** touch **`CameraOffset`**. Yields to ragdoll, **`TackleImpactFeel`**, active Speed Blitz.
- **Speed Blitz dash camera:** **`SpeedBlitzDashCamera`** — wind-up lerp → blended dash spike (`WindUpToDashBlendDurationSeconds`) → on hit **`BeginHitRecoveryBlend()`** (`DashEndBlendDurationSeconds` to baseline at contact). Auto-added by ult. Tune on Speedster prefab.
- **Speed Blitz connect SFX:** Host picks random **`ConnectImpactSoundA/B`** → **`PlaySpeedBlitzConnectImpactSoundRpc`** (all clients; dasher id dedupes client-owner **predict crunch**). **`LaunchSound`** at ragdoll impulse after hang.
- **Speed Blitz wind-up feel (2d):** **`SpeedBlitzWindUpFeel`** — synced **`NetPhase`** / **`IsConnectPoseFrozen`**; **`speedblitzwindupvfx`** clone **wind-up only**; **`speedblitzdischargevfx`** at connect-hang end (launch); **`PlayFeelSoundAt`** (electric / rise / dash); electric **stops at connect**. **`PlayerSpeedBlitzWindUpAnim`** — `blitz_windup` scrub from **`GetWindUpLerp()`**, weight ~**0.3s** in; auto on network spawn. Tune **Wind-up feel** + **Knockdown feel** (`DischargeVfx*`) on ult.
- **Speed Blitz body glow (2d):** **`SpeedBlitzBodyGlow`** — **`SceneObject.ColorTint`** + optional point light (`NetworkMode.Never` child, **destroyed** when glow ends); **`SpeedBlitzBodyGlowRenderSystem`** syncs light each frame on remotes. No **`HighlightOutline`**. Tune **`BodyTintStrength`**, **`ClothingTintStrength`**, **`DischargeFadeSeconds`** on Speedster (auto-added by ult).
- **Speed Blitz dash hits:** Host + owner predict — **touch** only (`TryFindDashHitAlongSegment`): corridor width/range filter, then **3D contact radius**, **`MaxHitVerticalSeparation`**, **LOS trace** (walls block); **no snap-through**. Dash stops at **actual** position. Preview corridor ≠ guaranteed hit (walls/height).
- **Speed Blitz body freeze:** **`BlitzConnectPoseFreeze`** — attacker + victim **`PlaybackRate = 0`** during blitz hang only (`IsConnectPoseFrozen` / `IsAwaitingSpeedBlitzRagdollLaunch`). Optional **`ConnectImpactChargeRunCycle`** snap on dasher.
- **Owner camera FOV rule:** Never set **`CameraComponent.FieldOfView`** only in `OnUpdate` — use **`PlayerController.IEvents.PostCameraSetup`**. **`CameraOffset`**: set in `OnUpdate` before PC setup; respect **`[Order]`** — idle ult cam must not stomp throw offset.
- **Walk into the ball = pick it up.** No kick button. Pickup = cylinder: **`InteractDistance`** horizontal + **`MaxPickupVerticalSeparation`** vertical from player root (default **80** — head height without widening ground reach). While held, ball follows **`hold_R`** on **Body** `SkinnedModelRenderer` (`BallGrab.HoldBoneName`; falls back to `HoldAnchor`). Old `HandHoldPoint` + `citizen_holdball_test` IK was for classic citizen — human uses bone attach.
- **Ball carrier hold/throw anim (v1):** **`PlayerBallHoldAnim`** — **`holditem`** + **RH** while holding; on release **`b_attack`**. Charge = masked **`throw_windup`** on forked **`utd_citizen_human_m.vanmgrph`** (`throw_charge` / `throw_charge_weight`). **RMB cancel** → **`NotifyThrowChargeCancelled`**. **`ClearHoldPoseAfterKnockdown()`** on stand-up (body was hidden during ragdoll — clears stuck hold). **`BallThrow.IsThrowPlantLocked`** — planted feet during charge + release delay. Auto-added on network spawn.
- **Outdoor audio (v1):** s&box **room simulation** echoes under canopies/trees — **off** for Turf Wars via **`MatchAudioBootstrap.DisableRoomSimulation`**. Do not rely on editor mixer tweaks alone for MP (join clients can diverge). See **Works today** → global dry audio.
- **Online: the host is the referee** — clients request; host decides.
- **Tackles:** Only at full charge speed (`NetAtChargeSpeed`). Host ragdoll + client **request** RPC. **Connect crunch** on player tackles (not blitz/traffic) — **`TackleConnectImpactSoundA/B`**, host random + broadcast; owner predict dedupe. **Attacker on connect → walk ramp reset** (all classes). **`PreLaunchPauseSeconds` > 0:** **`NetAwaitingRagdollLaunch`** — victim **visible + frozen**, then impulse + ragdoll. **Client victim/attacker feel predict** (Tier A) — host still owns knockdown. **`CombatFeelPredictDedupe`** dedupes host feel RPCs.
- **Charge run overlay:** **`PlayerChargeRunAnim`** drives graph params when **`IsAtChargeSpeed`** (synced) — not owner-only ramp HUD.
- **Dodge:** Double-tap A or D. Tackle iframe only. **Capped lateral slide** (`DodgeChannelDurationSeconds`) — class `DodgeDistance` = literal travel; **air dodge OK**; **hard horizontal stop** at channel end (blitz-style — no dodge+jump cannonball).
- **Ragdoll / knockdown:** **Walk** ramp resets **on knockdown** (`TriggerForceWalkRampOnHost` + local snap of `smoothedMoveSpeedCap` in `CatchUpSpeedBoost`); ramp timers frozen while down. **✅ Working.**
- **Charge tier + W+S:** **✅ Fixed** — `ApplyMutuallyExclusiveForwardBackwardInput` patches `AnalogMove.x` (not `.y`); `[Order(-100)]` + `OnFixedUpdate` so `PlayerController` sees mutex before movement.
- **Crouch:** Disabled — do not rebind `Duck` without re-enabling intentionally.
- **Test dummies:** Tag `practice_npc` on **dummies only**. Scene-placed dummies = **`NetworkMode.Snapshot`** — **`[Sync]` on `PlayerTackle` / `PracticeNpcPatrol` does not replicate**; MP knockdown = host **`PracticeNpcClient*Rpc`**; patrol runner movement = host **`PracticeNpcPatrolPoseRelay`** → **`PracticeNpcPatrolPoseRpc`** (clients snap to host pose). **Never `NetworkSpawn` player-prefab practice NPCs**. Static dummies must not run host tackle detect (patrol runners only). **`PlayerCosmeticsSync`** disabled on tag.
- **Weapons later:** Ball **or** weapon, not both (not implemented). **Unarmed melee** specced — **combat slice 1** (before weapons slice 7); LMB shared with throw when not holding ball.
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
4. **Throw polish:** trajectory arc + landing marker, charge camera/bar; **planted charge + RMB cancel**; tackle while charging (ragdoll cam OK).
5. **Ball carrier glow:** teammate = white ↔ green; enemy = white ↔ red; **you carry** — no glow; behind wall — no glow.
6. **Ball compass:** triangle orbits ring toward ball; green / red / white by possession; you carry → **BALL** hub + ring, no triangle.
7. **Held ball:** sits on carrier’s **right hand** (`hold_R`), not hip; both windows agree.
8. **Hold/throw anim:** **holditem** while carrying; throw motion on release; **ball leaves hand** after **`ThrowReleaseDelaySeconds`** (not on button-up); remote sees anim (`PlayerBallHoldAnim` RPC).
9. **Charge run overlay:** no ball, max ramp — `charge_run` on **both** windows (remote uses **`NetAtChargeSpeed`**).
10. **Tackle juice:** hitstop/shake/punch on connect; victim **visible freeze** then launch when **`PreLaunchPauseSeconds` > 0**; host→client and client→host.
11. Spam actions once to probe desync.
12. **Ult charge:** % creeps in **Playing** only; frozen in celebration/intermission; goal/tackle bumps; **assist +25 on throw→teammate goal** (window / void rules); FF tackle no bump; persists across rounds; rematch clears; HUD floored % + blue flash at 100%. **Assist playtest OK (2026-06-30)**.
13. **Combat feel predict:** client tackler / dasher / victim / car-hit — juice on contact frame, no double feel; idle targets OK (2026-06-14).
14. **Speed Blitz 2d (MP):** Olympic pose + body glow + discharge + SFX on remotes; electric cut + dash woosh; no sparks on dash/connect/miss. **Dash hits:** wall/roof blocks — no teleport hit. **Client dasher:** connect crunch on predict. **Joining client wind-up spark sprites = blue squares** (editor limitation — publish only).
15. **Ball OOB (map 1):** roll into zone → whistle + banner → drop disc at **thrower feet** + stack → sky-drop; stand on marker → ball grabs at head height; rematch/round reset cancels marker; client ball hidden during countdown.
16. **Practice arena NPCs (MP) ✅ (2026-06-23):** idle + patrol runner — knockdown visuals (`PracticeNpcClient*Rpc`), patrol pose sync (`PracticeNpcPatrolPoseRelay`), tackle + Speed Blitz contact (no freeze snap-back / invisible hits). Host solo must **not** self-launch on static dummies.
17. **Dodge channel (2026-07-04):** dodge→jump / jump→dodge — no long glide; dodge off ledge completes slide then horizontal stop; wall early stop; iframe + tier penalty still apply. **2-window MP** when convenient.

**Ball jittery on client only?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

**Client tackle looks short or late?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Ragdoll (technical)”. Don’t re-add `StartAsleep` or mute collision sounds without waking bodies — broke launch (2026-05-18).

---

## Multiplayer gotcha (practice NPCs)

- Scene **`practice_npc`** dummies use the **player prefab** but are **not** network-spawned players — default **`NetworkMode.Snapshot`** in scene.
- **`[Sync]` does not replicate** on scene dummies (`PlayerTackle`, `PracticeNpcPatrol`) — use broadcast RPCs instead.
- **Knockdown visuals:** host **`PracticeNpcClientFreezeRpc` / `PracticeNpcClientRagdollRpc` / `PracticeNpcClientStandUpRpc`** from a network-spawned player's `PlayerTackle`.
- **Patrol runner pose:** host **`PracticeNpcPatrolPoseRelay`** (auto on network spawn) broadcasts **`PracticeNpcPatrolPoseRpc`** each fixed tick — clients snap to host position (do **not** client-sim the path; drifts).
- **Blitz contact on clients:** `BeginPracticeNpcClientContactFreeze` pins dummy at visual contact; dasher predict stop not overwritten by `NotifyOwnerDashEndedRpc`.
- **Do not `NetworkSpawn` scene practice NPCs** — shared Input counter-tackle, Main Camera hijack, host cosmetics on dummies.
- **Patrol runner** host tackle is intentional (`TryGetHostTackleMove`); **static** idle dummies must not run `TryDetectAndApplyHostTackle` (`CanUseHostTackleDetection`).

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
- **`MatchAudioBootstrap`** — auto via **`GameNetworkManager`**; **`Disable Room Simulation`** on (outdoor default). Uncheck for indoor/tunnel maps.

**`MatchHud` empty (scene UI root):**
- `MatchScoreHud`, `MatchClockHud`, `GoalBannerHud`, `IntermissionHud`, **`MatchOverHud`**
- **`OutOfBoundsBannerHud`** + **`BallOobDropZoneHud`** auto-add on **Main Camera** via **`GameNetworkManager`** — tune in **edit mode** (save scene): **`Ring Model Path`** (your disc `.vmdl`), **`Ring Material Path`** (`oob_drop_ring.vmat`), **`Ring Outline Extra Diameter`**, **`Stack Panel Size`** / font sizes / **`Stack Row Gap`**
- **`BallOutOfBoundsHost`** on **`main_ball`** — auto via **`GameNetworkManager`**, or add manually on **`main_ball`**

**Map:**
- Two **`GoalZone`** — opposite `Defending Team`, tuned `Box Size`
- **`BallSpawn`** at center → wired on `MatchDirector`
- **Out of bounds:** parent empty **`OutOfBounds`** optional → child empties per strip/roof/alley → **`OutOfBoundsZone`** each; tune **`Box Size`** + rotation (oriented box + **editor gizmo**). **Player bounds:** empty + **`BoxCollider`** + GameObject tag **`playerclip`**. **Ball:** `main_ball` tag **`ball`**. **`ball` + `playerclip` → Ignore** in **`Collision.config`**. **Turf Wars:** old `invisible.vmat` ball blockers **removed** — OOB zones + `playerclip` only (**2026-07-04**).
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
- **`PlayerUltCharge`** — ult % meter (host sync); tune `PassivePointsPerSecond`, `GoalChargePoints` (**40**), `AssistChargePoints` (**25**), `TackleChargePoints` (**10**). **Max cap** is on each ult (`IPlayerUlt`) — not here. **Add on prefab** (not auto-spawned).
- **`SpeedsterSpeedBlitzUlt`** — **Speedster only** (class gate). **Add on Speedster prefab** (not auto-spawned). Tune **`MaxChargePoints`** (default **100**), `WindUpDurationSeconds` (default **2**), `DashRange`, `DashSpeed`, `HitHalfWidth`, **`MaxHitVerticalSeparation`** (default **56**), `DefaultTargetBodyRadius`, `KnockdownLaunchSpeed`, `KnockdownLaunchArc`. **Knockdown feel:** `KnockdownPreLaunchPauseSeconds` (**0.65**), **`ConnectImpactChargeRunCycle`**, **`LaunchSound`**, **`ConnectImpactSoundA/B`**, volumes, impact feel fields; **`DischargeVfxPrefab`** → `speedblitzdischargevfx.prefab`, **`DischargeVfxLocalOffset`**, **`DischargeVfxCleanupSeconds`**. **Wind-up feel (2d):** **`WindUpVfxPrefab`** → `speedblitzwindupvfx.prefab`; electric/rise/dash **`SoundEvent`**s; offsets, volumes, **`MissVfxFadeSeconds`**. Auto-adds **`SpeedBlitzDashCamera`**, **`SpeedBlitzWindUpFeel`**, **`SpeedBlitzBodyGlow`**. Optional **`Enable Speed Blitz Debug Logs`**.
- **`SpeedBlitzAimPreview`** — **Speedster only**, same prefab as ult (manual). Segmented ground corridor while holding X; `models/dev/plane.vmdl`, `materials/turfwarspoly/speed_blitz_preview.vmat`; **`PlaneWidthBaseSize`** (playtest **~175** for hit-width read) + **`PlaneLengthBaseSize`** **100** (segment length — do not merge into one knob); `SegmentSpacing`, `CorridorAlpha` / `CorridorLift`, `MarkerAlpha`.
- **`UltChargeHud`** — floored **%** centered (left of `MovementRampHud`); **`ReadyHighlightDelaySeconds`** (~0.4s white at 100% then blue). **Add on prefab** with `PlayerUltCharge`.
- **`BallGrab`** — **`Hold Bone Name`** = `hold_R` (default); **`Interact Distance`** (horizontal, default **45**); **`Max Pickup Vertical Separation`** (default **80** — head-height pickup without widening ground reach); optional **`Body Renderer`** → Body `SkinnedModelRenderer`; tune **`Hold Bone Local Offset`** if grip looks off; **`HoldAnchor`** / `HandHoldPoint` = legacy fallback only
- **`PlayerBallHoldAnim`** — auto-added on network spawn. Tune `IdleHoldPoseHand` (~0.1), `ThrowAttackStrong`, `ThrowPoseHoldSeconds` (~0.9), `ThrowPlaybackRate` (~0.7). **Throw charge:** `UseAnimGraphChargePose` on — `throw_charge`/`throw_charge_weight` on **`utd_citizen_human_m.vanmgrph`**; tune **`ChargeWindupCycleEnd`** if wind-up finishes before bar is full; **`ChargeCancelBlendOutSeconds`** (~0.1) for RMB cancel ease. Graph re-applied after cosmetics.
- **`PlayerTackle`** — **`PreLaunchPauseSeconds`** (default **0.05**; **0** = legacy launch); **Impact SFX:** **`TackleConnectImpactSoundA/B`** (defaults = blitz crunch paths); tune with **`TackleImpactFeel.HitstopDurationSeconds`**
- **`PlayerDodge`** — class **`DodgeDistance`** (literal slide units); **`DodgeChannelDurationSeconds`** on prefab (lower = snappier — code default **0.12**). **Removed:** `ShoveVelocityMultiplier` (ignore if still in scene JSON).
- **`PlayerChargeRunAnim`** — auto-added on network spawn. **`UseAnimGraphChargeRunPose`** on; **`IsAtChargeSpeed`** (not local HUD tier). **`SpeedBlitzChargeRunBlendInSeconds`** (default **0.03** — charge_run builds faster during dash). Graph → [`CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md)
- **`TackleImpactFeel`** — auto-added on network spawn. Tune **Hitstop** / **Shake** / **Attacker punch**; **`ShakeForAttacker`** + **`ShakeForVictim`**
- **`CombatFeelPredictDedupe`** — auto-added on network spawn (with **`TackleImpactFeel`**). No inspector tuning.
- **`BlitzConnectPoseFreeze`** — auto-added on network spawn. No inspector tuning (optional **`ConnectImpactChargeRunCycle`** on **`SpeedsterSpeedBlitzUlt`**).
- `PlayerController` camera **X = 185**; **no** `ModelPhysics` on player
- **`BallThrow`** — tune **`ThrowReleaseDelaySeconds`** (**0.25** Turf Wars default) to match anim release frame; **`Throw Direction Source`** optional (else **`PlayerController.EyeAngles`**)

**`main_ball`:**
- `ModelRenderer` — e.g. **`ball_v2.vmat`** (emissive gold + pattern scroll; team read from glow/compass not ball albedo)
- **`BallCarrierOutline`** — tune `OutlineWidth` (~1–1.5), `PulseWhiteColor`, `FriendlyAccentColor`, `EnemyAccentColor`
- **`BallPassAssistState`** — auto-created on host at first throw; tune **`AssistWindowSeconds`**, **`EnableAssistDebugLogs`**

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

**Practice arena (`practice_arena.scene` only):**
- **`MapMatchConfig`** — **`Practice Arena Mode`** on; **`Practice Spawn Team Id`** = **0**
- **`GameNetworkManager`** — **`Team0Spawns`** wired; clear team-1 spawns optional
- **`PracticeLaunchLane`** empty — back edge of first line (NPC feet); local **Y** = down lane; **`PracticeLaunchMeasure`** → **`ReadoutSign`** = **`LaunchReadoutSign`**
- **`LaunchReadoutSign`** — **`PracticeLaunchReadout`** (+ optional **`WorldPanel`**); tune **`Panel Size`**, **`Score Font Size`**, **`Score Font Family`**, **`Score Color`**
- Three **`practice_npc`** dummies (player prefab); ruler lines every **128** (12 line + 116 gap); gap labels **1, 2, 3…** (visual only)
- One **`GoalZone`** if testing scoring — **`Defending Team`** = **1** when all players are team **0**
- **Moving runner dummy (separate section):** duplicate player prefab → tag **`practice_npc`** → **Speedster** `PlayerClass` → **`PlayerController` disabled** → add **`PlayerBallHoldAnim`** (forked graph) + **`PracticeNpcPatrol`** with **Point A** / **Point B** empties. Optional: wire **`BodyRenderer`** on patrol. **Do not** enable PC on runner.

---

## Prefab split + loadout — decided (2026-07-06)

**Status:** Spec locked — **not started** (Max will say **go** on code). **Refined 2026-07-06** (architecture, always-equipped, force-commit, Speedster preset).

### Build order (this sequence)

1. **Code 1–3** — spawn contract + **`LoadoutPersistence`** + **`PlayerLoadout`** + **`GameNetworkManager`** template map (may point all three refs at single `Player` until editor split).
2. **Editor (Max)** — duplicate prefabs + wire `.cdata` + strip Speedster-only (after code 1–3).
3. **Code 4+** — host swap paths + basic **class + ult** UI + intermission force-commit.
4. **Intermission** loadout swaps (frozen intermission OK v1 — menu overlay).
5. **Ult slice 5** — Juggernaut stomp (+ register in ult catalog).
6. **Intermission movement** — walkable spawn room (gate change; team spawns OK until room art).
7. **`MatchSetup` phase** + pre-match timer (round 1 **and** rematch — see below).
8. **Starter room** art/layout (cosmetic).

**Do not** block prefab split on walkable starter room or pre-match timer.

### Code architecture (three layers — **not** `PlayerTeam`)

| Layer | What | Notes |
|-------|------|--------|
| **`LoadoutPersistence`** | Local save by **SteamId** (`FileSystem.Data`) | Account prefs — not on pawn. **String slug IDs** (`speed_blitz`, `tackle_ramp`, …). |
| **`PlayerLoadout`** | Component on spawned pawn — apply logic, host swap RPCs, explicit equipped ult ref for **`PlayerUltCharge`** | **`[Sync(FromHost)]`** equipped class/ult/passive ids for remote read. |
| **`PlayerTeam`** | Match flow only | **Do not** put loadout fields here (phase, OOB, round reset mirror). |

**Future sumo / FFA shrink-ring:** same three layers — **`MapMatchConfig`** only changes **when** picker is allowed (match-start pick + force-commit). Not a separate storage model.

### Always-equipped loadout

Every player **always** has a committed loadout for gameplay (class + passive + ult when catalog has entries).

- **First ever session (no save):** preset **Speedster** + **`speed_blitz`** + Speedster default passive — **not** random.
- **After any save write:** last committed loadout persists across Turf Wars, practice, and relaunch.
- **Class switch in picker:** auto-select **first passive** and **first ult** for that class in catalog (no separate ult confirm when only one option).
- **`UltChargeHud`:** **always on** for all classes — no hide-when-no-ult. Pre–slice 5/6 Jugg/Sniper: meter fills, **X inactive** until ult component exists.

**Pending vs committed (picker UI):**

- **Pending** — highlighted while loadout screen is open.
- **Committed** — what spawn/combat use.
- **Force-commit:** when round begins (`Intermission` → `Playing`, **`MatchSetup`** timer → 0, sumo match start, …) — close loadout UI; **pending → committed**; that is the loadout for the round even if player never pressed Confirm.
- **Save writes:** first preset commit, picker Confirm, force-commit at round start, host apply after class change — **not** every highlight change while browsing.

### Pre–ult gap (Juggernaut / Sniper before slice 5/6) — **approach A**

- Classes **pickable now** in loadout; passive auto-fills (e.g. Jugg **`tackle_ramp`** via `.cdata`).
- Ult **catalog empty** for that class → no ult id / no `IPlayerUlt` on pawn; charge % HUD still runs; **X does nothing**.
- When stomp / path zones ship → add to catalog; class switch auto-picks first ult. **Not shipping publicly until ults exist** — gap is dev-only.

### Prefab split

- **Three class templates** (disabled scene roots): `Player_Speedster`, `Player_Juggernaut`, `Player_Sniper` — each `PlayerClass.CurrentClass` → matching `.cdata`.
- **`GameNetworkManager`** clones template from committed loadout **class** (not one shared `Player` for everyone).
- **Hybrid spawn policy** (not pure A or B):
  - **Class prefabs** own the full gameplay stack (ball, tackle, dodge, `PlayerUltCharge`, class ult + preview, HUDs, cameras) — editor WYSIWYG.
  - **`GameNetworkManager`** auto-adds **universal only**: `PlayerTeam`, `PlayerDisableCrouch`, `PlayerEnemyOutline`, `BallCompassHud`, `PlayerBallHoldAnim`, `PlayerChargeRunAnim`, `TackleImpactFeel`, `CombatFeelPredictDedupe`, `PlayerFootstepAudio`, `PracticeNpcPatrolPoseRelay`.
  - **Remove from global auto-add** → **Speedster prefab only:** `BlitzConnectPoseFreeze`, `PlayerSpeedBlitzWindUpAnim`.
- **Shared on all three** (duplicate per prefab): `BallGrab`, `BallThrow`, `CatchUpSpeedBoost`, `PlayerDodge`, `PlayerTackle`, `PlayerUltCharge`, `PlayerClass`, movement HUDs, **`UltChargeHud`**, etc. **Speedster-only:** `SpeedsterSpeedBlitzUlt`, `SpeedBlitzAimPreview`, blitz feel/cam/glow. **Juggernaut / Sniper:** first ult + preview when built (slice 5/6) — **no ult on prefab until then**.
- Full checklist → [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6.

### Loadout model (Overwatch-style, casual v1)

- **Slots:** class + ult + **passive** (all in save; **no passive picker** until a class has **2+** passive options).
- **v1 UI:** class + ult picker; passive auto on class switch.
- **Ult rule:** when class catalog has **≥1 ult**, one must be selected (auto if only one). Empty ult slot only when catalog is empty (approach A above).
- **More ults / passives per class:** after **each class has first ult**, and **after weapons slice** — not before.
- Replace `PlayerUltCharge` “first enabled `IPlayerUlt`” with **`PlayerLoadout`** explicit equipped ult → **`ResyncFromEquippedUltOnHost()`** on ult swap. Raw ult points carry; `%` resyncs; **rematch → 0%** unchanged.

### When loadout can change

| Phase | Turf Wars / match maps | Practice (`PracticeArenaMode`) |
|-------|------------------------|--------------------------------|
| **`MatchSetup`** (future) | **Yes** — pre-round timer + pick; force-commit when timer ends | — |
| **`Intermission`** | **Yes** (v1: frozen + menu OK; walkable later); force-commit when intermission ends | — |
| **`Playing`** | **Locked** | **Anytime** |
| **`GoalCelebration`** | **No** | Anytime |
| **`MatchOver`** | **No** | Anytime |

- **Casual v1:** change every **intermission** + **pre-match** (when `MatchSetup` ships) — **not** lock-for-full-match. **Competitive (later):** stricter rules via `MapMatchConfig`.
- **Rematch (when `MatchSetup` exists):** `MatchOver` → **`MatchSetup`** (swap window) → `Playing` — **not** straight to `Playing` like today.
- **Class change** in allowed window → **host respawn** (destroy pawn → clone class prefab at team spawn → cosmetics → `NetworkSpawn`). **Ult/passive only** → in-place enable + `ResyncFromEquippedUltOnHost()`.

### Persistence

- **v1:** **local save** keyed by **SteamId** (e.g. `FileSystem.Data`).
- **Default (no file):** Speedster + Speed Blitz + class default passive.
- **Last loadout** = last **committed** write (Turf Wars **or** practice — whichever saved most recently).
- **Mid-round join:** spawn with last saved committed loadout; change at next **intermission** (no picker mid-`Playing`). Optional quick-pick if join during intermission — later.
- **v1 unlocks:** all class/ult/passive options in catalog (no grind gate).
- **Later slice:** unlocks + XP + skill points → **server-trusted** storage; migrate local → cloud when that ships.

### MP (host authority)

- Client requests loadout change → host validates **`MatchSetup` / `Intermission`** (or practice free-swap) → apply committed loadout.
- Sync equipped ids on **`PlayerLoadout`** (`[Sync]`).
- No combat predict needed — non-combat phase only.

### Editor (Max — after code steps 1–3)

- Duplicate current `Player` → three disabled templates; wire `.cdata` per class; strip Speedster-only components from Juggernaut/Sniper.
- `GameNetworkManager`: three template inspector refs (class → prefab).
- Practice arena: same loadout system; `PracticeArenaMode` = change anytime.

---

## Open decisions (not chosen yet)

- **Competitive vs casual FF (later):** v1 = friendly fire **on** for tackles + melee; **ults enemies-only** (no FF). Future competitive mode may enable FF on everything; casual may disable FF — single host flag when that mode ships.
- **Competitive loadout rules (later):** casual v1 = intermission + pre-match swaps OK; ranked may lock class for full match or cap swaps — tie to competitive `MapMatchConfig` flag when mode ships.
- **Loadout unlocks / XP persistence (later):** v1 = all options in catalog (first-run **Speedster preset**, not random). When leveling/skill-point unlocks ship → filter picks to **unlocked only**; dedicated slice (local loadout prefs may stay local; unlocks on server-trusted storage).
- **Sumo / shrinking-ring gamemode (later):** separate **mode slice** — FFA, loadout pick at **match start** (same **`LoadoutPersistence`** + **`PlayerLoadout`** + force-commit when round begins); reuses combat slice 1 melee; not a separate melee ruleset.
- **Player body for v1:** **`citizen_human_*`** (branch tested) vs classic **`citizen.vmdl`** — leaning **human** (audience + looks good); citizen fits chaotic meme tone. No custom rig (account cosmetics).
- Closed roof on arena vs open roof + sun for lighting
- **Tackle oof/grunt** — layered on built-in ragdoll collision audio (not shipped)
- **Practice patrol zigzag** — optional intermediate waypoints between A/B (straight ping-pong only for now)
- **Practice arena ball OOB (later):** instant-respawn volumes on fall-off — **not** match OOB flow; separate component when practice map needs it
- Map vote: allow changing vote during the 30s window?
- **Traffic knockdown tuning:** **`KnockdownLaunchSpeed`** / hit box vs dodgeability
- **Ball compass polish:** optional distance readout on `BallCompassHud`
- **Charge wind-up bone mask choice:** `Blend_UpperBody_HalfSpine_FullArms` (arm + some spine lean, smoother) vs `Only_RightArm` (strictly arm) on the graph's Bone Mask node — pick whichever looks better in playtest
- **Hero asset art:** maps/props low poly; **players + ball** may get higher-detail models later — ball on **`ball_v2.vmat`** (emissive gold + scroll) for now; **leaning white ball** later (fits blue ult VFX). `BallCarrierOutline` still copies ball material for carry breathe
- **Comic word scope:** tackles/knockdowns only for v1; **ults** (+ weapon KOs later) get own burst — not throws/dodges. **Ult palette ✅:** `ComicBurstPalette.Ult` — blue fill (`#24b0ff`) + pale cyan highlight; Speed Blitz spawns **after ragdoll launch** (not at connect hang). Future ults pass `ComicBurstPalette.Ult` to `NotifyHostKnockdown`.
- **Charge tier + backward (S) while W held:** **✅ Fixed** — mutex was writing forward/back on `AnalogMove.y` (strafe axis); s&box uses `.x` for forward/back. W wins when both held.
- **Speed Blitz ball strip on connect:** During blitz connect hang, **`BallGrab`** on the victim can lose the ball to the dasher if pickup overlap runs — **intentional** (reward for hitting carriers); not a bug to remove without design pass.
- **Speed Blitz aim preview v3:** **✅ Shipped (2026-06-30)** — segmented `plane.vmdl` + `speed_blitz_preview.vmat`; plain blue (no scroll grid / comic layers). **Wall/LOS clip: won't do.** **`PlaneWidthBaseSize`** / **`PlaneLengthBaseSize`** split so width tuning doesn't gap segments.
- **Speed Blitz 2d electric at connect:** **✅ Shipped:** electric **SFX** hard stop at connect crunch; wind-up **VFX** off dash/connect/hang (**2026-06-18**).
- **Speed Blitz wind-up VFX scope:** **✅ Shipped (2026-06-18):** **`speedblitzwindupvfx`** **wind-up only** — not dash/connect/hang.
- **Speed Blitz body glow:** **✅ Shipped (2026-06-18):** **`SpeedBlitzBodyGlow`** — tint + point light on dasher; discharge at launch; no victim glow; no ult outline (enemy red outline unchanged).
- **Speed Blitz launch discharge VFX:** **✅ Shipped (2026-06-18):** **`speedblitzdischargevfx`** on dasher chest at ragdoll launch (hit only); tune in prefab + **`DischargeVfxLocalOffset`** on ult.
- **Speed Blitz impact stride `charge_run_cycle`:** snap dasher to a fixed cycle at connect (shoulder-in frame) vs freeze whatever pose contact landed on — scrub `charge_run` in ModelDoc; inspector default TBD in playtest.
- **Speed Blitz victim flinch (later):** optional masked hit-react clip + graph layer during hang (same pattern as `throw_windup` / `charge_run`) — polish on top of body freeze v1; ship or skip after playtest
- **Juggernaut post-tackle run recovery (passive slot):** optional passive keeps **run** (sprint tier, not charge) after landing a tackle instead of walk reset — bundle with loadout UI; mutually exclusive with **tackle ramp bonus** passive.
- **Speed Blitz 2d — client wind-up spark sprites (MP):** editor join-client shows **blue squares**; owner OK. **Deferred to publish smoke test (2026-06-30).** **Soft ring:** skipped — pose + VFX + audio sufficient.
- **Dodge MP fairness / feel (deferred):** if players feel **cheated** (“I dodged but got tackled”) or client dodge feels late — optional pass: **(1)** owner movement predict on RPC send (channel starts on double-tap; cancel if host rejects); **(2)** host **`NetTackleIframeUntil`** with capped intent grace (client press time + small backdate cap) and/or slightly longer iframe. See [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md). **Not planned for foreseeable future** — channel + solo/2-window OK (**2026-07-04**); revisit only if soak/playtest complains.
- **Outdoor room reverb ✅ (2026-07-05):** **Off** — **`MatchAudioBootstrap`** (`DisableRoomSimulation` default on). **Indoor map later:** uncheck on Main Camera bootstrap; optional **`passaudio`** on decorative overhead geo only.

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
- [x] ~~Optional: yaw-only camera lock or wider hit cone~~ — **signed off (2026-06-15):** keep **full camera lock** + lane corridor aim filter; **hits = physical contact + LOS (2026-06-22)**, not corridor teleport
- [x] Ult **blue** comic burst — `ComicBurstPalette.Ult`; Speed Blitz spawns on launch (not connect)

#### Slice 2d — Speed Blitz **electric charge** polish (VFX phases + SFX + Olympic pose) — **✅ SHIPPED (2026-06-30)**

**Shipped:** **`speedblitzwindupvfx`** (wind-up sparks only) + **`speedblitzdischargevfx`** (launch burst) + **`SpeedBlitzWindUpFeel`**; **`SpeedBlitzBodyGlow`** + render system; electric SFX hard cut at connect; **`PlayFeelSoundAt`** wind-up sounds; **`speedblitz_windup`** animgraph layer + **`PlayerSpeedBlitzWindUpAnim`**. **2-window MP ✅** (practice patrol 2026-06-23). **Dash jump-over fix ✅ (2026-06-30)** — contact cylinder matches tackle (`TryValidateContactCylinder`).

**Deferred / skipped:**

- **Joining-client wind-up spark sprites** — blue squares in editor "Join via new instance"; publish smoke test only
- **Soft ring** — skipped after playtest (pose + VFX + audio sufficient at distance)

**Design reference:** energy-anime blue **`#24b0ff`**; wind-up = sparks + SFX rise + body glow ramp; launch discharge = accent only vs comic + launch boom.

<details>
<summary>Slice 2d original spec (collapsed — superseded on VFX scope 2026-06-18)</summary>

**VFX phase map (current shipped):**

| Phase | VFX |
|-------|-----|
| **Wind-up** | Attractor + sparks; body glow ramp; **`GetWindUpLerp()`** |
| **Dash** | Body glow peak; **no** wind-up sparks |
| **Connect + hang** | Body glow peak; **no** sparks |
| **Ragdoll launch** | Discharge burst on dasher + body glow fade; comic + launch boom |
| **Miss** | Body glow fade at dash end |
| **Interrupt** | Hard off |

**Audio:** electric + rise (wind-up) → dash woosh → **electric cut at crunch** → launch boom on ragdoll impulse.

</details>

#### Slice 3 — assist charge ✅ **SHIPPED + playtest OK (2026-06-30)**

- [x] **`BallPassAssistState`** on `main_ball` — host throw chain, **`AssistWindowSeconds`** (default **10**)
- [x] Window starts on first **solid** contact (ground/wall/prop/traffic) or **teammate grab** (perfect catch); player bodies ignored
- [x] Void: **enemy grab**, **enemy tackle** on ball carrier; relay re-throw resets passer credit
- [x] **`GrantAssistChargeOnHost()`** — default **25** pts; goal **40**, tackle **10**
- [x] **`PracticeArenaMode`** — assists off
- [x] Playtest sign-off — throw assist, voids, relay; charge bumps on scorer + passer

#### Slice 4 — per-ult charge max (balance pass) ✅ **SHIPPED + playtest OK (2026-07-02)**

- [x] **`IPlayerUlt`** + **`MaxChargePoints`** on each ult component (Speed Blitz default **100**)
- [x] **`PlayerUltCharge`** resolves equipped ult (v1: first enabled `IPlayerUlt` on player); **`ResyncFromEquippedUltOnHost()`** for loadout swap
- [x] Display still 0–100%; same universal event point awards (40 / 25 / 10)
- [x] Ult swap: raw points carry over; no penalty

**Loadout UI:** v1 spec → [§ Prefab split + loadout](#prefab-split--loadout--decided-2026-07-06). Per-ult % on picker = polish later.

---

## Ship order (after ult slice 4)

| Order | Slice | Notes |
|-------|-------|--------|
| **1** ✅ | **Map slice 1** — out of bounds | **Shipped 2026-07-04** (solo + 2-window MP) |
| **2** | **Prefab split + loadout v1** | [§ Prefab split + loadout](#prefab-split--loadout--decided-2026-07-06) + [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6 |
| **2b** (later) | **MatchSetup + walkable spawn room** | After slice 5; not before prefab split |
| **3** | **Ult slice 5** — Juggernaut stomp | |
| **4** | **Ult slice 6** — Sniper path zones | |
| **5** | **Combat slice 1** — unarmed melee | Before weapons; foundation for sumo endgame + slice 7 |
| **6** | **Combat slice 2** — parry | Melee only — not tackles |
| **7** | **Ult slice 7** — Weapons | Armed swings reuse melee hit pipeline |

---

### Map slice 1 — out of bounds (ball) ✅ **SHIPPED (2026-07-04)**

**Why:** Replace invisible ball walls with sports-style OOB — whistle, UI, delayed sky-drop. **Players** stay in bounds via **map collision / player-only walls** (v1); **ball** uses OOB zones only.

**Editor (you):** Mirror **`GoalZone`** — empty per volume, **`OutOfBoundsZone`**, inspector **`Box Size`**, rotate for sidelines/roofs; **gizmo outline** in scene view (`ExecuteInEditor`). Multiple zones per map. Remove old invisible ball blockers once zones are placed.

#### Map slice 1a — core (solo / host) ✅ **CODE SHIPPED (2026-07-03)**

- [x] **`OutOfBoundsZone`** — oriented box overlap test (like **`GoalZone`**)
- [x] Host **`main_ball`** OOB watcher — **`MatchPhase.Playing`** only; **loose ball only** (not held)
- [x] **Confirm rules:** overlapping OOB + **supported by any solid** (trace down) + speed below threshold for **~1.0s** continuous
- [x] **Last-touch ledger** (host, on ball): throw / drop / tackle knock-off → credit + **credited player feet** anchor. **Fallback:** no ledger → **`BallSpawn`**
- [x] On confirm: **`BallPassAssistState.ResetOnHost()`**; hide ball; **`match_whistle`** (2D broadcast; asset **UI** flag); white **`OUT OF BOUNDS!`** screen ~**3s**; world drop marker **10s** (ground disc + **DROP ZONE** / countdown / ▼)
- [x] Sky-drop: enable ball at **anchor XY** (player feet at last touch), ground Z + **Z + DropHeight** (**450** code default)
- [x] One OOB sequence at a time; **cancel on round reset / rematch**
- [x] **`ball` + `playerclip` → Ignore** in **`Collision.config`**
- [x] **Editor (Turf Wars):** **`OutOfBoundsZone`** + **`playerclip`** walls — **Max 2026-07-03**
- [x] **Solo playtest** — core flow OK; polish backlog below

#### Map slice 1b — MP + polish ✅ **SHIPPED (2026-07-04)**

- [x] **Solo polish:** whistle global; custom ground disc + outline; world stack layout; player-feet anchor + ground trace; cylindrical ball pickup; **playerclip** throw preview; compass → drop anchor; roof/ceiling drop fix; stack **shadow-only** (no highlight extrusion)
- [x] **Feel polish:** **`BallOobDropZoneHud`** — continuous **DROP ZONE** scale pulse + **▼** bob; ring alpha pulse
- [x] **2-window MP** — whistle + banner + drop marker on all clients; sky-drop; ball hidden during countdown; rematch cancel; sky-drop head-height grab
- [x] **`BallGrab`** — host-authoritative auto-grab for all players (`TryAutoPickupOnHostAuthority`) — fixes client OOB sky-drop bounce / missed pickup vs host
- [x] **Editor (Turf Wars):** invisible `invisible.vmat` ball blockers removed — OOB + `playerclip` only (**2026-07-04**)

**Prefab split + loadout (ship order #2):** Full spec → [§ Prefab split + loadout](#prefab-split--loadout--decided-2026-07-06). [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6 for component checklist.

#### Ult slice 5 — **Juggernaut** ult (ground stomp)

- [ ] New component `Code/Ultimates/` — AOE knockdown around self
- [ ] Reuse `ApplyKnockdownFromHost`; MOBA preview pattern as needed
- [ ] Same `PlayerUltCharge` gate + commit rules

#### Ult slice 6 — **Sniper** ult (ball path ragdoll zones)

- [ ] Requires **ball**; exception to “no ult while holding”
- [ ] Zones along throw path — ties into `BallThrow` / trajectory
- [ ] Most complex of the three first ults

### Combat slice 1 — unarmed melee (`PlayerMelee`)

**Why:** Scrappy knockdown when there’s no room to **charge** tackle (future sumo shrink endgame, tight spaces). Weaker than tackle; **2 hits** to confirm. **Weapons slice 7** reuses this pipeline for armed LMB swings.

**Movement tiers (3 only):** Walk → Sprint (HUD “Sprint” = middle tier) → Charge (`CatchUpSpeedBoost.IsAtChargeSpeed`). **Tackle** = charge tier only. **Melee** = walk + sprint only — **blocked at charge tier**.

**Input:** **LMB** tap without ball → melee (future: weapon swing when armed). Hold LMB with ball = throw charge (unchanged). No ball → no throw; holding ball → no melee.

**Hierarchy:** Tackle (charge, 1 hit, unparriable, class mass) → unarmed 2-hit → parry punish (slice 2).

#### Combat slice 1a — core (solo / host)

- [ ] **`Code/Player/PlayerMelee.cs`** — host validates hits; owner predict feel (like tackle — **`CombatFeelPredictDedupe`**)
- [ ] **LMB** swing — short range, aim at target (tackle-like validation; tune range/arc in playtest). Active hit frames per swing (~**0.2–0.3s**, tunable). **Whiff** uses swing recovery too (anti-spam)
- [ ] **Per-victim combo** (host): **2 hits** within **`ComboWindowSeconds`** (default **5**, tunable) from **first hit on that victim** → knockdown. Stacks **persist** if attacker switches targets (1v2: hit B, hit C, hit B again → B knocks down). **Dodge does not** clear attacker’s stack on victim
- [ ] **Hit 1:** hitmarker UI + COD-style tick SFX; micro-hitstop; **tiny** knockback (tunable). **No** ball drop. **No** victim speed-tier drop (attacker **swing recovery** only)
- [ ] **Hit 1 on target in committed ult wind-up:** hitmarker + micro-hitstop **only** — **no knockback**; wind-up **not** interrupted until knockdown
- [ ] **Hit 2 / knockdown:** **`ApplyKnockdownFromHost`** — **weak** universal ragdoll impulse (**not** class-scaled; class balance stays on tackles). Ball drops on knockdown (like tackle). **Enemy-only** ult charge **+10** (same as tackle; **no** FF charge)
- [ ] **Knockdown** interrupts committed ult wind-up (e.g. Speed Blitz) — harder than tackle (needs 2 connects in wind-up window). Hit 1 does **not** reset wind-up timer
- [ ] **Can melee hit ball carriers**; **carriers cannot** melee or parry (can throw / dodge / space)
- [ ] **Cannot melee while:** holding ball, ragdolled, active ult, dodging, **~1s after dodge** (tunable), charging throw, **`IsAtChargeSpeed`** (charge tier). **Can** melee while walking backward
- [ ] **Dodge iframes:** melee hits respect same iframes as tackle
- [ ] **No hits** on victim while ragdolled or on post-stand-up tackle invincibility (same as tackle)
- [ ] **Friendly fire:** can hit teammates; **no** ult charge on FF knockdown (same as tackle). **All ults enemies-only** for v1 (no ult FF)
- [ ] **No** tackle comic on hit 1 — hitmarker only

#### Combat slice 1b — MP + tune

- [ ] 2-window verify; tune range, swing recovery, combo window, knockback, ragdoll impulse
- [ ] [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md) checklist — owner predict + host dedupe on confirm

### Combat slice 2 — parry (later)

- [ ] **Melee swings only** — **cannot** parry tackles (tackles stay premium)
- [ ] Successful parry → next melee confirm on that attacker = **1-hit** knockdown (within window — tune with slice 2)
- [ ] Not while holding ball (same as melee)

#### Ult slice 7 — **Weapons** (after combat slice 1)

- [ ] Per [`GAMEPLAY_DESIGN.md`](GAMEPLAY_DESIGN.md) → Weapons

---

### MP test checklist (ults — delete with this section when shipped)

| After slice | Verify |
|-------------|--------|
| **1** ✅ | % creeps Playing only; frozen celebration/intermission; goal/tackle bumps; FF tackle no bump; persists rounds; rematch 0%; HUD floor % + blue at 100% |
| **2a** ✅ | Commit, dash, knockdown, walk ramp; **2-window MP OK (2026-06-14)** |
| **2b** ✅ | Preview owner-only; release aim = dash direction; segmented ground telegraph v3 (**2026-06-30**). Hits = **contact + LOS**; preview may show through walls (**wall clip won't do**) |
| **2c** ✅ | Camera + hit recovery ✅; connect crunch + launch boom ✅; body freeze ✅; dash charge_run blend ✅; ult blue comic ✅; MP remote anims ✅; dash tuning signed off (**2026-06-16**) |
| **2d** ✅ | Solo + MP (practice patrol); soft ring skipped; spark sprites deferred (editor/publish); dash jump-over = tackle cylinder (**2026-06-30**) |
| **3** ✅ | Throw → teammate goal &lt; window; bounce / perfect catch; enemy grab / tackle void; relay A→B→C — **playtest OK (2026-06-30)** |
| **4** ✅ | Per-ult **`MaxChargePoints`** via **`IPlayerUlt`**; HUD still 0–100%; event awards unchanged; **`ResyncFromEquippedUltOnHost()`** ready for loadout — **playtest OK (2026-07-02)** |
| **Map 1** | OOB stop dwell, last-touch sky-drop, assist void, **`BallSpawn`** fallback if no credit |
| **Combat 1** | 2-hit unarmed LMB, walk/sprint only, carrier can’t attack, enemy KD +10, ult wind-up chip rules |
| **Combat 2** | Parry → 1-hit punish (melee only) |

---

## Ball carrier UX — shipped (2026-06)

`BallCompassHud` + `BallCarrierOutline` + `hold_R` + throw charge wind-up. Details → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) if needed.

---

## Known issues

- [ ] **Ragdoll arms z-fight (LEFT AS-IS 2026-06-23)** — ragdoll **body skin now matches** standing (fixed via `primaryRenderer.CopyFrom(baseVictimRenderer)` in `PlayerTackle.SpawnRagdollObject`, replacing `Model = …` which left the model's default skin → whole-black ragdoll). **Remaining:** the **arms flicker/z-fight** (black vs white) on the ragdoll. **Not caused by the last 15 commits** — ragdoll body-skin code dates to May 6–7 (`fa93cf2` / `8c2a2b04`); avatar LOD/cosmetics from May 13 (`1c97a9c`). **Leading untested theory:** citizen arms are a **separate bone-merged `SkinnedModelRenderer`** ("body extras merge to torso" per `CitizenAvatarLod`); `AddVictimClothingToRagdoll` clones that arms mesh on top of the body's own arm geometry → two overlapping arm meshes fight. Possible fix to try **only if revisited:** skip cloning the arms/body-part renderer (clone clothing only), or hide the body's arm body-group on the ragdoll — **do not** add a second full body mesh (that was the earlier z-fight cause). Max chose to **leave it** — current state is acceptable.
- [ ] **Tackle comic text** — Les Flos import + **2-window MP verify** (optional; exits good enough for v1)
- **Speed Blitz aim preview v3 ✅ (2026-06-30)** — segmented planes + `speed_blitz_preview.vmat`; width/length split; wall clip / comic edge / scroll grid **won't do**.
- [ ] **Tackle juice — moving victims** — idle soak **✅ (2026-06-14)**; **practice patrol runner MP ✅ (2026-06-23)** (`PracticeNpcPatrolPoseRelay` + contact freeze pin). Tier C1 lag-comp only if normal-ping misses still unfair.
- [ ] **Throw charge wind-up — MP verify + polish** — solo **✅ (2026-06-11)** + **plant / RMB cancel / cancel blend ✅ (2026-07-04)**. Remaining: 2-window MP check (remotes scrub via `NetThrowChargeLerp`); Blender clip tune if wanted; bone mask (see Open decisions).
- [ ] Throw strength still needs playtest tuning
- [ ] **Walk/run legs while charging throw** — move input blocked + planted (**2026-07-04**); legs may still locomote in place in animgraph (cosmetic only — not blocking)
- [ ] **Hold/throw anim MP verify** — post-throw stuck hold on remotes **fixed (2026-06-16)**; **carrier tackled → stuck hold on stand-up fixed (2026-07-05)** (`ClearHoldPoseAfterKnockdown`). Still verify charge wind-up scrub (`NetThrowChargeLerp`) + release + ball detach in 2-window MP
- [ ] **MP join visual glitch (host)** — brief **black mesh face** flash when client joins; stops when client leaves; likely joining player **`PlayerCosmeticsSync`** / human body before `ClothingContainer.ApplyAsync` (~0.25s delay) — **unconfirmed**; not caused by compass HUD (lines only)
- [ ] Need longer multiplayer playtests (15–20 min, two windows)
- [ ] **Spot-light shadow lines on ground (LEFT AS-IS — engine)** — thin camera-dependent black lines on pavement; **not** MP/post-process/game code. **Isolated:** `Spot Light` **Shadows off** on a lamp removes lines locally; hardness bump helps only a little. Likely same class as [s&box #10960](https://github.com/Facepunch/sbox-public/issues/10960) (shadow map cube edges). **Turf Wars** — street/station lamp spots; **practice arena** — mostly **Sun** cascades if lines appear there too. **Defer** until Facepunch patch or pre-ship pass; fallback = shadows off on non-hero lamps only.
- [ ] **Clutter** sometimes missing after **engine reload** — save scene after paint; check clutter **Volume** bounds; verify in **Play** (not only editor flycam)
- [ ] **Traffic engine loops** — seam click in-game vs clean Audacity preview; re-export with DC offset remove + zero-crossing trim if needed
- [ ] **Tackle ragdoll after ModelDoc changes** — if victims freeze ~8s with no flop (`ApplyRagdollLocally` in console but no physics ragdoll), try **editor reboot** or `utd_citizen_human_throw.vmdl` Save + Full Compile before chasing code. Stale compiled extension cache suspected (2026-06-11 — reboot fixed). Code fallback (spawn ragdoll on base `citizen_human_*`) deferred unless it returns.
- **Speed Blitz 2d client wind-up spark sprites:** **Joining client** (editor **Join via new instance**) shows **blue squares** instead of `vfx/spark_01.sprite` sparks; **dasher owner / host view OK**. Console: `spark_01.sprite_c` / `spark_01.png` **ERROR_FILEOPEN**. **Confirmed (2026-06-22):** s&box "Join via new instance" does not mount compiled texture/sprite assets (`.png_c`, `.sprite_c`) from the local project — sounds, code, and class data all work fine; textures/sprites specifically do not. Disabled scene prefab + direct sprite component workarounds do not fix this. **Not a game bug — will work correctly for real players on publish.** Editor-only limitation; use a publish test to verify VFX appearance on clients.

---

## For AI chats

Paste at the start of a new chat:

```
Read SESSION_NOTES.md → prefab split + loadout spec (2026-07-06, refined) → ult 5–6.
Loadout: LoadoutPersistence + PlayerLoadout [Sync]; always-equipped; Speedster preset; force-commit on round start; UltChargeHud always on.
Code 1–3 before editor prefab split. Not started until Max says go.
Do not edit .scene / .vmdl / .vanmgrph unless I explicitly say yes.
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-07-06 (loadout spec refined ✅):** **`LoadoutPersistence`** + **`PlayerLoadout`** `[Sync]` (not `PlayerTeam`); always-equipped; **Speedster preset** (not random); pending/committed + **force-commit** when round starts; class switch auto first ult/passive; string slug IDs; **`UltChargeHud` always on**; Jugg/Sniper pre-ult **approach A**; sumo match-start pick = same model later; code **1–3** then editor prefab split. → [§ Prefab split + loadout](#prefab-split--loadout--decided-2026-07-06).
- **2026-07-06 (prefab split + loadout spec ✅):** Locked hybrid spawn policy, per-class prefabs, Overwatch-style intermission/pre-match swaps (casual v1), practice free-swap, local save by SteamId, class change = host respawn, `MatchSetup` on rematch when that phase ships, build order (prefab+loadout before walkable room). → [§ Prefab split + loadout](#prefab-split--loadout--decided-2026-07-06).
- **2026-07-05 (defaults sync ✅):** Code `[Property]` defaults synced from **`throwdown_turf_wars.scene`** (player template, `main_ball`, Main Camera HUDs, `MatchHud`, traffic template/spawner) — new maps / auto-added components pick up Turf Wars tuning without re-inspector.
- **2026-07-05 (audio + tackle SFX + hold pose ✅):** **Global dry audio** — **`MatchAudioBootstrap`** + **`PlayerFootstepAudio`**; room sim off for outdoor Turf Wars. **Player tackle connect crunch** — **`TackleConnectImpactSoundA/B`** (host random, MP dedupe). **Ball carrier tackled** — **`ClearHoldPoseAfterKnockdown`** (no stuck `holditem` after stand-up). **2-window MP OK.**
- **2026-07-04 (dodge channel ✅):** Capped displacement + horizontal stop at end; air dodge (ledge-friendly). **`ShoveVelocityMultiplier` → `DodgeChannelDurationSeconds`** on prefab. **Solo playtest OK** (tune duration for snap).
- **2026-07-04 (ball throw + map slice 1 ✅):** **Throw charge** — planted wind-up (`IsThrowPlantLocked`, no jump/air steer); **RMB** cancel (`CancelActiveThrowCharge`); cancel pose ease (`NotifyThrowChargeCancelled` / `ChargeCancelBlendOutSeconds`). **OOB** — 2-window MP OK; stack pulse; host-authoritative `BallGrab`; invisible walls removed. **Next: prefab split.**
- **2026-07-03 (map slice 1a ✅ + editor):** Turf Wars **`OutOfBoundsZone`** + **`playerclip`**; core OOB loop shipped.
- **2026-07-02 (slice 4 ✅ playtest + OOB spec):** Per-ult **`MaxChargePoints`** shipped. **Map slice 1** specced — OOB zones, stop dwell, last-touch sky-drop, **`BallSpawn`** fallback only; ship **before prefab split**.
- **2026-06-30 (aim preview v3 ✅):** Segmented `plane.vmdl` + `speed_blitz_preview.vmat`; **`PlaneWidthBaseSize`** / **`PlaneLengthBaseSize`** (width ~175 playtest, length 100); wall/comic/grid clip **won't do**. **Blitz jump-over** = `TryValidateContactCylinder`. Spot-light shadow lines deferred (#10960).

Older detail → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
