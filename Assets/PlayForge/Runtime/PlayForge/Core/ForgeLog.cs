using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Level-gated logging for PlayForge: all the runtime says goes through here, and this file alone writes to Unity's
    /// console, so a project sets how much it hears in one place: Preferences → PlayForge → Logging in the editor, kept
    /// and put back on a domain reload, or <see cref="MinimumLevel"/> from code.
    ///
    /// <para>Each message is of a system, effects, abilities, workers, numbers, processes, sequences and the rest, and a
    /// system can take a level of its own, <see cref="SetLevel"/>, as the Creator Lab's log does; <see cref="Logged"/>
    /// hears each message let through, with the entity it's about where it names one.</para>
    ///
    /// <para>What's said only when asked, a debug tool's output (TagCache.LogWeights, the Threshold Debugger worker,
    /// LogAction), ProcessControl's logs while its inspector's toggles are on, and a sequence's injection or chain
    /// logging, is information: the toggle decides whether it's made, and the level whether it's shown.</para>
    ///
    /// <para>Its methods are marked to hide in the Console's stack, as the Console's Strip Logging Callstack hides them, and
    /// a double-click on a message it wrote opens the code that logged it, not this file.</para>
    /// </summary>
    public static class ForgeLog
    {
        public enum ELevel
        {
            /// <summary>Chatty lifecycle traces (sequence chain progress, registrations).</summary>
            Verbose = 0,
            /// <summary>Notable one-off events.</summary>
            Info = 1,
            Warning = 2,
            Error = 3,
            /// <summary>Silence everything, including framework errors.</summary>
            None = 4,
        }

        /// <summary>The part of PlayForge a message is of, each able to take a level of its own.</summary>
        public enum ESystem
        {
            General,
            Effects,
            Abilities,
            Attributes,
            Workers,
            Numbers,
            Processes,
            Tags,
            Items,
            Saves,
            Input,
            Lab,
            Sequences,
        }

        /// <summary>Messages below this level are dropped, of a system with no level of its own. Default: Info.</summary>
        public static ELevel MinimumLevel = ELevel.Info;

        private const string Prefix = "[PlayForge] ";

        private static readonly ELevel?[] Levels = new ELevel?[Enum.GetValues(typeof(ESystem)).Length];

        /// <summary>
        /// Raised for each message let through, as it goes to Unity's console, with its system and the entity it's about
        /// where it names one: the Creator Lab's log hears it.
        /// </summary>
        public static event Action<ForgeLogEntry> Logged;

        /// <summary>A system's level: its own, or <see cref="MinimumLevel"/> where it has none.</summary>
        public static ELevel LevelOf(ESystem system) => Levels[(int)system] ?? MinimumLevel;

        /// <summary>Whether a system has a level of its own.</summary>
        public static bool HasOwnLevel(ESystem system) => Levels[(int)system].HasValue;

        /// <summary>Gives a system a level of its own; null gives it back to <see cref="MinimumLevel"/>.</summary>
        public static void SetLevel(ESystem system, ELevel? level) => Levels[(int)system] = level;

        /// <summary>Gives every system back to <see cref="MinimumLevel"/>.</summary>
        public static void ClearLevels() => Array.Clear(Levels, 0, Levels.Length);

        /// <summary>Whether a message of a level, of a system, is let through.</summary>
        public static bool Allows(ESystem system, ELevel level) => level < ELevel.None && level >= LevelOf(system);

        [HideInCallstack]
        public static void Verbose(string message, Object context = null) => Write(ESystem.General, ELevel.Verbose, message, context, null);

        [HideInCallstack]
        public static void Info(string message, Object context = null) => Write(ESystem.General, ELevel.Info, message, context, null);

        [HideInCallstack]
        public static void Warning(string message, Object context = null) => Write(ESystem.General, ELevel.Warning, message, context, null);

        [HideInCallstack]
        public static void Error(string message, Object context = null) => Write(ESystem.General, ELevel.Error, message, context, null);

        /// <summary>A message of a system, about an entity where it names one, which a log filtered to it shows.</summary>
        [HideInCallstack]
        public static void Verbose(ESystem system, string message, Object context = null, object subject = null) =>
            Write(system, ELevel.Verbose, message, context, subject);

        /// <inheritdoc cref="Verbose(ESystem, string, Object, object)"/>
        [HideInCallstack]
        public static void Info(ESystem system, string message, Object context = null, object subject = null) =>
            Write(system, ELevel.Info, message, context, subject);

        /// <inheritdoc cref="Verbose(ESystem, string, Object, object)"/>
        [HideInCallstack]
        public static void Warning(ESystem system, string message, Object context = null, object subject = null) =>
            Write(system, ELevel.Warning, message, context, subject);

        /// <inheritdoc cref="Verbose(ESystem, string, Object, object)"/>
        [HideInCallstack]
        public static void Error(ESystem system, string message, Object context = null, object subject = null) =>
            Write(system, ELevel.Error, message, context, subject);

        /// <summary>
        /// An exception of a system, as an error, where an error is let through: the console shows it as Unity's own
        /// LogException does, with its stack, and <see cref="Logged"/> hears its type and message.
        /// </summary>
        [HideInCallstack]
        public static void Exception(ESystem system, System.Exception exception, Object context = null, object subject = null)
        {
            if (exception == null || !Allows(system, ELevel.Error)) return;
            Debug.LogException(exception, context);
            Logged?.Invoke(new ForgeLogEntry(ELevel.Error, system, $"{exception.GetType().Name}: {exception.Message}", context,
                subject, ForgeTime.Time, ForgeTime.FrameCount));
        }

        [HideInCallstack]
        private static void Write(ESystem system, ELevel level, string message, Object context, object subject)
        {
            if (!Allows(system, level)) return;
            switch (level)
            {
                case ELevel.Warning:
                    Debug.LogWarning(Prefix + message, context);
                    break;
                case ELevel.Error:
                    Debug.LogError(Prefix + message, context);
                    break;
                default:
                    Debug.Log(Prefix + message, context);
                    break;
            }
            Logged?.Invoke(new ForgeLogEntry(level, system, message, context, subject, ForgeTime.Time, ForgeTime.FrameCount));
        }
    }

    /// <summary>One message ForgeLog let through: its level, its system, the message, and whom it's about.</summary>
    public readonly struct ForgeLogEntry
    {
        public readonly ForgeLog.ELevel Level;
        public readonly ForgeLog.ESystem System;
        public readonly string Message;

        /// <summary>The object the message points the console at, as an asset.</summary>
        public readonly Object Context;

        /// <summary>The entity it's about, where it names one: a GAS hub, or another object of its own.</summary>
        public readonly object Subject;

        /// <summary>The game's time and frame as it was logged.</summary>
        public readonly float Time;
        public readonly int Frame;

        public ForgeLogEntry(ForgeLog.ELevel level, ForgeLog.ESystem system, string message, Object context, object subject,
            float time, int frame)
        {
            Level = level;
            System = system;
            Message = message;
            Context = context;
            Subject = subject;
            Time = time;
            Frame = frame;
        }

        /// <summary>The message in a line: "Effects, warning: Burn was applied again inside its own application …".</summary>
        public override string ToString() => $"{System}, {Level.ToString().ToLowerInvariant()}: {Message}";
    }
}
