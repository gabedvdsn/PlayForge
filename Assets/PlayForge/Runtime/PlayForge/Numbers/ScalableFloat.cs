using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An authored gameplay number (FEAT-29). It is a plain number by default, and it can also be one of the kinds the
    /// inspector's kind menu offers: a value by level, a multiple of an attribute, a value its caller sets, any scaler, or a
    /// kind a project writes (<see cref="NumberKind"/>). It is the magnitude field it grew from, under a new name, with the
    /// same saved fields: its number (<see cref="Magnitude"/>), its scaler, and how the two combine
    /// (<see cref="RealMagnitude"/>). So every magnitude saved before it loads as it was.
    /// <para>
    /// A float converts to one, so <c>public ScalableFloat Range = 20f;</c> and <c>effect.Range = 5f;</c> compile. There is
    /// no conversion back, as it would drop the kind: a read is <see cref="Evaluate(in NumberContext)"/>, once, at the
    /// site's natural moment, as an effect as it's applied or a task as it starts.
    /// </para>
    /// </summary>
    [Serializable]
    public class ScalableFloat
    {
        /// <summary>The number: the whole value of a constant, and what a scaler combines with otherwise.</summary>
        public float Magnitude;

        /// <summary>How the number combines with the scaler: the scaler, either plus or times the number, or the number alone.</summary>
        public EMagnitudeOperation RealMagnitude = EMagnitudeOperation.AddScaler;

        /// <summary>The kind, when the number isn't a constant: any scaler, or a <see cref="NumberKind"/>.</summary>
        [SerializeReference]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))]
        [DeriveScalerName]
        public AbstractScaler Scaler;

        public ScalableFloat() { }

        /// <summary>A constant: the number alone.</summary>
        public ScalableFloat(float value)
        {
            Magnitude = value;
            RealMagnitude = EMagnitudeOperation.UseMagnitude;
        }

        /// <summary>A number of a kind: the kind's value, combined with the number as <paramref name="combine"/> says.</summary>
        public ScalableFloat(AbstractScaler kind, float number = 1f, EMagnitudeOperation combine = EMagnitudeOperation.MultiplyWithScaler)
        {
            Scaler = kind;
            Magnitude = number;
            RealMagnitude = kind == null ? EMagnitudeOperation.UseMagnitude : combine;
        }

        /// <summary>A constant, so a float assigns to a number field: <c>Range = 20f</c>.</summary>
        public static implicit operator ScalableFloat(float value) => new(value);

        /// <summary>
        /// A number worked out by a function, for an asset built in code, as the Battle sample's are. It isn't saved, as a
        /// function can't be: saved and loaded, it reads its number, 0.
        /// </summary>
        public static ScalableFloat From(Func<NumberContext, float> function) =>
            new(new FunctionNumberKind(function), 1f, EMagnitudeOperation.UseScaler) { Magnitude = 0f };

        /// <summary>
        /// Whether the number reads the same wherever it's read: it has no scaler, or it reads its number alone. A
        /// constant costs a read of <see cref="Constant"/> and nothing more.
        /// </summary>
        public bool IsConstant => Scaler == null || RealMagnitude == EMagnitudeOperation.UseMagnitude;

        /// <summary>
        /// What a constant reads: its number, or, through a Combine that reads a scaler it hasn't got, 0. A number of
        /// another kind reads its number here, the value it falls back to where its kind can't be worked out. For editor
        /// previews and setup code.
        /// </summary>
        public float Constant => RealMagnitude switch
        {
            EMagnitudeOperation.UseMagnitude => Magnitude,
            _ when Scaler != null => Magnitude,
            _ => ForgeHelper.MagnitudeAndScalerOperation(Magnitude, 0f, RealMagnitude),
        };

        /// <summary>
        /// Reads the number where it is: a constant at once, another kind through the context, its scaler evaluating
        /// against the context's carrier or a reusable one the context fills, so a read allocates nothing. A kind of a
        /// project's own that can't be worked out here, as one that reads the target where there's none, reads the number
        /// and logs once.
        /// </summary>
        public float Evaluate(in NumberContext context)
        {
            if (IsConstant) return Constant;
            if (!NumberContext.CanRead(Scaler, in context)) return Magnitude;
            return ForgeHelper.MagnitudeAndScalerOperation(Magnitude, NumberContext.ReadScaler(Scaler, in context), RealMagnitude);
        }

        /// <summary>Reads the number against an impact carrier, as an effect's own numbers are read.</summary>
        public float Evaluate(IAttributeImpactCarrier spec)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(spec) ?? 0f, RealMagnitude
            );
        }

        /// <summary>
        /// Container-aware evaluation — scalers that read container state (stack count)
        /// see the live container instead of just the spec. Scalers that don't care fall
        /// back to spec-based evaluation via <see cref="AbstractScaler.Evaluate(AbstractStackingEffectContainer)"/>.
        /// </summary>
        public float Evaluate(AbstractStackingEffectContainer container)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(container) ?? 0f, RealMagnitude
            );
        }

        public override string ToString() => NumberText.Of(this);
    }

    /// <summary>
    /// A whole gameplay number (FEAT-29): <see cref="ScalableFloat"/> for counts, as an effect's ticks or a stack's size. It
    /// takes every kind a <see cref="ScalableFloat"/> does, and rounds what it reads as <see cref="Rounding"/> says, so a
    /// kind is written once. It is the whole-number magnitude field it grew from, with the same saved fields.
    /// </summary>
    [Serializable]
    public class ScalableInt
    {
        /// <summary>The number: the whole value of a constant, and what a scaler combines with otherwise.</summary>
        [Min(0)] public int Magnitude;

        /// <summary>How the number combines with the scaler: the scaler, either plus or times the number, or the number alone.</summary>
        public EMagnitudeOperation RealMagnitude = EMagnitudeOperation.AddScaler;

        /// <summary>The kind, when the number isn't a constant: any scaler, or a <see cref="NumberKind"/>.</summary>
        [SerializeReference]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))]
        [DeriveScalerName]
        public AbstractScaler Scaler;

        /// <summary>How a value read through a scaler becomes a whole number.</summary>
        public ERoundingOperation Rounding;

        public ScalableInt() { }

        /// <summary>A constant: the number alone.</summary>
        public ScalableInt(int value)
        {
            Magnitude = value;
            RealMagnitude = EMagnitudeOperation.UseMagnitude;
        }

        /// <summary>A number of a kind: the kind's value, combined with the number, then rounded.</summary>
        public ScalableInt(AbstractScaler kind, int number = 1, EMagnitudeOperation combine = EMagnitudeOperation.MultiplyWithScaler,
            ERoundingOperation rounding = ERoundingOperation.Floor)
        {
            Scaler = kind;
            Magnitude = number;
            RealMagnitude = kind == null ? EMagnitudeOperation.UseMagnitude : combine;
            Rounding = rounding;
        }

        /// <summary>A constant, so an int assigns to a whole-number field: <c>Ticks = 3</c>.</summary>
        public static implicit operator ScalableInt(int value) => new(value);

        /// <summary>Whether the number reads the same wherever it's read: it has no scaler, or it reads its number alone.</summary>
        public bool IsConstant => Scaler == null || RealMagnitude == EMagnitudeOperation.UseMagnitude;

        /// <summary>
        /// What a constant reads: its number, or, through a Combine that reads a scaler it hasn't got, 0. A number of
        /// another kind reads its number here, the value it falls back to where its kind can't be worked out.
        /// </summary>
        public int Constant => RealMagnitude switch
        {
            EMagnitudeOperation.UseMagnitude => Magnitude,
            _ when Scaler != null => Magnitude,
            _ => ForgeHelper.MagnitudeAndScalerOperation(Magnitude, 0f, RealMagnitude, Rounding),
        };

        /// <summary>Reads the number where it is, as <see cref="ScalableFloat.Evaluate(in NumberContext)"/> does, rounded.</summary>
        public int Evaluate(in NumberContext context)
        {
            if (IsConstant) return Constant;
            if (!NumberContext.CanRead(Scaler, in context)) return Magnitude;
            return ForgeHelper.MagnitudeAndScalerOperation(Magnitude, NumberContext.ReadScaler(Scaler, in context), RealMagnitude, Rounding);
        }

        /// <summary>Reads the number against an impact carrier, as an effect's own numbers are read.</summary>
        public int Evaluate(IAttributeImpactCarrier spec)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(spec) ?? 0f, RealMagnitude, Rounding
            );
        }

        public override string ToString() => NumberText.Of(this);
    }
}
