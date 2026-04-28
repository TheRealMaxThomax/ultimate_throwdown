# Playtest Checklist

Run this quick checklist before committing gameplay changes.

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

## Stability
- [ ] No script compile errors.
- [ ] `BallThrow` appears in Add Component list.
- [ ] `ThrowChargeBar` appears in Add Component list.
- [ ] No obvious regressions in grab/drop behavior.

## If Component Is Missing
Follow `Component Missing Recovery (s&box)` in `SESSION_NOTES.md`.
