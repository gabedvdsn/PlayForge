using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What works out a number for a carrier, an effect's spec or another: <c>Initialize</c> as the carrier is made,
    /// which can capture what the number needs, then <c>Evaluate</c> each time it's read. Every <c>AbstractScaler</c>
    /// is one, and a carrier keeps what a scaler captures under the scaler. <c>Generate</c> makes a <c>CustomScaler</c>
    /// from two delegates, or, given none, one that reads how far up its range the origin's level is, from 0 to 1.
    /// </summary>
    public interface IScaler
    {
        public void Initialize(IAttributeImpactCarrier deriv);
        public float Evaluate(IAttributeImpactCarrier deriv);

        public static CustomScaler Generate()
        {
            return new CustomScaler(InitFunc, EvalFunc);

            void InitFunc(IAttributeImpactCarrier spec)
            {
            }

            float EvalFunc(IAttributeImpactCarrier spec) => spec.GetOrigin().GetLevel().Ratio;
        }

        public static CustomScaler Generate(Action<IAttributeImpactCarrier> initAction, Func<IAttributeImpactCarrier, float> evalFunc)
        {
            return new CustomScaler(initAction, evalFunc);
        }
        
    }

    /// <summary>
    /// A scaler made of two delegates, one run as a carrier initializes it and one that works out its value, as
    /// <c>IScaler.Generate</c> makes. It isn't an <c>AbstractScaler</c>, so a number field can't hold it.
    /// </summary>
    public class CustomScaler : IScaler
    {
        private Action<IAttributeImpactCarrier> InitializationAction;
        private Func<IAttributeImpactCarrier, float> EvaluationFunc;

        public CustomScaler(Action<IAttributeImpactCarrier> initializationAction, Func<IAttributeImpactCarrier, float> evaluationFunc)
        {
            InitializationAction = initializationAction;
            EvaluationFunc = evaluationFunc;
        }

        public void Initialize(IAttributeImpactCarrier deriv)
        {
            InitializationAction(deriv);
        }
        public float Evaluate(IAttributeImpactCarrier deriv)
        {
            return EvaluationFunc(deriv);
        }
    }
}
