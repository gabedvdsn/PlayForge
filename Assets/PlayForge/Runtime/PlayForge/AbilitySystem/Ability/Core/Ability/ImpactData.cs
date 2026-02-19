namespace FarEmerald.PlayForge
{
    public struct ImpactData
    {
        private ImpactData(ITarget target, Attribute attribute, SourcedModifiedAttributeValue sourcedModifier, AttributeValue realImpact, AttributeValue oldValue)
        {
            Target = target;
            Attribute = attribute;
            SourcedModifier = sourcedModifier;
            RealImpact = realImpact;
            OldValue = oldValue;
        }

        public ITarget Target;
        public Attribute Attribute;
        public SourcedModifiedAttributeValue SourcedModifier;
        public AttributeValue RealImpact;
        public AttributeValue OldValue;
        public AttributeValue LiveValue => OldValue + RealImpact;

        public static ImpactData Generate(ITarget target, Attribute attribute, SourcedModifiedAttributeValue sourcedModifier, AttributeValue realImpact, AttributeValue oldValue)
        {
            return new ImpactData(target, attribute, sourcedModifier, realImpact, oldValue);
        }
        
        public override string ToString()
        {
            return $"{SourcedModifier.Derivation.GetCacheKey()} -> {Target} => {Attribute} ({RealImpact})";
        }
    }
}
