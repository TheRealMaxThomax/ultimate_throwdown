# Refactor plan — split `PlayerTackle` + `SpeedsterSpeedBlitzUlt`

**What this is:** Step-by-step execution plan to split the two god components flagged in [`ARCHITECTURE.md`](ARCHITECTURE.md) § Before slice 5/6. Written by Sonnet after reading both files in full; intended to be executed by Composer, **one step at a time**, with an editor smoke test between every step.

**Do not skip the testing step between numbered steps.** Each step is a standalone, compilable, testable unit. If something breaks, you know exactly which step did it.

**Order:** Finish and confirm the entire `PlayerTackle` track before starting the `SpeedsterSpeedBlitzUlt` track. They reference each other in a couple of places, but only one line needs touching for that (called out below) — no reason to interleave them.

**Instruction for whoever executes this plan (Composer): implement exactly ONE numbered step, then stop.** Summarize what changed and which editor test(s) from that step's checklist Max should run, then wait for Max to confirm it's working before starting the next step. Do not chain multiple steps together in one pass even if they seem quick — that defeats the point of testing incrementally.

---

## Ground rules for every step (read once, apply throughout)

1. **Sibling components, not partial classes.** Every split creates a new `Component` on the *same GameObject* — **add it manually on the player prefab** (see `.cursor/rules/no-auto-add-components.mdc`). Code uses `Components.Get<T>()` + `ComponentRequire` warnings when missing; no `GetOrCreate` on prefab-owned components.
2. **Preserve the public/internal API of the original class wherever another file calls it.** If method `Foo()` on `PlayerTackle` is called from `SpeedsterSpeedBlitzUlt.cs` (or vice versa), either keep a one-line forwarding method on the original class, or update that single external call site directly — whichever is simpler for that step (stated per-step below). Call sites **within the same file** being edited just get updated directly.
3. **RPCs must live on a component attached to the networked GameObject.** Since siblings are added to the same GameObject, `[Rpc.Broadcast]` / `[Rpc.Host]` / `[Rpc.Owner]` methods work identically after moving — this is already proven by `TackleImpactFeel` and `CombatFeelPredictDedupe`.
4. **Inspector (`[Property]`) values that move to a new sibling component need re-wiring in the editor.** Serialized values live per-component-instance; moving a `[Property]` to a different class means the field appears blank on the new component until re-assigned. This plan minimizes how often that happens and calls it out explicitly wherever it's unavoidable.
5. **Update `NAMING_CANON.md`** after each step — one line for the new component + its job, and move any "often-used" member bullets that now live on the new component.
6. **Compile before testing.** s&box hotload can mask reference errors; do a full stop/play cycle after each step, not just hotload.

---

## Track A — `PlayerTackle.cs` (currently 1,857 lines)

### A1. Extract ragdoll spawn + recovery physics → `Code/Player/TackleRagdollLifecycle.cs`

**Moves:** `SpawnRagdollObject`, `AddVictimClothingToRagdoll`, `WaitForRagdollBodiesAsync`, `HasRagdollLaunchBodies`, `SetRagdollRenderersEnabled`, `TryApplyRagdollLaunchImpulse`, `FreezeRagdollPhysics`, `HandleRagdollRecovery`, `IsRagdollGroundedAndSettled`, `TraceStandUpPosition`, `ComputeStandUpPositionFromRagdoll`, `DestroyRagdollObjectOnHost`, and the `ragdollObject` field.

**Stays on `PlayerTackle`:** `SyncedRagdollPelvisPosition` (reads `NetRagdollPosition`, not `ragdollObject` — no change needed), `TryGetRagdollOrbitCamera` (uses `playerController`/`WorldPosition`, not `ragdollObject`).

**New cross-reference:** `PlayerTackle.OnUpdate()` reads `ragdollObject.WorldPosition` to update `NetRagdollPosition` — this needs a reference to the new sibling (`ComponentRequire.On<TackleRagdollLifecycle>()` in `OnStart`, expose `RagdollObject` as a public property on the sibling). `ForceStandUpFromHost()` and `ApplyVictimKnockdownFromHost()` call into ragdoll spawn/destroy — update those calls to go through the sibling.

**Cross-file touch:** None outside `PlayerTackle.cs` — `ragdollObject` was never referenced from other files.

**Est. lines removed:** ~450–480.

**Editor changes needed:** None (auto-added).

**Test after this step:**
- Tackle a player → ragdoll spawns, launches, settles, victim stands up in the right spot.
- Traffic/hazard knockdown still ragdolls correctly (no attacker).
- `MatchDirector` round reset (`ForceStandUpFromHost`) still cleanly ends an active ragdoll.
- Speed Blitz knockdown (pre-launch hang → ragdoll launch) still works — this path goes through the same spawn code.

---

### A2. Extract tackle + Speed Blitz impact SFX/feel relay → `Code/Player/TackleImpactRelay.cs`

**Moves:** `PickTackleConnectImpactSoundResourcePath`, `GetTackleConnectImpactSoundPosition`, `OwnerPlayPredictedTackleConnectImpactSound`, `BroadcastTackleConnectImpactSoundOnHost`, `PlayTackleConnectImpactSoundAt`, `BroadcastTackleConnectImpactSound`, `PlayTackleConnectImpactSoundRpc`, `TryConsumeHostTackleConnectSoundDedupeForAttacker`, `TryConsumeHostTackleConnectSoundDedupe`, `PlaySpeedBlitzLaunchSoundRpc`, `BroadcastSpeedBlitzConnectImpactSound`, `PlaySpeedBlitzConnectImpactSoundRpc`, `NotifyTackleImpactFeel`, `TriggerTackleImpactFeelAsAttackerRpc`, `TriggerTackleImpactFeelAsVictimRpc`, `ResolveSpeedBlitzImpactFeelOverrides`, plus the `TackleConnectImpactSoundA` / `TackleConnectImpactSoundB` / `TackleConnectImpactSoundVolume` properties and `ownerPredictedTackleConnectSound` field.

**⚠️ Editor rewiring needed:** `TackleConnectImpactSoundA`/`B` are drag-dropped `SoundEvent` assets on the player prefabs today. After this step, **add `TackleImpactRelay` to each player prefab** and **re-drag the two crunch sound assets** onto the new component — they'll be blank on `PlayerTackle` after the properties move.

**Cross-file touch:** `SpeedsterSpeedBlitzUlt.HostApplyDashKnockdown()` calls `victim.BroadcastSpeedBlitzConnectImpactSound(...)`. Update this **one line** in `SpeedsterSpeedBlitzUlt.cs` to `victim.Components.Get<TackleImpactRelay>()?.BroadcastSpeedBlitzConnectImpactSound(...)`. Everything else that calls into this group (`ExecuteTackle`, `ApplyVictimKnockdownFromHost`, and — after A1 — `TackleRagdollLifecycle`'s knockdown-launch call to `PlaySpeedBlitzLaunchSoundRpc`) lives inside files you're already editing this step.

**Est. lines removed:** ~220–250.

**Test after this step:**
- Tackle connect crunch sound plays once (not doubled) for both the owner-predicted client and the host broadcast.
- Speed Blitz connect crunch + launch boom sound both still play.
- Hitstop/shake/FOV punch impact feel still fires correctly for both plain tackles and Speed Blitz knockdowns (these use different `TackleImpactFeelOverrides`).

---

### A3. Extract practice-NPC client-visual mirroring → `Code/Player/PracticeNpcTackleClientRelay.cs`

**Moves:** `BroadcastPracticeNpcFreezeForClient`, `BroadcastPracticeNpcRagdollForClient`, `BroadcastPracticeNpcStandUpForClient`, `ResolvePracticeNpcVisualBroadcaster`, `PracticeNpcClientFreezeRpc`, `PracticeNpcClientRagdollRpc`, `PracticeNpcClientStandUpRpc`, `TryFindPracticeNpcTackle`, `BeginPracticeNpcClientContactFreeze`, `MirrorPracticeNpcFreezeFromHost`, `PinPracticeNpcClientContactFreezePosition`, `ApplyKnockdownFreezeWorldPosition`, `ClearPracticeNpcClientContactFreezePin`, `MirrorPracticeNpcRagdollFromHost`, `MirrorPracticeNpcStandUpFromHost`, plus `practiceNpcClientContactFreezePinned` / `practiceNpcClientContactFreezePos` fields.

**Needs new internal API on `PlayerTackle`:** the mirror methods currently set `isRagdolled` / `wasRagdolled` directly and call the private `ApplyRagdollLocally()` / `StandUpLocally()` / `ApplyKnockdownAwaitingFreezeLocally()`. Add small `internal` methods on `PlayerTackle` for the sibling to call (e.g. `internal void ForceLocalRagdollTransitionForPracticeNpc(bool ragdolled)`), rather than exposing the private fields directly. Not a breaking/inspector change — just new internal surface.

**Cross-file touch:** None — practice NPC dummy scripts (`PracticeNpcPatrol`, etc. in `Code/Map/`) don't call any of these moved methods directly.

**Est. lines removed:** ~180.

**Test after this step (2-window MP — this only matters for non-host joiners):**
- Host + joined client, tackle a practice dummy from the **joiner's** window — dummy freeze/ragdoll/stand-up visuals should still mirror correctly on the client that isn't host.
- Speed Blitz hit on a practice dummy — same check.

---

### A4 (optional / stretch — higher risk). Extract ragdoll orbit camera + stand-up blend → `Code/Player/TackleRagdollCameraFeel.cs`

**Moves:** the `OnPreRender()` camera-lerp logic, `ComputeRagdollOrbitCamera`, `TryGetRagdollOrbitCamera`, and the camera-blend fields (`lastRagdollCameraPos/Rot`, `standUpCameraBlendFrom/StartTime`, `ragdollEnterBlendFrom/StartTime`, `deferringRagdollCamForImpactFeel`, `activeCamera`).

**Why this is riskier than A1–A3:** `PlayerTackle` has no explicit `[Order]` attribute today, so this logic's execution order relative to other camera components (`ThrowChargeCamera` `[Order(10002)]`, `SpeedBlitzDashCamera` `[Order(10012)]`) is implicit. Moving it to a new sibling means you may need an explicit `[Order]` tag to reproduce today's behavior, and subtle camera-ordering bugs (a one-frame snap, wrong blend start position) are much harder to spot in a quick smoke test than a missing ragdoll.

**Recommendation:** do this only after A1–A3 have shipped and felt solid for a few sessions. Test thoroughly: ragdoll orbit camera framing, stand-up blend smoothness, and the hazard-impact defer-then-blend path (`TackleImpactFeel.IsHazardImpact`) specifically.

**Est. lines removed:** ~120.

---

### PlayerTackle summary

| After steps | Approx. size |
|---|---|
| Today | 1,857 |
| A1 | ~1,380 |
| A1 + A2 | ~1,140 |
| A1 + A2 + A3 (**recommended stopping point**) | **~950–1,000** |
| + A4 (stretch) | ~830 |

What's left in core `PlayerTackle` after A1–A3: inspector properties, sync state, the main `OnUpdate`/`OnPreRender` loop, host tackle detection + owner RPC request + hit geometry validation (shared static helpers used by Speed Blitz too — leave these alone, `SpeedsterSpeedBlitzUlt` calls `PlayerTackle.TryValidateContactCylinder`), `ExecuteTackle`/`ApplyKnockdownFromHost`/`ApplyVictimKnockdownFromHost`, Juggernaut ramp (`StepTackleChargeBonus`), local down/up renderer/collider toggling, and the team-check helper. That's a coherent "tackle detection + authority" component — a reasonable place to stop.

---

## Track B — `SpeedsterSpeedBlitzUlt.cs` (currently 1,089 lines)

Only start this after Track A is fully shipped and confirmed stable.

### B1. Extract dash hit-detection geometry → `Code/Ultimates/Speedster/SpeedBlitzDashHitDetector.cs`

*(New file goes straight into the `Speedster/` subfolder per `ARCHITECTURE.md`'s target layout — no need to move the older Speedster files yet.)*

**Moves:** `TryFindDashHitAlongSegment`, `IsDashTargetInCorridor`, `TryGetDashContactPointOnSegment`, `GetDashContactDistance`, `IsDashHitPathClear`, `BuildDashHitTrace`, `ClosestPointOnSegment`, `ProjectAlongDashCorridor`, `LateralDistanceToDashCorridor`, `ClampDashSweepEndPosition`, `GetMaxDashSweepStepDistance`, `IsValidDashTarget`, `GetDasherBodyRadius`, `GetDashTargetBodyRadius`.

**Shape:** sibling component with a back-reference to `SpeedsterSpeedBlitzUlt` (via `Components.Get<>()` in its own `OnStart`), reading tunables (`HitHalfWidth`, `DashRange`, `DefaultTargetBodyRadius`, `HitStopContactGap`, `MaxHitVerticalSeparation`, `DashSweepStepMultiplier`, `DashSpeed`) straight off the orchestrator — **no property moves**, so no editor rewiring at all.

**Both callers stay the same shape:** `HostDashHitCheck`/`HostDashFinalHitCheck` (host) and `OwnerPredictDashHitCheck` (owner predict) both call the same shared method today — after this step they call `dashHitDetector.TryFindHitAlongSegment(...)` instead. Both call sites are inside `SpeedsterSpeedBlitzUlt.cs`, so no cross-file changes.

**Est. lines removed:** ~280–300.

**Test after this step:**
- Speed Blitz dash connects with an enemy at a range of angles/distances — corridor width and hit timing should feel identical to before.
- Dash whiffs correctly when a wall blocks line-of-sight to a target that's technically in the corridor.
- Host and owner-predict still agree on hit/miss (no case where the dasher's screen shows a hit but the host doesn't register it, or vice versa) — test in 2-window MP.

---

### B2. Extract internal-only connect-impact SFX plumbing → `Code/Ultimates/Speedster/SpeedBlitzConnectImpactRelay.cs`

**Moves (internal-only helpers — none of these are called from `PlayerTackle.cs`):** `PickConnectImpactSoundResourcePath`, `BroadcastConnectImpactSoundOnHost`, `TryConsumeHostConnectSoundDedupe`, `OwnerPlayPredictedConnectImpactSound`, `GetBlitzConnectImpactSoundPosition`, the `ownerPredictedConnectSoundThisDash` field, and the `ConnectImpactSoundA`/`ConnectImpactSoundB`/`ConnectImpactSoundVolume` properties.

**Deliberately stays on `SpeedsterSpeedBlitzUlt`** (do **not** move these — they're called from `PlayerTackle.cs` using the static syntax `SpeedsterSpeedBlitzUlt.MethodName(...)`, and moving them would force cross-file edits for very little line-count benefit): `ResolveLaunchSound`, `ResolveLaunchSoundResourcePath`, `ResolveLaunchSoundVolume`, `PlayLaunchSoundAt`, `ResolveConnectImpactSoundVolume`, `PlayConnectImpactSoundAt`, `TryConsumeHostConnectSoundDedupeForDasher`, `DefaultKnockdownImpactFeelOverrides`, `GetKnockdownImpactFeelOverrides`.

**⚠️ Editor rewiring needed:** `ConnectImpactSoundA`/`B` move with this step — re-drag those two crunch sound assets onto the new sibling on the Speedster prefab after adding it.

**Est. lines removed:** ~100–130 (smaller step — most of the SFX surface has to stay put per above).

**Test after this step:**
- Speed Blitz connect crunch sound still plays once (owner predict + host dedupe still works, no double-play).

---

### B3 (optional / stretch — higher risk). Extract owner movement + dash predict → `Code/Ultimates/Speedster/SpeedBlitzOwnerMovement.cs`

**Moves:** `OwnerUpdate`, `OwnerUpdateAimAndCommit`, `SuppressOwnerController`, `RestoreOwnerController`, `ApplyOwnerLookLock`, `ApplyPlantedHorizontalFreeze`, `OwnerZeroHorizontalVelocity`, `OwnerDriveDashMovement`, `ReportDashSamplePositionToHost`, `OwnerPredictDashHitCheck`, `OwnerApplyPredictedDashHit`, `ResetOwnerDashPredictState`, `GetHorizontalCommitDirection`, `LockEyeAnglesToHorizontalDirection`, and ~13 owner-only fields (look lock, controller-suppress state, dash predict position history, commit timing).

**Cross-file touch:** `SpeedBlitzAimPreview` (UI) calls `ult.GetAimPreviewParams(...)` — keep this as a one-line forwarding method on `SpeedsterSpeedBlitzUlt` that delegates to the new sibling, so the UI component needs zero changes.

**Why this is the riskiest step in the whole plan:** this is the component's own predictive-movement core, called every `OnFixedUpdate`/`OnPreRender` tick, sharing state with the host phase machine (`NetPhase`, `IsDashing`) and with B1's hit detector (`ownerPredictedHitThisDash` also gates B2's dedupe). A subtle mistake here manifests as "dash occasionally doesn't feel right" or "aim lock stutters" — exactly the kind of bug that's easy to miss in a short editor pass and only shows up after real play. Recommend doing this only if B1+B2 have felt solid for a while, and testing with a longer, deliberate 2-window session (commit → wind-up → dash → hit and miss cases → wall-slide case) rather than a quick smoke test.

**Est. lines removed:** ~300–330.

---

### SpeedsterSpeedBlitzUlt summary

| After steps | Approx. size |
|---|---|
| Today | 1,089 |
| B1 | ~800 |
| B1 + B2 (**recommended stopping point**) | **~650–700** |
| + B3 (stretch) | ~350–400 |

Note the ~220 lines of `[Property]` declarations + sync fields + the wind-up-feel `Resolve*` helpers (used externally by `SpeedBlitzWindUpFeel`) stay on the orchestrator regardless of how far you go — that's expected and matches the existing pattern, not a sign the split failed.

---

## After both tracks ship

- Update `ARCHITECTURE.md` § God components table with new sizes.
- Update `SESSION_NOTES.md` — check off "Split `PlayerTackle`" / "Split `SpeedsterSpeedBlitzUlt`" once landed.
- Update `NAMING_CANON.md` with the new component rows (should already be done incrementally per step 5 of the ground rules — this is just a final pass to make sure nothing was missed).
- Optional cleanup pass: move the remaining older Speedster files (`SpeedsterSpeedBlitzUlt.cs`, `SpeedBlitzWindUpFeel.cs`, `SpeedBlitzBodyGlow.cs`, `SpeedBlitzBodyGlowRenderSystem.cs`, `SpeedBlitzVfxResources.cs`) into `Code/Ultimates/Speedster/` alongside the new split files. Pure file relocation, zero logic change, zero functional risk — safe to do whenever.
