using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class TestMultiProcess : LazyMonoProcess
    {
        private void Start()
        {
            ProcessControl.Register(this, out _);
        }
    }
}
