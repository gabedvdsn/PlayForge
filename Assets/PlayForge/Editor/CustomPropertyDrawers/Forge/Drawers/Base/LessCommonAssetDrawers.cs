namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A Value Pipeline field as a reference to an asset: New beside it while it's empty, which makes one where The Forge
    /// makes assets and uses it, and Open once it names one (FN-11). It was Unity's bare object field.
    /// </summary>
    [UnityEditor.CustomPropertyDrawer(typeof(ValuePipeline))]
    public class ValuePipelineDrawer : AbstractForgeObjectDrawer<ValuePipeline>
    {
    }

    /// <summary>A Crowd Control Rules field as a reference to an asset, with New and Open beside it (FN-11).</summary>
    [UnityEditor.CustomPropertyDrawer(typeof(CrowdControlRules))]
    public class CrowdControlRulesDrawer : AbstractForgeObjectDrawer<CrowdControlRules>
    {
    }

    /// <summary>An Effect Modifier field as a reference to an asset, with New and Open beside it (FN-11).</summary>
    [UnityEditor.CustomPropertyDrawer(typeof(EffectModifier))]
    public class EffectModifierDrawer : AbstractForgeObjectDrawer<EffectModifier>
    {
    }
}
