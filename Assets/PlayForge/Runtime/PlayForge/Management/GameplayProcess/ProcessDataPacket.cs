using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// ProcessDataPackets (and subclasses) contain data pertaining to processes and abilities
    /// Example usage:
    ///     An ability, Cast, fires a ball that homes in on its target
    ///     Cast is the ability and via implicit data and/or targeting tasks assigns some data to the packet
    ///     The Ball is a MonoProcess and is created via ProcessControl
    ///     Before initializing the MonoProcessWrapper is passed the data packet and its initial values are set
    ///     After initializing the MonoProcess is passed the data packet
    ///     It is responsible for procuring data from the data packet
    ///         E.g. procuring the target transform via Data.TryGetPayload[Transform](Target, GameRoot.TransformParameter, Primary, out Transform value)
    ///         This procures the Transform value under the Target classification stored under the key GameRoot.TransformParameter
    /// </summary>
    public class ProcessDataPacket : IValidationReady, IGameplayProcessHandler
    {
        private const char pathSeparator = '>';
        
        protected Dictionary<Tag, List<object>> _payload = new();
        public IReadOnlyDictionary<Tag, List<object>> Payload => _payload;
        public bool InUse = true;
        
        public readonly Dictionary<int, ProcessRelay> HandlerRelays = new();
        
        public AbstractMonoProcessInstantiator CustomInstantiator;

        public string Path { get; protected set; }
        public void AppendPath(string r)
        {
            if (!string.IsNullOrEmpty(Path)) Path += pathSeparator;
            Path += r;
        }
        
        protected ProcessDataPacket()
        { }

        public ProcessDataPacket(ProcessDataPacket other)
        {
            _payload = new Dictionary<Tag, List<object>>();

            if (other is null) return;
            
            foreach (var kvp in other.Payload)
            {
                _payload[kvp.Key] = new List<object>();
                foreach (object data in kvp.Value) _payload[kvp.Key].Add(data);
            }
            
            InUse = other.InUse;
        }
        
        #region Construction

        /// <summary>
        /// Empty data packet handled by GameRoot.
        /// </summary>
        public static ProcessDataPacket Default()
        {
            return Internal_GenerateDataPacket(false, null);
        }

        /// <summary>
        /// Data packet handled by GameRoot, automatically assigned as a child of GameRoot in-scene.
        /// </summary>
        public static ProcessDataPacket SceneRoot()
        {
            return Internal_GenerateDataPacket(true, GameRoot.Instance.transform);
        }
        
        /// <summary>
        /// Data packet handled by GameRoot, assigned as a child of parent in-scene
        /// </summary>
        /// <param name="parent">Parent transform to assign</param>
        /// <returns></returns>
        public static ProcessDataPacket SceneLocal(Transform parent)
        {
            return Internal_GenerateDataPacket(true, parent);
        }

        private static ProcessDataPacket Internal_GenerateDataPacket(bool setParent, Transform parent)
        {
            var data = new ProcessDataPacket();
            
            if (setParent) data.SetPrimary(Tags.PARENT_TRANSFORM, parent);
            
            return data;
        }
        
        #endregion
        
        #region Core

        public void AddPayload<T>(Tag key, T value)
        {
            if (!_payload.ContainsKey(key))
            {
                _payload[key] = new List<object>()
                {
                    value
                };
            }
            else _payload[key].Add(value);
        }
        
        public void AddPayload<T>(Tag key, IEnumerable<T> values)
        {
            if (!_payload.ContainsKey(key))
            {
                _payload[key] = new List<object>();
            }
            
            foreach (var v in values)
            {
                _payload[key].Add(v);
            }
        }
        
        public void InsertPayload<T>(Tag key, int index, T value)
        {
            if (!_payload.ContainsKey(key))
            {
                _payload[key] = new List<object>()
                {
                    value
                };
            }
            else
            {
                int _index = Mathf.Clamp(index, 0, _payload[key].Count);
                if (_index > 0) _payload[key].Insert(_index, value);
                else _payload[key].Add(value);
            }
        }

        public void SetPayload<T>(Tag key, int index, T value)
        {
            if (!_payload.ContainsKey(key))
            {
                _payload[key] = new List<object>()
                {
                    value
                };
            }
            else if (index >= 0 && _payload[key].Count > index)
            {
                _payload[key][index] = value;
            }
        }

        public T Get<T>(Tag key, EDataTarget target, T fallback = default)
        {
            return TryGet<T>(key, target, out var value) ? value : fallback;
        }
        
        public bool TryGet<T>(Tag key, EDataTarget target, out T value)
        {
            value = default;
            
            if (!_payload.ContainsKey(key))
            {
                return false;
            }

            object o = target switch
            {
                EDataTarget.Primary => _payload[key][0],
                EDataTarget.Any => _payload[key].RandomChoice(),
                EDataTarget.Last => _payload[key][^1],
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            };

            if (o is not T cast) return false;
            value = cast;
            return value is not null;
        }

        public bool TryGetFirst<T>(Tag key, out T value)
        {
            value = default;
            if (!_payload.ContainsKey(key)) return false;
            
            foreach (object o in _payload[key])
            {
                if (o is not T cast) continue;
                
                value = cast;
                return true;
            }
            
            return false;
        }
        
        public bool TryGet<T>(Tag key, out DataValue<T> dataValue)
        {
            if (!_payload.ContainsKey(key))
            {
                dataValue = default;
                return false;
            }
            
            List<T> tObjects = new List<T>();
            foreach (object o in _payload[key])
            {
                if (o is T cast) tObjects.Add(cast);
            }

            dataValue = new DataValue<T>(tObjects);
            return dataValue.Valid;
        }

        public bool Remove(Tag key)
        {
            return _payload.Remove(key);
        }

        public bool Remove<T>(Tag key, T obj)
        {
            if (!_payload.ContainsKey(key)) return false;
            int index = -1;
            for (int i = 0; i < _payload[key].Count; i++)
            {
                if (_payload[key][i] is not T cast || !obj.Equals(cast)) continue;
                
                index = i;
                break;
            }

            if (index < 0) return false;
            
            _payload[key].RemoveAt(index);
            return true;
        }

        public bool ContainsKey(Tag key) => _payload.ContainsKey(key);
        
        public bool Contains<T>(T value, Tag key)
        {
            if (!_payload.ContainsKey(key)) return false;
            
            foreach (object o in _payload[key])
            {
                if (o is T cast && cast.Equals(value)) return true;
            }

            return false;
        }

        #endregion
        
        #region Convenience
        
        /// <summary>
        /// Gets the primary (first) payload value with a default fallback.
        /// </summary>
        public T GetPrimary<T>(Tag key, T fallback = default)
        {
            return TryGet<T>(key, EDataTarget.Primary, out var value) ? value : fallback;
        }
        
        /// <summary>
        /// Gets the last payload value with a default fallback.
        /// </summary>
        public T GetLast<T>(Tag key, T fallback = default)
        {
            return TryGet<T>(key, EDataTarget.Last, out var value) ? value : fallback;
        }
        
        /// <summary>
        /// Sets or overwrites the primary (first) payload value.
        /// </summary>
        public void SetPrimary<T>(Tag key, T value)
        {
            SetPayload(key, 0, value);
        }
        
        /// <summary>
        /// Checks if a payload key exists and has a retrievable value.
        /// </summary>
        public bool Contains(Tag key)
        {
            return TryGet<object>(key, EDataTarget.Primary, out _);
        }
        
        /// <summary>
        /// Returns how many entries exist under a payload key.
        /// </summary>
        public int Count(Tag key)
        {
            return _payload.TryGetValue(key, out var list) ? list.Count : 0;
        }
        
        /// <summary>
        /// Gets or initializes a value (GetOrAdd pattern).
        /// If the key does not exist, adds the default value and returns it.
        /// </summary>
        public T GetOrInit<T>(Tag key, T defaultValue)
        {
            if (TryGet<T>(key, EDataTarget.Primary, out var value))
                return value;
            
            AddPayload(key, defaultValue);
            return defaultValue;
        }
        
        /// <summary>
        /// Increments an integer payload value. Creates the key with the result if it doesn't exist.
        /// </summary>
        public int Increment(Tag key, int amount = 1)
        {
            int current = GetPrimary<int>(key, 0);
            int newValue = current + amount;
            SetPrimary(key, newValue);
            return newValue;
        }
        
        /// <summary>
        /// Decrements an integer payload value. Creates the key with the result if it doesn't exist.
        /// </summary>
        public int Decrement(Tag key, int amount = 1)
        {
            return Increment(key, -amount);
        }
        
        /// <summary>
        /// Increments a float payload value. Creates the key with the result if it doesn't exist.
        /// </summary>
        public float IncrementFloat(Tag key, float amount)
        {
            float current = GetPrimary<float>(key, 0f);
            float newValue = current + amount;
            SetPrimary(key, newValue);
            return newValue;
        }
        
        /// <summary>
        /// Decrements a float payload value. Creates the key with the result if it doesn't exist.
        /// </summary>
        public float DecrementFloat(Tag key, float amount)
        {
            return IncrementFloat(key, -amount);
        }

        public bool Toggle(Tag key)
        {
            bool current = GetPrimary<bool>(key);
            SetPrimary(key, !current);
            return !current;
        }
        
        #endregion
        
        #region Process Handling
        public ProcessRelay[] GetRelays()
        {
            return HandlerRelays.Values.ToArray();
        }
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (AbilityDataPacket)handler == this;
        }
        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            return HandlerRelays.ContainsKey(relay.CacheIndex);
        }
        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            HandlerRelays.Add(relay.CacheIndex, relay);
        }
        public bool HandlerVoidProcess(ProcessRelay relay)
        {
            return HandlerRelays.Remove(relay.CacheIndex);
        }
        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono)
        {
            return CustomInstantiator;
        }
        #endregion
        public virtual string GetName()
        {
            return $"Anon-PDP";
        }
        public virtual string GetDescription()
        {
            return $"Anonymous Process Data Packet";
        }
        public virtual Texture2D GetDefaultIcon()
        {
            return null;
        }
    }
    
    public struct DataValue<T> : IEnumerable<T>
    {
        private List<T> Data;
        public bool Valid => Data is not null && Data.Count > 0;
        
        public DataValue(List<T> data)
        {
            Data = data;
        }

        public T Get(EDataTarget target)
        {
            return target switch
            {

                EDataTarget.Primary => Primary,
                EDataTarget.Any => Any,
                EDataTarget.Last => Last,
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            };
        }

        public T Primary => Valid ? Data[0] : default;
        public T Any => Valid ? Data.RandomChoice() : default;
        public List<T> All => Valid ? Data : new List<T>();
        public List<T> AllDistinct => Valid ? All.Distinct().ToList() : new List<T>();
        public T Last => Valid ? Data[^1] : default;

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)Data).GetEnumerator();
        }
        public override string ToString()
        {
            if (!Valid) return "NullProxyData";
            string s = "";
            for (int i = 0; i < Data.Count; i++)
            {
                s += $"{Data[i]}";
                if (i < Data.Count - 1) s += ", ";
            }
            
            return s;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T[] ToArray()
        {
            T[] arr = new T[Data.Count];
            for (int i = 0; i < Data.Count; i++)
            {
                arr[i] = Data[i];
            }

            return arr;
        }
    }

    public enum EDataTarget
    {
        Primary,
        Any,
        Last
    }
}