using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{

    [Serializable]
    public class DataEntry
    {
        public int Id;
        public string Name;
        public EDataType Type;
    }
    
    public class FrameworkIndex : ScriptableObject
    {
        [HideInInspector] public string FrameworkKey;
        [HideInInspector] public bool IsUpToDate = true;

        [HideInInspector] public ForgeJsonUtility.SettingsWrapper Settings;
        
        [HideInInspector] public List<DataEntry> Attributes = new();
        [HideInInspector] public List<DataEntry> AttributeSets = new();
        [HideInInspector] public List<DataEntry> Tags = new();
        [HideInInspector] public List<DataEntry> Entities = new();
        [HideInInspector] public List<DataEntry> Abilities = new();
        [HideInInspector] public List<DataEntry> Effects = new();
    }
}
