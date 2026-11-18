namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A change as it landed on an attribute: the <c>Target</c> it landed on, the <c>Attribute</c>, the
    /// <c>SourcedModifier</c> that made it, whose carrier names its source and its effect, the <c>RealImpact</c>, what
    /// the attribute's Current and Base changed by after workers, bounds and rounding, and the <c>OldValue</c> before
    /// it. Attribute callbacks, impact workers, an effect's Impact hook and the frame summary are each handed one;
    /// <c>Ends</c> marks a lasting modifier coming off, a change but no hit.
    /// </summary>
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
        /// impact, its dealer's impact workers don't run, and no Impact Taken or Dealt is sent.
        /// </summary>
        public bool Ends;

        // Its commit's steps, while the commit is in progress, where they're recorded: the combat log copies them as it
        // writes. Null otherwise
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
