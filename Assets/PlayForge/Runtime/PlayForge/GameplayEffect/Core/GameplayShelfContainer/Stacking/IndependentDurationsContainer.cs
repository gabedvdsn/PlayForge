using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    public class IndependentDurationsContainer : AbstractStackingEffectContainer
    {
        protected IndependentDurationsContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
            Packets = new List<StackableContainerPacket>();
        }

        public static IndependentDurationsContainer Generate(GameplayEffectSpec spec, bool ongoing)
        {
            return new IndependentDurationsContainer(spec, ongoing);
        }

        private List<StackableContainerPacket> Packets;
        
        public override float DurationRemaining => Packets.Count > 0 ? Packets.Sum(p => p.DurationRemaining) : float.MaxValue;
        public override float NextDurationRemaining => Packets.Count > 0 ? Packets[0].DurationRemaining : float.MaxValue;
        public override float TimeUntilPeriodTick => Packets.Count > 0 ? Packets[0].TimeUntilPeriodTick : float.MaxValue;

        public override int InstantExecuteTicks => 1;
        
        private struct StackableContainerPacket
        {
            public float DurationRemaining;
            public float TimeUntilPeriodTick;

            public StackableContainerPacket(float totalDuration, float periodDuration)
            {
                DurationRemaining = totalDuration;
                TimeUntilPeriodTick = periodDuration;
            }

            public void UpdateTimeRemaining(float deltaTime)
            {
                DurationRemaining -= deltaTime;
            }

            public void TickPeriodic(float deltaTime, float periodDuration, out bool executeTick)
            {
                TimeUntilPeriodTick -= deltaTime;
                if (TimeUntilPeriodTick <= 0f)
                {
                    TimeUntilPeriodTick += periodDuration;
                    executeTick = true;
                }
                else
                {
                    executeTick = false;
                }
            }

            public void Refresh(float totalDuration, float periodDuration)
            {
                DurationRemaining = totalDuration;
                TimeUntilPeriodTick = periodDuration;
            }

            public void Extend(float duration, float extendDuration) => DurationRemaining = duration + extendDuration;
        }
        
        public override void SetDurationRemaining(float duration)
        {
            for (int i = 0; i < Packets.Count; i++)
            {
                if (Packets[i].DurationRemaining > duration) Packets[i] = new StackableContainerPacket(duration, periodDuration);
            }
        }
        
        public override void SetTimeUntilPeriodTick(float duration)
        {
            for (int i = 0; i < Packets.Count; i++)
            {
                if (Packets[i].TimeUntilPeriodTick > duration) Packets[i] = new StackableContainerPacket(totalDuration, duration);
            }
        }
        
        public override void UpdateTimeRemaining(float deltaTime)
        {
            int removeIndex = -1;
            foreach (StackableContainerPacket packet in Packets)
            {
                packet.UpdateTimeRemaining(deltaTime);
                if (packet.DurationRemaining <= 0f) removeIndex += 1;
            }
            
            if (removeIndex >= 0) RemoveStacks(removeIndex);
        }

        private void RemoveStacks(int rangeIndex)
        {
            for (int i = 0; i <= rangeIndex; i++) Packets.RemoveAt(i);
            Stacks -= rangeIndex + 1;
        }
        
        public override void TickPeriodic(float deltaTime, out int executeTicks)
        {
            executeTicks = 0;
            foreach (var packet in Packets)
            {
                packet.TickPeriodic(deltaTime, periodDuration, out bool execute);
                if (execute) executeTicks += 1;
            }
        }
        public override void Refresh()
        {
            Packets[0].Refresh(totalDuration, periodDuration);
        }
        public override void Extend(float duration)
        {
            Stack(1);
        }
        public override void Stack(int amount)
        {
            for (int _ = 0; _ < amount; _++)
            {
                Packets.Add(new StackableContainerPacket(totalDuration, periodDuration));
                Stacks += 1;
            }
        }
    }
}
