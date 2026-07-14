# Component checklist — manual editor wiring

**Policy (since 2026-07-07):** gameplay components live on prefabs and scene objects in the editor. Code uses `ComponentRequire` and logs when something is missing — it does **not** silently add prefab-owned components at runtime.

**Use this doc when:** setting up a new scene, duplicating a class prefab, after a merge/hotload weirdness, or when something “used to work” with no code change.

**Canonical reference scenes:** `Assets/scenes/throwdown_turf_wars.scene` (full match), `Assets/scenes/practice_arena.scene` (practice + NPCs + launch lane).

**Related:** [`ARCHITECTURE.md`](ARCHITECTURE.md) § Component wiring · [`NAMING_CANON.md`](NAMING_CANON.md) · [`no-auto-add-components.mdc`](.cursor/rules/no-auto-add-components.mdc) · [`component-checklist.mdc`](.cursor/rules/component-checklist.mdc) (agent rule — update this doc when adding manual components)

---

## Quick verify after join / spawn

1. Play → join or spawn a player → watch the console for `[ComponentRequire]` / `GameNetworkManager.Spawn` warnings.
2. On **Main Camera** startup, GNM also warns if camera/ball HUD pieces are missing (warn only — still add them manually).
3. Cross-check your prefab against the tables below — **not every required component logs on spawn** (see § Spawn warnings).

---

## Player class prefabs (`Player_Speedster`, `Player_Juggernaut`, `Player_Sniper`)

Templates sit under **`PlayerTemplateRoot`** (disabled in scene). GNM clones the template matching committed loadout class.

Wire all three templates unless noted. **Reference:** Turf Wars scene has the complete set; **`practice_arena.scene` is currently missing `BallClientFeel` on all three templates — add it there too.**

### Standard citizen engine stack (root)

| Component | Notes |
|-----------|--------|
| `Sandbox.PlayerController` | Wire **Body** → root `Rigidbody`, **Renderer** → `Body` child `SkinnedModelRenderer`, **ColliderObject** → collider child |
| `Sandbox.Rigidbody` | On root |
| `Sandbox.Movement.MoveModeWalk` | |
| `Sandbox.Movement.MoveModeSwim` | |
| `Sandbox.Movement.MoveModeLadder` | |
| `Sandbox.Dresser` | **BodyTarget** → `Body` `SkinnedModelRenderer` |

### Required child objects (hierarchy)

| Child | Purpose |
|-------|---------|
| **`Body`** | `SkinnedModelRenderer` — citizen model + animgraph |
| **Collider** (name varies) | `CapsuleCollider` + `BoxCollider` — referenced by `PlayerController.ColliderObject` |
| **`HoldAnchor`** (empty GO) | Fallback hold point — drag onto **`BallGrab.HoldAnchor`** |

### Shared gameplay — all class prefabs (37 custom components)

Add every row on **Speedster, Juggernaut, and Sniper** roots:

| Component | If missing / broken | Spawn warning? |
|-----------|---------------------|----------------|
| **`BallGrab`** | No pickup; no carrier state | No |
| **`BallThrow`** | No throw / charge | No |
| **`BallClientFeel`** | **Client throw looks like a drop** (ball rigidbody re-enabled locally); free-ball jitter | **No** — high priority |
| **`ThrowChargeBar`** | No charge bar UI | No |
| **`ThrowTrajectoryPreview`** | No throw arc preview | No |
| **`ThrowChargeCamera`** | No charge camera pull-back / FOV | No |
| **`PlayerBallHoldAnim`** | No hold/charge/release anim; wire **BodyRenderer** → `Body` | Yes |
| **`PlayerClass`** | Wrong scale/stats; set **CurrentClass** to class `.cdata` | No |
| **`PlayerCosmeticsSync`** | Workshop clothes / LOD issues on clients | No |
| **`CatchUpSpeedBoost`** | No movement ramp / charge speed | No |
| **`PlayerChargeRunAnim`** | No charge-run legs overlay | Yes |
| **`MovementRampHud`** | No movement ramp HUD | No |
| **`PlayerTackle`** | No tackle | No |
| **`TackleImpactFeel`** | No tackle hitstop / camera punch | Yes |
| **`RagdollClientFeel`** | Ragdoll pelvis stutter on clients | No |
| **`PlayerDodge`** | No dodge | No |
| **`DodgeCooldownHud`** | No dodge recharge UI | No |
| **`PlayerDisableCrouch`** | Crouch still available | Yes |
| **`PlayerEnemyOutline`** | No red enemy outline (needs camera `Highlight` — see Main Camera) | Yes |
| **`PlayerUltCharge`** | Ult never charges | No |
| **`UltChargeHud`** | No ult charge bar | No |
| **`PlayerTeam`** | Match/OOB/team gates break; spawn may fail | No (hard require at spawn) |
| **`PlayerLoadout`** | Loadout / class swap broken | No (hard require at spawn) |
| **`BallCompassHud`** | No ball compass | Yes |
| **`CombatFeelPredictDedupe`** | Double SFX/VFX on owner predict | Yes |
| **`PlayerFootstepAudio`** | No footsteps | Yes |
| **`PracticeNpcPatrolPoseRelay`** | Patrol dummy pose desync on clients | Yes |
| **`LoadoutClientState`** | Q loadout picker state broken | Yes |
| **`LoadoutPickerHud`** | No loadout picker UI | Yes |
| **`TackleRagdollLifecycle`** | Knockdown / stand-up broken | Yes |
| **`TackleImpactRelay`** | No tackle connect SFX / comic routing | Yes |
| **`PracticeNpcTackleClientRelay`** | Practice NPC tackle client relay broken | Yes |

### Speedster-only (`Player_Speedster`)

| Component | If missing / broken | Spawn warning? |
|-----------|---------------------|----------------|
| **`SpeedsterSpeedBlitzUlt`** | No Speed Blitz ult | No |
| **`SpeedBlitzDashHitDetector`** | Blitz dash hits never register | On ult start (`ComponentRequire.On`) |
| **`SpeedBlitzConnectImpactRelay`** | No blitz connect crunch SFX | On ult start |
| **`SpeedBlitzAimPreview`** | No blitz aim corridor | Yes (from ult) |
| **`SpeedBlitzDashCamera`** | No dash camera | Yes (from ult) |
| **`SpeedBlitzWindUpFeel`** | No wind-up VFX/SFX | Yes (from ult) |
| **`SpeedBlitzBodyGlow`** | No dasher body glow | Yes (from ult) |
| **`PlayerSpeedBlitzWindUpAnim`** | No blitz wind-up anim | Yes if Speedster class at runtime |
| **`BlitzConnectPoseFreeze`** | No connect pose freeze | Yes if Speedster class at runtime |

**Host enable rule:** `PlayerLoadout.ConfigureSpeedsterOnlyComponentsOnHost()` enables **`PlayerSpeedBlitzWindUpAnim`** and **`BlitzConnectPoseFreeze`** only for Speedster — but both components must still exist on **all three** prefabs (disabled on Juggernaut/Sniper).

### Juggernaut-only (`Player_Juggernaut`)

| Component | If missing / broken | Spawn warning? |
|-----------|---------------------|----------------|
| **`JuggernautQuakeSlamUlt`** | No Quake Slam | No |
| **`QuakeSlamOwnerPredict`** | No owner slam predict / movement | On ult start |
| **`QuakeSlamAimPreview`** | No ring aim preview | Yes (from ult) |
| **`QuakeSlamFeel`** | No slam SFX/VFX | Yes (from ult) |

### Sniper-only (`Player_Sniper`)

No ult siblings yet (slice 6). Sniper uses the **shared** table only; **`PlayerClass.CurrentClass`** → `classes/sniper.cdata`.

---

## Main Camera (every gameplay scene)

| Component | If missing / broken | Spawn warning? |
|-----------|---------------------|----------------|
| `Sandbox.CameraComponent` | No render | — |
| **`MapMatchConfig`** | Wrong team names; practice mode flag | No |
| **`GameNetworkManager`** | No player spawn; wire templates + spawns (below) | No |
| **`MatchDirector`** | No match flow; wire **BallSpawn** | No |
| **`EnemyOutlineCameraSetup`** | Enemy outlines don’t render | No |
| **`TackleComicTextHud`** | No knockdown comic words | Yes (GNM startup) |
| **`MatchAudioBootstrap`** | Outdoor reverb / room sim wrong | Yes (GNM startup) |
| **`OutOfBoundsBannerHud`** | No OOB white banner | Yes (GNM startup) |
| **`BallOobDropZoneHud`** | No drop-zone world marker | Yes (GNM startup) |

**`GameNetworkManager` inspector wiring:**

- **PlayerTemplateRoot** → parent of class templates  
- **SpeedsterPlayerTemplate** / **JuggernautPlayerTemplate** / **SniperPlayerTemplate** → each class root  
- **Team0Spawns** / **Team1Spawns** → spawn point empties (6 per team on Turf Wars)  
- **DisableTemplateOnStart** → on (templates stay disabled until spawn)

**Post-process:** `EnemyOutlineCameraSetup` auto-adds engine **`Highlight`** on the camera (allowed exception). Keep **Enable Post Processing** on `CameraComponent`.

**Ship check:** `MatchDirector.Enable Debug Force Goal` → **off** for production.

---

## Match HUD root (`MatchHUD` empty)

All on the same UI root object:

| Component | If missing / broken |
|-----------|---------------------|
| **`MatchScoreHud`** | No score bar |
| **`MatchClockHud`** | No match timer |
| **`GoalBannerHud`** | No “TEAM X SCORED!” banner |
| **`IntermissionHud`** | No intermission countdown |
| **`MatchOverHud`** | No match-over / rematch UI |

(`MatchHudDraw` is a static helper — not a component.)

---

## Ball (`main_ball`)

| Component | If missing / broken | Spawn warning? |
|-----------|---------------------|----------------|
| `Sandbox.ModelRenderer` | Ball invisible | — |
| `Sandbox.Rigidbody` | Ball doesn’t simulate | — |
| `Sandbox.SphereCollider` | No collisions | — |
| **`BallCarrierOutline`** | No carrier glow | On grab (`BallGrab`) |
| **`BallLastTouchLedger`** | OOB credit / last-touch wrong | On OOB host start |
| **`BallPassAssistState`** | Pass-assist charge rules broken | On access |
| **`BallOutOfBoundsHost`** | No OOB sequence (whistle, drop, sky-drop) | Yes (GNM startup) |

**Do not** add `BallClientFeel` on the ball — it lives on the **player** prefab (reads `BallGrab`).

---

## Turf Wars map objects

| Object / volume | Component | Notes |
|-----------------|-----------|--------|
| Each goal volume | **`GoalZone`** | Team id + score dwell; ×2 |
| **`BallSpawn`** empty | *(none)* | Wire to **`MatchDirector.BallSpawn`** |
| OOB floor strips / volumes | **`OutOfBoundsZone`** | Many strips on Turf Wars |
| Each traffic lane empty | **`TrafficSpawner`** | Wire **CarTemplate**, **Waypoints**, **CarModelVariants** |
| **`TrafficCarTemplate`** (disabled) | **`TrafficCar`** | Child **Body** → `ModelRenderer` + collider; template stays disabled |
| Street lamp clusters | **`StreetLightFlicker`** | Wire **Spot** child + optional bulb material |
| Petrol station lights | **`StationLightFlicker`** | Wire spot + mesh toggle |

**Collision.config:** `ball` + `playerclip` → **Ignore** (player wall clip vs ball).

---

## Practice arena only

| Object | Components | Notes |
|--------|------------|--------|
| **`MapMatchConfig`** on Main Camera | **`PracticeArenaMode`** = on | Disables match OOB flow |
| **`LaunchReadoutSign`** | **`PracticeLaunchReadout`**, **`PracticeLaunchReadoutRoot`**, **`Sandbox.WorldPanel`** (optional — readout can create WorldPanel) | TV score display |
| **`PracticeLaunchLane`** | **`PracticeLaunchMeasure`** | Drag **Readout Sign** → `LaunchReadoutSign` |
| Class templates | Same as § Player class prefabs | Fix missing **`BallClientFeel`** on practice templates |

---

## Practice NPCs (`practice_npc` tag)

**Never `NetworkSpawn` these** — scene-placed only.

### Static tackle dummy (e.g. `NPC_Speedster`, `NPC_Juggernaut`, `NPC_Sniper`)

Minimum set on root (tag **`practice_npc`**):

| Component | Notes |
|-----------|--------|
| Engine stack | Same as player **or** `PlayerController` **disabled** + `Rigidbody` kinematic/locked as configured |
| **`PlayerClass`** | Match class for tackle tuning |
| **`PlayerCosmeticsSync`** | Optional cosmetics |
| **`CatchUpSpeedBoost`** | Charge speed for tackle tests |
| **`PlayerTackle`** | |
| **`TackleRagdollLifecycle`** | |
| **`TackleImpactRelay`** | |
| **`CombatFeelPredictDedupe`** | |
| **`PracticeNpcTackleClientRelay`** | |
| **`RagdollClientFeel`** | |
| **`TackleImpactFeel`** | Recommended — victim/attacker feel on dummy |

Static dummies in practice scene also carry **`BallGrab` / `BallThrow` / `ThrowChargeBar`** for throw tests — not required for tackle-only dummies.

### Patrol runner (e.g. `NPC_Running`)

Everything in **static dummy**, plus:

| Component | Notes |
|-----------|--------|
| **`PracticeNpcPatrol`** | Wire **PointA** / **PointB**; optional **BodyRenderer** |
| **`PracticeNpcPatrolPoseRelay`** | Client pose mirror |
| **`PlayerController`** | **Disabled** — host moves root directly |
| **`PlayerChargeRunAnim`** | Run legs during patrol |
| **`PlayerBallHoldAnim`** | If testing hold anim on runner |

---

## Do **not** add manually (runtime / engine exceptions)

| What | Why |
|------|-----|
| **`Highlight`** on Main Camera | Added by **`EnemyOutlineCameraSetup`** |
| **`HighlightOutline`** on players | Added by **`PlayerEnemyOutline`** if missing |
| **`HighlightOutline`** on knockdown ragdoll | Added by **`RagdollEnemyOutline`** on host-spawned ragdoll |
| **`BallPassAssistCollisionRelay`** | Created on arbitrary bodies the ball touches |
| Comic / OOB / trajectory **child GOs** | Spawned by HUD/VFX code |
| **`StartupMapBootstrap`**, **`CitizenAvatarLodSystem`** | `GameObjectSystem` — no scene object |

---

## Spawn warning coverage (gaps to know)

**Warns on player spawn** (`GameNetworkManager.WarnMissingPlayerPrefabComponents`):  
`PlayerDisableCrouch`, `PlayerEnemyOutline`, `BallCompassHud`, `PlayerBallHoldAnim`, `PlayerChargeRunAnim`, `TackleImpactFeel`, `CombatFeelPredictDedupe`, `PlayerFootstepAudio`, `PracticeNpcPatrolPoseRelay`, `LoadoutClientState`, `LoadoutPickerHud`, `TackleRagdollLifecycle`, `TackleImpactRelay`, `PracticeNpcTackleClientRelay`.

**Does not warn on spawn (add manually — silent failures):**  
**`BallGrab`**, **`BallThrow`**, **`BallClientFeel`**, **`ThrowChargeBar`**, **`ThrowTrajectoryPreview`**, **`ThrowChargeCamera`**, **`PlayerTackle`**, **`PlayerDodge`**, **`PlayerUltCharge`**, **`UltChargeHud`**, **`MovementRampHud`**, **`DodgeCooldownHud`**, **`RagdollClientFeel`**, **`CatchUpSpeedBoost`**, **`PlayerClass`**, **`PlayerCosmeticsSync`**, class ult orchestrators.

When in doubt, diff your prefab against **`throwdown_turf_wars.scene`** class templates.

---

## Copy-paste manifest (shared player root — all classes)

```
BallGrab
BallThrow
BallClientFeel
ThrowChargeBar
ThrowTrajectoryPreview
ThrowChargeCamera
PlayerBallHoldAnim
PlayerClass
PlayerCosmeticsSync
CatchUpSpeedBoost
PlayerChargeRunAnim
MovementRampHud
PlayerTackle
TackleImpactFeel
RagdollClientFeel
PlayerDodge
DodgeCooldownHud
PlayerDisableCrouch
PlayerEnemyOutline
PlayerUltCharge
UltChargeHud
PlayerTeam
PlayerLoadout
BallCompassHud
CombatFeelPredictDedupe
PlayerFootstepAudio
PracticeNpcPatrolPoseRelay
LoadoutClientState
LoadoutPickerHud
TackleRagdollLifecycle
TackleImpactRelay
PracticeNpcTackleClientRelay
```

**+ Speedster:** `SpeedsterSpeedBlitzUlt`, `SpeedBlitzDashHitDetector`, `SpeedBlitzConnectImpactRelay`, `SpeedBlitzAimPreview`, `SpeedBlitzDashCamera`, `SpeedBlitzWindUpFeel`, `SpeedBlitzBodyGlow`, `BlitzConnectPoseFreeze`, `PlayerSpeedBlitzWindUpAnim`

**+ Juggernaut:** `JuggernautQuakeSlamUlt`, `QuakeSlamOwnerPredict`, `QuakeSlamAimPreview`, `QuakeSlamFeel`
