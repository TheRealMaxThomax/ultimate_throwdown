# Backlog

Use this file to track ideas, bugs, and next milestones.
Keep items short and clear.

## Current Milestone
- Make core grab/drop/throw loop feel good and stable.

## TODO (Next Up)
- [ ] Run extended 2-window multiplayer stress pass (15-20 min): pickup/drop/throw spam + jump-drop edge cases.
- [ ] Improve free-ball client feel to better match host while keeping shared position consistency.
- [ ] Tune `BallClientFeel` values (`FreeBallVisualFollowSharpness`, `ContactBoostSharpness`, `ContactBoostDuration`) with repeatable multiplayer push tests.
- [ ] Tune `ThrowForce`, `ThrowUpForce`, and `ThrowStartOffset`.
- [ ] Tune charged throw values (`MinThrowChargeTime`, `MaxThrowChargeTime`, `MinThrowForceMultiplier`, `MinThrowUpForceMultiplier`).
- [ ] Tune movement ramp values (`StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`).
- [ ] Decide if throw cooldown is needed.
- [ ] Decide next loop piece: scoring/reset or pass/catch.
- [ ] HUD timing decision: keep temporary debug charge bar until throw feel is stable for a few sessions, then build proper HUD charge panel.

## Bugs / Issues
- [ ] While charging throw, movement is locked but walk/run animations still play in place.
  - Plan: Fix during animation pass by driving animator from explicit charge/throw state (`IsChargingThrow`) instead of movement input alone.
- [ ] Verify no remaining client-only ball-drop desyncs in longer sessions.
  - Plan: Run multiplayer consistency checklist and record exact repro steps if any mismatch returns.
- [ ] Free-ball client collision feel is still less direct than host feel.
  - Plan: Keep host-authoritative ball truth, then improve client perceived responsiveness via safe visual tuning.

## Ideas (Later)
- [ ] Build proper HUD throw charge bar (single rectangle with red/yellow/green sections and smooth fill).
- [ ] Add animation hooks for hold/throw.
- [ ] Add catch-up sprint animation (head slightly down, arm up, charging posture) for the non-ball-carrier catch-up state.
- [ ] Add sound effects for pickup/drop/throw.
- [ ] Add UI feedback for throw cooldown (if added).

## Done
- [x] Split throw logic into `BallThrow`.
- [x] Keep hold-state source of truth in `BallGrab`.
- [x] Add pickup lockout after throw.
- [x] Set up Git + GitHub backup workflow.
- [x] Add hold-to-charge throw strength scaling.
- [x] Add `ThrowChargeBar` gameplay feedback component.
- [x] Lock player movement during throw charge while preserving camera aim.
- [x] Add `CatchUpSpeedBoost` component for staged movement speed ramp.
- [x] Make catch-up timer start only after sprint stage (prevents skipping middle speed).
- [x] Reset movement ramp while throw-charging so post-throw movement restarts at start speed.
- [x] Add multiplayer test setup/checklist items for `Join via new instance`.
- [x] Add `GameNetworkManager` and wire startup scene to `throwdown_prototype`.
- [x] Move grab/drop/throw into host-approved multiplayer flow.
- [x] Improve held-ball stability by handling rigidbody/collider state across ball hierarchy while held.
- [x] Switch `EnableNetDebugLogs` to off-by-default after stabilization pass.
- [x] Split client-side smoothing/feel logic from `BallGrab` into `BallClientFeel`.
