using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Plays its cues' bursts at a stage's exact moment, as a sword's arc or a spell's release: on its caster, on its
    /// activation's target, or at its activation's point. A cue there hears the activation's data, and a magnitude
    /// read as it plays, for a cue to scale with. An ability's lifelong moments play through its Cues component.
    /// </summary>
    [Serializable]
    [ForgeMenu("Cues", Name = "Play Cue", Description = "Plays its cues at this moment of the stage: on the caster, the target or the point.")]
    public class PlayCueTask : AbstractAbilityTask
    {
        [Tooltip("What it plays, in order")]
        [SerializeReference]
        public List<AbstractCueHandler> Cues = new();

        [Tooltip("Where it plays: on its caster, on its activation's target, or at its activation's point")]
        public EAreaCentre At = EAreaCentre.Caster;

        [Tooltip("A number it hands its cues, read as it plays at its ability's level, as a hit's damage for a cue to scale with")]
        public ScalableFloat Magnitude = 0f;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (Cues == null || Cues.Count == 0) return UniTask.CompletedTask;
            var caster = data.EffectOrigin?.GetSource();
            var on = caster as IGameplayAbilitySystem;
            Vector3? point = null;
            switch (At)
            {
                // On the target where it's an entity; else at its place, on the caster
                case EAreaCentre.Target:
                    if (data.TryGetFirstTarget(out var target) && target is IGameplayAbilitySystem entity) on = entity;
                    else if (data.GetTargetingPacket(Tags.TARGET, out var packet, autoInterrupt: false) && packet != null) point = packet.position;
                    break;
                case EAreaCentre.Point:
                    if (data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 at)) point = at;
                    break;
            }
            float magnitude = Magnitude != null ? Read(Magnitude, data) : 0f;
            ForgeCues.Burst(Cues, new CueContext(on, ECueMoment.Stage, source: caster, origin: data.EffectOrigin, data: data,
                magnitude: magnitude, point: point, key: data), CueSource.Of((data.EffectOrigin as AbilitySpec)?.Base));
            return UniTask.CompletedTask;
        }
    }
}
