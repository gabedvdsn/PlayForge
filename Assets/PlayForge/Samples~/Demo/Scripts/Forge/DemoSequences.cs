using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The demo's task sequences: what an ability does that its stages' tasks alone don't say. An ability runs one with a
    /// Run Sequence task, which hands it the activation's data, so it reads what the ability's asset loaders loaded and
    /// what the ability's Local Data holds.
    /// </summary>
    public static class DemoSequences
    {
        /// <summary>The tag Renewal's asset loaders load its pulse under.</summary>
        [ForgeTagExposed(ForgeContext.Data)] public static readonly Tag RenewalPulse = Tag.Generate("Demo.Renewal.Pulse");
        /// <summary>The tag Renewal's asset loaders load its cleanse under.</summary>
        [ForgeTagExposed(ForgeContext.Data)] public static readonly Tag RenewalCleanse = Tag.Generate("Demo.Renewal.Cleanse");
        /// <summary>The Local Data key a volley's arrow prefab is under, on the ability.</summary>
        [ForgeTagExposed(ForgeContext.Data)] public static readonly Tag VolleyArrow = Tag.Generate("Demo.Volley.Arrow");
        /// <summary>The Local Data key a volley's radius is under, on the ability.</summary>
        [ForgeTagExposed(ForgeContext.Data)] public static readonly Tag VolleyRadius = Tag.Generate("Demo.Volley.Radius");
        /// <summary>The Local Data key the most arrows a volley looses is under, on the ability.</summary>
        [ForgeTagExposed(ForgeContext.Data)] public static readonly Tag VolleyArrows = Tag.Generate("Demo.Volley.Arrows");

        /// <summary>
        /// Renewal: the cleanse its loaders loaded, at once, then a pulse of healing each second, five times, to the
        /// Oracle. The pulse and the cleanse are effects the ability loads, so a designer changes them in the ability's
        /// asset, not here.
        /// </summary>
        [TaskSequenceMethod("Demo: Renewal")]
        public static TaskSequence Renewal() =>
            TaskSequenceBuilder.Create("Renewal")
                .WithAppliedEffects(RenewalCleanse, maxApplications: 1, "a cleanse, at once")
                .WithAppliedEffects(RenewalPulse, maxApplications: 5, "a pulse of healing each second")
                .WithAnnotation("Cleanses, then heals in five pulses, one a second")
                .Task(async (d, token) =>
                {
                    if (d is not AbilityDataPacket data || data.EffectOrigin?.GetSource() is not ITarget oracle) return;
                    data.TryApplyEffects(oracle, RenewalCleanse);
                    for (int pulse = 0; pulse < 5; pulse++)
                    {
                        if (oracle.IsDead) return;
                        data.TryApplyEffects(oracle, RenewalPulse);
                        await Wait(1f, token);
                    }
                })
                .BuildSequence();

        /// <summary>
        /// Volley: an arrow at each enemy around the point the ability targets, one after another, from the Ranger. The
        /// arrow, the radius and how many it looses at most are the ability's Local Data, so each Ranger's volley can
        /// differ.
        /// </summary>
        [TaskSequenceMethod("Demo: Volley")]
        public static TaskSequence Volley() =>
            TaskSequenceBuilder.Create("Volley")
                .WithAnnotation("Looses an arrow at each enemy around the point, one after another")
                .Task(async (d, token) =>
                {
                    if (d is not AbilityDataPacket data || data.EffectOrigin?.GetSource() is not { } ranger) return;
                    if (data.EffectOrigin is not AbilitySpec spec || spec.Base == null) return;
                    if (!data.GetTargetingPacket(Tags.TARGET, out var point, autoInterrupt: false) || point == null) return;
                    if (!spec.Base.TryGetLocalData(VolleyArrow, out var arrowData) || arrowData.monoBehaviourValue is not AbstractMonoProcess arrow) return;

                    float radius = spec.Base.TryGetLocalData(VolleyRadius, out var r) ? r.floatValue : 3f;
                    int most = spec.Base.TryGetLocalData(VolleyArrows, out var n) ? Mathf.Max(1, n.intValue) : 5;
                    int loosed = 0;
                    foreach (var target in DemoTeams.Around(point.position, radius, ~0))
                    {
                        if (loosed >= most || !DemoTeams.Reaches(ranger, target, EDemoAffects.Enemies)) continue;
                        Loose(arrow, ranger, target, data);
                        loosed++;
                        await Wait(0.12f, token);
                    }
                })
                .BuildSequence();

        // An arrow from the ranger at a target, as the ability's own: its origin the ability, its magnitudes the caller's
        private static void Loose(AbstractMonoProcess arrow, ISource ranger, ITarget target, AbilityDataPacket data)
        {
            var shot = ProcessDataPacket.Default();
            shot.SetPrimary(Tags.TARGET_REAL, target);
            shot.SetPrimary(Tags.DERIVATION, data.EffectOrigin);
            shot.SetPrimary(Tags.POSITION, ranger.GetTargetingPacket().position + Vector3.up);
            shot.TakeSetByCaller(data.SetByCallerMagnitudes);
            ProcessControl.Register(arrow).HandledBy(ranger).With(shot).In(ProcessWorld.Of(ranger)).Run();
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
