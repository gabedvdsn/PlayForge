using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    public class Hero : Character
    {
        public override void WhenUpdate()
        {
            base.WhenUpdate();
            
            foreach (var level in LevelSystem.GetAllLevels())
            {
                // Debug.Log($"[{GetName()}-{level.Key}] {level.Value}");
            }
        }
    }
}
