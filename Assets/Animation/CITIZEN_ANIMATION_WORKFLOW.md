# Citizen human — custom animation workflow

**Use this when:** You add new Blender animations for the human avatar (throw charge, stand-up, hit, wave, emotes, etc.) and need them to work in s&box without breaking the rig.

**Related code:** `Code/Player/PlayerBallHoldAnim.cs` (throw charge poses via direct playback). Other mechanics (tackle stand-up, emotes) will get their own components later but **the same ModelDoc + FBX rules apply**.

---

## The short version

1. Animate on the **official citizen human rig** (centimeter scale).
2. Export FBX: **armature only, no mesh**.
3. Add an **AnimFile** sequence to `utd_citizen_human_throw.vmdl` (name it clearly, e.g. `wave`, `hit_react`).
4. Keep **ScaleAndMirror 0.3937** on that `.vmdl`.
5. **Compile** → preview in ModelDoc → wire in code or anim graph.

---

## Files in this folder

| File | Purpose |
|------|---------|
| `throw_anim_work.blend` | Blender working file |
| `citizen_human_male_REF_personal.fbx` | Your reference import (verify scale vs official) |
| `human@*.fbx` | Exported animation clips |
| `utd_citizen_human_throw.vmdl` | **Extension model** — adds custom sequences on top of `citizen_human_male` |
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

- **Animation Graph** = `citizen_human_m.vanmgrph` (unchanged)
- **Body Model** — `PlayerBallHoldAnim.CustomBodyModelPath` (default `animation/utd_citizen_human_throw.vmdl`) re-applies the extension model on spawn and **after cosmetics** (`ClothingContainer.ApplyAsync` resets Body to plain `citizen_human_male`). You do not need to hand-wire the Model on the prefab unless you use a different `.vmdl` path.

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
- **Throw charge (current):** one merged sequence **`throw_windup`** — `PlayerBallHoldAnim` scrubs charge bar **0→1** across the whole clip (no pops between phases).
- **Legacy fallback:** `hold_ready`, `charge_min`, `charge_max` if `throw_windup` is missing. Tune `ChargeHoldReadyPhaseEnd` / `ChargeMinPhaseEnd`.

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

Same sequence name = nothing else to touch. New sequence name = update the AnimFile node name + `PlayerBallHoldAnim.ChargeWindupSequenceName` + the Clip node in the graph.

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

**Code side (already implemented):** `PlayerBallHoldAnim.UseAnimGraphChargePose` (default on) sets `throw_charge` to the charge lerp and ramps `throw_charge_weight` 0→1 while charging (in/out blend times tunable). If the graph file is missing it logs once and charge shows the plain holditem stance (no T-pose).

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
| Anim only plays after unticking **NLA Strips** on FBX export | Action lives on an NLA track — exporter bakes strips, not the active action | Untick **NLA Strips** (keep Bake Animation on, All Actions off) |

**Isolation test:** Disable all custom AnimFile nodes → compile → preview should be normal citizen. Re-enable one clip at a time.

---

## Playing animations in code (patterns)

### Direct playback (overlay on anim graph)

Experimental only for **throw charge** in `PlayerBallHoldAnim`:

```csharp
// Preferred: one merged clip scrubbed by charge lerp
renderer.SceneModel.DirectPlayback.Play( "throw_windup" );
// Scrub: DirectPlayback.StartTime = Time.Now - (chargeLerp * Duration);
renderer.SceneModel.DirectPlayback.Cancel();
```

- Requires sequences on the Body **Model** (the extension `.vmdl`).
- The clip **must key every bone** (full-body). Bake the idle pose onto unkeyed bones — see "Full-body wind-up" above. Arm-only clips T-pose the rest of the body.
- **If revisiting custom graph-driven charge later:** prefer **`throw_windup`** (single clip). Legacy three-clip mode needs matching poses at joins.
- **Do not** scrub `*_delta` sequences or charge AnimFile weight-list experiments through DirectPlayback (pancake risk). Deltas are for holdtype graph Add nodes.
- Look during charge: `BallThrow` keeps look input enabled; movement stays blocked separately.
- Remotes need synced state if the pose must match (throw uses `NetThrowChargeLerp`).

### Built-in anim graph (`renderer.Set(...)`)

Used for **throw release** today: `holdtype`, `b_attack`, etc. on `citizen_human_m.vanmgrph`.

- Good for: hold/throw, locomotion, anything the graph already supports.
- Prefer built-in when it looks good (throw release stayed built-in).

### Future examples (not wired yet)

| Animation | Likely owner | Notes |
|-----------|----------------|-------|
| Stand up | `PlayerTackle` or ragdoll feel | May blend from ragdoll; test MP early |
| Hit / flinch | Combat / ball interact | Short one-shot direct playback or graph param |
| Wave / emote | New small component or emote RPC | Broadcast `Play` + sequence name for remotes |

When adding a new system, reuse: **same `.vmdl`**, new **Name**, new component or method that calls `DirectPlayback.Play( "your_name" )`.

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
