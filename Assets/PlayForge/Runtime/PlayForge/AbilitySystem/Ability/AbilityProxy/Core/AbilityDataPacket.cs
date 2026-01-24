using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilityDataPacket : ProcessDataPacket
    {
        public IEffectOrigin Spec;

        private AbilityDataPacket(IEffectOrigin spec)
        {
            Spec = spec;
            Handler = spec.GetOwner();
            
            AddPayload(
                Tags.SOURCE,
                spec.GetOwner()
            );
        }

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

        public bool TryGetTarget(EProxyDataValueTarget policy, out ITarget target) => TryGet(Tags.TARGET_REAL, policy, out target);
        public bool TryGetFirstTarget(out ITarget target) => TryGetFirst(Tags.TARGET_REAL, out target);

        #endregion
    }
}
