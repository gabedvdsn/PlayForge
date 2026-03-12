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
        public float GetRelativeLevel();
        public string GetName();
        public List<Tag> GetAffiliation();
        public bool IsActive();
        
        public static SourceEffectOrigin GenerateSourceDerivation(ISource source)
        {
            return new SourceEffectOrigin(source);
        }

        public static LevelerEffectOrigin GenerateLevelerDerivation(ISource source, int level, int maxLevel)
        {
            return new LevelerEffectOrigin(source, level, maxLevel);
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
        public virtual int GetLevel()
        {
            return Owner.GetLevel();
        }
        public void SetLevel(int level)
        {
            Owner.SetLevel(level);
        }
        public virtual float GetRelativeLevel()
        {
            return ForgeHelper.RelativeOffsetValue(Owner.GetLevel(), Owner.GetMaxLevel());
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

    public class LevelerEffectOrigin : SourceEffectOrigin
    {
        public int Level;
        public int MaxLevel;

        public LevelerEffectOrigin(ISource owner, int level, int maxLevel) : base(owner)
        {
            Level = level;
            MaxLevel = maxLevel;
        }

        public override int GetLevel()
        {
            return Level;
        }
        public override float GetRelativeLevel()
        {
            return ForgeHelper.RelativeOffsetValue(Level, MaxLevel);
        }

    }
}
