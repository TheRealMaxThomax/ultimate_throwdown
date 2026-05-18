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
| `ThrowChargeBar` | UI bar while charging throw |

**Often-used on `BallGrab`:** `IsHolding`, `MainBall`, `InteractDistance`, `NetIsHolding`  
**Often-used on `BallThrow`:** `ThrowForce`, `ThrowUpForce`, `IsChargingThrow`, `NetIsChargingThrow`

---

## Match (`Code/Match/`)

| Name | Job |
|------|-----|
| `MapMatchConfig` | Per-map team display names (`Team0DisplayName`, `Team1DisplayName`) |
| `MatchTeamIds` | Constants `Team0` / `Team1` (ids `0` / `1`) |
| `MatchDirector` | Phase state machine, round wins, match timer, debug force goal |
| `MatchPhase` | Enum: `Playing`, `GoalCelebration`, `Intermission`, `MatchOver` |
| `GoalZone` | Defended goal volume + host dwell scoring (`DefendingTeam`, `ScoreDwellSeconds`) |

**Often-used on `PlayerTeam`:** `TeamId`, `NetMatchPhase`, `NetRoundResetSequence`, `IsMatchGameplayInputAllowed`, `ApplyRoundResetTransform()` (synced, host-assigned)

**Often-used on `MatchDirector`:** `CurrentPhase`, `IsGameplayInputAllowed`, `BallSpawn`, `RegisterGoal()`, `HostRequestRematch()`, `NetTeam0RoundWins`, `NetTeam1RoundWins`, `NetMatchTimeRemaining`, `NetPhaseTimeRemaining`, `NetLastGoalScoringTeamId`, `NetIsOvertime`, `EnableDebugForceGoal`, `DebugForceGoalAction` (`DebugForceGoal` → `,` in `Input.config`)

**Often-used on `PlayerTackle`:** `ForceStandUpFromHost()` (match reset)

**Often-used on `GoalZone`:** `DefendingTeam`, `BoxSize`, `ScoreDwellSeconds`, `EnableGoalZoneDebugLogs`

---

## Player (`Code/Player/`)

| Name | Job |
|------|-----|
| `PlayerTeam` | Synced match team id (`0` / `1`) |
| `CatchUpSpeedBoost` | Walk / sprint / charge speed ramps |
| `PlayerDodge` | Dodge (lives at **bottom of** `CatchUpSpeedBoost.cs` — same file on purpose) |
| `PlayerClass` | Which class is equipped |
| `ClassData` | Class stats asset type (lives in **`PlayerClass.cs`**, not its own file) |
| `PlayerTackle` | Tackle and ragdoll |
| `RagdollClientFeel` | Smoother ragdoll camera for the owning player |
| `PlayerCosmeticsSync` | Outfits / avatar look only |

**Often-used on `PlayerTackle`:** `TackleLaunchSpeed`, `TackleLaunchArc`, `NetIsRagdolled`, `RagdollPhysicsInitDelay`  
**Often-used on `PlayerDodge`:** `IsImmuneToTackle`, `ShoveVelocityMultiplier`, `DodgeCooldownRemaining`  
**Often-used on `CatchUpSpeedBoost`:** `IsAtChargeSpeed`, `GetMovementRampDisplay`, `MovementRampTier`  
**Tag for test dummies only:** `practice_npc`

---

## UI (`Code/UI/`)

| Name | Job |
|------|-----|
| `DodgeCooldownHud` | Placeholder dodge cooldown timer (owner HUD) |
| `MovementRampHud` | Placeholder walk / sprint / charge ramp bar (owner HUD) |
| `MatchScoreHud` | *(planned)* Round wins top bar |
| `GoalBannerHud` | *(planned)* "TEAM A SCORED!" during celebration |
| `IntermissionHud` | *(planned)* Intermission countdown |
| `MatchOverHud` | *(planned)* Winner + rematch |

---

## Network & map

| Name | Job |
|------|-----|
| `GameNetworkManager` | Spawns players when joining; team balance + team spawns (`Code/Network/`) |
| `StartupMapBootstrap` | Loads `testing_map` on host and clients (`Code/Map/`) |

**Often-used on `GameNetworkManager`:** `Team0Spawns`, `Team1Spawns`, `SpawnPointOccupiedRadius`, `MatchConfig`, `ApplyRoundResetToAllPlayers()`, `ApplyRoundResetToPlayer()` (legacy: `Team0Spawn`, `Team1Spawn`, `JoinSpawnSpacing`)

---

## Class stats (`.cdata` / `ClassData`)

Examples — full list is in the editor on the asset:

`Mass`, `StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TriggerSphereRadius`, `DodgeCooldown`, `DodgeDistance`, `ThrowPower`, `RagdollDuration`, `TackleChargeRampRate`

---

## Project settings

- Map source: `Assets/Maps/testing_map.vmap`
- Playable map name: `testing_map`
- `.sbproj`: **`"Resources": null`** (required for stable multiplayer join)

---

## Full property list (agents / deep reference)

<details>
<summary>Click to expand — every named field (for AI or rare lookups)</summary>

**BallGrab:** `HeldBall`, `MainBallName`, `InteractAction`, `HoldAnchor`, `PickupDelayAfterDrop`, `DropperNoPushWindow`, `DropSideOffset`, `DropVelocityScale`, `NetPickupBlockedRemain`, `ReleaseHeldBall()`, `BlockPickupForSeconds()`, …

**BallThrow:** `ThrowAction`, `ThrowStartOffset`, `MinThrowChargeTime`, `MaxThrowChargeTime`, `ClearThrowChargeLocal()`, …

**BallClientFeel:** `InterpolationDelay`, `MaxSnapshots`, `FreeBallVisualFollowSharpness`, …

**CatchUpSpeedBoost:** `ForwardAction`, `NetAtChargeSpeed`, `IsAtChargeSpeed`, …

**PlayerTackle:** `TackleDirectionThreshold`, `NetStandUpPosition`, `NetPracticeNpcStandEyeAngles`, `StandUpCameraBlendDuration`, …

**PlayerDodge:** `LeftStrafeAction`, `RightStrafeAction`, `DoubleTapMaxInterval`, `IsDodging`, …

**Editor:** `PlayerController` camera `CameraOffset` X = **185**

</details>

If you add a new public field or component, add one line to the section above (or the expandable list).
