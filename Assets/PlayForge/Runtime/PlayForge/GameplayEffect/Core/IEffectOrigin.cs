using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Sources of Gameplay Effects
    /// </summary>
    public interface IEffectOrigin : IAffiliated
    {
        public ISource GetOwner();
        public IHasReadableDefinition GetReadableDefinition();
        public List<Tag> GetContextTags();
        public Tag GetAssetTag();
        public IntValuePairClamped GetLevel();
        public bool IsActive();
        
        public static SourceEffectOrigin GenerateSourceDerivation(ISource source)
        {
            return new SourceEffectOrigin(source);
        }

        public static LevelerEffectOrigin GenerateLevelerDerivation(ISource source, IntValuePairClamped level)
        {
            return new LevelerEffectOrigin(source, level);
        }
    }

    public interface IAffiliated
    {
        public List<Tag> GetAffiliation();
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
        public IHasReadableDefinition GetReadableDefinition()
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
        public virtual IntValuePairClamped GetLevel()
        {
            return Owner.GetLevel();
        }
        public List<Tag> GetAffiliation()
        {
            return Owner.GetAffiliation();
        }
        public bool IsActive()
        {
            return true;
        }
    }

    public class LevelerEffectOrigin : SourceEffectOrigin
    {
        public IntValuePairClamped Level;

        public LevelerEffectOrigin(ISource owner, IntValuePairClamped level) : base(owner)
        {
            Level = level;
        }

        public override IntValuePairClamped GetLevel()
        {
            return Level;
        }
    }
}
