# Backlog

Use this file to track ideas, bugs, and next milestones.
Keep items short and clear.

## Current Milestone
- **Gameplay polish + MP soak** — v1 match flow (slices 1–6) shipped. See [`MATCH_FLOW_PLAN.md`](MATCH_FLOW_PLAN.md).

## TODO (Next Up)
- [ ] **`BallCarrierOffscreenHud`** — fix/polish team-only edge arrow toward off-screen teammate ball carrier.
- [ ] Run extended 2-window multiplayer stress pass (15-20 min): goals, OT, intermission, match over, rematch, HUD sync.
- [ ] **Map vote** — 30s timer, all players vote `Slot1`–`N`, plurality wins, random tie-break among top (see MATCH_FLOW_PLAN → Later).
- [ ] Improve free-ball client feel to better match host while keeping shared position consistency.
- [ ] Tune `BallClientFeel` values (`FreeBallVisualFollowSharpness`, `ContactBoostSharpness`, `ContactBoostDuration`) with repeatable multiplayer push tests.
- [ ] Tune `ThrowForce`, `ThrowUpForce`, and `ThrowStartOffset`.
- [ ] Tune charged throw values (`MinThrowChargeTime`, `MaxThrowChargeTime`, `MinThrowForceMultiplier`, `MinThrowUpForceMultiplier`).
- [ ] Tune movement ramp values (`StartMoveSpeed`, `SprintMoveSpeed`, `CatchUpMoveSpeed`, `TimeToSprintSpeed`, `TimeToCatchUpSpeed`).
- [ ] Match HUD: replace placeholder draw with styled UI when art pass is ready (slice 5 placeholders work).
- [ ] Replace placeholder owner HUDs (`ThrowChargeBar`, `MovementRampHud`, `DodgeCooldownHud`) with styled final UI when art pass is ready (`ThrowChargeBar` already on screen — vertical above dodge).

## Bugs / Issues
- [ ] While charging throw, movement is locked but walk/run animations still play in place.
  - Plan: Fix during animation pass by driving animator from explicit charge/throw state (`IsChargingThrow`) instead of movement input alone.
- [ ] Verify no remaining client-only ball-drop desyncs in longer sessions.
  - Plan: Run multiplayer consistency checklist and record exact repro steps if any mismatch returns.
- [ ] Free-ball client collision feel is still less direct than host feel.
  - Plan: Keep host-authoritative ball truth, then improve client perceived responsiveness via safe visual tuning.

## Ideas (Later)
- [ ] Final art pass on throw charge bar (red/yellow/green sections, smooth fill — replace placeholder vertical bar).
- [ ] Add animation hooks for hold/throw.
- [ ] Add catch-up sprint animation (head slightly down, arm up, charging posture) for the non-ball-carrier catch-up state.
- [ ] Add sound effects for pickup/drop/throw.

## Done
- [x] **Ball carrier glow** — `BallCarrierOutline`: gold colour-pulse outline + emissive breathe; non-carrier viewers only; no through walls; ring width distance-scaled.
- [x] **Throw polish MP (2026-06-09)** — 2-window OK: trajectory, charge camera, charge bar, tackle-while-charging.
- [x] **Throw trajectory landing marker** — 1:1 held-ball clone + `ball_translucent.vmat`; client translucent grain fix.
- [x] **Throw trajectory preview (polish)** — scrolling dashed arc, landing sphere, simulation-time dash scroll; `ThrowReleaseMath` physics match.
- [x] **`ThrowChargeCamera`** — charge-scaled third-person pullback + mild FOV; release blend; ragdoll/stand-up handoff (`PlayerTackle.OnPreRender` ragdoll cam).
- [x] **Throw trajectory preview (v1)** — `ThrowTrajectoryPreview` + `ThrowReleaseMath`; owner-only first arc + landing marker; physics-matched (gravity, damping, sphere sweep, release pivot).
- [x] **`ThrowChargeBar` screen HUD** — vertical bar above dodge (placeholder; replaced world `DebugOverlay` text).
- [x] **MP tackle launch parity** — impulse before `NetworkSpawn`, body poll, owner Juggernaut bonus in client tackle RPC (`PlayerTackle`).
- [x] **Enemy team outlines** — `Highlight` on camera, `PlayerEnemyOutline`, `RagdollEnemyOutline` (copies prefab `HighlightOutline`, `NetVictimTeamId` for clients).
- [x] **Match flow slice 6** — `MatchOverHud`, 10s match-over celebration, host `1` rematch, `SnapBallToGround`, `NetMatchWinnerTeamId` sync.
- [x] Match flow slices 1–5 (teams, director, goals, reset/freeze, HUD).
- [x] No throw cooldown — only existing `PickupDelayAfterThrow` / drop lockouts gate re-grab.
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
