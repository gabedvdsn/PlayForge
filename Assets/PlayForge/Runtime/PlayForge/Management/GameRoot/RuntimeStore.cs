using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using Unity.VisualScripting.Dependencies.Sqlite;

namespace FarEmerald.PlayForge
{
    public partial class GameRoot
    {
        private FrameworkProject Fp;

        private static Dictionary<int, AttributeData> attributes;
        private static Dictionary<int, TagData> tags;

        private static Dictionary<int, AbilityData> abilities;
        private static Dictionary<int, AttributeSetData> attributeSets;

        private static Dictionary<int, EntityData> entities;
        private static Dictionary<Tag, int> entityMap;

        private static Dictionary<int, EffectData> effects;
        private static Dictionary<int, GameplayEffect> effectMap;

        public static void SetFramework(FrameworkProject fp)
        {
            Instance.Fp = fp;
            Instance.Prepare();
        }

        void Prepare()
        {
            attributes = Fp?.Attributes.ToDictionary(d => d.Id) ?? new Dictionary<int, AttributeData>();
        }
    }
}
