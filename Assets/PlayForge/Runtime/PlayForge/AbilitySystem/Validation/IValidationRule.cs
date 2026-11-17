using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractAttributeValidationRule : IAbilityValidationRule
    {
        public Attribute Attribute;

        public abstract bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);
        public abstract string GetName();
    }

    /// <summary>
    /// A rule an ability's activation keeps, for its source or its target. It took the generic IValidationRule, which
    /// nothing else took; it declares its own Validate.
    /// <para>
    /// A rule is a question, asked as often as the activation needs it answered: as it's requested, each frame a
    /// buffered request waits, and again once targeting has found its target, as the caster may have changed meanwhile.
    /// So a rule changes nothing and draws nothing: one that rolled a chance rolled it each time it was asked.
    /// </para>
    /// <para>
    /// Validate checks the entity <c>getSource</c> hands it: the caster among an ability's Caster Checks, the target among
    /// its Target Checks. Refused, it says why in <c>error</c>, as "Hale's Health, 80, isn't &lt; 50". A kind says which
    /// of the two it belongs among with <see cref="ValidationSideAttribute"/>.
    /// </para>
    /// </summary>
    public interface IAbilityValidationRule
    {
        bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);

        public string GetName();

        /// <summary>
        /// The kind of refusal it's told as where it refuses among the Caster Checks: Rule, as here, which its name names;
        /// Cooldown, Cost and Is Alive say Cooldown, Cost and Dead, which name none. Its error is the refusal's words
        /// whatever its kind; among the Target Checks, every refusal is the target's.
        /// </summary>
        public EActivationRefusal RefusedAs => EActivationRefusal.Rule;
    }

    /// <summary>Which of an ability's checks a rule belongs among: its Caster Checks, its Target Checks, or either.</summary>
    public enum EValidationSide
    {
        /// <summary>Either: it checks whichever entity its list hands it, the caster or the target.</summary>
        Either,

        /// <summary>The Caster Checks alone, as a cost or a cooldown, which ask of the ability as its caster holds it.</summary>
        Caster,

        /// <summary>The Target Checks alone, as a line of sight, which ask of a target against its caster.</summary>
        Target
    }

    /// <summary>
    /// Says which of an ability's checks a rule kind belongs among, and its picker offers it there alone. A kind that says
    /// none belongs among either.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class ValidationSideAttribute : System.Attribute
    {
        /// <summary>Which of an ability's checks the kind belongs among.</summary>
        public EValidationSide Side { get; }

        public ValidationSideAttribute(EValidationSide side) => Side = side;

        /// <summary>Which of an ability's checks a rule kind belongs among: the side its attribute says, or either.</summary>
        public static EValidationSide Of(Type type) =>
            type != null && GetCustomAttribute(type, typeof(ValidationSideAttribute), true) is ValidationSideAttribute said
                ? said.Side
                : EValidationSide.Either;
    }

    /// <summary>A check an ability's component stands behind, whatever the component's kind.</summary>
    public interface IComponentValidationRule
    {
        /// <summary>The component it stands for, or null for one held from before it was a component's.</summary>
        AbstractAbilityComponent Component { get; }

        /// <summary>What it checks, in one line with its component's numbers; null to say its name alone.</summary>
        string Describe();
    }

    /// <summary>
    /// A check an ability's component stands behind, as Check Range is the Cast Range component's: shown among the ability's
    /// checks while the ability holds the component, and tied to it. The component enforces what it says, so the rule
    /// passes; no list holds it and the picker doesn't offer it, as it comes with its component and goes with it. A
    /// component of your own brings one with its <see cref="AbstractAbilityComponent.TargetCheck"/> or
    /// <see cref="AbstractAbilityComponent.CasterCheck"/>.
    /// </summary>
    public abstract class AbstractComponentValidationRule<TComponent> : IAbilityValidationRule, IComponentValidationRule
        where TComponent : AbstractAbilityComponent
    {
        protected AbstractComponentValidationRule(TComponent component) => Component = component;

        /// <summary>The component it stands for.</summary>
        public TComponent Component { get; }

        AbstractAbilityComponent IComponentValidationRule.Component => Component;

        /// <summary>Passes: its component enforces what it says.</summary>
        public virtual bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = null;
            return true;
        }

        /// <summary>Its name, as the checks show it: its [ForgeMenu] name, or its class's.</summary>
        public virtual string GetName() => ForgeMenuAttribute.NameOf(GetType()) ?? GetType().Name;

        /// <summary>What it checks, in one line with its component's numbers; null to say its name alone.</summary>
        public virtual string Describe() => null;
    }
}
