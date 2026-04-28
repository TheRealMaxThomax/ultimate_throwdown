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

## Stability
- [ ] No script compile errors.
- [ ] `BallThrow` appears in Add Component list.
- [ ] No obvious regressions in grab/drop behavior.

## If Component Is Missing
Follow `Component Missing Recovery (s&box)` in `SESSION_NOTES.md`.
