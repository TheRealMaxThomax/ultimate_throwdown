# Playtest Checklist

Run this quick checklist before committing gameplay changes.

## Multiplayer Test Setup (Solo)
- [ ] Start play mode in editor (this is your host window).
- [ ] In header bar, click the network status icon.
- [ ] Click `Join via new instance` to spawn a second client window.
- [ ] Confirm second window joins host session successfully.
- [ ] If reconnect is needed, use `reconnect` in the second window console.
- [ ] If needed, manual join fallback is `connect local` in second window console.

## Core Ball Loop
- [ ] Can approach the ball and see prompt text.
- [ ] Can pick up ball using `InteractAction`.
- [ ] While holding, can drop ball correctly.
- [ ] While holding, can throw ball using `ThrowAction`.
- [ ] After throw, pickup is blocked briefly then allowed again.

## Throw Feel
- [ ] Throw direction follows `ThrowDirectionSource` as expected.
- [ ] Throw distance feels reasonable.
- [ ] Throw arc (`ThrowUpForce`) feels reasonable.
- [ ] Ball spawn offset on throw does not look broken/clipping.
- [ ] Short tap throw is clearly weaker than full-charge throw.
- [ ] Charge timing feels good (`MinThrowChargeTime` to `MaxThrowChargeTime`).
- [ ] Charge bar updates smoothly and matches throw strength.

## Charge Lock Behavior
- [ ] While charging, player cannot move.
- [ ] While charging, camera aim still works.
- [ ] On release throw, movement returns immediately.

## Movement Ramp (`CatchUpSpeedBoost`)
- [ ] Forward movement without ball ramps through all 3 stages (start -> sprint -> catch-up) with no stage skip.
- [ ] Forward movement with ball ramps to sprint stage only (no catch-up).
- [ ] Dropping ball while sprinting starts catch-up countdown from non-holder sprint time (no instant catch-up).
- [ ] While charging throw, movement ramp resets to start speed.
- [ ] After throw release, movement ramp restarts from start speed.

## Stability
- [ ] No script compile errors.
- [ ] `BallThrow` appears in Add Component list.
- [ ] `ThrowChargeBar` appears in Add Component list.
- [ ] `CatchUpSpeedBoost` appears in Add Component list.
- [ ] No obvious regressions in grab/drop behavior.

## Multiplayer Consistency (2 Windows)
- [ ] Client pickup appears correctly on host window.
- [ ] Client throw appears correctly on host window (no "double ball" mismatch).
- [ ] Rapid pickup/throw spam stays consistent on both windows.
- [ ] Throw charge bar appears only for the throwing player.

## If Component Is Missing
Follow `Component Missing Recovery (s&box)` in `SESSION_NOTES.md`.
