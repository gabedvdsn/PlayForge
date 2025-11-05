using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class AbstractGameplayMonoProcess : LazyMonoProcess
    {
        protected IEffectOrigin Origin;
        protected SystemComponentData Source;

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            
            if (!regData.TryGet(Tags.PAYLOAD_DERIVATION, EProxyDataValueTarget.Primary, out Origin))
            {
                Origin = GameRoot.Instance;
            }

            Source = Origin.GetOwner().AsData();
        }
    }
}
