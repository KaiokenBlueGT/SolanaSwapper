using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometrySwapper
{
    /// <summary>
    /// Provides robust methods for saving level data.
    /// </summary>
    public static class SaveHelper
    {
        /// <summary>
        /// Prepares the level for saving by fixing inconsistencies.
        /// </summary>
        private static void PrepareLevelForSave(Level level)
        {
            Console.WriteLine("\n=== Preparing Level For Save ===");

            // 1. Make sure mobyIds are properly synced
            if (level.mobs != null)
            {
                level.mobyIds = level.mobs.Select(m => m.mobyID).ToList();
            }

            // 2. Restore and validate grind paths from the original file to prevent data loss.
            // This is the critical step to prevent grind paths from being deleted.
            GrindPathSwapper.RestoreAndValidateGrindPaths(level);

            // 3. Ensure other critical collections are not null
            if (level.pVars == null) level.pVars = new List<byte[]>();

            // 4. Clear chunk data to prevent saving them
            level.terrainChunks = new List<Terrain>();
            level.collisionChunks = new List<Collision>();
            level.collBytesChunks = new List<byte[]>();
            if (level.levelVariables != null)
            {
                level.levelVariables.chunkCount = 0;
            }

            // 5. Update transform matrices for all mobys
            if (level.mobs != null)
            {
                foreach (var moby in level.mobs)
                {
                    moby.UpdateTransformMatrix();
                }
            }

            Console.WriteLine("✅ Level prepared for saving");
        }

        /// <summary>
        /// Writes a big-endian uint to a file stream.
        /// </summary>
        private static void WriteUintBigEndian(FileStream fs, uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            fs.Write(b, 0, 4);
        }

        /// <summary>
        /// Safely saves the level using a robust method.
        /// </summary>
        /// <param name="level">The level to save</param>
        /// <param name="outputPath">Path where the level should be saved (should be the engine.ps3 file)</param>
        /// <returns>True if save was successful</returns>
        public static bool SaveLevelSafely(Level level, string outputPath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrEmpty(directory))
                {
                    Console.WriteLine("❌ Invalid output directory");
                    return false;
                }

                // Ensure directory exists
                Directory.CreateDirectory(directory);

                // Prepare the level for saving using the robust preparation method
                PrepareLevelForSave(level);

                // Save the level using the standard Level.Save method
                Console.WriteLine($"Saving level to {directory}...");
                level.Save(directory);

                // Patch the engine header with required values for RC2, if applicable
                if (level.game.num == 2)
                {
                    string outputEngineFile = Path.Combine(directory, "engine.ps3");
                    Console.WriteLine("Patching engine.ps3 header values for RC2...");
                    try
                    {
                        using (var fs = File.Open(outputEngineFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            fs.Seek(0x08, SeekOrigin.Begin); WriteUintBigEndian(fs, 0x00020003);
                            fs.Seek(0x0C, SeekOrigin.Begin); WriteUintBigEndian(fs, 0x00000000);
                            fs.Seek(0xA0, SeekOrigin.Begin); WriteUintBigEndian(fs, 0xEAA90001);
                        }
                        Console.WriteLine("✅ engine.ps3 patched successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error while patching engine.ps3: {ex.Message}");
                    }
                }

                Console.WriteLine("✅ Level saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during safe save: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return false;
            }
        }
    }
}
