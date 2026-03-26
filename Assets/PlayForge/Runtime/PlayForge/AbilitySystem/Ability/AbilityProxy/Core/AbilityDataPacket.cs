using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilityDataPacket : ProcessDataPacket
    {
        public readonly IEffectOrigin Spec;
        public readonly AbilitySystemComponent.AbilityActivationRequest Request;

        private AbilityDataPacket(IEffectOrigin spec, AbilitySystemComponent.AbilityActivationRequest request)
        {
            Spec = spec;
            Request = request;
            Handler = spec.GetOwner();
            
            AddPayload(
                Tags.SOURCE,
                spec.GetOwner()
            );
        }
        
        #region Readable Definition
        
        public override string GetName()
        {
            return $"AbilityDataPacket.{Spec.GetReadableDefinition().GetName()}";
        }
        public override string GetDescription()
        {
            return $"Active data packet for an active ability runtime: {Spec.GetReadableDefinition().GetName()}: {Spec.GetReadableDefinition().GetDescription()}";
        }
        public override Texture2D GetPrimaryIcon()
        {
            return Spec.GetReadableDefinition().GetPrimaryIcon();
        }
        
        #endregion

        public static AbilityDataPacket GenerateFrom(IEffectOrigin spec, AbilitySystemComponent.AbilityActivationRequest req, bool useImplicitTargeting)
        {
            AbilityDataPacket data = new AbilityDataPacket(spec, req);
            
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
