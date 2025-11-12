
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class DisjointProxyTaskBehaviour : AbstractProxyTaskBehaviour
    {
        public static Tag IS_DISJOINTABLE => Tag.Generate("CB_IS_DISJOINTABLE");
        public static Tag Command => Tag.Generate("CB_DISJOINT");
        
        public override UniTask RunAsync(CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        
        public override EActionStatus End()
        {
            return EActionStatus.NoOp;
        }
        
        public override AbstractProxyTaskBehaviour CreateInstance()
        {
            return new DisjointProxyTaskBehaviour();
        }
        
    }
}
