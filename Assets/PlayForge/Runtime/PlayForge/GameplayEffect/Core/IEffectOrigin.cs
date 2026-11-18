using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Sources of Gameplay Effects
    /// </summary>
    public interface IEffectOrigin : IAffiliated
    {
        public ISource GetSource();
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

    /// <summary>
    /// What has sides: <c>GetAffiliation</c> gives its affiliation tags, as an entity's team or faction. Every target
    /// and every effect origin has them, and an effect's Affiliation compares its origin's with its target's.
    /// </summary>
    public interface IAffiliated
    {
        public List<Tag> GetAffiliation();
    }
    
    /// <summary>
    /// An origin that stands for an entity itself, for an effect the entity applies with no ability or item behind it:
    /// its source, definition, context tags, asset tag, level and sides are the entity's, and it's always active.
    /// <c>IEffectOrigin.GenerateSourceDerivation</c> makes one, as in
    /// <c>gas.ApplyGameplayEffect(IEffectOrigin.GenerateSourceDerivation(gas), effect)</c>.
    /// </summary>
    public class SourceEffectOrigin : IEffectOrigin
    {
        private ISource Owner;

        public SourceEffectOrigin(ISource owner)
        {
            Owner = owner;
        }

        public ISource GetSource()
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

    /// <summary>
    /// An origin that stands for an entity at a level of its own: a <c>SourceEffectOrigin</c> whose <c>GetLevel</c>
    /// gives the <c>Level</c> it holds, not the entity's. A leveler carrier reads its numbers through one, and a save
    /// puts one back where an effect's origin was one; <c>IEffectOrigin.GenerateLevelerDerivation</c> makes one.
    /// </summary>
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
