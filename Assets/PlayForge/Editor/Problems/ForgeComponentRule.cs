using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>A rule about one kind of component, whose problems an asset's inspector shows under its Components list.</summary>
    public interface IForgeComponentRule
    {
    }

    /// <summary>
    /// A check of one kind of component, wherever an asset holds it: an ability's, an effect's, an item's or an entity's.
    /// The validator finds it as it finds every rule, and it hears each component of its kind the asset holds, with the
    /// component's place in its list, which a problem names as its property path; an ability's inspector says its
    /// problems under the Components list. Derive from it, with a public constructor that takes nothing, for a component
    /// of your own, as PlayForge's components' rules do.
    /// </summary>
    public abstract class ForgeComponentRule<TComponent> : ForgeValidationRule, IForgeComponentRule
        where TComponent : AbstractForgeComponent
    {
        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            var components = ComponentsOf(asset);
            if (components == null) return;
            for (int i = 0, nth = 0; i < components.Count; i++)
            {
                if (components[i] is not TComponent component) continue;
                nth++;
                Check(asset, component, nth, $"Components.Array.data[{i}]", problems);
            }
        }

        /// <summary>
        /// Adds the problems one of its components has: the <paramref name="nth"/> of its kind the asset holds, from 1, at
        /// <paramref name="path"/>, its place, as "Components.Array.data[1]". Nothing, here.
        /// </summary>
        protected virtual void Check(Object asset, TComponent component, int nth, string path, List<ForgeProblem> problems) { }

        // An asset's components, whichever of the four kinds of asset holds them
        private static IReadOnlyList<AbstractForgeComponent> ComponentsOf(Object asset) => asset switch
        {
            Ability ability => ability.Components,
            GameplayEffect effect => effect.Components,
            Item item => item.Components,
            EntityIdentity entity => entity.Components,
            _ => null,
        };
    }
}
