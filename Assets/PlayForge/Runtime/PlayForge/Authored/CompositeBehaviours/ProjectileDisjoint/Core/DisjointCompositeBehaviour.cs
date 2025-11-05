
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointCompositeBehaviour : AbstractCompositeBehaviour
    {
        public static Tag IS_DISJOINTABLE => Tag.Generate("CB_IS_DISJOINTABLE");
        public static Tag Command => Tag.Generate("CB_DISJOINT");
        
        
        public override UniTask Run(CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        
        public override EActionStatus End()
        {
            return EActionStatus.NoOp;
        }
        
        public override AbstractCompositeBehaviour CreateInstance()
        {
            return new DisjointCompositeBehaviour();
        }
        
    }
}
