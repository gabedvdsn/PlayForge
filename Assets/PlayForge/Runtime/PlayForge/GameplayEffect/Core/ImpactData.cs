namespace FarEmerald.PlayForge
{
    public struct ImpactData
    {
        private ImpactData(ITarget target, IAttribute attribute, SourcedModifiedAttributeValue sourcedModifier, AttributeValue realImpact, AttributeValue oldValue)
        {
            Target = target;
            Attribute = attribute;
            SourcedModifier = sourcedModifier;
            RealImpact = realImpact;
            OldValue = oldValue;
            Ends = false;
            Steps = null;
        }

        public ITarget Target;
        public IAttribute Attribute;
        public SourcedModifiedAttributeValue SourcedModifier;
        public AttributeValue RealImpact;
        public AttributeValue OldValue;
        public AttributeValue LiveValue => OldValue + RealImpact;

        /// <summary>
        /// A lasting modifier coming off: a change, told as every change is, but no hit. Its effect's workers hear no
        /// impact, its dealer's impact workers don't run, and no Impact Taken or Dealt is sent (GAS-85, Decision 84).
        /// </summary>
        public bool Ends;

        // Its commit's steps, while the commit is in progress, where they're recorded: the combat log copies them as it
        // writes (LAB-13). Null otherwise
        internal ChangeStepRecorder Steps;

        public static ImpactData Generate(ITarget target, IAttribute attribute, SourcedModifiedAttributeValue sourcedModifier, AttributeValue realImpact, AttributeValue oldValue)
        {
            return new ImpactData(target, attribute, sourcedModifier, realImpact, oldValue);
        }
        
        public override string ToString()
        {
            return $"{SourcedModifier.Carrier.GetReadableDefinition()} -> {Target.GetName()} => {Attribute.GetName()} ({RealImpact})";
        }
    }
}
