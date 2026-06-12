using System;

namespace FarEmerald.PlayForge
{
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
