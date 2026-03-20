using System;
using UnityEngine;

namespace FarEmerald.PlayForge.Examples
{
    public class TestWhyBrok : MonoBehaviour
    {
        private void OnDestroy()
        {
            Debug.Log($"I AM DEAD NOW! {gameObject.name}");
        }
    }
}
