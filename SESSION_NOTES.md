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

**Goal:** **Slice 2d** — **solo ✅ (2026-06-18)**. **2-window MP:** partial — **known engine limitation:** joining client sees blitz wind-up **spark sprites as blue squares** (texture/sprite assets don't mount in editor "Join via new instance"; sounds/code/data work fine). **Not fixable in editor — will work on publish.** Other 2d MP (pose, glow, SFX, etc.) not fully signed off yet.

**Next session (priority order):**
1. **2-window MP on moving patrol targets** — idle **`practice_npc`** knockdown visuals **✅ (2026-06-23)**; verify patrol runner tackle feel in MP, then Tier B tuning / Tier C1 lag-comp
2. **Later:** soft ring; aim preview art v3 + **wall/LOS clip** (preview still shows corridor through walls — hits use contact rules)

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
- **Speed Blitz (slice 2a/2b/2c ✅)** — **`SpeedsterSpeedBlitzUlt`** + owner **`SpeedBlitzAimPreview`** (blue `#24b0ff` tint); hold X → corridor preview; release → commit; **2c shipped (2026-06-16):** hang, dash cam, connect/launch SFX, **`ComicBurstPalette.Ult`** on launch, ball-strip on carrier connect (intentional), MP remote wind-up/throw anim fix; dash range/speed/feel signed off at current prefab values. **Dash hits (2026-06-22):** **physical contact** only — 3D body-radius touch + **`MaxHitVerticalSeparation`** (default **56**) + line-of-sight trace; **no corridor teleport** through walls/roofs; stop at actual dash position. Corridor / preview = coarse aim filter only.
- **Speed Blitz wind-up feel (slice 2d — solo ✅ 2026-06-18)** — **`SpeedBlitzWindUpFeel`**: **`speedblitzwindupvfx`** **wind-up only** (off dash / connect hang); **`speedblitzdischargevfx`** on **dasher chest** at ragdoll launch (hit only). **`SpeedBlitzBodyGlow`** + render system: tint + point light (`GetWindUpLerp()` ramp → peak → discharge); **point light destroyed on end** (remote host-dasher fix **2026-06-22**). **`PlayerSpeedBlitzWindUpAnim`**: masked **`speedblitz_windup`** via `blitz_windup` / `blitz_windup_weight` while **`IsWindUp`** (synced). **SFX:** electric hard stop at connect, windup rise, dash woosh — **`PlayFeelSoundAt`**; **client dasher connect crunch on predict** (host broadcast dedupes — **2026-06-22**). **Connect hang timing:** pre-launch pause runs **parallel** with ragdoll body init — launch aligns with pose unfreeze (**2026-06-22**). **MP:** owner/host wind-up looks OK; **joining client spark sprites = blue squares** (engine limitation — publish only)
- **Owner cameras (2026-06-15)** — **`PostCameraSetup`** for all owner FOV (PC resets preference FOV every frame). **`ThrowChargeCamera`** `[Order(10002)]`: charge offset + release blend after ball leaves hand (transition-frame hold — no pop). **`SpeedBlitzDashCamera`** `[Order(10012)]`: idle must **not** stomp **`CameraOffset`** (throw owns offset). **`TackleImpactFeel`**: blitz attacker uses overrides — hitstop freezes **world pose only**; dash cam eases during freeze; no blitz attacker offset/FOV punch (recovery blend owns it). Player tackles unchanged.
- **MP combat feel predict** — **`CombatFeelPredictDedupe`** (auto on join): client-owner early **`TackleImpactFeel`** for blitz dash, tackle connect, victim freeze (tackle/blitz), traffic ragdoll; host **`NetCombatFeelApplyId`** dedupe. Details → [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md). **2–3 window idle-target soak OK (2026-06-14)**; moving-target fairness → practice moving dummies + Tier C1 later.
- **Practice arena (`practice_arena.scene`) ✅ (2026-06-22)** — **`MapMatchConfig.PracticeArenaMode`**: unlimited clock/goals, all joiners team **0** + team-0 spawns only, **no top score/clock HUD**. **`PracticeLaunchMeasure`** on **`PracticeLaunchLane`** (origin = first line at NPC feet; local **Y** down lane; **`BandPitch` 128** → score **1, 2, 3…** from max pelvis **`along`**). **`PracticeLaunchReadout`** on **`LaunchReadoutSign`** TV. Three static **`practice_npc`** dummies + ruler art (editor). **`PracticeNpcPatrol` + `PracticeNpcPatrolHostState` ✅ (2026-06-23):** host ping-pong **Point A ↔ Point B** at charge speed, instant 180°, knockdown pause + pre-hit snap-back resume; **can tackle player** (`TryGetHostTackleMove`) and **be tackled**; **`PlayerBallHoldAnim`** required for forked graph + **`charge_run`** overlay. **Run legs ✅ (2026-06-23):** animgraph needs **`move_x`** (local forward, positive) + **`move_groundspeed`** — `move_groundspeed` alone stays in idle; `move_y` = 0. **Tackle power + clean launch ✅ (2026-06-23):** NPC freezes for tackle cooldown duration (`IsInTackleCooldown`) so player gets clean launch impulse; `TackleChargeBonus = 0` (class ramp, same as player); `PatrolTackleChargeBonus` API on abstract state for override. **Practice NPC MP knockdown visuals ✅ (2026-06-23):** scene dummies stay **`NetworkMode.Snapshot`** — **do not `NetworkSpawn` player-prefab NPCs**; host **`PracticeNpcClient*Rpc`** via network-spawned player hides standing body on clients. Static dummies: **`CatchUpSpeedBoost`** ignores global Input; host tackle detect patrol-only; **`PlayerCosmeticsSync`** off on tag. Do **not** enable **`PlayerController`** on runner (Update exceptions).

**Before ship (optional):** Uncheck **`Enable Debug Force Goal`** on `MatchDirector` in scene if you don’t want `,` testing in builds (already **off** by default in code).

**Still later:** Tackle tuning, map vote (30s, all players, `Slot1`–`N`).

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
| `Code/Ball/` | Ball pickup, throw, charge bar, trajectory preview (`ThrowReleaseMath`), **`BallCarrierOutline`**, smooth ball on clients |
| `Code/Player/` | Movement, dodge, tackle, **`CombatFeelPredictDedupe`**, team, class, cosmetics, **`PlayerBallHoldAnim`**, **`PlayerChargeRunAnim`**, **`PlayerSpeedBlitzWindUpAnim`** (2d), **no crouch** |
| `Code/Network/` | Spawning players when people join |
| `Code/Match/` | `MatchDirector`, `GoalZone`, **`MapMatchConfig`** (`PracticeArenaMode` on practice map only) |
| `Code/Ultimates/` | **`PlayerUltCharge`** (slice 1); **`SpeedsterSpeedBlitzUlt`** (2a–2c); **`SpeedBlitzWindUpFeel`**, **`SpeedBlitzBodyGlow`** (2d) |
| `Code/UI/` | Match HUD + owner HUDs + **`UltChargeHud`** + **`BallCompassHud`** + **`TackleComicTextHud`** / **`TackleComicBurst`** + **`PracticeLaunchReadoutRoot`** / **`PracticeLaunchScorePanel`** |
| `Code/Map/` | `StartupMapBootstrap` (practice NPC locks); **`PracticeLaunchMeasure`** / **`PracticeLaunchReadout`**; **`PracticeNpcPatrol`**; **`StreetLightFlicker`**; **`StationLightFlicker`**; **`TrafficSpawner`** / **`TrafficCar`** |

**Scenes:** `scenes/throwdown_turf_wars.scene` (Turf Wars WIP) · **`scenes/practice_arena.scene`** (training — enable **`PracticeArenaMode`**) · `throwdown_prototype.scene` = greybox fallback.

**Important:** AI should **not** edit `.scene`, `.vmdl`, `.vanmgrph`, or other editor-owned assets unless you **explicitly give permission** — see `.cursor/rules/editor-asset-ownership.mdc`. Give steps; you wire in the s&box editor.

---

## Multiplayer gotcha (match flow)

`MatchDirector` is on **Main Camera** — each machine has its own copy. **Clients do not** use it for freeze/HUD/score.

**Authoritative on clients:** synced fields on **`PlayerTeam`** (on each network-spawned player). Host pushes via `MatchDirector.PushMatchHudStateToPlayers()`.

---

## Multiplayer feel & netcode

**Read [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md)** when changing host RPCs, `[Sync]`, owner-driven movement, combat hits, ragdolls, or **adding new combat features**. That doc covers: host authority vs client-side prediction (feel only), reconciliation, priority order (now → per-feature → tuning → late dev), and the **new feature checklist**.

**Tier 0–A3 + A2b ✅ (2026-06-14):** Client predict for blitz dasher, tackle attacker, victim freeze, traffic ragdoll; **`CombatFeelPredictDedupe`**. **Next netcode:** Tier B tuning / Tier C1 lag-comp after **moving** practice dummies — see [`MULTIPLAYER_NETCODE.md`](MULTIPLAYER_NETCODE.md).

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
- **Walk into the ball = pick it up.** No kick button. While held, ball follows **`hold_R`** on **Body** `SkinnedModelRenderer` (`BallGrab.HoldBoneName`; falls back to `HoldAnchor`). Old `HandHoldPoint` + `citizen_holdball_test` IK was for classic citizen — human uses bone attach.
- **Ball carrier hold/throw anim (v1):** **`PlayerBallHoldAnim`** — **`holditem`** + **RH** while holding; on release **`b_attack`** (built-in medium throw). **`ThrowPoseHoldSeconds`** / **`ThrowPlaybackRate`**; **`BallThrow.ThrowReleaseDelaySeconds`** delays ball velocity. Charge = masked **`throw_windup`** layer on forked **`utd_citizen_human_m.vanmgrph`** (`throw_charge` / `throw_charge_weight`; body alive). Sequences on **`utd_citizen_human_throw.vmdl`**. Auto-added on network spawn.
- **Online: the host is the referee** — clients request; host decides.
- **Tackles:** Only at full charge speed (`NetAtChargeSpeed`). Host ragdoll + client **request** RPC. **Attacker on connect → walk ramp reset** (all classes). **`PreLaunchPauseSeconds` > 0:** **`NetAwaitingRagdollLaunch`** — victim **visible + frozen**, then impulse + ragdoll. **Client victim/attacker feel predict** (Tier A) — host still owns knockdown. **`CombatFeelPredictDedupe`** dedupes host feel RPCs.
- **Charge run overlay:** **`PlayerChargeRunAnim`** drives graph params when **`IsAtChargeSpeed`** (synced) — not owner-only ramp HUD.
- **Dodge:** Double-tap A or D. Tackle iframe only.
- **Ragdoll / knockdown:** **Walk** ramp resets **on knockdown** (`TriggerForceWalkRampOnHost` + local snap of `smoothedMoveSpeedCap` in `CatchUpSpeedBoost`); ramp timers frozen while down. **✅ Working.**
- **Charge tier + W+S:** **✅ Fixed** — `ApplyMutuallyExclusiveForwardBackwardInput` patches `AnalogMove.x` (not `.y`); `[Order(-100)]` + `OnFixedUpdate` so `PlayerController` sees mutex before movement.
- **Crouch:** Disabled — do not rebind `Duck` without re-enabling intentionally.
- **Test dummies:** Tag `practice_npc` on **dummies only**. Scene-placed dummies = **`NetworkMode.Snapshot`** (transform only) — **`[Sync]` on `PlayerTackle` does not replicate**; MP knockdown hide/show = host **`PracticeNpcClientFreezeRpc` / `PracticeNpcClientRagdollRpc` / `PracticeNpcClientStandUpRpc`** on a network-spawned player. **Never `NetworkSpawn` player-prefab practice NPCs** — host-owned spawn caused counter-tackle (shared Input), Main Camera hijack, and host cosmetics on dummies. Static dummies must not run host tackle detect (patrol runners only). **`PlayerCosmeticsSync`** disabled on tag.
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
14. **Speed Blitz 2d (MP):** Olympic pose + body glow + discharge + SFX on remotes; electric cut + dash woosh; no sparks on dash/connect/miss. **Dash hits:** wall/roof blocks — no teleport hit. **Client dasher:** connect crunch on predict. **Joining client wind-up spark sprites = blue squares** (editor limitation — publish only).
15. **Practice arena NPCs (MP):** client (or host) tackles idle **`practice_npc`** — standing body hides, ragdoll only, stand-up snap-back; host solo must **not** self-launch. Patrol runner MP feel still open.

**Ball jittery on client only?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Client free-ball jitter”.

**Client tackle looks short or late?** → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) → “Ragdoll (technical)”. Don’t re-add `StartAsleep` or mute collision sounds without waking bodies — broke launch (2026-05-18).

---

## Multiplayer gotcha (practice NPCs)

- Scene **`practice_npc`** dummies use the **player prefab** but are **not** network-spawned players — default **`NetworkMode.Snapshot`** in scene.
- **`[Sync]` knockdown state does not reach clients** — host ragdoll spawns via `NetworkSpawn`, but standing body hide is host-only unless **`PracticeNpcClient*Rpc`** broadcast runs (from a network-spawned player's `PlayerTackle`).
- **Do not `NetworkSpawn` scene practice NPCs** to “fix” sync — host ownership on a player-prefab dummy: **`CatchUpSpeedBoost`** reads the host's global **Input** (counter-tackle), **`Network.IsOwner`** steals **Main Camera** on NPC ragdoll, **`PlayerCosmeticsSync`** applies host outfit.
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
- **`SpeedsterSpeedBlitzUlt`** — **Speedster only** (class gate). **Add on Speedster prefab** (not auto-spawned). Tune `WindUpDurationSeconds` (default **2**), `DashRange`, `DashSpeed`, `HitHalfWidth`, **`MaxHitVerticalSeparation`** (default **56**), `DefaultTargetBodyRadius`, `KnockdownLaunchSpeed`, `KnockdownLaunchArc`. **Knockdown feel:** `KnockdownPreLaunchPauseSeconds` (**0.65**), **`ConnectImpactChargeRunCycle`**, **`LaunchSound`**, **`ConnectImpactSoundA/B`**, volumes, impact feel fields; **`DischargeVfxPrefab`** → `speedblitzdischargevfx.prefab`, **`DischargeVfxLocalOffset`**, **`DischargeVfxCleanupSeconds`**. **Wind-up feel (2d):** **`WindUpVfxPrefab`** → `speedblitzwindupvfx.prefab`; electric/rise/dash **`SoundEvent`**s; offsets, volumes, **`MissVfxFadeSeconds`**. Auto-adds **`SpeedBlitzDashCamera`**, **`SpeedBlitzWindUpFeel`**, **`SpeedBlitzBodyGlow`**. Optional **`Enable Speed Blitz Debug Logs`**.
- **`SpeedBlitzAimPreview`** — **Speedster only**, same prefab as ult (manual). Owner corridor while holding X; default **`CorridorTint`** / **`MarkerTint`** = ult blue (`#24b0ff`); tune `CorridorAlpha`, `SegmentSpacing`, **`MarkerModelBaseSize`** (default **80** on Speedster prefab).
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

**Practice arena (`practice_arena.scene` only):**
- **`MapMatchConfig`** — **`Practice Arena Mode`** on; **`Practice Spawn Team Id`** = **0**
- **`GameNetworkManager`** — **`Team0Spawns`** wired; clear team-1 spawns optional
- **`PracticeLaunchLane`** empty — back edge of first line (NPC feet); local **Y** = down lane; **`PracticeLaunchMeasure`** → **`ReadoutSign`** = **`LaunchReadoutSign`**
- **`LaunchReadoutSign`** — **`PracticeLaunchReadout`** (+ optional **`WorldPanel`**); tune **`Panel Size`**, **`Score Font Size`**, **`Score Font Family`**, **`Score Color`**
- Three **`practice_npc`** dummies (player prefab); ruler lines every **128** (12 line + 116 gap); gap labels **1, 2, 3…** (visual only)
- One **`GoalZone`** if testing scoring — **`Defending Team`** = **1** when all players are team **0**
- **Moving runner dummy (separate section):** duplicate player prefab → tag **`practice_npc`** → **Speedster** `PlayerClass` → **`PlayerController` disabled** → add **`PlayerBallHoldAnim`** (forked graph) + **`PracticeNpcPatrol`** with **Point A** / **Point B** empties. Optional: wire **`BodyRenderer`** on patrol. **Do not** enable PC on runner.

---

## Open decisions (not chosen yet)

- **Player body for v1:** **`citizen_human_*`** (branch tested) vs classic **`citizen.vmdl`** — leaning **human** (audience + looks good); citizen fits chaotic meme tone. No custom rig (account cosmetics).
- Closed roof on arena vs open roof + sun for lighting
- **Tackle oof/grunt** — layered on built-in ragdoll collision audio (not shipped)
- **Practice patrol zigzag** — optional intermediate waypoints between A/B (straight ping-pong only for now)
- **Practice arena party MP (later):** clients in **`practice_arena`** with scene **`practice_npc`** — today = Snapshot dummies + host **`PracticeNpcClient*Rpc`** for knockdown visuals; **not** `NetworkSpawn` on player-prefab scene NPCs. Future party-up may need host-spawned dummy clones or a stripped non-player prefab — decide before expanding beyond broadcast-RPC visuals.
- Map vote: allow changing vote during the 30s window?
- **Traffic knockdown tuning:** **`KnockdownLaunchSpeed`** / hit box vs dodgeability
- **Ball compass polish:** optional distance readout on `BallCompassHud`
- **Charge wind-up bone mask choice:** `Blend_UpperBody_HalfSpine_FullArms` (arm + some spine lean, smoother) vs `Only_RightArm` (strictly arm) on the graph's Bone Mask node — pick whichever looks better in playtest
- **Hero asset art:** maps/props low poly; **players + ball** may get higher-detail models later — ball on **`ball_v2.vmat`** (emissive gold + scroll) for now; **leaning white ball** later (fits blue ult VFX). `BallCarrierOutline` still copies ball material for carry breathe
- **Comic word scope:** tackles/knockdowns only for v1; **ults** (+ weapon KOs later) get own burst — not throws/dodges. **Ult palette ✅:** `ComicBurstPalette.Ult` — blue fill (`#24b0ff`) + pale cyan highlight; Speed Blitz spawns **after ragdoll launch** (not at connect hang). Future ults pass `ComicBurstPalette.Ult` to `NotifyHostKnockdown`.
- **Ult charge point values** — goal / tackle / passive rates TBD in playtest (`PlayerUltCharge` inspector defaults are placeholders).
- **Charge tier + backward (S) while W held:** **✅ Fixed** — mutex was writing forward/back on `AnalogMove.y` (strafe axis); s&box uses `.x` for forward/back. W wins when both held.
- **Speed Blitz ball strip on connect:** During blitz connect hang, **`BallGrab`** on the victim can lose the ball to the dasher if pickup overlap runs — **intentional** (reward for hitting carriers); not a bug to remove without design pass.
- **Speed Blitz aim preview v3 (2c+):** replace dev-box corridor/end with **custom translucent blue `.vmat`** + **`Model.Plane`** or **`DecalRenderer`** (SMITE-like ground telegraph); Max authors material in editor. **Also:** clip preview to **walls/LOS** so corridor matches contact hit rules (hits already physical-only — **2026-06-22**).
- **Speed Blitz 2d electric at connect:** **✅ Shipped:** electric **SFX** hard stop at connect crunch; wind-up **VFX** off dash/connect/hang (**2026-06-18**).
- **Speed Blitz wind-up VFX scope:** **✅ Shipped (2026-06-18):** **`speedblitzwindupvfx`** **wind-up only** — not dash/connect/hang.
- **Speed Blitz body glow:** **✅ Shipped (2026-06-18):** **`SpeedBlitzBodyGlow`** — tint + point light on dasher; discharge at launch; no victim glow; no ult outline (enemy red outline unchanged).
- **Speed Blitz launch discharge VFX:** **✅ Shipped (2026-06-18):** **`speedblitzdischargevfx`** on dasher chest at ragdoll launch (hit only); tune in prefab + **`DischargeVfxLocalOffset`** on ult.
- **Speed Blitz impact stride `charge_run_cycle`:** snap dasher to a fixed cycle at connect (shoulder-in frame) vs freeze whatever pose contact landed on — scrub `charge_run` in ModelDoc; inspector default TBD in playtest.
- **Speed Blitz victim flinch (later):** optional masked hit-react clip + graph layer during hang (same pattern as `throw_windup` / `charge_run`) — polish on top of body freeze v1; ship or skip after playtest
- **Player prefab component count (3 classes):** **✅ Chosen: Option A — per-class prefab variants** before slice 5/6 (`Player_Speedster` / `Player_Juggernaut` / `Player_Sniper`; `GameNetworkManager` picks template by class). Not doing yet — see [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6 + roadmap note below. Move **`BlitzConnectPoseFreeze`** off global auto-add when splitting.
- **Juggernaut post-tackle run recovery (passive slot):** optional passive keeps **run** (sprint tier, not charge) after landing a tackle instead of walk reset — bundle with loadout UI; mutually exclusive with **tackle ramp bonus** passive.
- **Speed Blitz 2d — client wind-up spark sprites (MP):** editor join-client shows **blue squares**; owner OK. **Confirmed engine limitation (2026-06-22):** s&box "Join via new instance" does not mount compiled texture/sprite assets; sounds/code/data work fine. Not fixable in editor — will work on publish.

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

#### Slice 2d — Speed Blitz **electric charge** polish (VFX phases + SFX + Olympic pose) — **PARTIAL ✅ (solo 2026-06-18)**

**Shipped solo:** **`speedblitzwindupvfx`** (wind-up sparks only) + **`speedblitzdischargevfx`** (launch burst) + **`SpeedBlitzWindUpFeel`**; **`SpeedBlitzBodyGlow`** + render system; electric SFX hard cut at connect; **`PlayFeelSoundAt`** wind-up sounds; **`speedblitz_windup`** animgraph layer + **`PlayerSpeedBlitzWindUpAnim`**. **Solo sign-off ✅.**

**Still open:**

- [x] **Electric hard cut at connect** (SFX)
- [x] **Wind-up VFX only** (no dash/connect/hang sparks)
- [x] **Launch discharge VFX** — dasher chest at ragdoll launch
- [x] **Body glow** — subtle tint + point light; discharge at launch
- [x] **Wind-up pose** — `speedblitz_windup` + `blitz_windup` / `blitz_windup_weight` + **`PlayerSpeedBlitzWindUpAnim`**
- [ ] **2-window MP** verify remaining 2d phases (pose/glow/SFX OK on remotes; **client wind-up spark sprites deferred** — blue squares)
- [ ] **Optional:** soft ring

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

#### Slice 3 — assist charge

- [ ] Ball possession / pass history on host
- [ ] On goal: credit assist passer per void rules (enemy touch, chain pass)
- [ ] Point value **TBD**

#### Slice 4 — per-class charge max (balance pass)

- [ ] `maxPoints` per class/ult (e.g. Juggernaut stomp 150, Speed Blitz 100)
- [ ] Display still 0–100%; same universal event point awards

**Before slice 5/6 (prefab split — not blocking 2d–4):** **→ Read [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6 first** (spawn policy, tackle/ult splits, prefab checklist). Then: split shared player into **per-class prefab variants** (Option A). Duplicate shared components (`PlayerTackle`, `BallGrab`, `PlayerUltCharge`, HUDs, …) on each; **class-only** ult + preview + wind-up VFX live only on that class prefab. Update **`GameNetworkManager`** to spawn the template matching **`PlayerClass`**. Remove Speedster-only **`BlitzConnectPoseFreeze`** from global auto-add — keep on Speedster prefab or `GetOrCreate` from blitz ult. Do this **before Juggernaut + Sniper** land, not required for Speed Blitz 2d.

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
| **2b** ✅ | Preview owner-only; release aim = dash direction; corridor shows max range (**2026-06-14**). Hits = **contact + LOS** since **2026-06-22** — preview may still show targets behind walls until clip pass |
| **2c** ✅ | Camera + hit recovery ✅; connect crunch + launch boom ✅; body freeze ✅; dash charge_run blend ✅; ult blue comic ✅; MP remote anims ✅; dash tuning signed off (**2026-06-16**) |
| **2d** | Solo ✅ (2026-06-18): sparks, glow, discharge, SFX, **Olympic pose**. MP partial — **client wind-up spark sprites = blue squares** (deferred); verify rest when revisiting |

---

## Ball carrier UX — shipped (2026-06)

`BallCompassHud` + `BallCarrierOutline` + `hold_R` + throw charge wind-up. Details → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md) if needed.

---

## Known issues

- [ ] **Tackle comic text** — Les Flos import + **2-window MP verify** (optional; exits good enough for v1)
- [ ] **Speed Blitz aim preview vs hits** — hits use contact + LOS; dev-box **corridor preview** may still show enemies through walls / wrong floor until wall-clip pass (bundled with aim preview v3)
- [ ] **Tackle juice — moving victims** — predict tested on **idle** targets only (2026-06-14); patrol runner run legs + clean tackle launch fixed ✅ — **idle practice NPC MP knockdown visuals ✅ (2026-06-23)**; still verify **patrol runner** 2-window MP feel + Tier C1 lag-comp
- [ ] **Throw charge wind-up — MP verify + polish** — ✅ **WORKS solo (2026-06-11)**: masked layer in forked graph `utd_citizen_human_m.vanmgrph`; body keeps locomotion/look-at while arm winds up. Remaining: 2-window MP check (remotes scrub via `NetThrowChargeLerp`); improve the wind-up clip in Blender if wanted (overwrite `throw_windup.fbx` — see workflow doc "Iterating on a clip"); pick final bone mask (see Open decisions).
- [ ] Throw strength still needs playtest tuning
- [ ] Walk/run animations while charging throw (legs still locomote in place — `PlayerBallHoldAnim` does not fix; charge blocks move input)
- [ ] **Hold/throw anim MP verify** — post-throw stuck hold on remotes **fixed (2026-06-16)**; still verify charge wind-up scrub (`NetThrowChargeLerp`) + release + ball detach in 2-window MP
- [ ] **MP join visual glitch (host)** — brief **black mesh face** flash when client joins; stops when client leaves; likely joining player **`PlayerCosmeticsSync`** / human body before `ClothingContainer.ApplyAsync` (~0.25s delay) — **unconfirmed**; not caused by compass HUD (lines only)
- [ ] Need longer multiplayer playtests (15–20 min, two windows)
- [ ] **Clutter** sometimes missing after **engine reload** — save scene after paint; check clutter **Volume** bounds; verify in **Play** (not only editor flycam)
- [ ] **Traffic engine loops** — seam click in-game vs clean Audacity preview; re-export with DC offset remove + zero-crossing trim if needed
- [ ] **Tackle ragdoll after ModelDoc changes** — if victims freeze ~8s with no flop (`ApplyRagdollLocally` in console but no physics ragdoll), try **editor reboot** or `utd_citizen_human_throw.vmdl` Save + Full Compile before chasing code. Stale compiled extension cache suspected (2026-06-11 — reboot fixed). Code fallback (spawn ragdoll on base `citizen_human_*`) deferred unless it returns.
- **Speed Blitz 2d client wind-up spark sprites:** **Joining client** (editor **Join via new instance**) shows **blue squares** instead of `vfx/spark_01.sprite` sparks; **dasher owner / host view OK**. Console: `spark_01.sprite_c` / `spark_01.png` **ERROR_FILEOPEN**. **Confirmed (2026-06-22):** s&box "Join via new instance" does not mount compiled texture/sprite assets (`.png_c`, `.sprite_c`) from the local project — sounds, code, and class data all work fine; textures/sprites specifically do not. Disabled scene prefab + direct sprite component workarounds do not fix this. **Not a game bug — will work correctly for real players on publish.** Editor-only limitation; use a publish test to verify VFX appearance on clients.

---

## For AI chats

Paste at the start of a new chat:

```
Read SESSION_NOTES.md → practice_arena ✅; PracticeNpcPatrol ✅; practice_npc MP idle knockdown visuals ✅ (2026-06-23) — do NOT NetworkSpawn scene dummies. Speed Blitz 2d solo ✅; MP partial — client wind-up spark sprites blue squares (editor/publish only). MULTIPLAYER_NETCODE.md for net/combat work.
Match flow slices 1–6 done. MP combat predict Tier 0–A3 + A2b shipped. PracticeNpcPatrolHostState + PracticeNpcPatrol; PlayerBallHoldAnim required on runner; PC disabled on dummies; PlayerCosmeticsSync off on practice_npc.
Do not edit .scene / .vmdl / .vanmgrph unless I explicitly say yes.
```

**Undecided list:** Add bullets under **Open decisions** when we postpone a choice; remove when settled.

---

## Recent session notes

- **2026-06-23 (practice NPC MP — idle knockdown ✅):** Client tackle on static **`practice_npc`** — standing body stayed visible (double dummy) because scene dummies are **`NetworkMode.Snapshot`** (`[Sync]` never replicates). **Rejected:** host **`NetworkSpawn`** on player-prefab NPCs — host self-launch (shared Input → counter-tackle), Main Camera hijack, host cosmetics on all dummies. **Shipped:** host **`PracticeNpcClient*Rpc`** broadcast via network-spawned player (`MirrorPracticeNpc*` on clients); static dummies skip global Input in **`CatchUpSpeedBoost`**; **`CanUseHostTackleDetection`** patrol-only; **`PlayerCosmeticsSync`** disabled on tag; practice NPCs never drive owner knockdown camera.
- **2026-06-23 (practice patrol — full ✅):** Run legs fixed — animgraph needs **`move_x` = speed** (positive = forward in this graph) + `move_groundspeed`; `move_groundspeed` alone stays idle. Tackle launch fixed — NPC was continuously shoving player after hit; `IsInTackleCooldown` added to `PlayerTackle` and checked in `IsMovementBlocked()` so NPC freezes for cooldown duration → clean impulse. `TackleChargeBonus` default **0** (class ramp, same as player); `PatrolTackleChargeBonus` abstract API for override. **`PracticeNpcPatrol`** + **`PracticeNpcPatrolHostState`** — A↔B host teleport, charge tier + **`charge_run`** via **`CatchUpSpeedBoost`** / **`PlayerChargeRunAnim`**; NPC can tackle player. **`PlayerBallHoldAnim`** required on runner. PC stays disabled.
- **2026-06-22 (practice arena):** **`practice_arena.scene`** + **`PracticeArenaMode`**, **`PracticeLaunchMeasure`** / readout, static **`practice_npc`** + ruler.
- **2026-06-22 (tackle attacker ramp):** regular tackle connect → **`TriggerForceWalkRampOnHost`** for attacker (all classes); removed sprint-only **`TackleStripRamp`** path.
- **2026-06-22 (Speed Blitz polish):** contact-only dash hits + client predict connect crunch + hang/body-init timing + remote body-glow light cleanup.

Older detail → [`SESSION_NOTES_ARCHIVE.md`](SESSION_NOTES_ARCHIVE.md).
