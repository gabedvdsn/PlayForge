namespace FarEmerald.PlayForge
{
    public struct ImpactData
    {
        private ImpactData(ITarget target, Attribute attribute, SourcedModifiedAttributeValue sourcedModifier, AttributeValue realImpact)
        {
            Target = target;
            Attribute = attribute;
            SourcedModifier = sourcedModifier;
            RealImpact = realImpact;
        }

        public ITarget Target;
        public Attribute Attribute;
        public SourcedModifiedAttributeValue SourcedModifier;
        public AttributeValue RealImpact;

        public static ImpactData Generate(ITarget target, Attribute attribute, SourcedModifiedAttributeValue sourcedModifier, AttributeValue realImpact)
        {
            return new ImpactData(target, attribute, sourcedModifier, realImpact);
        }

        public override string ToString()
        {
            return $"{SourcedModifier.Derivation.GetSource()} -> {Target} => {Attribute} ({RealImpact})";
        }
    }
}
