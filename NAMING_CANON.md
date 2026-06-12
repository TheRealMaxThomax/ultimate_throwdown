# Naming canon

**What this is:** Official names for scripts, components, and important properties.  
**When you need it:** Adding or renaming code, or when chat and code use different words for the same thing.

**Rule:** Don’t invent new names for things that already exist — use the names below.

---

## Ball (`Code/Ball/`)

| Name | Job |
|------|-----|
| `BallGrab` | Who holds the ball; pickup, drop, auto-grab; hold pose via **`hold_R`** bone (`HoldBoneName`, `TryGetHoldAnchorWorldTransform`) |
| `BallThrow` | Throwing and charge |
| `BallClientFeel` | Makes the ball look smooth on clients (not gameplay authority) |
| `ThrowChargeBar` | Owner screen HUD while charging throw (vertical bar above dodge; placeholder) |
| `ThrowTrajectoryPreview` | Owner-only dashed arc + first-hit landing sphere while charging throw |
| `BallCarrierOutline` | Team pulse `HighlightOutline` on held ball — white ↔ green (teammate) / white ↔ red (enemy); non-carrier viewers; no wallhack |
| `ThrowReleaseMath` | Shared throw release + first-arc sim (`ComputeRelease`, `TryGetBallFlightParameters`, `TrySimulateFirstImpact`) |

**Often-used on `BallGrab`:** `IsHolding`, `MainBall`, `InteractDistance`, `HoldBoneName`, `BodyRenderer`, `HoldBoneLocalOffset`, `TryGetHoldAnchorWorldTransform()`, `GetPredictedThrowReleasePivotPosition()`  
**Often-used on `BallThrow`:** `ThrowForce`, `ThrowUpForce`, `ThrowReleaseDelaySeconds` (default `0.35` — ball velocity after anim wind-up), `IsChargingThrow`, `IsPendingThrowRelease`, `NetIsChargingThrow`, `NetThrowChargeLerp` (synced charge scrub for remotes), `ThrowDirectionSource` (optional; if unset, throw uses `PlayerController.EyeAngles`), `GetThrowChargeLerp()`, `TryGetThrowPreviewSnapshot()`  
**Often-used on `ThrowTrajectoryPreview`:** `ArcDashScrollSpeed`, `LandingMarkerAlpha`, `TranslucentBallMaterialPath`, `LandingMarkerLift`  
**Often-used on `BallCarrierOutline`:** `PulseWhiteColor`, `FriendlyAccentColor`, `EnemyAccentColor`, `PulseSeconds`, `OutlineWidth`, `EnableEmissivePulse`, `EmissiveBrightnessMax`  
**Often-used on `BallCompassHud`:** `LabelText`, `LabelFontSize`, `MarginLeft`, `MarginBottom`, `CompassSize`, `MarkerOrbitRadiusFraction`, `MarkerTipLength`, `MarkerHalfWidth`, `LooseColor`, `FriendlyColor`, `EnemyColor`, `LocalCarryRingColor` (player `EyeAngles` bearing; white hub label centered in ring; triangle orbits edge toward ball)

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
| `ThrowChargeCamera` | Owner-only throw-charge third-person pullback + mild FOV widen (scales with charge lerp) |
| `TackleImpactFeel` | Owner-only tackle connect juice — local camera hitstop, screen shake, attacker FOV/offset punch (`PlayerTackle` owner RPCs) |
| `PlayerBallHoldAnim` | Built-in citizen `holditem` RH hold + throw on release (`holdtype`, `holdtype_pose_hand`, `holdtype_attack`, `b_attack`); throw wind-up via forked animgraph (`throw_charge` / `throw_charge_weight`); pairs with `BallThrow.ThrowReleaseDelaySeconds` for ball detach timing |
| `PlayerChargeRunAnim` | Movement **Charge**-tier overlay (`charge_run` masked layer via `charge_run_weight` / `charge_run_cycle`); only when `CatchUpSpeedBoost.GetMovementRampDisplay` tier is `MovementRampTier.Charge` and **not** holding the ball — ball carriers never reach charge speed by design |
| `PlayerEnemyOutline` | Enables red `HighlightOutline` for enemies (local viewer); off while ragdolled |
| `RagdollEnemyOutline` | Outline on host-spawned tackle ragdoll; copies victim `HighlightOutline` |
| `EnemyOutlineCameraSetup` | Ensures main camera has `Highlight` post-process |

**Often-used on `PlayerEnemyOutline`:** `CopyOutlineFromPlayer()`, `CopyOutlineSettings()`, `ShouldShowOutlineForTeamId()`, `FindLocalViewerTeamId()` — **tune `HighlightOutline` on the player prefab** (source of truth for look)

**Often-used on `RagdollEnemyOutline`:** `NetVictimTeamId` (synced), `ConfigureFromVictim()` (host, before network spawn)

**Often-used on `ThrowChargeCamera`:** `ExtraCameraDistanceAtFullCharge`, `ExtraCameraHeightAtFullCharge`, `ExtraFieldOfViewAtFullCharge`, `ReleaseCameraBlendDuration` — skips when `PlayerTackle.IsRagdolled`, `IsStandUpCameraBlending`, or `TackleImpactFeel.IsImpactFeelActive`

**Often-used on `TackleImpactFeel`:** `EnableHitstop`, `HitstopDurationSeconds` (default `0.055`), `ShakeForAttacker` / `ShakeForVictim` (default both on), `ShakeDurationSeconds`, `ShakePositionAmplitude`, `AttackerFovPunchDegrees`, `AttackerCameraOffsetPunchX` / `AttackerCameraOffsetPunchZ`, `AttackerPunchDurationSeconds`, `IsImpactFeelActive`, `TriggerAsAttacker()` / `TriggerAsVictim()`

**Often-used on `PlayerBallHoldAnim`:** `BodyRenderer`, `IdleHoldPoseHand` (default `0.1`), `IdleHoldTypePose`, `ThrowAttackStrong` (default `0` = medium throw; `1` = strong), `ThrowPoseHoldSeconds` (default `0.9`), `ThrowPlaybackRate` (default `0.7` during throw window), `CustomBodyModelPath` (default `animation/utd_citizen_human_throw.vmdl`), `EnsureCustomBodyModel()` (re-applies Body model + custom animgraph after cosmetics), **`UseAnimGraphChargePose`** (default `true`), **`CustomAnimGraphPath`** (default `animation/utd_citizen_human_m.vanmgrph`), **`ChargeCycleParamName`** (default `throw_charge`), **`ChargeWeightParamName`** (default `throw_charge_weight`), **`ChargeWindupCycleStart`** / **`ChargeWindupCycleEnd`** (default `0` / `1` — map charge bar 0→1 to a clip sub-range; lower `End` if wind-up motion only uses the start of the clip), **`ChargeWeightBlendInSeconds`** (default `0.12`), **`ChargeWeightBlendOutSeconds`** (default `0.15`), `NotifyThrowReleased()`

**Often-used on `PlayerChargeRunAnim`:** `BodyRenderer`, `UseAnimGraphChargeRunPose` (default `true`), `ChargeRunWeightParamName` (default `charge_run_weight`), `ChargeRunCycleParamName` (default `charge_run_cycle`), `ChargeRunCycle` (default `0` — static pose), `ChargeRunWeightBlendInSeconds` (default `0.12`), `ChargeRunWeightBlendOutSeconds` (default `0.15`)

**Custom citizen animation assets:** `Assets/Animation/utd_citizen_human_throw.vmdl` (sequences + optional custom weight lists e.g. `UTD_Charge_Overlay`) and `Assets/Animation/utd_citizen_human_m.vanmgrph` (forked graph: independent masked layers for `throw_windup` and `charge_run`). Workflow: [`Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md`](Assets/Animation/CITIZEN_ANIMATION_WORKFLOW.md)

**Often-used on `PlayerTackle`:** `TackleLaunchSpeed`, `TackleLaunchArc`, `NetIsRagdolled`, **`IsAwaitingRagdollLaunch`**, **`IsKnockedDown`** (ragdolled or awaiting launch), `IsStandUpCameraBlending`, `RagdollPhysicsInitDelay` (body poll timeout), **`PreLaunchPauseSeconds`** (default `0.05` — victim frozen visible → pause → impulse + spawn; **`0`** = legacy impulse-then-spawn); `ApplyKnockdownFromHost()` (traffic/hazards); RPC `RequestTackleApplyOnHost` (+ owner charge bonus arg); owner RPCs `TriggerTackleImpactFeelAsAttackerRpc` / `TriggerTackleImpactFeelAsVictimRpc` → `TackleImpactFeel`  
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
| `BallCompassHud` | Local viewer bottom-left compass toward match ball (white loose / green teammate / red enemy; needle hidden when local player carries) |

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

**BallGrab:** `HeldBall`, `MainBallName`, `InteractAction`, `HoldAnchor`, `BodyRenderer`, `HoldBoneName` (`hold_R`), `HoldBoneLocalOffset`, `HoldBoneLocalAngles`, `TryGetHoldAnchorWorldTransform()`, `PickupDelayAfterDrop`, `DropperNoPushWindow`, `DropSideOffset`, `DropVelocityScale`, `NetPickupBlockedRemain`, `ReleaseHeldBall()`, `BlockPickupForSeconds()`, …

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
