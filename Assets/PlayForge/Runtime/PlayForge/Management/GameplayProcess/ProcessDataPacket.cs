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
    public class ProcessDataPacket : IValidationReady
    {
        protected Dictionary<Tag, List<object>> _payload = new();
        public IReadOnlyDictionary<Tag, List<object>> Payload => _payload;
        
        public EActionStatus Status { get; protected set; }
        public bool InUse = true;
        
        public IGameplayProcessHandler Handler;

        protected ProcessDataPacket()
        {
            Handler = GameRoot.Instance;
        }

        private ProcessDataPacket(IGameplayProcessHandler handler)
        {
            Handler = handler;
        }

        public void SetStatus(EActionStatus status)
        {
            Status = status;
        }

        public void ResetStatus(EActionStatus status = EActionStatus.Pending) => Status = status;

        #region Construction

        public static ProcessDataPacket RootDefault()
        {
            var data = new ProcessDataPacket();
            data.AddPayload(Tags.PARENT_TRANSFORM, GameRoot.Instance.transform);
            return data;
        }

        public static ProcessDataPacket RootDefault(IGameplayProcessHandler handler)
        {
            var data = new ProcessDataPacket(handler);
            data.AddPayload(Tags.PARENT_TRANSFORM, GameRoot.Instance.transform);
            return data;
        }
        
        /// <summary>
        /// Returns a new data packet where GameRoot is the assigned Transform IF GameRoot is not within the parental hierarchy
        /// </summary>
        /// <returns></returns>
        public static ProcessDataPacket RootLocal(MonoBehaviour obj)
        {
            var data = new ProcessDataPacket();
            
            if (obj.GetComponentInParent<GameRoot>()) return data;
            data.AddPayload(Tags.PARENT_TRANSFORM, GameRoot.Instance.transform);
            return data;
        }

        public static ProcessDataPacket RootLocal(MonoBehaviour obj, IGameplayProcessHandler handler)
        {
            var data = new ProcessDataPacket(handler);
            
            if (obj.GetComponentInParent<GameRoot>()) return data;
            data.AddPayload(Tags.PARENT_TRANSFORM, GameRoot.Instance.transform);
            return data;
        }

        public static ProcessDataPacket LocalDefault(MonoBehaviour obj)
        {
            var data = new ProcessDataPacket();
            
            data.AddPayload(Tags.POSITION, obj.transform.position);
            data.AddPayload(Tags.ROTATION, obj.transform.rotation);
            data.AddPayload(Tags.PARENT_TRANSFORM, obj.transform.parent);

            return data;
        }
        
        public static ProcessDataPacket LocalDefault(MonoBehaviour obj, IGameplayProcessHandler handler)
        {
            var data = new ProcessDataPacket(handler);
            
            data.AddPayload(Tags.POSITION, obj.transform.position);
            data.AddPayload(Tags.ROTATION, obj.transform.rotation);
            data.AddPayload(Tags.PARENT_TRANSFORM, obj.transform.parent);

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

        public T Get<T>(Tag key, EProxyDataValueTarget target, T fallback = default)
        {
            return TryGet<T>(key, target, out var value) ? value : fallback;
        }
        
        public bool TryGet<T>(Tag key, EProxyDataValueTarget target, out T value)
        {
            value = default;
            
            if (!_payload.ContainsKey(key))
            {
                return false;
            }

            object o = target switch
            {
                EProxyDataValueTarget.Primary => _payload[key][0],
                EProxyDataValueTarget.Any => _payload[key].RandomChoice(),
                EProxyDataValueTarget.Last => _payload[key][^1],
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
            return true;
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
    }
    
    public struct DataValue<T> : IEnumerable<T>
    {
        private List<T> Data;
        public bool Valid => Data is not null && Data.Count > 0;
        
        public DataValue(List<T> data)
        {
            Data = data;
        }

        public T Get(EProxyDataValueTarget target)
        {
            return target switch
            {

                EProxyDataValueTarget.Primary => Primary,
                EProxyDataValueTarget.Any => Any,
                EProxyDataValueTarget.Last => Last,
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

    public enum EProxyDataValueTarget
    {
        Primary,
        Any,
        Last
    }
}
