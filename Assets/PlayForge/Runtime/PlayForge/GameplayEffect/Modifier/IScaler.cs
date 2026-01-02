using System;

namespace FarEmerald.PlayForge
{
    public interface IScaler
    {
        public void Initialize(IAttributeImpactDerivation spec);
        public float Evaluate(IAttributeImpactDerivation spec);

        public static CustomScaler Generate()
        {
            return new CustomScaler(InitFunc, EvalFunc);

            void InitFunc(IAttributeImpactDerivation spec)
            {
            }

            float EvalFunc(IAttributeImpactDerivation spec) => spec.GetEffectDerivation().GetRelativeLevel();
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

        public void Initialize(IAttributeImpactDerivation spec)
        {
            InitializationAction(spec);
        }
        public float Evaluate(IAttributeImpactDerivation spec)
        {
            return EvaluationFunc(spec);
        }
    }
}
