using System;
using System.Collections.Generic;
using System.IO;
using LibReplanetizer;
using LibReplanetizer.Headers;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Parsers;
using LibReplanetizer.Serializers; // Add this namespace
using static LibReplanetizer.DataFunctions;
using System.Numerics;
using System.Linq;
using OpenTK.Mathematics;

namespace MobyConverter
{
    internal class Program
    {
        static List<byte[]> LoadPvars(GameplayHeader header, FileStream fs)
        {
            var pVars = new List<byte[]>();
            if (header.pvarSizePointer == 0 || header.pvarPointer == 0)
                return pVars;
            byte[] pVarSizes = ReadBlock(fs, header.pvarSizePointer, header.pvarPointer - header.pvarSizePointer);
            int pVarSizeBlockSize = ReadInt(pVarSizes, pVarSizes.Length - 0x08) + ReadInt(pVarSizes, pVarSizes.Length - 0x04);
            if (pVarSizeBlockSize == 0)
                pVarSizeBlockSize = ReadInt(pVarSizes, pVarSizes.Length - 0x10) + ReadInt(pVarSizes, pVarSizes.Length - 0x0C);
            int pvarCount = pVarSizes.Length / 0x08;
            byte[] pVarBlock = ReadBlock(fs, header.pvarPointer, pVarSizeBlockSize);
            for (int i = 0; i < pvarCount; i++)
            {
                uint start = ReadUint(pVarSizes, i * 8);
                uint count = ReadUint(pVarSizes, i * 8 + 4);
                pVars.Add(GetBytes(pVarBlock, (int) start, (int) count));
            }
            return pVars;
        }

        static List<Moby> LoadRc1Mobies(GameplayHeader header, FileStream fs, List<Model> models, List<byte[]> pvars)
        {
            var mobies = new List<Moby>();
            if (header.mobyPointer == 0)
                return mobies;
            int count = ReadInt(ReadBlock(fs, header.mobyPointer, 4), 0);
            byte[] block = ReadBlock(fs, header.mobyPointer + 0x10, GameType.RaC1.mobyElemSize * count);
            for (int i = 0; i < count; i++)
            {
                mobies.Add(new Moby(GameType.RaC1, block, i, models, pvars));
            }
            return mobies;
        }

        static Moby ConvertToRc2(Moby rc1, List<byte[]> allPvars)
        {
            try
            {
                // Initialize rc2Bytes array with the correct size
                byte[] rc2Bytes = new byte[GameType.RaC2.mobyElemSize];

                /* Build RC2 file bytes from RC1 fields */
                WriteInt(rc2Bytes, 0x00, GameType.RaC2.mobyElemSize);
                WriteInt(rc2Bytes, 0x04, rc1.missionID);
                WriteInt(rc2Bytes, 0x08, rc1.dataval);
                WriteInt(rc2Bytes, 0x0C, (int) rc1.spawnType);
                WriteInt(rc2Bytes, 0x10, rc1.mobyID);
                WriteInt(rc2Bytes, 0x14, rc1.bolts);
                WriteShort(rc2Bytes, 0x18, 0);          // unk3A
                WriteShort(rc2Bytes, 0x1A, 0);          // unk3B
                WriteInt(rc2Bytes, 0x1C, 0);            // exp
                WriteInt(rc2Bytes, 0x20, 0);            // unk9
                WriteInt(rc2Bytes, 0x24, 0);            // unk6
                WriteInt(rc2Bytes, 0x28, rc1.modelID);
                WriteFloat(rc2Bytes, 0x2C, rc1.scale.X);
                WriteInt(rc2Bytes, 0x30, rc1.drawDistance);
                WriteInt(rc2Bytes, 0x34, rc1.updateDistance);
                WriteShort(rc2Bytes, 0x38, rc1.unk7A);
                WriteShort(rc2Bytes, 0x3A, rc1.unk7B);
                WriteShort(rc2Bytes, 0x3C, rc1.unk8A);
                WriteShort(rc2Bytes, 0x3E, rc1.unk8B);
                WriteFloat(rc2Bytes, 0x40, rc1.position.X);
                WriteFloat(rc2Bytes, 0x44, rc1.position.Y);
                WriteFloat(rc2Bytes, 0x48, rc1.position.Z);

                OpenTK.Mathematics.Vector3 eulerOpenTK = rc1.rotation.ToEulerAngles();
                System.Numerics.Vector3 euler = new System.Numerics.Vector3(eulerOpenTK.X, eulerOpenTK.Y, eulerOpenTK.Z);

                WriteFloat(rc2Bytes, 0x4C, euler.X);
                WriteFloat(rc2Bytes, 0x50, euler.Y);
                WriteFloat(rc2Bytes, 0x54, euler.Z);

                WriteInt(rc2Bytes, 0x58, rc1.groupIndex);
                WriteInt(rc2Bytes, 0x5C, rc1.isRooted);
                WriteFloat(rc2Bytes, 0x60, rc1.rootedDistance);
                WriteShort(rc2Bytes, 0x64, rc1.unk12A);
                WriteShort(rc2Bytes, 0x66, rc1.unk12B);

                // Correctly write the original pvarIndex
                WriteInt(rc2Bytes, 0x68, rc1.pvarIndex);

                WriteInt(rc2Bytes, 0x6C, rc1.occlusion ? 1 : 0);
                WriteInt(rc2Bytes, 0x70, (int) rc1.mode);
                WriteUint(rc2Bytes, 0x74, rc1.color.R);
                WriteUint(rc2Bytes, 0x78, rc1.color.G);
                WriteUint(rc2Bytes, 0x7C, rc1.color.B);
                WriteInt(rc2Bytes, 0x80, rc1.light);
                WriteInt(rc2Bytes, 0x84, rc1.cutscene);

                List<Model> modelList = new List<Model>();
                if (rc1.model != null)
                {
                    modelList.Add(rc1.model);
                }

                // Build RC2 moby from byte array, passing the *full* pVars list
                var rc2 = new Moby(
                    GameType.RaC2,
                    rc2Bytes,
                    0,
                    modelList,
                    allPvars // Pass the full list here
                );

                // The Moby constructor now correctly links the pVars based on the preserved index.
                // We just need to ensure the model reference is also preserved.
                rc2.model = rc1.model;

                rc2.UpdateTransformMatrix();
                return rc2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting moby: {ex.Message}");
                var fallbackMoby = new Moby(GameType.RaC2, new byte[GameType.RaC2.mobyElemSize], 0, new List<Model>(), new List<byte[]>());
                fallbackMoby.mobyID = rc1.mobyID;
                fallbackMoby.modelID = rc1.modelID;
                fallbackMoby.position = rc1.position;
                fallbackMoby.rotation = rc1.rotation;
                fallbackMoby.scale = rc1.scale;
                fallbackMoby.pvarIndex = -1;
                return fallbackMoby;
            }
        }

        /// <summary>
        /// Safely copies a block from one array to another, checking bounds on both arrays
        /// </summary>
        private static void SafeCopyBlock(byte[] source, int srcOffset, byte[] dest, int destOffset, int length)
        {
            if (source == null || dest == null ||
                srcOffset < 0 || destOffset < 0 || length <= 0 ||
                srcOffset + length > source.Length || destOffset + length > dest.Length)
                return;

            Buffer.BlockCopy(source, srcOffset, dest, destOffset, length);
        }

        /// <summary>
        /// Safely copies a ushort value from one array to another, checking bounds on both arrays
        /// </summary>
        private static void SafeCopyUshort(byte[] source, int srcOffset, byte[] dest, int destOffset)
        {
            if (source == null || dest == null ||
                srcOffset < 0 || destOffset < 0 ||
                srcOffset + 2 > source.Length || destOffset + 2 > dest.Length)
                return;

            WriteUshort(dest, destOffset, ReadUshort(source, srcOffset));
        }

        /// <summary>
        /// Helper to read a ushort value from a specific byte array offset
        /// </summary>
        private static ushort ReadUshort(byte[] buffer, int offset)
        {
            return (ushort) ((buffer[offset + 1] << 8) | buffer[offset]);
        }

        /// <summary>
        /// Helper to write a ushort value to a specific byte array offset
        /// </summary>
        private static void WriteUshort(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte) (value & 0xFF);
            buffer[offset + 1] = (byte) (value >> 8);
        }

        /// <summary>
        /// Helper method to write a signed byte value to a specific offset in a byte array
        /// </summary>
        private static void WriteSbyte(byte[] buffer, int offset, sbyte value)
        {
            buffer[offset] = (byte) value;
        }

        /// <summary>
        /// Helper method to write a byte value to a specific offset in a byte array
        /// </summary>
        private static void WriteByte(byte[] buffer, int offset, byte value)
        {
            buffer[offset] = value;
        }

        /// <summary>
        /// Helper method to write an unsigned long value to a specific offset in a byte array
        /// </summary>
        private static void WriteUlong(byte[] buffer, int offset, ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            Array.Copy(bytes, 0, buffer, offset, 8);
        }

        /// <summary>
        /// Initializes empty collections for the given level.
        /// </summary>
        private static void InitializeEmptyCollections(Level rc2Level)
        {
            rc2Level.english = new List<LanguageData>();
            rc2Level.ukenglish = new List<LanguageData>();
            rc2Level.french = new List<LanguageData>();
            rc2Level.german = new List<LanguageData>();
            rc2Level.spanish = new List<LanguageData>();
            rc2Level.italian = new List<LanguageData>();
            rc2Level.japanese = new List<LanguageData>();
            rc2Level.korean = new List<LanguageData>();
            rc2Level.lights = new List<Light>();
            rc2Level.directionalLights = new List<DirectionalLight>();
            rc2Level.pointLights = new List<PointLight>();
            rc2Level.envSamples = new List<EnvSample>();
            rc2Level.gameCameras = new List<GameCamera>();
            rc2Level.soundInstances = new List<SoundInstance>();
            rc2Level.envTransitions = new List<EnvTransition>();
            rc2Level.cuboids = new List<Cuboid>();
            rc2Level.spheres = new List<Sphere>();
            rc2Level.cylinders = new List<Cylinder>();
            rc2Level.splines = new List<Spline>();
            rc2Level.grindPaths = new List<GrindPath>();
            rc2Level.pVars = new List<byte[]>();
            rc2Level.type50s = new List<KeyValuePair<int, int>>();
            rc2Level.type5Cs = new List<KeyValuePair<int, int>>();
            rc2Level.tieIds = new List<int>();
            rc2Level.shrubIds = new List<int>();
            rc2Level.tieData = new byte[0];
            rc2Level.shrubData = new byte[0];
            rc2Level.unk6 = new byte[0];
            rc2Level.unk7 = new byte[0];
            rc2Level.unk14 = new byte[0];
            rc2Level.unk17 = new byte[0];
            rc2Level.tieGroupData = new byte[0];
            rc2Level.shrubGroupData = new byte[0];
            rc2Level.areasData = new byte[0];
        }

        /// <summary>
        /// Saves a level without generating chunk files, only creating engine.ps3 and gameplay_ntsc files.
        /// This avoids crashes when chunk data might not be properly initialized.
        /// </summary>
        /// <param name="level">The level to save</param>
        /// <param name="outputPath">The directory where the level files should be saved</param>
        private static void SaveWithoutChunks(Level level, string outputPath)
        {
            string? directory;
            if (File.Exists(outputPath) && File.GetAttributes(outputPath).HasFlag(FileAttributes.Directory))
            {
                directory = outputPath;
            }
            else
            {
                directory = Path.GetDirectoryName(outputPath);
            }

            if (directory == null) return;

            // Set chunkCount to 0 to indicate no chunks
            if (level.levelVariables != null)
            {
                level.levelVariables.chunkCount = 0;
            }

            // Empty terrain and collision chunks to ensure none get created
            level.terrainChunks = new List<Terrain>();
            level.collisionChunks = new List<Collision>();
            level.collBytesChunks = new List<byte[]>();

            // Only save engine and gameplay files, skip chunks
            Console.WriteLine($"Saving level to {directory} (engine and gameplay only, no chunks)...");
            EngineSerializer engineSerializer = new EngineSerializer();
            engineSerializer.Save(level, directory);
            GameplaySerializer gameplaySerializer = new GameplaySerializer();
            gameplaySerializer.Save(level, directory);

            Console.WriteLine("Level saved successfully without chunks");
        }

        public static void RunMobyConverter(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: MobyConverter <rc1 engine.ps3> <output directory>");
                return;
            }

            string rc1EnginePath = args[0];
            string outputDir = args[1];
            Directory.CreateDirectory(outputDir);

            Console.WriteLine($"Starting conversion using RC1 level '{rc1EnginePath}'...");
            string donorRc2Path = @"C:\Users\Ryan_\Downloads\temp\Oltanis_RaC2Port_Final\engine.ps3";

            try
            {
                Console.WriteLine($"Loading RC1 level from '{rc1EnginePath}'...");
                Level rc1Level = new Level(rc1EnginePath);
                if (rc1Level.game.num != 1)
                {
                    Console.WriteLine("❌ Error: The provided source level is not a Ratchet & Clank 1 level.");
                    return;
                }
                Console.WriteLine($"✅ Loaded RC1 level with {rc1Level.mobs?.Count ?? 0} mobys and {rc1Level.pVars?.Count ?? 0} pVar blocks.");

                if (!File.Exists(donorRc2Path))
                {
                    Console.WriteLine($"❌ Error: Donor RC2 level not found at '{donorRc2Path}'. This file is required.");
                    return;
                }
                Console.WriteLine($"Loading donor RC2 level from '{donorRc2Path}' for assets and structure...");
                Level donorRc2Level = new Level(donorRc2Path);
                if (donorRc2Level.game.num != 2)
                {
                    Console.WriteLine("❌ Error: The donor level is not a Ratchet & Clank 2 level.");
                    return;
                }
                Console.WriteLine($"✅ Loaded donor RC2 level with {donorRc2Level.mobyModels?.Count ?? 0} moby models.");

                var donorModelMap = donorRc2Level.mobyModels?.ToDictionary(m => m.id, m => m) ?? new Dictionary<short, Model>();

                Console.WriteLine("Converting and refining mobys...");
                var finalRc2Mobies = new List<Moby>();
                int successCount = 0;
                int errorCount = 0;

                if (rc1Level.mobs != null)
                {
                    foreach (var rc1Moby in rc1Level.mobs)
                    {
                        try
                        {
                            // Pass the full pVars list from the RC1 level to the converter
                            Moby convertedMoby = ConvertToRc2(rc1Moby, rc1Level.pVars);

                            if (donorModelMap.TryGetValue((short) convertedMoby.modelID, out Model? donorModel) && donorModel != null)
                            {
                                convertedMoby.model = donorModel;
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ Warning: Model ID {convertedMoby.modelID} for moby oClass {convertedMoby.mobyID} not found in donor level. The model may be missing.");
                            }

                            finalRc2Mobies.Add(convertedMoby);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error during conversion of moby oClass {rc1Moby.mobyID}: {ex.Message}");
                            errorCount++;
                        }
                    }
                }
                Console.WriteLine($"✅ Conversion process complete. Successfully converted {successCount} mobys with {errorCount} errors.");

                Level outputLevel = donorRc2Level;
                outputLevel.path = rc1EnginePath;
                outputLevel.mobs = finalRc2Mobies;
                outputLevel.mobyIds = finalRc2Mobies.Select(m => m.mobyID).ToList();
                outputLevel.pVars = rc1Level.pVars;

                Console.WriteLine($"\nSaving converted level to '{outputDir}'...");
                SaveWithoutChunks(outputLevel, outputDir);
                Console.WriteLine($"\n✅ Level saved successfully to '{outputDir}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ An unexpected error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
