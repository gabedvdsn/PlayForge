using System;

namespace FarEmerald.PlayForge
{
    public interface IScaler
    {
        public void Initialize(IAttributeImpactDerivation deriv);
        public float Evaluate(IAttributeImpactDerivation deriv);

        public static CustomScaler Generate()
        {
            return new CustomScaler(InitFunc, EvalFunc);

            void InitFunc(IAttributeImpactDerivation spec)
            {
            }

            float EvalFunc(IAttributeImpactDerivation spec) => spec.GetEffectDerivation().GetLevel().Ratio;
        }

        public static CustomScaler Generate(Action<IAttributeImpactDerivation> initAction, Func<IAttributeImpactDerivation, float> evalFunc)
        {
            return new CustomScaler(initAction, evalFunc);
        }
        
    }

    public class CustomScaler : IScaler
    {
        private Action<IAttributeImpactDerivation> InitializationAction;
        private Func<IAttributeImpactDerivation, float> EvaluationFunc;

        public CustomScaler(Action<IAttributeImpactDerivation> initializationAction, Func<IAttributeImpactDerivation, float> evaluationFunc)
        {
            InitializationAction = initializationAction;
            EvaluationFunc = evaluationFunc;
        }

        public void Initialize(IAttributeImpactDerivation deriv)
        {
            InitializationAction(deriv);
        }
        public float Evaluate(IAttributeImpactDerivation deriv)
        {
            return EvaluationFunc(deriv);
        }
    }
}
