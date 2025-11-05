using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        struct ValidationPacket
        {
            public Func<DataContainer, EditorFieldData, ConsoleEntry> ConsolePointer;

            public ValidationPacket(Func<DataContainer, EditorFieldData, ConsoleEntry> consolePointer)
            {
                ConsolePointer = consolePointer;
            }
        }

        /// <summary>
        /// Sets alerts on the
        /// </summary>
        /// <param name="node"></param>
        void ValidateNode(ForgeDataNode node)
        {
            var fields = GetEditableFields(node.GetType());
            foreach (var fi in fields)
            {
                //ValidateField(node, fi);
            }
        }
        
        void ValidateFieldFromCreator(EditorFieldData efd, bool refresh = true)
        {
            ForceResolveConsoleSource(EConsoleContext.Creator, efd);
            
            var validations = efd.Validate();
            
            
            ConsoleEntry severe = null;
            foreach (var packet in validations)
            {
                var ce = packet.ConsolePointer.Invoke(ReservedFocus, efd);
                if (severe is null || (int)ce.code > (int)severe.code) severe = ce;
                
                ce.link = _ => efd.Link(ce, this);
                ce.trace = flag => efd.Trace(ce, this, flag);
                
                LogConsoleEntry(ce, refresh: false);
            }
            
            UpdateFieldIndicator(efd, severe?.code ?? EValidationCode.Ok, severe?.message);
            if (refresh) RefreshConsole();
        }

        void ValidateFrameworkAsync()
        {
            SetNavigationPermit(EForgeContextExpanded.All, false, "All activity is suspended while the framework is being validated.");

            DoTaskProcess(EProgressLock.All, "Validating Framework",
                DoPerformCompleteValidation,
                onFinish: () => SetNavigationPermit(EForgeContextExpanded.All, true),
                runout: true, runoutTitle: "Validation Complete");
        }
        
        private const int updateCheckInFrames = 32;
        
        async UniTask DoPerformCompleteValidation(Action<float> onProgress)
        {
            if (Project is null) return;

            var data = Project.GetCompleteNodes();
            var items = data.Values.SelectMany(i => i).ToArray();
            
            int total = Project.DataCount;
            int completed = 0;

            var progress = Progress<float>.Create(onProgress);

            int _maxConc = maxConcurrencyThreads < 1 ? Environment.ProcessorCount - 1 : maxConcurrencyThreads;
            var queues = Enumerable.Range(0, _maxConc)
                .Select(_ => new Queue<ForgeDataNode>())
                .ToArray();
            
            for (int i = 0; i < items.Length; i++)
            {
                queues[i % _maxConc].Enqueue(items[i]);
            }

            var workers = queues.Select(q =>
            {
                return UniTask.RunOnThreadPool(async () =>
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        ForgeDataNode node;
                        lock (q)
                        {
                            if (q.Count == 0) break;
                            node = q.Dequeue();
                        }

                        await UniTask.SwitchToMainThread(token);
                        ValidateNode(node);
                        await UniTask.SwitchToThreadPool();

                        int done = Interlocked.Increment(ref completed);
                        if (done % updateCheckInFrames == 0 || (done & 31) == 0 || done == total)
                        {
                            await UniTask.SwitchToMainThread(token);
                            progress.Report(done / (float)total);
                            await UniTask.SwitchToThreadPool();
                        }

                    }
                }, false, token);
            }).ToArray();

            await UniTask.WhenAll(workers);

            await UniTask.SwitchToMainThread(token);
            progress.Report(1f);
        }
        
        private static Dictionary<EValidationCode, List<(IConsoleMessenger source, string focus, string msg, string descr)>> CombineValidationFuncs(
            object value,
            params Func<object, List<(EValidationCode code, (IConsoleMessenger source, string focus, string msg, string descr) detail)>>[] funcs)
        {
            var results = new Dictionary<EValidationCode, List<(IConsoleMessenger source, string focus, string msg, string descr)>>();
            foreach (var f in funcs)
            {
                var alerts = f?.Invoke(value);
                if (alerts is null) continue;

                foreach (var alert in alerts)
                {
                    results.TryAdd(alert.code, new List<(IConsoleMessenger source, string focus, string msg, string descr)>());
                    results[alert.code].Add(alert.detail);
                }
            }

            return results;
        }

        public static EValidationCode[] CodesInOrder(params EValidationCode[] codes)
        {
            return codes
                .GroupBy(c => (int)c)
                .Select(c => c.First())
                .OrderByDescending(c => (int)c)
                .ToArray();
        }

        public static EValidationCode MostSevere(params EValidationCode[] codes)
        {
            return codes.Max(c => c);
        }
    }
}
