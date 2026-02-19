using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge;
using Unity.VisualScripting;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class BaseForgeLinkProvider : BaseForgeObject, ILevelProvider
    {
        public virtual int GetMaxLevel() { return 0; }
        public virtual int GetStartingLevel() { return 0; }
        public virtual string GetProviderName() { return string.Empty; }
        public virtual Tag GetProviderTag() { return Tags.NONE; }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield break;
        }

        public abstract bool LinkToProvider(BaseForgeLinkProvider provider);
        public abstract bool IsLinkedTo(ScriptableObject provider);
        public abstract bool IsLinked { get; }
        public abstract BaseForgeLinkProvider LinkedProvider { get; set; }

        /// <summary>
        /// Checks if can link src to linkTo by analysing for a circular dependency.
        /// Issue indicates
        /// </summary>
        /// <param name="linkTo"></param>
        /// <param name="issue"></param>
        /// <param name="chain"></param>
        /// <returns></returns>
        public bool HasCircularDependency(BaseForgeLinkProvider linkTo, out BaseForgeLinkProvider issue, out string chain)
        {
            issue = null;
            chain = "";
            
            // Cannot link to self
            if (this == linkTo)
            {
                chain += $"[{GetProviderName()}] (\u2192) [{GetProviderName()}]";
                issue = this;
                return true;
            }
            
            if (!linkTo.IsLinked) return false;

            System.Collections.Generic.List<string> _chain = new System.Collections.Generic.List<string>() { $"(\u2192) [{GetProviderName()}]" };
            var result = CheckRecursive(linkTo, out issue);
            _chain.Add($"[{GetProviderName()}] ");
            
            _chain.Reverse();
            foreach (var c in _chain)
            {
                chain += c;
            }
            
            // chain += $"{_chain[0]}";
            return result;
            
            bool CheckRecursive(BaseForgeLinkProvider curr, out BaseForgeLinkProvider _issue)
            {
                if (curr.IsLinked)
                {
                    _chain.Add($"\u2192 {curr.GetProviderName()} ");
                    
                    if (!curr.IsLinkedTo(this))
                    {
                        return CheckRecursive(curr.LinkedProvider, out _issue);
                    }
                    
                    _issue = curr;
                    return true;
                }

                _issue = null;
                return false;
            }
        }
    }
}
