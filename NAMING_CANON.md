# Naming canon

**What this is:** Official names for scripts, components, and important properties.  
**When you need it:** Adding or renaming code, or when chat and code use different words for the same thing.

**Rule:** Don’t invent new names for things that already exist — use the names below.

---

## Ball (`Code/Ball/`)

| Name | Job |
|------|-----|
| `BallGrab` | Who holds the ball; pickup, drop, auto-grab |
| `BallThrow` | Throwing and charge |
| `BallClientFeel` | Makes the ball look smooth on clients (not gameplay authority) |
| `ThrowChargeBar` | Owner screen HUD while charging throw (vertical bar above dodge; placeholder) |
| `ThrowTrajectoryPreview` | Owner-only dashed arc + first-hit landing sphere while charging throw |
| `ThrowReleaseMath` | Shared throw release + first-arc sim (`ComputeRelease`, `TryGetBallFlightParameters`, `TrySimulateFirstImpact`) |

**Often-used on `BallGrab`:** `IsHolding`, `MainBall`, `InteractDistance`, `NetIsHolding`, `GetPredictedThrowReleasePivotPosition()`  
**Often-used on `BallThrow`:** `ThrowForce`, `ThrowUpForce`, `IsChargingThrow`, `NetIsChargingThrow`, `ThrowDirectionSource` (optional; if unset, throw uses `PlayerController.EyeAngles`), `TryGetThrowPreviewSnapshot()`

---

## Match (`Code/Match/`)

| Name | Job |
|------|-----|
| `MapMatchConfig` | Per-map team display names (`Team0DisplayName`, `Team1DisplayName`) |
| `MatchTeamIds` | Constants `Team0` / `Team1` (ids `0` / `1`) |
| `MatchDirector` | Phase state machine, round wins, match timer, debug force goal |
| `MatchPhase` | Enum: `Playing`, `GoalCelebration`, `Intermission`, `MatchOver` |
| `GoalZone` | Defended goal volume + host dwell scoring (`DefendingTeam`, `ScoreDwellSeconds`) |

**Often-used on `PlayerTeam`:** `TeamId`, `NetMatchPhase`, `NetTeam0RoundWins`, `NetTeam1RoundWins`, `NetMatchTimeRemaining`, `NetPhaseTimeRemaining`, `NetLastGoalScoringTeamId`, `NetIsOvertime`, `NetMatchWinnerTeamId`, `NetRoundResetSequence`, `IsMatchGameplayInputAllowed`, `ApplyRoundResetTransform()` (synced, host-assigned)

**Often-used on `MatchDirector`:** `CurrentPhase`, `IsGameplayInputAllowed`, `BallSpawn`, `RegisterGoal()`, `PushMatchHudStateToPlayers()`, `HostRequestRematch()`, `MatchOverCelebrationSeconds`, `NetTeam0RoundWins`, `NetTeam1RoundWins`, `NetMatchTimeRemaining`, `NetPhaseTimeRemaining`, `NetLastGoalScoringTeamId`, `NetIsOvertime`, `NetMatchWinnerTeamId`, `EnableDebugForceGoal`, `DebugForceGoalAction` (`DebugForceGoal` → `,` in `Input.config`)

**Often-used on `PlayerTackle`:** `ForceStandUpFromHost()` (match reset)

**Often-used on `GoalZone`:** `DefendingTeam`, `BoxSize`, `ScoreDwellSeconds`, `EnableGoalZoneDebugLogs`

---

## Player (`Code/Player/`)

| Name | Job |
|------|-----|
| `PlayerTeam` | Synced match team id (`0` / `1`) |
| `CatchUpSpeedBoost` | Walk / sprint / charge speed ramps |
| `PlayerDodge` | Double-tap dodge, iframe, cooldown, ramp penalties (`Code/Player/PlayerDodge.cs`) |
| `PlayerClass` | Which class is equipped |
| `ClassData` | Class stats asset type (lives in **`PlayerClass.cs`**, not its own file) |

**Often-used on `PlayerClass`:** `CurrentClass`, `NeutralMenuHeight`, `ApplyClassAppearance()`, `PrepareDresserBeforeSpawn()` (disable menu height before spawn; class `ModelScale` + neutral `scale_height`)
| `PlayerTackle` | Tackle and ragdoll |
| `RagdollClientFeel` | Smoother ragdoll camera for the owning player |
| `PlayerCosmeticsSync` | Outfits / avatar look only |
| `PlayerDisableCrouch` | Blocks duck/crouch on `PlayerController` |
| `PlayerEnemyOutline` | Enables red `HighlightOutline` for enemies (local viewer); off while ragdolled |
| `RagdollEnemyOutline` | Outline on host-spawned tackle ragdoll; copies victim `HighlightOutline` |
| `EnemyOutlineCameraSetup` | Ensures main camera has `Highlight` post-process |

**Often-used on `PlayerEnemyOutline`:** `CopyOutlineFromPlayer()`, `CopyOutlineSettings()`, `ShouldShowOutlineForTeamId()`, `FindLocalViewerTeamId()` — **tune `HighlightOutline` on the player prefab** (source of truth for look)

**Often-used on `RagdollEnemyOutline`:** `NetVictimTeamId` (synced), `ConfigureFromVictim()` (host, before network spawn)

**Often-used on `PlayerTackle`:** `TackleLaunchSpeed`, `TackleLaunchArc`, `NetIsRagdolled`, `RagdollPhysicsInitDelay` (max poll for bodies → impulse → `NetworkSpawn` + `RagdollEnemyOutline`); `ApplyKnockdownFromHost()` (traffic/hazards); RPC `RequestTackleApplyOnHost` (+ owner charge bonus arg)  
**Often-used on `PlayerDodge`:** `IsImmuneToTackle`, `ShoveVelocityMultiplier`, `DodgeCooldownRemaining`  
**Often-used on `CatchUpSpeedBoost`:** `IsAtChargeSpeed`, `GetMovementRampDisplay`, `MovementRampTier`  
**Tag for test dummies only:** `practice_npc`

---

## UI (`Code/UI/`)

| Name | Job |
|------|-----|
| `DodgeCooldownHud` | Placeholder dodge cooldown timer (owner HUD) |
| `MovementRampHud` | Placeholder walk / sprint / charge ramp bar (owner HUD) |
| `MatchHudDraw` | Shared HUD read/draw helpers (`FormatMatchClock`, `TryGetHudState`, `IsMatchOverCelebrating`) |
| `MatchScoreHud` | Top bar: team names + round wins |
| `MatchClockHud` | Match timer `M.SS` (10.00, 9.59, …); `OVERTIME` label |
| `GoalBannerHud` | "TEAM A SCORED!" during celebration |
| `IntermissionHud` | "Resuming in N…" during intermission |
| `MatchOverHud` | Winner + final score + host rematch (`RematchVoteSlot`, default `1` → `Slot1` key) |

---

## Network & map

| Name | Job |
|------|-----|
| `GameNetworkManager` | Spawns players when joining; team balance + team spawns (`Code/Network/`) |
| `StartupMapBootstrap` | Scene startup; locks `practice_npc` rigidbodies (`Code/Map/`) |
| `StreetLightFlicker` | One lamp cluster: flickers child `SpotLight` + bulb material slot (`Code/Map/`) |
| `StationLightFlicker` | Petrol-station light block: flickers child `SpotLight` + optional mesh visual on/off (`Code/Map/`) |
| `TrafficSpawner` | Host lane spawner: clones disabled **`CarTemplate`**, **`CarModelVariants`** (after **`NetworkSpawn`**: Body renderer + collider), filleted **`Waypoints`**, speed/knockdown/hit box (`Code/Map/`) |
| `TrafficCar` | On car template root: host path follower + knockdown; idle/drive **`SoundEvent`** loops; client proxies use **`NetWorldPosition`**, **`NetWorldRotation`**, **`NetMeshUniformScale`**, **`NetCurrentSpeed`**, **`NetDriveBlend`**, **`ProxyPoseFollowSharpness`** (`Code/Map/`) |

**Often-used on `TrafficSpawner`:** `CarTemplate`, `Waypoints`, `CarModelVariants` (random Body `.vmdl` per spawn — **after `NetworkSpawn`**, sets renderer + collider; per-lane lists; `Model.Load` fallback), `CarSpeed`, `CarAcceleration`, `CarDeceleration`, `CornerFilletRadius`, `CornerArcSamples`, `CurveSlowLookAhead`, `CurveMinSpeedFraction`, `FacingYawOffsetDegrees`, `CarHeightOffset`, `HitHalfExtents`, `HitBoxCenterOffset`, `KnockdownLaunchSpeed`, `SpawnDelaySeconds` (per-lane wait after playing starts; e.g. Road1 = 5), `MaxAliveCars`, `DisableTemplateOnStart`, `OnlySpawnWhileMatchPlaying`

**Often-used on `TrafficCar`:** `EngineIdleSound`, `EngineDriveSound`, `EngineSoundVolume`, `EngineSoundMaxDistance`, `EngineSoundLocalOffset`, `EngineSoundBlendSharpness`, `MeshUniformScale`, `ProxyPoseFollowSharpness`

**Often-used on `StreetLightFlicker`:** `Spot`, `LampModel`, `BulbOffMaterial`, `BulbMaterialIndex` (`-1` = auto-detect `light.vmat` / emissive slot), `SyncBulbEmissive`

**Often-used on `GameNetworkManager`:** `Team0Spawns`, `Team1Spawns`, `SpawnPointOccupiedRadius`, `MatchConfig`, `SnapPositionToGround()`, `SnapBallToGround()`, `ApplyRoundResetToAllPlayers()`, `ApplyRoundResetToPlayer()` (legacy: `Team0Spawn`, `Team1Spawn`, `JoinSpawnSpacing`)

---

## Class stats (`.cdata` / `ClassData`)

Examples — full list is in the editor on the asset:

`Mass`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TriggerSphereRadius`, `DodgeCooldown`, `DodgeDistance`, `ThrowPower`, `RagdollDuration`, `TackleChargeRampRate`

---

## Project settings

- **Playable map:** geometry in active **`.scene`** (e.g. `scenes/throwdown_turf_wars.scene`). Set **Startup Scene** in `ultimate_throwdown.sbproj`.
- Legacy Hammer: `Assets/Maps/testing_map.vmap` — not auto-loaded (`StartupMapBootstrap` does not inject `MapInstance`).
- **Player movement:** `Move Mode Walk` → `StepUpHeight` on **Player** template (inspector only; global for all classes).
- `.sbproj`: **`"Resources": null`** (required for stable multiplayer join)
- `Input.config`: **`Duck`** action unbound (`KeyboardCode` / `GamepadCode` = `None`) — crouch disabled project-wide

---

## Map materials (`Assets/materials/` — Turf Wars)

**Pattern:** `{palette}` = base · `{palette}_{N}darker` · `{palette}_{N}lighter` (percent + direction — not `old_40`).

| vmat | Use |
|------|-----|
| `eggshell` | Base `#E0E0CE` — walls, new road lines |
| `eggshell_40darker` | Old road lines (faded) |
| `eggshell_50darker` | Old thin / broken line scraps |
| `grey` | Road asphalt, kerbs (`#67697C`) |
| `golden` | Wood, old-side dirt (matte) |
| `olive` | Grass / trees |

Shade examples: `eggshell_30lighter`, `grey_20lighter` (footpaths) — see `game_artstyle.md`.

---

## Full property list (agents / deep reference)

<details>
<summary>Click to expand — every named field (for AI or rare lookups)</summary>

**BallGrab:** `HeldBall`, `MainBallName`, `InteractAction`, `HoldAnchor`, `PickupDelayAfterDrop`, `DropperNoPushWindow`, `DropSideOffset`, `DropVelocityScale`, `NetPickupBlockedRemain`, `ReleaseHeldBall()`, `BlockPickupForSeconds()`, …

**BallThrow:** `ThrowAction`, `ThrowStartOffset`, `MinThrowChargeTime`, `MaxThrowChargeTime`, `ClearThrowChargeLocal()`, `TryGetThrowPreviewSnapshot()`, …

**ThrowReleaseMath:** `ReleaseSettings`, `BallFlightParameters`, `GetChargeLerp()`, `ComputeRelease()`, `TryGetBallFlightParameters()`, `TrySimulateFirstImpact()`, …

**ThrowTrajectoryPreview:** `ArcDotColor`, `LandingMarkerColor`, `SimulationStepSeconds`, …

**ThrowChargeBar:** `MarginRight`, `MarginBottom`, `GapFromDodgePanel`, `BarBlockCount`, `Show()`, `Hide()`, `SetCharge()`, …

**BallClientFeel:** `InterpolationDelay`, `MaxSnapshots`, `FreeBallVisualFollowSharpness`, …

**CatchUpSpeedBoost:** `ForwardAction`, `NetAtChargeSpeed`, `IsAtChargeSpeed`, …

**PlayerTackle:** `TackleDirectionThreshold`, `NetStandUpPosition`, `NetPracticeNpcStandEyeAngles`, `StandUpCameraBlendDuration`, …

**PlayerDodge:** `LeftStrafeAction`, `RightStrafeAction`, `DoubleTapMaxInterval`, `IsDodging`, …

**Editor:** `PlayerController` camera `CameraOffset` X = **185**

</details>

If you add a new public field or component, add one line to the section above (or the expandable list).
