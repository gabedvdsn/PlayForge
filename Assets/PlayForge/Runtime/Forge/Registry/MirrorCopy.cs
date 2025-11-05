using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FarEmerald
{
    public static class MirrorCopy
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void CopyByNameTypeAndRename(object src, object dst)
        {
            if (src == null || dst == null) return;

            // Build destination field map
            var dstFields = GetAllFields(dst.GetType());
            var dstByName = dstFields.ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

            // Build rename map from attributes on the destination type (e.g., AbilityData)
            // “From” = source field name; “To” = destination field name
            var renames = dst.GetType().GetCustomAttributes<MirrorRenameAttribute>()
                .GroupBy(a => a.From, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Last().To, StringComparer.Ordinal);

            foreach (var sf in GetAllFields(src.GetType()))
            {
                if (sf.IsStatic || sf.IsInitOnly) continue;

                var srcName = sf.Name;
                var dstName = renames.TryGetValue(srcName, out var to) ? to : srcName;

                if (!dstByName.TryGetValue(dstName, out var df)) continue;
                if (df.FieldType == sf.FieldType)
                {
                    var v = sf.GetValue(src);
                    df.SetValue(dst, v);
                    continue;
                }

                // Special case: allow T -> AssetRef<T> assignment if you use AssetRef<>
                if (IsAssetRefOf(df.FieldType, sf.FieldType))
                {
                    // editor-time you’d compute an address; at runtime you likely already have addresses
                    var aref = Activator.CreateInstance(df.FieldType);
    #if UNITY_EDITOR
                    // If you maintain an editor helper to compute Resource/Addressables keys, call it here to fill the address.
                    // Example: AssetRefUtility.AssignEditorObject(aref, (UnityEngine.Object)sf.GetValue(src));
    #endif
                    df.SetValue(dst, aref);
                }
            }
        }

        static IEnumerable<FieldInfo> GetAllFields(Type t)
        {
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                foreach (var f in cur.GetFields(F)) yield return f;
        }

        static bool IsAssetRefOf(Type dstType, Type srcType)
            => dstType.IsGenericType
            && dstType.GetGenericTypeDefinition().Name is "AssetRef`1"
            && dstType.GetGenericArguments()[0].IsAssignableFrom(srcType);
    }
}
