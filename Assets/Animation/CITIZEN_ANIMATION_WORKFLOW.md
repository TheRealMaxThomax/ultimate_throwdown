# Citizen human — custom animation workflow

**Use this when:** You add new Blender animations for the human avatar (throw charge, stand-up, hit, wave, emotes, etc.) and need them to work in s&box without breaking the rig.

**Related code:** `Code/Player/PlayerBallHoldAnim.cs` (throw wind-up), `Code/Player/PlayerChargeRunAnim.cs` (catch-up speed overlay). Other mechanics (tackle stand-up, emotes) will get their own components later but **the same ModelDoc + FBX + animgraph rules apply**.

---

## Choose how to play it (read this first)

Almost every custom human animation you make for this game should use **one of these three routes**. Pick based on whether the body must keep moving.

| Route | When to use | Body during playback | How |
|-------|-------------|----------------------|-----|
| **① Animgraph masked layer** | Custom motion **on top of** locomotion / look-at / idle (throw charge, aim overlays, partial gestures) | **Alive** — legs, spine look, head track | Fork `utd_citizen_human_m.vanmgrph` → Clip → Cycle Control → Bone Mask → 1D Blendspace → splice before final output. Drive float params from code. **This is the default for most of your work.** |
| **② Built-in graph params** | Facepunch already has it (hold item, throw release, locomotion) | Alive | `renderer.Set( "holdtype", … )`, `b_attack`, etc. Prefer this when it looks good. |
| **③ DirectPlayback** | Full-body **one-shot** emotes (wave, death, celebration) where a frozen body is OK | **Frozen** — whole skeleton replaced | `SceneModel.DirectPlayback.Play( "wave" )` in code. **Do not use for charge wind-up or anything that must layer on movement.** |

**Rule of thumb:** If you need the player to walk, turn, or look around while your animation plays → **animgraph fork (①)**. DirectPlayback (③) will T-pose or freeze the body.

**Project assets for route ①:**
- Sequences: `utd_citizen_human_throw.vmdl` (all custom FBX clips)
- Graph: `utd_citizen_human_m.vanmgrph` (fork of Facepunch `citizen_human_m.vanmgrph` + your masked layers)

### Shipped custom layers (two independent stacks — do not merge)

| Layer | Sequence | Graph params | Code | When it plays |
|-------|----------|--------------|------|----------------|
| **Throw wind-up** | `throw_windup` | `throw_charge`, `throw_charge_weight` | `PlayerBallHoldAnim` | Holding ball + charging throw (`BallThrow.IsChargingThrow`). Movement blocked. |
| **Charge run** | `charge_run` | `charge_run_cycle`, `charge_run_weight` | `PlayerChargeRunAnim` | **Not** holding ball + movement ramp tier **Charge** only (max of walk/sprint/charge — same label as `MovementRampHud`). |

**These never overlap:** throw charge blocks movement and ball carriers **cannot** reach charge speed (sprint cap while holding). **Separate** Clip → Cycle Control → Bone Mask → 1D Blendspace chains in `utd_citizen_human_m.vanmgrph`, wired **in series** before `"Restore helpers to clean state"` (`1D Blendspace A` = throw → `1D Blendspace B` = charge_run → restore helpers). Do **not** replace one clip with the other on a shared node.

**`charge_run` mask:** custom weight list `UTD_Charge_Overlay` (right arm + spine + head; **no** left arm so run swing stays on the base graph).

**`1D Blendspace B` wiring (charge_run) — verified 2026-06-11:**

| Blend entry | User value | Wire to |
|-------------|------------|---------|
| First | **0.0** | **`1D Blendspace A`** output (normal locomotion + throw layer) |
| Second | **1.0** | **Bone Mask** (`UTD_Charge_Overlay`) above the `charge_run` clip |

Parameter = `charge_run_weight`. **Common mistakes:** second entry still at **0.0** → overlay never shows at charge speed; bone mask on entry **0** only → overlay always on.

---

## The short version

1. Animate on the **official citizen human rig** (centimeter scale).
2. Export FBX: **armature only, no mesh**.
3. Add an **AnimFile** sequence to `utd_citizen_human_throw.vmdl` (name it clearly, e.g. `wave`, `hit_react`).
4. Keep **ScaleAndMirror 0.3937** on that `.vmdl`.
5. **Compile** → preview in ModelDoc.
6. **Wire in the animgraph fork** (route ①) for layered anims, or code `Set` / DirectPlayback for the other routes.

---

## Files in this folder

| File | Purpose |
|------|---------|
| `throw_anim_work.blend` | Blender working file |
| `citizen_human_male_REF_personal.fbx` | Optional Blender reference import |
| `throw_windup.fbx`, `charge_run.fbx` | **Active** exported clips (in ModelDoc) |
| `utd_citizen_human_throw.vmdl` | **Extension model** — custom sequences on `citizen_human_male` (currently **`throw_windup`** + **`charge_run`** only) |
| `utd_citizen_human_m.vanmgrph` | Forked animgraph (masked layers) |
| This doc | Workflow + troubleshooting |

**Note:** The `.vmdl` filename says “throw” but it is the **shared bucket** for all custom human sequences (wave, hit, stand-up, etc.). Add new **AnimFile** nodes here; you do not need a new `.vmdl` per animation.

---

## How the in-game model works

```
citizen_human_male.vmdl          ← Facepunch body, skeleton, built-in anims, ScaleAndMirror baked in
        ↑
        │  Base Model
        │
utd_citizen_human_throw.vmdl     ← YOUR extra sequences only + ScaleAndMirror 0.3937 on new FBXs
```

**Player prefab (when using custom sequences):**

- **Animation Graph** — code applies `utd_citizen_human_m.vanmgrph` via `PlayerBallHoldAnim` (no prefab wiring needed; re-applied after cosmetics).
- **Body Model** — `PlayerBallHoldAnim.CustomBodyModelPath` (default `animation/utd_citizen_human_throw.vmdl`) re-applies the extension model on spawn and **after cosmetics** (`ClothingContainer.ApplyAsync` resets Body to plain `citizen_human_male`).

While debugging a new clip, use ModelDoc preview on the extension `.vmdl` until sequences look correct.

---

## Blender — rig and scale

### Official source (compare / re-import if needed)

```
Steam\steamapps\common\sbox\addons\citizen\Assets\models\citizen_human\
```

Facepunch sources are **centimeters**. In Blender with metric display, a correct armature is roughly **~1.7–1.9 m** on its tallest axis (same as ~170–190 cm).

| Armature height | Verdict |
|-----------------|---------|
| ~1.7–1.9 m (metric) | Good — citizen cm workflow |
| ~0.7–0.75 m | Wrong — Hammer/game 72-unit scale; re-rig on official FBX |

**Do not** use `game_artstyle.md` 72-unit player block scale for citizen **animation** export. That scale is for map props.

### Before every export

1. Select **armature only** (mesh hidden / not selected).
2. **Ctrl+A → Apply → All Transforms** on the armature (scale **1, 1, 1**).
3. For full-body clips: frame 1 should be a sane idle unless the whole body is meant to move.
4. For partial clips (e.g. wave): unkeyed bones should still match **citizen idle**, not a broken bind.

---

## FBX export settings (Blender)

**File → Export → FBX (.fbx)**

| Setting | Value |
|---------|--------|
| Limit to | Selected Objects |
| Object Types | **Armature ON** · **Mesh OFF** |
| Bake Animation | ON |
| All Actions | OFF (one action per file) |

Naming: `human@<sequence_name>.fbx` (e.g. `human@wave.fbx`). The **sequence name** in ModelDoc (`wave`) can differ from the filename; keep them aligned when possible.

**Armature-only exports are much smaller** than mesh+armature. If the file is huge, mesh probably got included.

---

## ModelDoc — add a new sequence

1. Open **`utd_citizen_human_throw.vmdl`** in ModelDoc.
2. Confirm **Base Model** = `models/citizen_human/citizen_human_male.vmdl`.
3. Confirm **ModelModifierList → ScaleAndMirror** scale = **0.3937** (leave enabled).
4. Under **AnimationList**, add an **Anim File** (or right-click AnimationList → **Add Simple Animations** and pick the FBX).
5. Set the node **Name** to the sequence id code will use (e.g. `hold_ready`, `wave`, `hit_react`).
6. **Source File** → your `animation/human@….fbx`.
7. **Save + Compile**.
8. Preview: built-in **`IdlePose_Default`** should look normal; then preview **your** sequence.

### Sequence naming

- Use **unique, lowercase_with_underscores** names (`charge_max`, `stand_up`, `wave`).
- Avoid colliding with built-in citizen sequence names.
- **Throw charge (current):** one merged sequence **`throw_windup`** — `PlayerBallHoldAnim` scrubs charge bar **0→1** via animgraph (`throw_charge` / `throw_charge_weight`).
- **Movement charge overlay:** **`charge_run`** — `PlayerChargeRunAnim` at movement tier **Charge** only.
- **Removed 2026-06-11:** legacy three-phase `hold_ready` / `charge_min` / `charge_max` (DirectPlayback era) — delete AnimFiles before deleting FBX sources.

### Throw charge — seamless wind-up + idle body (important)

Direct playback **replaces the full skeleton** for any bone your clip touches. Two common bugs:

| Symptom | Cause | Fix |
|---------|--------|-----|
| Pop when charge phases change | Three separate clips (`hold_ready` → `charge_min` → `charge_max`) with mismatched first/last poses | **Merge into one `throw_windup` clip** in Blender (see below) and import as a single AnimFile |
| Legs / spine in **T-pose** while arms wind up | Clip only keys arm bones — DirectPlayback replaces the **whole skeleton**, so every **unkeyed** bone falls back to bind pose (T-pose) | **Make the clip full-body**: bake the citizen idle pose onto every unkeyed bone (see "Full-body wind-up" below). One keyframe per body bone at frame 1 is enough. |
| **Pancake** on charge | Playing `*_delta` / AnimSubtract, or charge clip weight-list experiments, through **DirectPlayback** | **Do not** use deltas or charge AnimFile weight lists with DirectPlayback. Built-in deltas go through holdtype **Add** nodes only. Keep `UseDeltaChargeSequences` off. |

#### Merge `hold_ready` + `charge_min` + `charge_max` → `throw_windup`

1. In `throw_anim_work.blend`, put the three actions on one NLA strip (or one timeline) **back-to-back**.
2. Match poses at each join — last frame of `hold_ready` = first frame of `charge_min`, etc.
3. Export **one** FBX: `throw_windup.fbx` (armature only).
4. ModelDoc → AnimFile **Name** = `throw_windup` on `utd_citizen_human_throw.vmdl`.
5. Compile. Code auto-uses it (`PlayerBallHoldAnim.ChargeWindupSequenceName`, default `throw_windup`).

#### Full-body wind-up — bake idle pose onto unkeyed bones (recommended v1)

DirectPlayback is fine **as long as the clip keys every bone**. The T-pose bug only happens when the clip is arm-only. Fix it in Blender by baking the official idle pose onto the rest of the body:

1. Official idle pose lives at:
   `C:\Program Files (x86)\Steam\steamapps\common\sbox\addons\citizen\Assets\models\citizen_human\animations\Human@IdlePose_Default.fbx`
2. In `throw_anim_work.blend` (your `throw_windup` action active): **File → Import → FBX** the idle file. It imports as a second armature, posed in standing idle, same bone names.
3. Imported armature → Pose Mode → select all bones (`A`) → **Pose → Copy Pose**.
4. Your rig → Pose Mode → **frame 1** → select all bones → **Pose → Paste Pose**.
5. **Deselect every bone that already has keyframes** (the throw-arm chain — check the Dope Sheet / Action Editor channel list).
6. With only the unkeyed body bones selected: `I` → **Location, Rotation & Scale**. One key at frame 1 holds for the whole clip.
7. Delete the imported idle armature. Export armature-only as usual → recompile the `.vmdl`.

Polish: match frame 1 of the wind-up roughly to the in-game `holditem` RH stance to avoid a pop when charge starts; hand-key the left arm if it looks stiff.

Trade-off: during charge the body is a frozen idle (no breathing/look-at for remote viewers). **Superseded for the throw charge** by the animgraph fork below (live body) — but the bake is still worth keeping in the clip: with the `Blend_UpperBody_HalfSpine_FullArms` mask, the spine + left arm **also come from your clip**, so they need sane keys.

#### Iterating on a clip (e.g. improving `throw_windup`)

Overwrite-and-go — no graph or code changes needed:

1. Animate in `throw_anim_work.blend` (same action). Keep all bones covered by the bone mask keyed (right arm + spine + left arm for the HalfSpine mask).
2. Export over `Assets/Animation/throw_windup.fbx` — armature only, transforms applied, Bake Animation ON, All Actions OFF, **NLA Strips OFF**.
3. The asset system usually recompiles `utd_citizen_human_throw.vmdl` on demand; if the change doesn't show, open it in ModelDoc → Save + Full Compile.
4. The charge bar maps 0→1 across the **whole clip length**, whatever it is.

Same sequence name = nothing else to touch. New sequence name = update the AnimFile node name + the Clip node in the graph + any code that drives it.

#### Custom anim layered over a LIVE body (animgraph fork — the real partial-body solution)

This is how you get "my custom arm wind-up plays, but the body still walks/turns/looks around naturally". It is also the general pattern for **any** future animation that must layer on top of built-in movement. DirectPlayback can never do this (it replaces the whole skeleton) — the layering must happen **inside the animgraph**, which is exactly how Facepunch does their own reloads/gestures/holdtypes.

**Key facts (verified against the shipped citizen files):**

- Graph source is editable: `Steam\steamapps\common\sbox\addons\citizen\Assets\models\citizen\citizen_human_m.vanmgrph` — copy it into the project and edit the copy.
- The human model ships ready-made bone masks (in `human_weightlistlist.vmdl_prefab`): `Only_RightArm`, `Only_UpperBody`, `Blend_UpperBody_HalfSpine_FullArms`, `Blend_UpperBody_HalfArms`, `Only_Fingers`, …
- Scrubbing a sequence with a float parameter = **Sequence node → Cycle Control node** (Cycle Control: `Value Source` = **Parameter**). This is Facepunch's own pattern. (**Not** the Single Frame node — that one only takes a fixed frame number.)
- A **Bone Mask** node plays a layer on only the bones in a weight list; everything else keeps the normal graph output (locomotion, look-at, idle).

**One-time setup (project graph fork) — VERIFIED WORKING 2026-06-11:**

1. Copy `citizen_human_m.vanmgrph` (source file, not `_c`) into `Assets/Animation/` and rename it **`utd_citizen_human_m.vanmgrph`**.
2. Double-click it in the Asset Browser to open the AnimGraph editor. Set the **preview model** to `utd_citizen_human_throw.vmdl` so custom sequences and weight lists are visible.
3. In the **Parameters** panel add two Float parameters (range 0–1, default 0): **`throw_charge`** (scrub) and **`throw_charge_weight`** (layer blend). Exact lowercase spelling — code sets them by name.
4. Find the splice point: scroll to the **far right** of the graph. Directly before the final output node sits a Bone Mask named **"Restore helpers to clean state"** (note about jittery helper bones). Splice **into its Input 1 wire** — never between it and the final output (it must stay last to clean helper/twist bones). The node feeding it = **X** (the finished live full-body pose).
5. Add four nodes via right-click on empty canvas. **Editor menu names differ from what you'd guess:**
   - **Add Clip** → pick `throw_windup` → creates the **Animation Clip** node (this *is* the "sequence node"; there is no "Sequence" entry in the menu).
   - **Add Node** → **Cycle Control** — set `Value Source` = Parameter → `throw_charge`. (Scrubs the clip from the parameter instead of a clock. **Not** Single Frame — that only takes a fixed frame number.)
   - **Add Node** → **Bone Mask** — weight list = `Blend_UpperBody_HalfSpine_FullArms` (or `Only_RightArm` for a tighter mask). **Input 1 = base, Input 2 = layer; bones in the weight list come from Input 2**, all others pass through from Input 1. Timing behavior: use Child 1; Root Motion Blend 0.
   - **Add Node** → **1D Blendspace** (the menu has no plain "Blend" — 1D Blendspace with two entries IS the blend node; Facepunch's own `holdtype_pose` slider is the same node type). Two entries: blend value **0.0** and **1.0**; `Blend Value Source` = Parameter → `throw_charge_weight`. Untick **Sync Cycles**.
6. Wire: Clip → Cycle Control → Bone Mask **Input 2**. X → Bone Mask **Input 1**. X → Blendspace entry **0.0**; Bone Mask → Blendspace entry **1.0**. Blendspace → "Restore helpers to clean state" Input 1.
7. Save (Ctrl+S), then **test in the editor before Play**: drag the `throw_charge_weight` slider to 1 in the Parameters panel and scrub `throw_charge` — preview body should stay alive while the masked bones run the wind-up.

**Code side (already implemented):** `PlayerBallHoldAnim.UseAnimGraphChargePose` (default on) sets `throw_charge` from the same 0→1 value as the throw charge bar (`BallThrow.GetThrowChargeLerp()` / `NetThrowChargeLerp` on remotes) and ramps `throw_charge_weight` 0→1 while charging. If the graph file is missing it logs once and charge shows the plain holditem stance (no T-pose).

#### Sync wind-up speed with the charge bar

`throw_charge` is **not a playback clock** — it is a **scrub position** (0 = start of clip, 1 = end). The charge bar and the anim use the **same** lerp, so they stay in sync **as long as the clip is authored correctly**.

| Symptom | Cause | Fix |
|---------|--------|-----|
| Wind-up finishes while the bar is still filling | All motion is packed into the **first part** of the clip (e.g. wind-up in frames 1–20, hold pose for frames 21–90) | **Best:** in Blender, spread the wind-up keys across the **full timeline** (~3 s to match `BallThrow.MaxThrowChargeTime`). Delete trailing hold frames at max pose. |
| Same, quick inspector fix | Motion only uses the first ~25% of the clip cycle | On `PlayerBallHoldAnim`, lower **`ChargeWindupCycleEnd`** (e.g. `0.25`) so a full charge bar maps to that sub-range only |
| Bar and preview slider match but in-game feels off | Clip **Playback Speed** on the Animation Clip node is not 0 | Set clip **Playback Speed = 0**; only Cycle Control drives position |

**Blender target:** one continuous wind-up from frame 1 → last frame, **no** long hold at max pose at the end. Re-export → recompile → done.

**CRITICAL runtime gotcha (cost us an hour):** clothing/cosmetics (`ClothingContainer.ApplyAsync`) swaps the Body model, which **rebuilds the renderer's scene model on the model's DEFAULT graph** — silently dropping the custom-graph override even though the component property still reports it as set. Symptoms: graph perfect in editor preview, parameters set in game, **nothing happens in game**. Fix (in `EnsureCustomAnimGraph`): **force re-assignment** (`renderer.AnimationGraph = null;` then `= customGraph;`) every time the Body model is ensured — never trust `renderer.AnimationGraph != customGraph` as an "already applied" check. `EnsureCustomBodyModel()` runs this on spawn and again after cosmetics.

**Adding more layered animations later:** same fork, new sequence in the `.vmdl`, new parameter(s) + another Clip → Cycle Control → Bone Mask → Blendspace cluster spliced at the same point (chain them: previous blendspace output feeds the next one's base). One graph holds all of them.

**Why the old attempts failed (do not retry):**

- Arm-only clips through DirectPlayback → unkeyed body bones become bind/T-pose.
- `*_delta` / AnimSubtract through DirectPlayback → pancake.
- Charge AnimFile `weight_list_name` through DirectPlayback → also pancaked in playtesting.
- Full-body baked clip through DirectPlayback → works but the body is **frozen** (no locomotion/look-at) — fine for emotes, wrong for charge.

---

## ScaleAndMirror — when it helps and when it hurts

| Setup | Result |
|-------|--------|
| Armature-only FBX + **ScaleAndMirror 0.3937** | **Correct** height (cm → engine inches) |
| Armature-only FBX, **no** ScaleAndMirror | **Too tall** (~2.5×) |
| Mesh + armature in FBX | **Pancake** / broken bind |
| Mesh in FBX + ScaleAndMirror | **Worse** pancake |

**Rule:** Armature-only export **and** ScaleAndMirror on the extension `.vmdl`. Both steps.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|----------------|-----|
| Pancake / flat mesh | Mesh exported with FBX | Re-export **armature only** |
| Freakishly tall | CM anim without ScaleAndMirror | Enable **0.3937** on extension `.vmdl` |
| Built-in idle OK, custom broken | FBX export / bind only | Re-export; check unkeyed bones |
| Sequence missing in game | Wrong Body model or name mismatch | Body = `utd_citizen_human_throw`; names match code |
| Console warning from `PlayerBallHoldAnim` | Sequence not compiled / wrong name | Compile `.vmdl`; check spelling |
| Graph layer works in **editor preview** but does nothing **in game** | Cosmetics rebuilt the scene model on the default graph — custom graph override silently dropped | Force re-assign the graph after every model change (`AnimationGraph = null` then the custom graph) — already done in `EnsureCustomAnimGraph()` |
| `charge_run` always on (walk/sprint/idle) | `1D Blendspace B` entry **0** wired to bone mask, or only one entry connected | Entry **0** = `1D Blendspace A`; entry **1** = bone mask; values **0.0** / **1.0** |
| `charge_run` never on at charge speed | Both blend entries at **0.0**, or `charge_run_weight` not reaching 1 | Set second entry user value to **1.0**; test in graph sim with `charge_run_weight` = 1 |
| Tackle: frozen ~8s, console shows `ApplyRagdollLocally` but no flop | Stale compiled extension after ModelDoc save; tackle state OK, physics ragdoll not | **Reboot editor** or open `utd_citizen_human_throw.vmdl` → Save + Full Compile; re-test. Enable `PlayerTackle.EnableTackleDebugLogs` → look for `Impulse failed` |
| Anim only plays after unticking **NLA Strips** on FBX export | Action lives on an NLA track — exporter bakes strips, not the active action | Untick **NLA Strips** (keep Bake Animation on, All Actions off) |

**Isolation test:** Disable all custom AnimFile nodes → compile → preview should be normal citizen. Re-enable one clip at a time.

---

## Playing animations in code (patterns)

### ① Animgraph params (layered custom anims — default)

**Throw wind-up** (`PlayerBallHoldAnim` — ball carrier, charging throw):

```csharp
renderer.Set( "throw_charge", cycle );       // 0→1 = throw charge bar
renderer.Set( "throw_charge_weight", weight );
```

**Charge run** (`PlayerChargeRunAnim` — no ball, at catch-up/charge/max speed only):

```csharp
renderer.Set( "charge_run_cycle", 0f );     // static pose; scrub if clip has motion
renderer.Set( "charge_run_weight", weight );
```

- Clips on `utd_citizen_human_throw.vmdl`; layers on `utd_citizen_human_m.vanmgrph` — **one independent stack per anim** (do not share nodes between `throw_windup` and `charge_run`).
- Remotes: `NetThrowChargeLerp` for throw; charge run uses same ramp tier logic (`MovementRampTier.Charge` / `CatchUpSpeedBoost.NetAtChargeSpeed`).

### ② Built-in anim graph (`renderer.Set(...)`)

Hold item, throw release, locomotion — `holdtype`, `b_attack`, etc. Prefer when Facepunch already has it.

### ③ DirectPlayback (full-body emotes only)

```csharp
renderer.SceneModel.DirectPlayback.Play( "wave" );
renderer.SceneModel.DirectPlayback.Cancel();
```

- **Frozen body** — whole skeleton replaced. Good for wave / celebration / short reactions.
- **Not** for charge wind-up or anything that must layer on movement (T-pose / pancake history).
- Clip must be full-body if you use this route.

### Future examples

| Animation | Route | Notes |
|-----------|-------|-------|
| Throw charge | ① Animgraph | Shipped — `throw_charge` / `throw_charge_weight` |
| Stand up | ① or ③ | Ragdoll blend may need graph; test MP early |
| Hit / flinch | ① | Short masked layer or graph one-shot |
| Wave / emote | ③ | Full-body DirectPlayback + RPC for remotes |

---

## Checklist — new animation end-to-end

- [ ] Animated on official-scale citizen rig (~1.8 m metric height)
- [ ] Exported **armature only**, transforms applied
- [ ] FBX saved under `Assets/Animation/`
- [ ] **AnimFile** added to `utd_citizen_human_throw.vmdl` with unique **Name**
- [ ] **ScaleAndMirror 0.3937** still on the `.vmdl`
- [ ] Compiled; **IdlePose_Default** + new sequence look correct in ModelDoc
- [ ] Player Body model = extension `.vmdl` (when testing in Play)
- [ ] Code plays sequence name; MP tested if others must see it

---

## References

- [s&box Model Editor](https://sbox.game/dev/doc/editor/model-editor) — Base Model, Add Simple Animations
- [Citizen characters](https://sbox.game/dev/doc/assets/ready-to-use-assets/citizen-characters) — cm sources, ScaleAndMirror 0.3937
- Project: `SESSION_NOTES.md` (ball carrier / throw anim summary)
- Project: `NAMING_CANON.md` → `PlayerBallHoldAnim`
