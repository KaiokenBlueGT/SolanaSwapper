using LibReplanetizer;
using System;
using System.Linq;

namespace GeometrySwapper
{
    /// <summary>
    /// Provides validation helpers for levels after swapping operations.
    /// </summary>
    public static class Validator
    {
        /// <summary>
        /// Ensures pVar indices assigned to mobys are contiguous starting from 0.
        /// Throws InvalidOperationException if a gap is detected.
        /// </summary>
        /// <param name="level">Level to validate.</param>
        public static void AssertContiguousPVars(Level level)
        {
            if (level?.mobs == null)
                return;

            var ids = level.mobs
                           .Select(m => m.pvarIndex)
                           .Where(i => i >= 0)
                           .OrderBy(i => i)
                           .ToList();
            for (int i = 0; i < ids.Count; ++i)
            {
                if (ids[i] != i)
                    throw new InvalidOperationException($"pVar hole at {i} (found {ids[i]})");
            }
        }
    }
}
