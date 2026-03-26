using UnityEngine;

namespace FarEmerald.PlayForge.Examples
{
    public static class DemoAbilitySequences
    {
        [TaskSequenceMethod("Fireball")]
        public static TaskSequence FireballSequence()
        {
            return TaskSequenceBuilder.Create("Fireball!")
                .Task(async (d, t) =>
                {
                    var fireball = DemoSequences.CreatePrim(PrimitiveType.Sphere).Scale(Vector3.one * 3f);
                    d.SetPrimary(Tags.DATA, fireball);
                    
                    var target = d.GetPrimary<ITarget>(Tags.TARGET_REAL);
                    await SequenceTaskLibrary.MoveTo(fireball.transform, target.AsTransform().position, 1f, t);
                    await SequenceTaskLibrary.PunchScale(fireball.transform, 1.6f, .4f, t);
                    await SequenceTaskLibrary.ScaleTo(fireball.transform, 0f, .2f, t);
                })
                .OnTerminate((ctx, success) =>
                {
                    var fireball = ctx.Data.GetPrimary<GameObject>(Tags.DATA);
                    Object.Destroy(fireball.gameObject);
                })
                .BuildSequence();
        }
    }
}
