using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A console message ForgeLog wrote opens at the code that logged it: a double-click on it in the Console, which would
    /// open ForgeLog's own call to Unity's log, opens the first frame of its stack that isn't ForgeLog's, nor a method
    /// marked <see cref="HideInCallstackAttribute"/>, as a game's own logging wrapper may be. ForgeLog's methods are
    /// marked so too, which hides them from the stack with the Console's Strip Logging Callstack. Where the Console's
    /// message can't be read, it opens as Unity opens it.
    /// </summary>
    internal static class ForgeLogConsoleLink
    {
        // A frame of a Console stack with a source: "Namespace.Type:Method (arguments) (at Path/File.cs:42)"
        private static readonly Regex Frame = new(@"^(?<method>[^\s(]+)\s*\(.*\)\s*\(at (?<path>[^:)]+):(?<line>\d+)\)\s*$",
            RegexOptions.Compiled);

        private const string ForgeLogFrame = "FarEmerald.PlayForge.ForgeLog:";

        // The methods frames named, by their frames' names: each looked up once, and those marked to hide
        private static readonly HashSet<string> LookedUp = new();
        private static readonly HashSet<string> Hidden = new();

        [OnOpenAsset(0)]
        private static bool OnOpen(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not MonoScript script || script.GetClass() != typeof(ForgeLog))
                return false;
            var stack = SelectedConsoleText();
            if (stack == null || !TryCallerOf(stack, out var path, out int at)) return false;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            return asset != null && AssetDatabase.OpenAsset(asset, at);
        }

        /// <summary>
        /// The file and line of the first frame of a Console stack that isn't ForgeLog's, nor a method marked to hide, where
        /// the stack passes through ForgeLog; false for another stack, which opens as Unity opens it.
        /// </summary>
        internal static bool TryCallerOf(string stack, out string path, out int line)
        {
            path = null;
            line = 0;
            if (string.IsNullOrEmpty(stack)) return false;
            bool throughForgeLog = false;
            foreach (var raw in stack.Split('\n'))
            {
                var frame = Frame.Match(raw.Trim());
                if (!frame.Success) continue;
                var method = frame.Groups["method"].Value;
                if (method.StartsWith(ForgeLogFrame, StringComparison.Ordinal))
                {
                    throughForgeLog = true;
                    continue;
                }
                if (IsHidden(method)) continue;
                if (!throughForgeLog) return false;
                path = frame.Groups["path"].Value;
                line = int.Parse(frame.Groups["line"].Value);
                return true;
            }
            return false;
        }

        // Whether the method a frame names, "Namespace.Outer/Inner:Method", is marked to hide in the Console's stack
        private static bool IsHidden(string method)
        {
            if (LookedUp.Contains(method)) return Hidden.Contains(method);
            LookedUp.Add(method);
            bool hidden = false;
            int colon = method.LastIndexOf(':');
            if (colon > 0)
            {
                var typeName = method.Substring(0, colon).Replace('/', '+');
                var name = method.Substring(colon + 1);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(typeName, false);
                    if (type == null) continue;
                    foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                                               BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        if (candidate.Name == name && candidate.IsDefined(typeof(HideInCallstackAttribute), false))
                            hidden = true;
                    break;
                }
            }
            if (hidden) Hidden.Add(method);
            return hidden;
        }

        // The Console's selected message, with its stack, where the Console asked to open it; null otherwise, or where
        // this Unity's Console can't be read
        private static string SelectedConsoleText()
        {
            try
            {
                var console = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
                var window = console?.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as EditorWindow;
                if (window == null || EditorWindow.focusedWindow != window) return null;
                return console.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
