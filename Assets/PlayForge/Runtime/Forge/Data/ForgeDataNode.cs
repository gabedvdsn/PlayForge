using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    [Serializable]
    public abstract class ForgeDataNode
    {
        public int Id;
        
        public string Name;
        public string Description;
        public Sprite Icon;

        public Dictionary<Tag, object> editorTags = new();
        
        public abstract Dictionary<Tag, ForgeDataNode> GetRefs();

        public static T Build<T>(string name, string desc = "") where T : ForgeDataNode, new()
        {
            var t = new T()
            {
                Id = DataIdRegistry.Generate(),
                Name = name,
                Description = desc,
                editorTags = new Dictionary<Tag, object>()
            };
            return t;
        }
        
        public static T Clone<T>(T src, out int _id, string nameSuffix = " (SavedClone)", bool setCloneRef = true) where T : ForgeDataNode, new()
        {
            _id = -1;
            if (src is null) return null;

            var dst = new T();

            foreach (var f in src.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name == "Id") continue;
                if (f.IsInitOnly) continue;

                var v = f.GetValue(src);
                f.SetValue(dst, CloneValue(v, f.FieldType));
            }

            _id = DataIdRegistry.Generate();
            dst.Id = _id;
            
            if (!string.IsNullOrEmpty(dst.Name)) dst.Name += nameSuffix;
            else dst.Name = src.Name + nameSuffix;
            
            ForgeTags.ForCloneData(dst, src, setCloneRef);

            return dst;
        }
        
        private static object CloneValue(object value, Type t)
        {
            if (value == null) return null;

            if (t == typeof(object)) return CloneValue(value, value.GetType());

            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t.IsValueType) return value;

            if (typeof(ForgeDataNode).IsAssignableFrom(t)) return value;
            
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemT = t.GetGenericArguments()[0];
                var srcList = (IList)value;
                var dstList = (IList)Activator.CreateInstance(t);
                for (int i = 0; i < srcList.Count; i++)
                    dstList.Add(CloneValue(srcList[i], elemT));
                return dstList;
            }
            
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = t.GetGenericArguments();
                var keyT = args[0];
                var valT = args[1];
                var srcDict = (IDictionary)value;
                var dstDict = (IDictionary)Activator.CreateInstance(t);
                foreach (DictionaryEntry kv in srcDict)
                {
                    var k = CloneValue(kv.Key, keyT);
                    var v = CloneValue(kv.Value, valT);
                    dstDict.Add(k, v);
                }

                return dstDict;
            }
            
            if (t.IsArray)
            {
                var elemT = t.GetElementType();
                var srcArr = (Array)value;
                var dstArr = Array.CreateInstance(elemT, srcArr.Length);
                for (int i = 0; i < srcArr.Length; i++)
                    dstArr.SetValue(CloneValue(srcArr.GetValue(i), elemT), i);
                return dstArr;
            }
            
            // Plain POCO class: clone its public instance fields recursively
            var dstObj = Activator.CreateInstance(t);
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.IsInitOnly) continue;
                var v = f.GetValue(value);
                f.SetValue(dstObj, CloneValue(v, f.FieldType));
            }
            return dstObj;
        }
        
        public static bool CompareToClone<T>(ForgeDataNode gNode, EDataType kind, FrameworkProject project) where T : ForgeDataNode
        {
            var node = gNode as T;
            if (node == null) return true;

            int copyId = node.TagStatus<int>(ForgeTags.COPY_ID);
            if (copyId == 0) return true;
            if (!project.TryGet(copyId, "", kind, out var genCopy)) return true;

            var copy = genCopy as T;
            if (copy == null) return true;

            // Use runtime type to include derived fields
            var rootType = node.GetType();

            // visited set to prevent cycles
            var visited = new HashSet<(object A, object B)>(new RefPairComparer());

            return CompareValue(node, copy, rootType);

            bool CompareValue(object a, object b, Type t)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a is null || b is null) return false;

                // prevent infinite recursion on cycles
                var pair = (a, b);
                if (!visited.Add(pair)) return true;

                // Normalize runtime type when t is too generic
                if (a.GetType() != b.GetType()) return false;
                t ??= a.GetType();

                // Primitive / enum / string / simple value types
                if (t.IsPrimitive || t.IsEnum || t == typeof(string))
                    return Equals(a, b);

                if (t == typeof(float))
                    return Mathf.Approximately((float)a, (float)b);

                if (t == typeof(double))
                    return Math.Abs((double)a - (double)b) < 1e-9;

                if (t.IsValueType && !t.IsPrimitive && !t.IsEnum)
                {
                    // struct: compare its fields
                    return CompareObjectFields(a, b, t);
                }

                switch (t.IsGenericType)
                {
                    // List<T>
                    case true when t.GetGenericTypeDefinition() == typeof(List<>):
                    {
                        var elemT = t.GetGenericArguments()[0];
                        var aList = (IList)a;
                        var bList = (IList)b;
                        if (aList.Count != bList.Count) return false;
                        foreach (object aVal in aList)
                        {
                            int bi = bList.IndexOf(aVal);
                            if (bi < 0) return false;
                            if (!CompareValue(aVal, bList[bi], elemT)) return false;
                        }

                        return true;
                    }
                    // Dictionary<K,V>
                    case true when t.GetGenericTypeDefinition() == typeof(Dictionary<,>):
                    {
                        var args = t.GetGenericArguments();
                        var valT = args[1];
                        var aDict = (IDictionary)a;
                        var bDict = (IDictionary)b; // <-- fixed
                        if (aDict.Count != bDict.Count) return false;

                        foreach (DictionaryEntry kv in aDict)
                        {
                            if (!bDict.Contains(kv.Key)) return false;
                            if (!CompareValue(aDict[kv.Key], bDict[kv.Key], valT)) return false;
                        }

                        return true;
                    }
                }

                // Arrays
                if (t.IsArray)
                {
                    var elemT = t.GetElementType();
                    var aArr = (Array)a;
                    var bArr = (Array)b;
                    if (aArr.Length != bArr.Length) return false;
                    for (int i = 0; i < aArr.Length; i++)
                        if (!CompareValue(aArr.GetValue(i), bArr.GetValue(i), elemT))
                            return false;
                    return true;
                }

                // POCO / reference types: compare fields
                return CompareObjectFields(a, b, t);
            }

            bool CompareObjectFields(object a, object b, Type t)
            {
                // Include public OR [SerializeField] private instance fields, across inheritance
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    foreach (var f in cur.GetFields(flags))
                    {
                        if (f.IsStatic) continue;
                        // Only consider Unity-serialized fields or public (to match your persistence surface)
                        bool unityVisible = f.IsPublic || System.Attribute.IsDefined(f, typeof(SerializeField));
                        if (!unityVisible) continue;

                        if (f.IsInitOnly) continue; // skip readonly

                        var aVal = f.GetValue(a);
                        var bVal = f.GetValue(b);
                        if (!CompareValue(aVal, bVal, f.FieldType)) return false;
                    }
                }

                return true;
            }
        }

        // Reference-equality comparer for the visited set
        sealed class RefPairComparer : IEqualityComparer<(object A, object B)>
        {
            public bool Equals((object A, object B) x, (object A, object B) y) =>
                ReferenceEquals(x.A, y.A) && ReferenceEquals(x.B, y.B);
            public int GetHashCode((object A, object B) obj) =>
                unchecked((RuntimeHelpers.GetHashCode(obj.A) * 397) ^ RuntimeHelpers.GetHashCode(obj.B));
        }

        public override string ToString()
        {
            
            return $"({Name}-{Id.ToString("x8")[..3]}...)";
        }
    }

    public static class ForgeFields
    {
        public const string NameField = "Name";
        public const string DescriptionField = "Description";
        public const string IconField = "Icon";
        
        public const string AssetTagField = "AssetTag";

        public static class Ability
        {
            public const string CooldownField = "Cooldown";
            public const string CostField = "Cost";
            public const string DefinitionField = "Definition";
            public const string ActivationPolicyField = "ActivationPolicy";
            public const string ActivateImmediatelyField = "ActivateImmediately";
            public const string IgnoreWhenLevelZeroField = "IgnoreWhenLevelZero";
            public const string MaxLevelField = "MaxLevel";
            public const string StartingLevelField = "Level";
            public const string TargetingProxyField = "TargetingProxy";
            public const string UseImplicitTargetingField = "UseImplicitTargeting";
            public const string AbilityProxyStagesField = "AbilityProxyStages";

            public const string ContextTagsField = "ContextTags";
            public const string PassivelyGrantedTagsField = "PassivelyGrantedTags";
            public const string ActivelyGrantedTagsField = "ActivelyGrantedTags";

            public const string SourceRequirementsField = "SourceRequirements";
            public const string TargetRequirementsField = "TargetRequirements";
            
        }
    }
    
    [Serializable]
    [MirrorFrom(typeof(Attribute))]
    [MirrorSubstituteOpenGeneric(typeof(UnityEngine.Object), typeof(AssetRef<>))]
    public partial class AttributeData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }
    
    [Serializable]
    [MirrorFrom(typeof(Tag))]
    [MirrorSubstituteOpenGeneric(typeof(UnityEngine.Object), typeof(AssetRef<>))]
    public partial class TagData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    [MirrorFrom(typeof(Ability))]
    [MirrorSubstituteOpenGeneric(typeof(UnityEngine.Object), typeof(AssetRef<>))]
    public partial class AbilityData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    public class ProxyTaskData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }
    
    [Serializable]
    [MirrorFrom(typeof(GameplayEffect))]
    [MirrorSubstituteOpenGeneric(typeof(UnityEngine.Object), typeof(AssetRef<>))]
    public partial class EffectData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    [MirrorFrom(typeof(EntityIdentity))]
    [MirrorSubstituteOpenGeneric(typeof(UnityEngine.Object), typeof(AssetRef<>))]
    public partial class EntityData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    [MirrorFrom(typeof(AttributeSet))]
    [MirrorSubstituteOpenGeneric(typeof(UnityEngine.Object), typeof(AssetRef<>))]
    public partial class AttributeSetData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    public class ModifierData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    public class AttributeEventData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }
    
    [Serializable]
    public class ImpactWorkerData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    public class EffectWorkerData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    public class TagWorkerData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }

    [Serializable]
    public class ProcessInstantiatorData : ForgeDataNode
    {
        public override Dictionary<Tag, ForgeDataNode> GetRefs()
        {
            return null;
        }
    }
}
