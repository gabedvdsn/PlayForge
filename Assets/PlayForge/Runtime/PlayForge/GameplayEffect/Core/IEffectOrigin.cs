using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Sources of Gameplay Effects
    /// </summary>
    public interface IEffectOrigin
    {
        public ISource GetOwner();
        public List<Tag> GetContextTags();
        public Tag GetAssetTag();
        public int GetLevel();
        public void SetLevel(int level);
        public float GetRelativeLevel();
        public string GetName();
        public List<Tag> GetAffiliation();
        public bool IsActive();
        public static SourceEffectOrigin GenerateSourceDerivation(ISource source)
        {
            return new SourceEffectOrigin(source);
        }
    }
    
    public class SourceEffectOrigin : IEffectOrigin
    {
        private ISource Owner;

        public SourceEffectOrigin(ISource owner)
        {
            Owner = owner;
        }

        public ISource GetOwner()
        {
            return Owner;
        }
        public List<Tag> GetContextTags()
        {
            return Owner.GetContextTags();
        }
        public Tag GetAssetTag()
        {
            return Owner.GetAssetTag();
        }
        public int GetLevel()
        {
            return Owner.GetLevel();
        }
        public void SetLevel(int level)
        {
            Owner.SetLevel(level);
        }
        public float GetRelativeLevel()
        {
            float maxLevel = Owner.GetMaxLevel();
            return maxLevel > 1 ? (Owner.GetLevel() - 1) / (maxLevel - 1) : 1f;
        }
        public string GetName()
        {
            return Owner.GetName();
        }
        public List<Tag> GetAffiliation()
        {
            return Owner.GetAffiliation();
        }
        public bool IsActive()
        {
            return true;
        }
        public bool RetainEffectImpact()
        {
            return true;
        }
    }
}
