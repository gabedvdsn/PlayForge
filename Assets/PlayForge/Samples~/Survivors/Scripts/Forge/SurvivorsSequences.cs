using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The demo's task sequences, which its abilities run with a Run Sequence task: a volley that spawns the ability's
    /// process, so many and in the pattern its Local Data says. One sequence serves every ability that throws, shoots or
    /// drops something, and each ability's asset says what and how: a designer changes a volley in the ability, not here.
    /// </summary>
    public static class SurvivorsSequences
    {
        /// <summary>The Local Data key of the attribute whose value a volley adds to its count, as Projectile Amount.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag AddsToCount = Tag.Generate("Survivors.AddsToCount");

        private static readonly List<ITarget> Empty = new();

        /// <summary>
        /// Volley: the ability's process, its Count by its level plus its caster's value of the attribute its Local Data
        /// names, spawned in its Pattern: at its targets (Targets), fanned around its aim (Fan), all around its caster (Ring),
        /// on each target's spot (Spots), or spread around its caster to circle it (Orbit). Its Interval spaces them out.
        /// </summary>
        [TaskSequenceMethod("Survivors: Volley")]
        public static TaskSequence Volley() =>
            TaskSequenceBuilder.Create("Volley")
                .WithAnnotation("Spawns the ability's process, so many by its level and its caster's bonus, in its pattern")
                .Task(async (d, token) =>
                {
                    if (d is not AbilityDataPacket data || data.EffectOrigin is not AbilitySpec spec || spec.Base == null) return;
                    if (data.EffectOrigin.GetSource() is not { } caster) return;
                    var ability = spec.Base;
                    if (!ability.TryGetLocalData(SurvivorsTags.Projectile, out var shotData) || !shotData.TryGet(out AbstractMonoProcess shot) || !shot)
                        return;

                    int count = ability.TryGetLocalData(SurvivorsTags.Count, out var counted) && counted.TryGet(out ScalableInt number)
                        ? Mathf.Max(1, number.Evaluate(NumberContext.Of(data))) : 1;
                    if (ability.TryGetLocalData(AddsToCount, out var added) && added.TryGet(out Attribute bonus) && bonus != null
                        && caster is IGameplayAbilitySystem gas && gas.TryGetAttributeValue(bonus, out var value))
                        count += Mathf.Max(0, Mathf.FloorToInt(value.CurrentValue));

                    string pattern = ability.TryGetLocalData(SurvivorsTags.Pattern, out var named) && named.TryGet(out string kind) ? kind : "Targets";
                    float spread = Number(ability, SurvivorsTags.Spread, data, 40f);
                    float interval = Number(ability, SurvivorsTags.Interval, data, 0f);

                    var from = SurvivorsTeams.PlaceOf(caster as ITarget);
                    var targets = new List<ITarget>(ForgeResolveOrder.TargetsOf(data) ?? Empty);
                    targets.RemoveAll(target => !SurvivorsTeams.Stands(target) || ReferenceEquals(target, caster));
                    var aim = AimOf(data, from, targets);

                    for (int i = 0; i < count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        // Between shots its caster walks on, and a target may fall and be taken out
                        if (!SurvivorsTeams.Exists(caster)) return;
                        from = SurvivorsTeams.PlaceOf(caster as ITarget);
                        var target = targets.Count > 0 ? targets[i % targets.Count] : null;
                        if (!SurvivorsTeams.Stands(target)) target = null;
                        switch (pattern)
                        {
                            case "Fan":
                            {
                                float turn = count == 1 ? 0f : -spread / 2f + spread * i / (count - 1);
                                Spawn(shot, caster, data, from, Quaternion.Euler(0f, turn, 0f) * aim, null, 0f);
                                break;
                            }
                            case "Ring":
                                Spawn(shot, caster, data, from, Quaternion.Euler(0f, 360f * i / count, 0f) * aim, null, 0f);
                                break;
                            case "Spots":
                                if (target == null) continue;
                                Spawn(shot, caster, data, SurvivorsTeams.PlaceOf(target), aim, target, 0f);
                                break;
                            case "Orbit":
                                Spawn(shot, caster, data, from, aim, null, 360f * i / count);
                                break;
                            default:
                                var toward = target != null ? SurvivorsTeams.PlaceOf(target) - from : aim;
                                Spawn(shot, caster, data, from, toward, target, 0f);
                                break;
                        }
                        if (interval > 0f && i < count - 1) await Wait(interval, token);
                    }
                })
                .BuildSequence();

        /// <summary>
        /// Summon: raises the entity its ability's Local Data names under Summons, its Count of them by its level, in a ring
        /// around its caster, as the horde's own at the wave's level. A boss that broods or raises the dead casts it.
        /// </summary>
        [TaskSequenceMethod("Survivors: Summon")]
        public static TaskSequence Summon() =>
            TaskSequenceBuilder.Create("Summon")
                .WithAnnotation("Raises the entity its ability's Local Data names, so many by its level, around its caster")
                .Do(d =>
                {
                    if (d is not AbilityDataPacket data || data.EffectOrigin is not AbilitySpec spec || spec.Base == null) return;
                    var game = SurvivorsGame.Current;
                    if (!game || data.EffectOrigin.GetSource() is not ITarget caster) return;
                    var ability = spec.Base;
                    if (!ability.TryGetLocalData(SurvivorsTags.Summons, out var raised) || !raised.TryGet(out EntityIdentity identity)) return;
                    int count = ability.TryGetLocalData(SurvivorsTags.Count, out var counted) && counted.TryGet(out ScalableInt number)
                        ? Mathf.Max(1, number.Evaluate(NumberContext.Of(data))) : 1;
                    game.Horde.Summon(identity, count, SurvivorsTeams.PlaceOf(caster));
                })
                .BuildSequence();

        // A number the ability's Local Data holds under a key, read at the ability's level, or a default where it holds none
        private static float Number(Ability ability, Tag key, AbilityDataPacket data, float otherwise) =>
            ability.TryGetLocalData(key, out var held) && held.Value != null && held.Value.TryReadNumber(NumberContext.Of(data), out float number)
                ? number : otherwise;

        // The way a volley faces: its first target, else the point its activation holds, as the hero's facing, else ahead
        private static Vector3 AimOf(AbilityDataPacket data, Vector3 from, List<ITarget> targets)
        {
            var toward = targets.Count > 0 ? SurvivorsTeams.PlaceOf(targets[0]) - from
                : data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 point) ? point - from : Vector3.forward;
            toward.y = 0f;
            return toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
        }

        // One of the volley's processes: from the ability, at a place, aimed, with its target and its place on a circle,
        // and the magnitudes its caller set
        private static void Spawn(AbstractMonoProcess shot, ISource caster, AbilityDataPacket data, Vector3 at, Vector3 aim, ITarget target, float phase)
        {
            var packet = ProcessDataPacket.Default();
            packet.SetPrimary(Tags.DERIVATION, data.EffectOrigin);
            packet.SetPrimary(Tags.POSITION, at);
            aim.y = 0f;
            packet.SetPrimary(SurvivorsTags.Aim, aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward);
            packet.SetPrimary(SurvivorsTags.Phase, phase);
            if (target != null) packet.SetPrimary(Tags.TARGET_REAL, target);
            packet.TakeSetByCaller(data.SetByCallerMagnitudes);
            ProcessControl.Register(shot).HandledBy(caster).With(packet).In(ProcessWorld.Of(caster)).Run();
        }

        // A wait on the game's clock, a frame at a time
        private static async UniTask Wait(float seconds, CancellationToken token)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                token.ThrowIfCancellationRequested();
                waited += ForgeTime.DeltaTime;
                await ForgeTick.NextFrame(token);
            }
        }
    }
}
