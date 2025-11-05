using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace FarEmerald.PlayForge.Extended
{
    /// Centralized JSON rules used by both Editor and Runtime.
    public static class ForgeJsonSettings
    {
        static readonly Lazy<JsonSerializerSettings> _settings = new(() =>
        {
            var s = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore,
                Converters =
                {
                    new TagJsonConverter(),
                    new TagKeyedDictionaryConverter(),
                    new DataTypeKeyedDictionaryConverter()
                }
            };
            return s;
        });

        public static JsonSerializerSettings Settings => _settings.Value;
        public static JsonSerializer Serializer => JsonSerializer.Create(Settings);
    }

    public sealed class DataTypeKeyedDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (!objectType.IsGenericType) return false;
            var def = objectType.GetGenericTypeDefinition();
            if (def != typeof(Dictionary<,>)) return false;
            var args = objectType.GetGenericArguments();
            return args[0] == typeof(EDataType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var type = value.GetType();
            var args = type.GetGenericArguments();
            var valType = args[1];
            
            writer.WriteStartObject();

            var enumerator = (System.Collections.IEnumerable)value;
            foreach (var kv in enumerator)
            {
                var key = (EDataType)kv.GetType().GetProperty("Key")!.GetValue(kv, null);
                var keyName = key.ToString();
                writer.WritePropertyName(keyName);

                var v = kv.GetType().GetProperty("Value")!.GetValue(kv, null);
                serializer.Serialize(writer, v, valType);
            }
            
            writer.WriteEndObject();
        }
        
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var args = objectType.GetGenericArguments();
            var valType = args[1];

            var jo = JObject.Load(reader);
            var dict = (System.Collections.IDictionary)Activator.CreateInstance(objectType);

            foreach (var prop in jo.Properties())
            {
                var dataType = (EDataType)Enum.Parse(typeof(EDataType), prop.Name);
                var val = prop.Value.ToObject(valType, serializer);
                dict.Add(dataType, val);
            }

            return dict;
        }
    }
    
    /// Tag <-> "Tag/Path" (string)
    public sealed class TagJsonConverter : JsonConverter<Tag>
    {
        public override void WriteJson(JsonWriter w, Tag value, JsonSerializer s)
        {
            w.WriteValue(value.Name);
        }

        public override Tag ReadJson(JsonReader r, Type objectType, Tag existingValue, bool hasExistingValue, JsonSerializer s)
        {
            // Expect a string; tolerate null
            if (r.TokenType == JsonToken.Null) return default;
            if (r.TokenType != JsonToken.String) throw new JsonSerializationException("Tag expects string");
            var name = (string)r.Value;
            return Tag.Generate(name);
        }
    }

    /// Converts any Dictionary<Tag, T> to a JSON object with string keys (Tag.Name).
    /// Works for nested / complex T because we delegate values to the serializer.
    public sealed class TagKeyedDictionaryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (!objectType.IsGenericType) return false;
            var def = objectType.GetGenericTypeDefinition();
            if (def != typeof(Dictionary<,>)) return false;
            var args = objectType.GetGenericArguments();
            return args[0] == typeof(Tag); // Dictionary<Tag, *>
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var type = value.GetType();
            var args = type.GetGenericArguments();
            var valType = args[1];

            writer.WriteStartObject();

            var enumerator = (System.Collections.IEnumerable)value;
            var nameProp = typeof(Tag).GetProperty(nameof(Tag.Name));
            foreach (var kv in enumerator)
            {
                var key = (Tag)kv.GetType().GetProperty("Key")!.GetValue(kv, null);
                var keyName = key.Name; // nameProp!.GetValue(key, null) as string;
                writer.WritePropertyName(keyName);

                var v = kv.GetType().GetProperty("Value")!.GetValue(kv, null);
                serializer.Serialize(writer, v, valType);
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var args = objectType.GetGenericArguments();
            var valType = args[1];

            var jo = JObject.Load(reader);
            var dict = (System.Collections.IDictionary)Activator.CreateInstance(objectType);

            foreach (var prop in jo.Properties())
            {
                var tag = Tag.Generate(prop.Name);
                var val = prop.Value.ToObject(valType, serializer);
                dict.Add(tag, val);
            }

            return dict;
        }
    }
}
