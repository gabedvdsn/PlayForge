namespace FarEmerald.PlayForge
{
    public static class EffectContainerFactory
    {
        public static IndependentDurationsContainer NewIndependentContainer(GameplayEffectSpec spec, bool ongoing)
        {
            return IndependentDurationsContainer.Generate(spec, ongoing);
        }
    }
}
