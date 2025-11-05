using System;
using UnityEngine;

namespace FarEmerald
{
    [Serializable]
    public struct AssetRef<T> where T : UnityEngine.Object
    {
        [SerializeField] private string address;
        #if UNITY_EDITOR
        [SerializeField] private T editorObject;

        public void EditorSet(T obj, Func<T, string> toAddress)
        {
            editorObject = obj;
            address = toAddress?.Invoke(obj);
        }
        #endif

        public string Address => address;
        public bool IsEmpty => string.IsNullOrEmpty(address);
        public override string ToString() => address ?? "<None>";
    }
}
