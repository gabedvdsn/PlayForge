using System;

namespace FarEmerald.PlayForge
{
    public interface IMagnitudeModifier
    {
        public void Initialize(IAttributeImpactDerivation spec);
        public float Evaluate(IAttributeImpactDerivation spec);

        public static CustomMagnitudeModifier Generate()
        {
            return new CustomMagnitudeModifier(InitFunc, EvalFunc);

            void InitFunc(IAttributeImpactDerivation spec)
            {
            }

            float EvalFunc(IAttributeImpactDerivation spec) => spec.GetEffectDerivation().GetRelativeLevel();
        }

        public static CustomMagnitudeModifier Generate(Action<IAttributeImpactDerivation> initAction, Func<IAttributeImpactDerivation, float> evalFunc)
        {
            return new CustomMagnitudeModifier(initAction, evalFunc);
        }
        
    }

    public class CustomMagnitudeModifier : IMagnitudeModifier
    {
        private Action<IAttributeImpactDerivation> InitializationAction;
        private Func<IAttributeImpactDerivation, float> EvaluationFunc;

        public CustomMagnitudeModifier(Action<IAttributeImpactDerivation> initializationAction, Func<IAttributeImpactDerivation, float> evaluationFunc)
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
