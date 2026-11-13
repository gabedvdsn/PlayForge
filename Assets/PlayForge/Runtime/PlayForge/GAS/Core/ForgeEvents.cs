namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Sends a gameplay event to an entity as the core sends its own (TW-5, TW-12): only where an ability of the entity
    /// listens for it, queued for its frame's end where it has a queue, its data the source and the subject given.
    /// </summary>
    public static class ForgeEvents
    {
        /// <summary>Sends <paramref name="tag"/> to <paramref name="to"/>, with <paramref name="source"/> as Source and <paramref name="subject"/> as Subject.</summary>
        public static void Send(IGameplayAbilitySystem to, Tag tag, ITarget source, ITarget subject)
        {
            var abilities = to?.GetAbilitySystem();
            if (abilities == null || !tag.IsValid || !abilities.ListensFor(tag)) return;
            var data = ProcessDataPacket.Default();
            if (source != null) data.SetPrimary(Tags.SOURCE, source);
            if (subject != null) data.SetPrimary(Tags.EVENT_SUBJECT, subject);
            var queue = to.GetActionQueue();
            if (queue != null) queue.Enqueue(new LambdaAction(_ => abilities.SendGameplayEvent(tag, data), $"Send {tag}"));
            else abilities.SendGameplayEvent(tag, data);
        }
    }
}
