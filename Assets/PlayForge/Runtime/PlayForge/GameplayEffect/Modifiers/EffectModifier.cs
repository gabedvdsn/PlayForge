using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A modifier shared between assets (TW-7, Decision 47): a rule that an item's, an ability's, an effect's or an
    /// entity's modifier entry names in place of its own, as a Value Pipeline is shared. Its class has a file of its own
    /// name, which an asset needs to find its script: in EffectModifiers.cs, an Effect Modifier saved as an asset came
    /// back with no script, and a search by its type found none (FN-12).
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Effect Modifier", fileName = "Effect Modifier", order = 43)]
    public class EffectModifier : ScriptableObject
    {
        [Tooltip("What it's for, as The Forge shows it")]
        [TextArea] public string Description;

        public EffectModifierRule Rule = new();
    }
}
