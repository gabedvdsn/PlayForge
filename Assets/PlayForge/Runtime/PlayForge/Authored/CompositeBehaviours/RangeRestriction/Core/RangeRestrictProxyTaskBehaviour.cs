using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class RangeRestrictProxyTaskBehaviour : AbstractProxyTaskBehaviour
    {
        public static Tag IS_RANGE_RESTRICTED => Tag.Generate("CB_IS_RANGE_RESTRICTED");
        public static Tag Command => Tag.Generate("CB_RANGE_RESTRICTION");
        
        public override UniTask RunAsync(CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        public override EActionStatus End()
        {
            return EActionStatus.NoData;
        }
        public override AbstractProxyTaskBehaviour CreateInstance()
        {
            return new RangeRestrictProxyTaskBehaviour();
        }
    }
}
