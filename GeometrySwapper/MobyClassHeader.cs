using System.Runtime.InteropServices;

namespace LibReplanetizer.LevelObjects
{
    /// <summary>
    /// Header describing code callbacks for a moby class.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rc1MobyClassHeader
    {
        /// <summary>Size/version field at 0x00.</summary>
        public uint sizeVersion;
        /// <summary>Pointer to the update function at 0x04.</summary>
        public uint pUpdate;
        // TODO: remaining RC1 fields

        public const int SIZE_RC1 = 0x28;
    }

    /// <summary>
    /// Updated header used on GC builds (RC2 and later).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GcMobyClassHeader
    {
        /// <summary>Size/version field at 0x00.</summary>
        public uint sizeVersion;
        /// <summary>Pointer to the update function at 0x04.</summary>
        public uint pUpdate;
        /// <summary>Pointer to an additional routine at 0x08.</summary>
        public uint pExtra; // new field
        // TODO: copy rest of RC1 layout

        public const int SIZE_GC = 0x30;
    }
}
