#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// JSON helpers for settings (Dictionary<Tag, object>) and framework data.
    /// </summary>
    public static class ForgeJsonUtility
    {
        [Serializable]
        public sealed class SettingsWrapper
        {
            public Dictionary<Tag, object> Data;

            public SettingsWrapper()
            {
                Data = new Dictionary<Tag, object>();
            }

            public SettingsWrapper(Dictionary<Tag, object> data)
            {
                Data = data;
            }

            public bool Status(Tag target, bool fallback = false)
            {
                return Status<bool>(target, fallback);
            }

            public T Status<T>(Tag target, T fallback = default)
            {
                try
                {
                    if (!Data.ContainsKey(target)) return fallback;
                    return (T)Data[target];
                }
                catch
                {
                    return fallback;
                }
            }
            
            public bool StatusCheck(Tag target, out bool result, bool fallback = false)
            {
                return StatusCheck<bool>(target, out result, fallback);
            }

            public bool StatusCheck<T>(Tag target, out T result, T fallback = default)
            {
                try
                {
                    if (!Data.ContainsKey(target))
                    {
                        result = fallback;
                        return false;
                    }
                    result = (T)Data[target];
                    return true;
                }
                catch
                {
                    result = fallback;
                    return false;
                }
            }

            public void Set(Tag target, object data)
            {
                Data[target] = data;
            }
        }
        
        #region Helpers

        public static string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unnamed";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            var slug = new string(chars).Trim('_');
            return string.IsNullOrEmpty(slug) ? "Unnamed" : slug;
        }
        
        #endregion
        
        // ---------- SETTINGS (Tag->object (string|int|bool|list|nested map)) ----------

        public static void SaveSettings(SettingsWrapper src, string filePath)
        {
            var j = SerializeTagObjectMap(src.Data);
            File.WriteAllText(filePath, j.ToString(Formatting.Indented));
            ForgePaths.RefreshAssets();
        }

        public static SettingsWrapper LoadSettings(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }
            var root = JToken.Parse(File.ReadAllText(filePath));
            return new SettingsWrapper(DeserializeTagObjectMap(root as JObject));
        }

        // ---------- FRAMEWORK (FrameworkProject) ----------

        public static void SaveFramework(FrameworkProject proj, bool toProjectSettings = true)
        {
            if (proj == null) throw new ArgumentNullException(nameof(proj));

            var key = Slugify(proj.MetaName);
            var path = toProjectSettings ? ForgePaths.FrameworkPath(key) : ForgePaths.RuntimeFrameworkPath(key);

            var root = new JObject
            {
                ["Version"]   = proj.Version   ?? "",
                ["MetaName"]  = proj.MetaName  ?? "",
                ["MetaAuthor"]= proj.MetaAuthor?? ""
            };

            // Serialize all List<T> where T : GasifyDataNode across FrameworkProject
            var fields = typeof(FrameworkProject).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (!IsListOfDataNode(f.FieldType)) continue;

                var list = (IList)f.GetValue(proj);
                var arr = new JArray();
                if (list != null)
                {
                    foreach (var node in list)
                        arr.Add(SerializeNode((ForgeDataNode)node));
                }
                root[f.Name] = arr;
            }

            File.WriteAllText(path, root.ToString(Formatting.Indented));
            ForgePaths.RefreshAssets();
        }

        public static FrameworkProject LoadFramework(string frameworkKeyOrMetaName, bool loadFromProjectSettings = true)
        {
            var path = loadFromProjectSettings ? ForgePaths.FrameworkPath(frameworkKeyOrMetaName) : ForgePaths.RuntimeFrameworkPath(frameworkKeyOrMetaName);
            if (!File.Exists(path)) return null;

            var root = JObject.Parse(File.ReadAllText(path));

            var proj = new FrameworkProject
            {
                Version    = root.Value<string>("Version"),
                MetaName   = root.Value<string>("MetaName"),
                MetaAuthor = root.Value<string>("MetaAuthor")
            };

            // Rehydrate lists generically
            var fields = typeof(FrameworkProject).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (!IsListOfDataNode(f.FieldType))
                    continue;

                var elemT = f.FieldType.GetGenericArguments()[0];
                var arr = root[f.Name] as JArray ?? new JArray();
                var list = (IList)Activator.CreateInstance(f.FieldType);
                foreach (var item in arr.OfType<JObject>())
                {
                    list.Add(DeserializeNode(item, elemT));
                }
                f.SetValue(proj, list);
            }

            return proj;
        }

        // ---------- Serialization helpers ----------
        
        static JObject SerializeNode(ForgeDataNode node)
        {
            var jo = new JObject();
            // Only public instance fields are serialized currently (matching your existing approach)
            var fields = node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == nameof(ForgeDataNode.editorTags))
                {
                    var tags = (Dictionary<Tag, object>)f.GetValue(node);
                    jo[f.Name] = SerializeTagObjectMap(tags);
                }
                else
                {
                    var val = f.GetValue(node);
                    jo[f.Name] = val != null
                        ? JToken.FromObject(val, ForgeJsonSettings.Serializer)   // <-- uses converters (Tag, Attribute, etc.)
                        : JValue.CreateNull();
                }
            }
            return jo;
        }

        static ForgeDataNode DeserializeNode(JObject jo, Type nodeType)
        {
            var node = (ForgeDataNode)Activator.CreateInstance(nodeType);
            var fields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                var tok = jo[f.Name];
                if (tok == null) continue;

                if (f.Name == nameof(ForgeDataNode.editorTags))
                {
                    f.SetValue(node, DeserializeTagObjectMap(tok as JObject));
                }
                else
                {
                    var val = tok.ToObject(f.FieldType, ForgeJsonSettings.Serializer); // <-- uses converters
                    f.SetValue(node, val);
                }
            }
            return node;
        }

        // Tag -> object map (object is string/int/bool/list or nested Tag->object dictionary)
        // Keys are Tag.Name; values serialized manually to keep loose typing predictable.
        static JObject SerializeTagObjectMap(Dictionary<Tag, object> map)
        {
            var root = new JObject();
            if (map == null) return root;

            foreach (var (tag, value) in map)
            {
                string key = tag.Name;
                root[key] = SerializeTagValue(value);
            }
            return root;
        }
        
        static JToken SerializeTagValue(object value)
        {
            if (value == null) return JValue.CreateNull();

            // Nested Tag->object dictionary?
            if (value is Dictionary<Tag, object> nested)
                return SerializeTagObjectMap(nested);

            // Dictionary<DataType, int> or Dictionary<DataType, List<int>>
            if (IsDataTypeIntDict(value) || IsDataTypeIntListDict(value))
                return JToken.FromObject(value, ForgeJsonSettings.Serializer); // DataType keys handled by converter

            // Lists (keep elements via shared serializer so converters apply)
            if (value is IList list && value.GetType().IsGenericType)
                return JArray.FromObject(list, ForgeJsonSettings.Serializer);

            // Supported scalar types
            if (value is string or int or bool or long or float or double)
                return JToken.FromObject(value, ForgeJsonSettings.Serializer);

            // Fallback: ToString (keeps JSON sane)
            return JValue.CreateString(value.ToString());
        }

        static JObject SerializeDataTypeObjectMap(Dictionary<EDataType, object> map)
        {
            var root = new JObject();
            if (map == null) return root;

            foreach (var (kind, value) in map)
            {
                string key = kind.ToString();
                root[key] = SerializeTagValue(value);
            }
            return root;
        }
        
        static bool IsListOfDataNode(Type t) =>
            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) &&
            typeof(ForgeDataNode).IsAssignableFrom(t.GetGenericArguments()[0]);

        static bool IsDataTypeIntDict(object value)
        {
            var t = value.GetType();
            if (!t.IsGenericType) return false;
            if (t.GetGenericTypeDefinition() != typeof(Dictionary<,>)) return false;
            var args = t.GetGenericArguments();
            return args[0] == typeof(EDataType) && args[1] == typeof(int);
        }

        static bool IsDataTypeIntListDict(object value)
        {
            var t = value.GetType();
            if (!t.IsGenericType) return false;
            if (t.GetGenericTypeDefinition() != typeof(Dictionary<,>)) return false;
            var args = t.GetGenericArguments();
            if (args[0] != typeof(EDataType)) return false;

            var vt = args[1];
            return vt.IsGenericType &&
                   vt.GetGenericTypeDefinition() == typeof(List<>) &&
                   vt.GetGenericArguments()[0] == typeof(int);
        }
        
        // ---------- Deserialization helpers ----------
        
        static Dictionary<Tag, object> DeserializeTagObjectMap(JObject root)
        {
            var map = new Dictionary<Tag, object>();
            if (root == null) return map;

            foreach (var prop in root.Properties())
            {
                var tag = Tag.Generate(prop.Name);
                map[tag] = DeserializeTagValue(prop.Value);
            }
            return map;
        }

        static object DeserializeTagValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;

                // Try to recognize a Dict<DataType, int> or Dict<DataType, List<int>> payload
                if (TryDeserializeDataTypeIntOrIntList(obj, out var dtDict))
                {
                    return dtDict;
                }

                // Otherwise it's a nested Tag->object dictionary
                return DeserializeTagObjectMap(obj);
            }

            if (token.Type == JTokenType.Array)
                return ((JArray)token).Select(DeserializeTagValue).ToList();

            // scalars
            if (token.Type == JTokenType.Integer) return token.Value<int>();
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            if (token.Type == JTokenType.Float)   return token.Value<double>();

            return token.Value<string>();
        }
        
        // Recognize & build Dict<DataType,int> or Dict<DataType,List<int>>
        static bool TryDeserializeDataTypeIntOrIntList(JObject jo, out object result)
        {
            result = null;
            if (jo == null || !jo.Properties().Any()) return false;

            // 1) Validate keys & classify value shapes
            bool allKeysAreDataType = true;
            bool anyArray = false;
            bool allValuesValid = true;

            foreach (var p in jo.Properties())
            {
                if (!Enum.TryParse<EDataType>(p.Name, ignoreCase: false, out _))
                {
                    allKeysAreDataType = false;
                    break;
                }

                var val = p.Value;
                if (val.Type == JTokenType.Integer)
                {
                    // ok
                }
                else if (val.Type == JTokenType.Array)
                {
                    anyArray = true;
                    // Ensure array is all ints (tolerate numeric strings)
                    foreach (var e in (JArray)val)
                    {
                        if (e.Type == JTokenType.Integer) continue;
                        if (e.Type == JTokenType.Float) continue;
                        if (e.Type == JTokenType.String && int.TryParse((string)e, out _)) continue;
                        allValuesValid = false;
                        break;
                    }
                    if (!allValuesValid) break;
                }
                else
                {
                    allValuesValid = false;
                    break;
                }
            }

            if (!allKeysAreDataType || !allValuesValid)
                return false;

            // 2) Build strongly-typed dictionary. If any arrays are present, normalize to List<int>.
            if (anyArray)
            {
                var dict = new Dictionary<EDataType, List<int>>();
                foreach (var p in jo.Properties())
                {
                    var k = (EDataType)Enum.Parse(typeof(EDataType), p.Name);
                    var v = p.Value;

                    if (v.Type == JTokenType.Integer)
                    {
                        dict[k] = new List<int> { v.Value<int>() };
                    }
                    else // array
                    {
                        var list = new List<int>();
                        foreach (var e in (JArray)v)
                        {
                            if (e.Type == JTokenType.Integer) list.Add(e.Value<int>());
                            else if (e.Type == JTokenType.Float) list.Add((int)Math.Round(e.Value<double>()));
                            else if (e.Type == JTokenType.String && int.TryParse((string)e, out var iv)) list.Add(iv);
                        }
                        dict[k] = list;
                    }
                }
                result = dict;
                return true;
            }
            else
            {
                var dict = new Dictionary<EDataType, int>();
                foreach (var p in jo.Properties())
                {
                    var k = (EDataType)Enum.Parse(typeof(EDataType), p.Name);
                    dict[k] = p.Value.Value<int>();
                }
                result = dict;
                return true;
            }
        }
    }
}
#endif
