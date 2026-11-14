using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A component: its kind, always of some kind, then its fields (FN-14). A kind that allows one is offered while the
    /// list it's in holds none of it, the element that holds it still naming it.
    /// </summary>
    public abstract class AbstractComponentDrawer<T> : AbstractWorkerDrawer<T> where T : AbstractForgeComponent
    {
        // A kind reads by its name without its family's: an Aura Component as "Aura"
        protected override string[] TypeNameSuffixes => new[] { "EffectComponent", "AbilityComponent", "EntityComponent", "ItemComponent", "Component" };

        protected override bool Offers(SerializedProperty prop, Type type)
        {
            if (AllowsSeveral(type) || prop == null) return true;
            var list = ListOf(prop);
            if (list == null || !list.isArray) return true;
            for (int i = 0; i < list.arraySize; i++)
            {
                var other = list.GetArrayElementAtIndex(i);
                if (other.propertyPath == prop.propertyPath) continue;
                if (other.propertyType == SerializedPropertyType.ManagedReference && other.managedReferenceValue?.GetType() == type) return false;
            }
            return true;
        }

        // Whether a kind may be held more than once, as an object of it says
        internal static bool AllowsSeveral(Type type)
        {
            try
            {
                return (Activator.CreateInstance(type) as AbstractForgeComponent)?.AllowsSeveral ?? true;
            }
            catch (Exception)
            {
                return true;
            }
        }

        // The list an element is in, or null for a field of its own
        private static SerializedProperty ListOf(SerializedProperty element)
        {
            var path = element.propertyPath;
            int at = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            return at < 0 ? null : element.serializedObject.FindProperty(path.Substring(0, at));
        }
    }

    [CustomPropertyDrawer(typeof(AbstractEffectComponent), true)]
    public class EffectComponentDrawer : AbstractComponentDrawer<AbstractEffectComponent>
    {
    }

    [CustomPropertyDrawer(typeof(AbstractAbilityComponent), true)]
    public class AbilityComponentDrawer : AbstractComponentDrawer<AbstractAbilityComponent>
    {
    }

    [CustomPropertyDrawer(typeof(AbstractEntityComponent), true)]
    public class EntityComponentDrawer : AbstractComponentDrawer<AbstractEntityComponent>
    {
    }

    [CustomPropertyDrawer(typeof(AbstractItemComponent), true)]
    public class ItemComponentDrawer : AbstractComponentDrawer<AbstractItemComponent>
    {
    }
}
