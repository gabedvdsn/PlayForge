using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

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

    /// <summary>
    /// A field's own fields, drawn where it's held, as a component's own: a field of its whole drew a foldout inside the
    /// component (FN-14, as FN-17's Death).
    /// </summary>
    public class InlineFieldsDrawer : PropertyDrawer
    {
        // The name of the element that holds them
        protected virtual string HolderName => "Fields";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = HolderName };
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (!child.NextVisible(true)) return root;
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                root.Add(new PropertyField(child.Copy()) { name = child.name });
            } while (child.NextVisible(false));
            return root;
        }
    }

    /// <summary>An aura's fields, drawn where it's held, as an Aura component's own (FN-14).</summary>
    [CustomPropertyDrawer(typeof(AuraEntry))]
    public class AuraEntryDrawer : InlineFieldsDrawer
    {
        protected override string HolderName => "AuraFields";
    }

    /// <summary>An effect's area's fields, drawn where it's held, as an Area component's own (TW-10, FN-14).</summary>
    [CustomPropertyDrawer(typeof(EffectArea))]
    public class EffectAreaDrawer : InlineFieldsDrawer
    {
        protected override string HolderName => "AreaFields";
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
