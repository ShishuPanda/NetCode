using System;

namespace TekkenNetcodeDemo.Rollback
{
    /// <summary>
    /// Bitmask representation of player input for a single frame.
    /// Compact (1 byte) for minimal serialization and lightning-fast comparisons.
    /// </summary>
    [Flags]
    public enum InputButtons : byte
    {
        None       = 0,
        Left       = 1 << 0,
        Right      = 1 << 1,
        Up         = 1 << 2, // Jump
        Down       = 1 << 3, // Crouch
        LightPunch = 1 << 4, // J key
        HeavyKick  = 1 << 5, // K key
        Guard      = 1 << 6  // L key / Back hold
    }

    /// <summary>
    /// Immutable struct representing frame input.
    /// </summary>
    public struct RollbackInput : IEquatable<RollbackInput>
    {
        public uint Frame;
        public InputButtons Buttons;

        public RollbackInput(uint frame, InputButtons buttons)
        {
            Frame = frame;
            Buttons = buttons;
        }

        public bool IsSet(InputButtons button) => (Buttons & button) != 0;

        public bool Equals(RollbackInput other) => Buttons == other.Buttons;

        public override bool Equals(object obj) => obj is RollbackInput other && Equals(other);

        public override int GetHashCode() => (int)Buttons;

        public static bool operator ==(RollbackInput a, RollbackInput b) => a.Buttons == b.Buttons;
        public static bool operator !=(RollbackInput a, RollbackInput b) => a.Buttons != b.Buttons;

        public override string ToString()
        {
            if (Buttons == InputButtons.None) return "Neutral";
            return Buttons.ToString();
        }

        /// <summary>
        /// Formats active buttons with fighting game symbols for on-screen HUD telemetry.
        /// </summary>
        public string ToFormattedSymbols()
        {
            if (Buttons == InputButtons.None) return "⚪ Neutral";
            var sb = new System.Text.StringBuilder();
            if (IsSet(InputButtons.Left)) sb.Append("⬅️ [A] ");
            if (IsSet(InputButtons.Right)) sb.Append("➡️ [D] ");
            if (IsSet(InputButtons.Up)) sb.Append("⬆️ [W] ");
            if (IsSet(InputButtons.Down)) sb.Append("⬇️ [S] ");
            if (IsSet(InputButtons.LightPunch)) sb.Append("🥊 [J-Punch] ");
            if (IsSet(InputButtons.HeavyKick)) sb.Append("🦵 [K-Kick] ");
            if (IsSet(InputButtons.Guard)) sb.Append("🛡️ [L-Guard] ");
            return sb.ToString().TrimEnd();
        }
    }
}
