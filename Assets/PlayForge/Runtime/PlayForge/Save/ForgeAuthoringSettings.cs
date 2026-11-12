using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Where a type's new assets go, and how their files are named.</summary>
    [Serializable]
    public sealed class NewAssetSettings
    {
        [Tooltip("The asset type, by its class name")]
        public string Type;
        [Tooltip("The folder a new asset of this type goes in, within the project's folder")]
        public string Folder;
        [Tooltip("Text a new asset's file name starts with")]
        public string Prefix;
        [Tooltip("Text a new asset's file name ends with")]
        public string Suffix;
    }

    /// <summary>A type's defaults: the asset a new one starts as a copy of, and the assets Import lists first.</summary>
    [Serializable]
    public sealed class TypeDefaults
    {
        [Tooltip("The asset type, by its class name")]
        public string Type;
        [Tooltip("The asset a new asset of this type starts as a copy of")]
        public ScriptableObject NewAsset;
        [Tooltip("The assets Import lists first for this type")]
        public List<ScriptableObject> Import = new();
    }

    /// <summary>
    /// How The Forge makes a Forge Project's assets (TW-30, Decision 57): where each type's new assets go and how their
    /// files are named, each type's defaults, and the effects a new Cost or Cooldown copies. They were the whole Unity
    /// project's, in ProjectSettings/PlayForgeSettings.asset; each game keeps its own now. A game's build ignores them.
    /// </summary>
    [Serializable]
    public sealed class ForgeAuthoringSettings
    {
        [Tooltip("Where each type's new assets go, within the project's folder, and how their files are named")]
        public List<NewAssetSettings> NewAssets = new();

        [Tooltip("Each type's defaults: the asset a new one starts as a copy of, and the assets Import lists first")]
        public List<TypeDefaults> Defaults = new();

        [Tooltip("The effect a new Cost effect starts as a copy of")]
        public GameplayEffect CostEffect;

        [Tooltip("The effect a new Cooldown effect starts as a copy of")]
        public GameplayEffect CooldownEffect;

        [Tooltip("Assets of the project's own that a new asset can start as a copy of, listed in The Forge's Create menu " +
                 "under their type, after PlayForge's presets (TW-24)")]
        public List<UnityEngine.ScriptableObject> Templates = new();
    }
}
