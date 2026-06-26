---
name: memo-framework-skill
description: "Build, modify, review, or debug the MemoFramework_Skill optional package and Hoshino skill timeline system. Use when a task mentions Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Skill, Hoshino Skill, SkillEditor, .skill files, compiled .bytes skills, SkillDefinition, SkillRuntimeNode, SkillActionTrack, SkillClipType, SkillCustomData, SkillGeneratedSerialization, SkillExecutor, skill timelines, skill blackboard/special data, or adding skill clips/executors."
---

# MemoFramework Skill

Use this skill for the `MemoFramework_Skill` optional package. It is a Slate-based skill timeline editor and runtime data pipeline, not a full gameplay controller. Project gameplay code must provide the runtime scheduler/context that consumes compiled skills.

## First Pass

1. Inspect the package and generated outputs:
   - `rg --files Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Skill -g '*.cs' -g '*.asmdef'`
   - `rg --files Assets/Scripts/Generated/Skill -g '*.cs'`
   - `rg -n "SkillClipType|SkillCustomData|SkillExecutor|SkillDefinition|SkillGeneratedExecutorBindings|SkillRuntimePlayer" Assets -g '*.cs'`
2. Use current code over older docs. `docs/handoff/skill-runtime-readme.md` is helpful background, but current package code uses generic `TContext` executor bindings and domain `int`, not a hard-coded Battle controller.
3. If the task requires opening the SkillEditor menu, running generation menus, editing `.skill`, importing samples, or compiling skills in Unity, explain the exact Editor action to the user. Use the menu constants in the source files when you need the exact localized menu label.

## Data Flow

The pipeline is:

```text
SkillEditor Cutscene timeline
  -> Assets/SkillData/*.skill plus *.skill.json debug file
  -> run the compile-all skill definitions menu
  -> Assets/SkillData/Compiled/*.bytes
  -> SkillDefinition.FromBytes(bytes)
  -> generated preloaded XxxNodeData
  -> project scheduler builds TContext
  -> SkillGeneratedExecutorBindings.TryGetExecutor<TContext>
  -> Executor.Execute(context)
```

Key files:
- Editor source format: `SkillSerializer`, `SkillFileData`, `SkillFileManager`.
- Runtime binary format: `SkillDefinition`, `SkillRuntimeNode`, `SkillRuntimeSpecialData`.
- Generated serialization: `Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedSerialization.cs`.
- Generated editor serialization: `Assets/Scripts/Generated/Skill/Editor/SkillGeneratedEditorSerialization.cs`.
- Generated executor bindings: `Assets/Scripts/Generated/Skill/Runtime/SkillGeneratedExecutorBindings.cs`.
- Minimal scheduler sample: `Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Skill/Samples/SkillRuntimePlayer.cs`.

## Editor Menus

- `SkillEditor.OpenWindow` menu item: open the skill timeline editor.
- `SkillSerializationCodeGenerator.GenerateMenuPath`: scan registered groups/tracks/clips/special data and regenerate `SkillGeneratedSerialization.cs` and `SkillGeneratedEditorSerialization.cs`.
- `SkillExecutorCodeGenerator.GenerateMenuPath`: scan `[SkillExecutor]` types and regenerate `SkillGeneratedExecutorBindings.cs`.
- `SkillExecutorPostCompileCodeGeneration` auto-generate menu: toggle post-compile automatic executor binding generation.
- `SkillDefinitionCompiler.CompileAll` menu item: compile `Assets/SkillData/*.skill` into `Assets/SkillData/Compiled/*.bytes`.
- `SkillDefinitionCompiler` debug JSON menu: toggle compiled debug JSON output.

## Adding A Clip

1. Create a Slate `ActionClip` class, usually in namespace `Hoshino` or project runtime code.
2. Add `[SkillClipType(uniqueId)]` and `[Attachable(typeof(SkillActionTrack))]`.
3. Expose all serialized runtime data as public instance fields marked `[SkillCustomData]`.
4. Override `length` and `isValid`; add editor preview methods only when useful.
5. Run the serialization generation menu from `SkillSerializationCodeGenerator.GenerateMenuPath`, then use the generated `SkillGeneratedIds.XxxClip` and `XxxNodeData`.

Supported `[SkillCustomData]` field types:
- Primitive numeric types, `bool`, `char`, `string`.
- Enums.
- `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color`, `LayerMask`, `AnimationCurve`.
- One-dimensional arrays of supported element types.

Restrictions:
- `[SkillCustomData]` fields must be public and non-static.
- Multidimensional arrays and arbitrary classes/structs are not supported unless the generator is extended.
- IDs must be globally unique across groups, tracks, clips, and special data.

Built-in registrations:
- `SkillActionTrack` id `101`.
- `SetVelocityClip` id `1002`, `TeleportClip` id `1003`, `AttributeModifierClip` id `1006`, `SingleDamageClip` id `1007`, `MultiDamageClip` id `1008`.
- `DamageGroupData` special data id `2001`.

## Adding An Executor

1. Define a project context struct implementing `ISkillExecutionContext`:
   - `SkillDefinition Skill { get; }`
   - `SkillRuntimeNode Node { get; }`
   - `ESkillNodeLifecyclePhase LifecyclePhase { get; }`
   - Add project fields such as actor, player, motor, services, network state, or delta time.
2. Optionally define project domain base classes:

```csharp
[SkillExecutorDomain((int)EMySkillDomain.Gameplay)]
public abstract class GameplaySkillExecutor<TData>
    : LifecycleSkillNodeExecutor<MySkillContext, TData>
    where TData : struct
{
}
```

3. Create a concrete executor:

```csharp
[SkillExecutor(SkillGeneratedIds.MyDashClip)]
public sealed class MyDashExecutor : GameplaySkillExecutor<MyDashNodeData>
{
    protected override void OnTick(in MySkillContext context, in MyDashNodeData data)
    {
        // Use context project fields and generated node data.
    }
}
```

4. Run the executor binding generation menu from `SkillExecutorCodeGenerator.GenerateMenuPath`. The generated table stores executor instances and domain ids, so runtime lookup is reflection-free.

Executor notes:
- `SkillNodeExecutor<TContext,TData>` reads `context.Skill.PreloadedNodeData` through `ISkillPreloadedNodeData<TData>`.
- `SkillDefinition.FromBytes` calls generated `Preload`, so the hot path should not deserialize node blobs each tick.
- `LifecycleSkillNodeExecutor` dispatches `Start`, `Tick`, and `End`; derive from `SkillNodeExecutor` directly only for custom lifecycle behavior.
- `SkillGeneratedExecutorBindings.TryGetDomain(clipId, out int domain)` is available for schedulers that split client/server/prediction domains.

## Runtime Scheduling

The package does not ship a production `SkillController`. Follow the sample scheduler shape:

1. Load `TextAsset.bytes` with `SkillDefinition.FromBytes`.
2. Advance fixed ticks using `SkillTickUtility` and a local elapsed tick counter.
3. For each `SkillRuntimeNode`, call `node.IsActiveAt(elapsedTicks)`.
4. Track active node ids:
   - entering active interval: call `Start`, then `Tick`
   - staying active: call `Tick`
   - leaving active interval or stopping skill: call `End`
5. Build the project `TContext` and call `SkillGeneratedExecutorBindings.TryGetExecutor<TContext>(node.ClipId, out executor)`.
6. Call `executor.Execute(context)`.

If using networking, keep network authority and prediction rules in project code. The Skill package only provides data, generated bindings, and executor base classes.

## Special Data

- Define skill-level blackboard data with `[SkillSpecialDataType(uniqueId)]` and public `[SkillCustomData]` fields.
- Edit special data through the Skill blackboard window in `SkillEditor`; it is cached by `SkillBlackboardCache` because Slate `Cutscene` is not modified.
- Compilation writes `SkillRuntimeSpecialData[]` plus a special-data blob.
- Runtime code reads it via generated serialization, for example `SkillGeneratedSerializationServices.Runtime.TryReadSpecialData<RuntimeDamageGroupData>(skill, entry, out data)`.

## Common Pitfalls

- Generated files under `Assets/Scripts/Generated/Skill` are part of the compile surface. Regenerate after changing clip fields, ids, special data, or executors.
- A missing generated runtime implementation causes `SkillGeneratedSerializationServices.Runtime` to throw and instruct running the serialization generation menu.
- Compiled `.bytes` use the `HCSK` binary format; editor `.skill` files use the `HOSK` binary format. Do not parse one as the other.
- `LengthTicks` is computed from the latest node end tick, not the editor timeline's full visual length.
- Imported samples may bind sample executors into generated bindings. Remove or replace sample executors when a real project context supersedes them.
