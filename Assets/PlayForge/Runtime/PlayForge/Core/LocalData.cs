using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One entry of an asset's or an entity's Local Data: a value under a key. Its value is a class, a kind of its own: a
    /// number that can scale, a whole number, a string, a bool, a tag, an attribute, an effect, an ability, an entity, an
    /// object, a MonoBehaviour or a ScriptableObject; or a kind of your own, as a point or a colour, a [Serializable] class
    /// made from <see cref="LocalDataValue{T}"/> or <see cref="LocalDataValue"/>, which is offered beside them. Each entry
    /// saves its own value alone.
    /// </summary>
    [Serializable]
    public class DataWrapper
    {
        [ForgeTagContext(ForgeContext.LocalDataKey)]
        public Tag Key;

        [Tooltip("Its value: a number, a string, a tag, an asset, or a kind of your own")]
        [SerializeReference] public LocalDataValue Value;

        public DataWrapper() { }

        /// <summary>An entry of a value under a key.</summary>
        public DataWrapper(Tag key, LocalDataValue value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Whether its value holds a <typeparamref name="T"/>, and that value as it's held: a number's
        /// <see cref="ScalableFloat"/> or <see cref="ScalableInt"/> unread, a string, an asset. An empty reference holds
        /// none.
        /// </summary>
        public bool TryGet<T>(out T value)
        {
            if (Value?.Held is T held && !(held is Object reference && reference == null))
            {
                value = held;
                return true;
            }
            value = default;
            return false;
        }

        public override string ToString() => $"{Key}: {Value?.ToString() ?? "none"}";
    }

    /// <summary>
    /// A Local Data entry's value. A kind holds its value as <see cref="Held"/> gives it, gives it as a packet takes it
    /// with <see cref="Read"/>, a number read where it's used, and says whether it's a number with
    /// <see cref="TryReadNumber"/>, as a Local Data number and a tag rule read one. Most kinds hold one value, a
    /// <see cref="LocalDataValue{T}"/>'s.
    /// </summary>
    [Serializable]
    public abstract class LocalDataValue
    {
        /// <summary>The value as it's held: a number unread. Null here.</summary>
        public virtual object Held => null;

        /// <summary>The value as a packet takes it: a number read in <paramref name="context"/>. As it's held here.</summary>
        public virtual object Read(in NumberContext context) => Held;

        /// <summary>Whether it's a number, and the number, read in <paramref name="context"/>. Not here.</summary>
        public virtual bool TryReadNumber(in NumberContext context, out float number)
        {
            number = 0f;
            return false;
        }

        public override string ToString() => Held switch
        {
            null => "none",
            Object reference => reference ? reference.name : "none",
            var held => held.ToString(),
        };
    }

    /// <summary>A Local Data value that holds one <typeparamref name="T"/>, its <see cref="Value"/>, as most kinds do.</summary>
    [Serializable]
    public abstract class LocalDataValue<T> : LocalDataValue
    {
        [Tooltip("Its value")]
        public T Value;

        public override object Held => Value;
    }

    /// <summary>A number that can scale, read where it's used: at the level and with the caster it's read for.</summary>
    [Serializable]
    [ForgeMenu(Name = "Float", Description = "A number that can scale, read where it's used.")]
    public class LocalFloat : LocalDataValue<ScalableFloat>
    {
        public LocalFloat() => Value = 0f;

        public override object Read(in NumberContext context) => Value?.Evaluate(context) ?? 0f;

        public override bool TryReadNumber(in NumberContext context, out float number)
        {
            number = Value?.Evaluate(context) ?? 0f;
            return true;
        }
    }

    /// <summary>A whole number that can scale, read where it's used.</summary>
    [Serializable]
    [ForgeMenu(Name = "Int", Description = "A whole number that can scale, read where it's used.")]
    public class LocalInt : LocalDataValue<ScalableInt>
    {
        public LocalInt() => Value = 0;

        public override object Read(in NumberContext context) => Value?.Evaluate(context) ?? 0;

        public override bool TryReadNumber(in NumberContext context, out float number)
        {
            number = Value?.Evaluate(context) ?? 0;
            return true;
        }
    }

    /// <summary>A string.</summary>
    [Serializable]
    [ForgeMenu(Name = "String", Description = "A string.")]
    public class LocalString : LocalDataValue<string>
    {
    }

    /// <summary>A bool.</summary>
    [Serializable]
    [ForgeMenu(Name = "Bool", Description = "A bool.")]
    public class LocalBool : LocalDataValue<bool>
    {
    }

    /// <summary>A tag; one left empty is none.</summary>
    [Serializable]
    [ForgeMenu(Name = "Tag", Description = "A tag.")]
    public class LocalTag : LocalDataValue<Tag>
    {
        public override object Held => Value.IsValid ? Value : null;
    }

    /// <summary>An attribute.</summary>
    [Serializable]
    [ForgeMenu(Name = "Attribute", Description = "An attribute.")]
    public class LocalAttribute : LocalDataValue<Attribute>
    {
    }

    /// <summary>An effect.</summary>
    [Serializable]
    [ForgeMenu(Name = "Effect", Description = "An effect.")]
    public class LocalEffect : LocalDataValue<GameplayEffect>
    {
    }

    /// <summary>An ability.</summary>
    [Serializable]
    [ForgeMenu(Name = "Ability", Description = "An ability.")]
    public class LocalAbility : LocalDataValue<Ability>
    {
    }

    /// <summary>An entity's identity.</summary>
    [Serializable]
    [ForgeMenu(Name = "Entity", Description = "An entity's identity.")]
    public class LocalEntity : LocalDataValue<EntityIdentity>
    {
    }

    /// <summary>Any asset, as a prefab or a material.</summary>
    [Serializable]
    [ForgeMenu(Name = "Object", Description = "Any asset, as a prefab or a material.")]
    public class LocalObject : LocalDataValue<Object>
    {
    }

    /// <summary>A component on a prefab, as a process to spawn.</summary>
    [Serializable]
    [ForgeMenu(Name = "MonoBehaviour", Description = "A component on a prefab, as a process to spawn.")]
    public class LocalMonoBehaviour : LocalDataValue<MonoBehaviour>
    {
    }

    /// <summary>A ScriptableObject asset.</summary>
    [Serializable]
    [ForgeMenu(Name = "ScriptableObject", Description = "A ScriptableObject asset.")]
    public class LocalScriptableObject : LocalDataValue<ScriptableObject>
    {
    }
}
