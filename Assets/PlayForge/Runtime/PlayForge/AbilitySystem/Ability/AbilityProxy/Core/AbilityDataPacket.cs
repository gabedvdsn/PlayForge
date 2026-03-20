using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilityDataPacket : ProcessDataPacket
    {
        public readonly IEffectOrigin Spec;

        private AbilityDataPacket(IEffectOrigin spec)
        {
            Spec = spec;
            Handler = spec.GetOwner();
            
            AddPayload(
                Tags.SOURCE,
                spec.GetOwner()
            );
        }
        
        #region Readable Definition
        
        public string GetName()
        {
            return $"AbilityDataPacket.{Spec.GetReadableDefinition().GetName()}";
        }
        public string GetDescription()
        {
            return $"Active data packet for an active ability runtime: {Spec.GetReadableDefinition().GetName()}: {Spec.GetReadableDefinition().GetDescription()}";
        }
        public Texture2D GetPrimaryIcon()
        {
            return Spec.GetReadableDefinition().GetPrimaryIcon();
        }
        
        #endregion

        public static AbilityDataPacket GenerateRoot()
        {
            return new AbilityDataPacket(IEffectOrigin.GenerateSourceDerivation(GameRoot.Instance));
        }

        public static AbilityDataPacket GenerateFrom(IEffectOrigin spec, bool useImplicitTargeting)
        {
            AbilityDataPacket data = new AbilityDataPacket(spec);
            
            if (useImplicitTargeting)
            {
                data.AddPayload(Tags.TARGET_REAL, spec.GetOwner());
            }
            
            return data;
        }
        
        #region Common

        public bool TryGetTarget(EDataTarget policy, out ITarget target) => TryGet(Tags.TARGET_REAL, policy, out target);
        public bool TryGetFirstTarget(out ITarget target) => TryGetFirst(Tags.TARGET_REAL, out target);

        #endregion
    }
}
