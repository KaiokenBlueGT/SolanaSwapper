using System;
using System.IO; // Required for Path.Combine and File operations
using System.Linq;
using System.Collections.Generic;
using OpenTK.Mathematics; // Added this import for OpenTK's Vector3
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Serializers;
using static LibReplanetizer.DataFunctions; // For ReadUint, WriteShort etc. from the library

namespace MobyTranslator
{
    class Program
    {
        // Helper to write a uint in big-endian order to a FileStream
        // This is used for manual patching.
        static void WriteUintBigEndian(FileStream fs, uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            fs.Write(b, 0, 4);
        }

        static void Main(string[] args)
        {
            Console.WriteLine(">>> BUILD 31, May 21st, 2025 - Added Moby Conversion Logic <<<");

            string inputEngine = @"C:\Users\Ryan_\Downloads\temp\Oltanis_RaC1\engine.ps3";
            string outputDir = @"C:\Users\Ryan_\Downloads\temp\Quartu_RaC2_Converted\";
            Directory.CreateDirectory(outputDir); // Ensure output directory exists

            Console.WriteLine($"📂 Loading RC1 level: {inputEngine}");
            var level = new Level(inputEngine);

            Console.WriteLine($"Loaded mobies: {level.mobs?.Count ?? 0}");
            Console.WriteLine($"Loaded RC1 point lights: {(level.pointLights?.Count ?? 0)}");

            level.game = GameType.RaC2;
            GameType rc2GameType = level.game;

            // ... (Point Light and TIE conversion code remains the same) ...
            Console.WriteLine("Converting Point Lights to RC2 format...");
            List<PointLight> convertedPointLights = new List<PointLight>();
            if (level.pointLights != null)
            {
                int rc2ElementSize = PointLight.GetElementSize(rc2GameType);
                foreach (var rc1Light in level.pointLights)
                {
                    byte[] rc2LightBytes = new byte[rc2ElementSize];
                    WriteShort(rc2LightBytes, 0x00, (short) MathF.Round(rc1Light.position.X * 64.0f));
                    WriteShort(rc2LightBytes, 0x02, (short) MathF.Round(rc1Light.position.Y * 64.0f));
                    WriteShort(rc2LightBytes, 0x04, (short) MathF.Round(rc1Light.position.Z * 64.0f));
                    WriteShort(rc2LightBytes, 0x06, (short) MathF.Round(rc1Light.radius * 64.0f));
                    WriteUshort(rc2LightBytes, 0x08, (ushort) MathF.Round(rc1Light.color.X * 64.0f));
                    WriteUshort(rc2LightBytes, 0x0A, (ushort) MathF.Round(rc1Light.color.Y * 64.0f));
                    WriteUshort(rc2LightBytes, 0x0C, (ushort) MathF.Round(rc1Light.color.Z * 64.0f));
                    PointLight newRc2Light = new PointLight(rc2GameType, rc2LightBytes, 0);
                    newRc2Light.id = rc1Light.id;
                    convertedPointLights.Add(newRc2Light);
                }
            }
            level.pointLights = convertedPointLights;
            Console.WriteLine($"Converted {level.pointLights.Count} point lights to RC2 structure.");

            Console.WriteLine("Updating TIE culling distances (off54 to 4000)...");
            if (level.ties != null)
            {
                int updatedTies = 0;
                foreach (var tie in level.ties) { tie.off54 = 4000; updatedTies++; }
                Console.WriteLine($"Updated {updatedTies} TIEs.");
            }
            // ... (Moby conversion logic remains the same) ...
            // -------- MOBY CONVERSION RC1 -> RC2 --------
            Console.WriteLine("Converting Moby objects from RC1 to RC2 format...");
            List<Moby> rc2ConvertedMobs = new List<Moby>();
            if (level.mobs != null)
            {
                foreach (var rc1Moby in level.mobs)
                {
                    // Create a new Moby using a constructor that sets the game type correctly.
                    Moby newRc2Moby = new Moby(rc2GameType, rc1Moby.model, rc1Moby.position, rc1Moby.rotation, rc1Moby.scale);

                    // --- THIS IS THE FIX ---
                    // Manually set the modelID field. This field is likely inherited from ModelObject.
                    // Moby.cs shows modelID is an int. rc1Moby.model.id should also be an int.
                    if (rc1Moby.model != null)
                    {
                        newRc2Moby.modelID = rc1Moby.model.id;
                    }
                    else
                    {
                        // If the original RC1 Moby had no model, default its modelID to 11 (Vendor)
                        // or another suitable placeholder. This prevents null reference if model.id is accessed.
                        newRc2Moby.modelID = 11;
                        Console.WriteLine($"Warning: RC1 Moby with oClass {rc1Moby.mobyID} had a null model. Defaulting its RC2 modelID to 0.");
                    }
                    // --- END OF FIX ---

                    // Copy common/mapped fields from RC1 Moby to RC2 Moby
                    newRc2Moby.missionID = rc1Moby.missionID;
                    newRc2Moby.spawnType = rc1Moby.spawnType;
                    // newRc2Moby.mobyID is the oClass (unique instance ID/type), which is set by the constructor or later.
                    // The Moby constructor Moby(GameType game, Model model, ...) seems to set mobyID via MAX_ID++.
                    // If you want to preserve original oClass from RC1, you'd do:
                    newRc2Moby.mobyID = rc1Moby.mobyID;


                    newRc2Moby.bolts = rc1Moby.bolts;
                    newRc2Moby.dataval = rc1Moby.dataval;
                    // model (Model object reference), scale, position, rotation are set by constructor

                    newRc2Moby.drawDistance = rc1Moby.drawDistance;
                    newRc2Moby.updateDistance = rc1Moby.updateDistance;

                    newRc2Moby.unk7A = rc1Moby.unk7A;
                    newRc2Moby.unk7B = rc1Moby.unk7B;
                    newRc2Moby.unk8A = rc1Moby.unk8A;
                    newRc2Moby.unk8B = rc1Moby.unk8B;

                    newRc2Moby.groupIndex = rc1Moby.groupIndex;
                    newRc2Moby.isRooted = rc1Moby.isRooted;
                    newRc2Moby.rootedDistance = rc1Moby.rootedDistance;

                    newRc2Moby.unk12A = rc1Moby.unk12A;
                    newRc2Moby.unk12B = rc1Moby.unk12B;

                    newRc2Moby.pvarIndex = rc1Moby.pvarIndex;
                    // Make sure pVars are handled correctly. If pvarIndex is -1, pVars should be empty or null.
                    // The Moby constructor you are using initializes newRc2Moby.pVars = new byte[0];
                    // If rc1Moby.pVars is what you want to keep, you can assign it:
                    if (rc1Moby.pvarIndex != -1 && rc1Moby.pVars != null)
                    {
                        newRc2Moby.pVars = rc1Moby.pVars;
                    }
                    else
                    {
                        newRc2Moby.pVars = new byte[0]; // Ensure it's empty if no pvar
                        newRc2Moby.pvarIndex = -1;     // Ensure pvarIndex is also -1
                    }


                    newRc2Moby.occlusion = rc1Moby.occlusion;
                    newRc2Moby.mode = rc1Moby.mode;
                    newRc2Moby.color = rc1Moby.color;
                    newRc2Moby.light = rc1Moby.light;
                    newRc2Moby.cutscene = rc1Moby.cutscene;

                    // Fields specific to RC2/3 Mobys (initialize to defaults)
                    newRc2Moby.unk3A = 0;
                    newRc2Moby.unk3B = 0;
                    newRc2Moby.exp = 0;
                    newRc2Moby.unk9 = 0;
                    newRc2Moby.unk6 = 0;

                    newRc2Moby.UpdateTransformMatrix();
                    rc2ConvertedMobs.Add(newRc2Moby);
                }
                level.mobs = rc2ConvertedMobs;
                Console.WriteLine($"Converted {level.mobs.Count} Moby objects to RC2 structure.");
            }
            // -------- END OF MOBY CONVERSION --------

            // ... (LevelVariables setup and other preparations remain the same) ...
            ref var lv = ref level.levelVariables;
            lv.chunkCount = 0; //If this fucks up, set this back to 1, but I'm pretty sure it should stay 0, as almost every other GC planet I've seen so far has it set to 0.
            lv.ByteSize = 0x88;
            lv.off68 = 0; lv.off6C = 0;
            if (level.grindPaths != null) level.grindPaths.Clear();
            lv.shipPosition = new Vector3(260.368f, 172.407f, 48.354f);
            lv.shipRotation = 2.259f;
            lv.shipPathID = 127; lv.shipCameraStartID = 156; lv.shipCameraEndID = 157;
            lv.off58 = 0; lv.off7C = 0; lv.off78 = 0;

            Console.WriteLine("\n=== RC2 LevelVariables Structure Values (Pre-Save) ===");
            Console.WriteLine($"Struct Size (lv.ByteSize) : 0x{lv.ByteSize:X}");
            Console.WriteLine($"Ship Rotation (lv.shipRotation -> byte 0x48): {lv.shipRotation}");
            Console.WriteLine($"Ship Path ID (lv.shipPathID -> byte 0x4C): {lv.shipPathID}");
            Console.WriteLine($"Value for LV byte 0x58 (from lv.off58 property): {lv.off58}");
            Console.WriteLine($"Value for LV byte 0x7C (from lv.off7C property if chunkCount=0): {lv.off7C}");

            byte[] tieGroupData = new byte[0x20]; WriteInt(tieGroupData, 0x00, 0); WriteInt(tieGroupData, 0x04, 0x20); level.tieGroupData = tieGroupData;
            byte[] shrubGroupData = new byte[0x20]; WriteInt(shrubGroupData, 0x00, 0); WriteInt(shrubGroupData, 0x04, 0x20); level.shrubGroupData = shrubGroupData;
            byte[] areasData = new byte[0x20]; WriteInt(areasData, 0x00, 0); WriteInt(areasData, 0x04, 0x20); level.areasData = areasData;

            Console.WriteLine("Rebuilding Moby Groups for RC2 format...");
            if (level.mobs != null && level.mobs.Count > 0)
            {
                byte[] mobyGroupBlockData = new byte[0x20 + 8]; WriteInt(mobyGroupBlockData, 0x00, 1); WriteInt(mobyGroupBlockData, 0x04, 0x20); WriteInt(mobyGroupBlockData, 0x20, 0); WriteInt(mobyGroupBlockData, 0x24, level.mobs.Count); level.unk6 = mobyGroupBlockData;
                Console.WriteLine($"Created moby group block for {level.mobs.Count} mobys.");
            }
            else
            {
                byte[] mobyGroupBlockData = new byte[0x20]; WriteInt(mobyGroupBlockData, 0x00, 0); WriteInt(mobyGroupBlockData, 0x04, 0x20); level.unk6 = mobyGroupBlockData;
                Console.WriteLine("Created empty moby group block (no mobys).");
            }

            if (level.mobs != null) level.mobyIds = level.mobs.Select(m => m.mobyID).ToList(); else level.mobyIds = new List<int>();
            if (level.ties != null) { if (level.tieIds == null || level.tieIds.Count != level.ties.Count) { level.tieIds = level.ties.Select((t, i) => t.modelID).ToList(); Console.WriteLine("Rebuilt level.tieIds."); } } else { level.tieIds = new List<int>(); }
            if (level.shrubs != null) { if (level.shrubIds == null || level.shrubIds.Count != level.shrubs.Count) { level.shrubIds = level.shrubs.Select((s, i) => s.modelID).ToList(); Console.WriteLine("Rebuilt level.shrubIds."); } } else { level.shrubIds = new List<int>(); }
            Console.WriteLine($"Preserving {(level.mobs?.Count ?? 0)} mobys, {(level.ties?.Count ?? 0)} ties, {(level.shrubs?.Count ?? 0)} shrubs.");

            Console.WriteLine("Processing Global PVARs (type4Cs to unk7)...");
            if (level.type4Cs != null && level.type4Cs.Count > 0)
            {
                var gameplaySerializerForPvar = new LibReplanetizer.Serializers.GameplaySerializer(); level.unk7 = gameplaySerializerForPvar.GetType4CBytes(level.type4Cs); level.type4Cs.Clear();
                Console.WriteLine($"Converted type4Cs to unk7 blob.");
            }
            else { level.unk7 = new byte[0x10]; Console.WriteLine("No type4Cs, created empty unk7."); }

            Console.WriteLine("Clearing gadget models and textures...");
            if (level.gadgetModels != null) level.gadgetModels.Clear(); if (level.gadgetTextures != null) level.gadgetTextures.Clear();


            Console.WriteLine($"\n💾 Saving raw RC2 level to: {outputDir}");
            level.Save(outputDir);

            // --- Manual patching of engine.ps3 header values (big-endian) ---
            string outputEngineFile = Path.Combine(outputDir, "engine.ps3");
            Console.WriteLine("Patching engine.ps3 header values at 0x08, 0x0C, and 0xA0...");
            try
            {
                using (var fs = File.Open(outputEngineFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) // Open with ReadWrite
                {
                    // --- Patch offset 0x08 (Build ID) ---
                    fs.Seek(0x08, SeekOrigin.Begin);
                    WriteUintBigEndian(fs, 0x00020003); // Example RC2 build ID

                    // --- Patch offset 0x0C (Platform ID) ---
                    fs.Seek(0x0C, SeekOrigin.Begin);
                    WriteUintBigEndian(fs, 0x00000000); // Typically 0x0 in real RC2 engine files

                    // --- Patch offset 0xA0 (RC2 magic number) ---
                    fs.Seek(0xA0, SeekOrigin.Begin);
                    WriteUintBigEndian(fs, 0xEAA90001); // RC2 magic number required for detection
                }
                Console.WriteLine("✅ engine.ps3 patched successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error while patching engine.ps3: {ex.Message}");
            }

            // ... (Verification logic remains the same) ...
            Console.WriteLine("\n=== Engine File Header Verification (Post LibReplanetizer Save AND Patch) ==="); // Modified log
            try
            {
                // Use outputEngineFile which is already defined
                using (var fs = File.OpenRead(outputEngineFile))
                {
                    // LibReplanetizer.DataFunctions.ReadUint expects a byte array
                    byte[] buffer = new byte[4];

                    fs.Seek(0x08, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    uint v08 = LibReplanetizer.DataFunctions.ReadUint(buffer, 0); // Make sure ReadUint handles endianness or data is already BE
                    Console.WriteLine($"Value @0x08: 0x{v08:X8}");

                    fs.Seek(0x0C, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 4);
                    uint v0C = LibReplanetizer.DataFunctions.ReadUint(buffer, 0);
                    Console.WriteLine($"Value @0x0C: 0x{v0C:X8}");

                    fs.Seek(0xA0, SeekOrigin.Begin); // Also verify 0xA0
                    fs.Read(buffer, 0, 4);
                    uint vA0 = LibReplanetizer.DataFunctions.ReadUint(buffer, 0);
                    Console.WriteLine($"Value @0xA0: 0x{vA0:X8}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ Error verifying engine.ps3: {ex.Message}"); }


            Console.WriteLine("\n=== Inspecting gameplay_ntsc at offset 0xA0 (Post-Save) ===");
            string gameplayFilePath = Path.Combine(outputDir, "gameplay_ntsc");
            try
            {
                using (FileStream gs = File.OpenRead(gameplayFilePath))
                {
                    if (gs.Length >= (0xA0 + 4))
                    {
                        gs.Seek(0xA0, SeekOrigin.Begin); byte[] magicCheckBytes = new byte[4]; gs.Read(magicCheckBytes, 0, 4);
                        uint magicOnDisk = ReadUint(magicCheckBytes, 0); // Assumes ReadUint from DataFunctions handles endianness
                        Console.WriteLine($"Magic number read from disk: gameplay_ntsc at 0xA0: 0x{magicOnDisk:X8}");
                        Console.WriteLine($"Expected RC2 Magic (for engine.ps3, not necessarily gameplay_ntsc): 0x{0xEAA90001:X8}");
                        if (magicOnDisk == 0xEAA90001) Console.WriteLine("INFO: gameplay_ntsc at 0xA0 coincidentally matches engine magic. This may or may not be significant.");
                        else Console.WriteLine("INFO: gameplay_ntsc at 0xA0 does not match engine magic. This is likely OK.");
                    }
                    else { Console.WriteLine($"gameplay_ntsc is too short ({gs.Length} bytes) to read magic at 0xA0."); }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error inspecting gameplay_ntsc: {ex.Message}"); }

            Console.WriteLine("\n=== Post-Save Verification (Reloading Converted Level) ===");
            Console.WriteLine($"📂 Loading converted RC2 level: {outputEngineFile}");
            try
            {
                var convertedLevel = new Level(outputEngineFile);
                Console.WriteLine("\nLevel Variables after conversion and reload:");
                Console.WriteLine($"GameType reported by loaded level (num): {convertedLevel.game.num}");
                // ... other verification printouts
                Console.WriteLine($"Ship Position: ({convertedLevel.levelVariables.shipPosition.X}, {convertedLevel.levelVariables.shipPosition.Y}, {convertedLevel.levelVariables.shipPosition.Z})");
                Console.WriteLine($"Ship Rotation: {convertedLevel.levelVariables.shipRotation}");
                Console.WriteLine($"Ship Path ID: {convertedLevel.levelVariables.shipPathID}");
                Console.WriteLine($"Camera Start ID: {convertedLevel.levelVariables.shipCameraStartID}");
                Console.WriteLine($"Camera End ID: {convertedLevel.levelVariables.shipCameraEndID}");
                Console.WriteLine($"Chunk Count: {convertedLevel.levelVariables.chunkCount}");
                Console.WriteLine($"Byte Size of LV: 0x{convertedLevel.levelVariables.ByteSize:X}");
                Console.WriteLine($"LV Prop off58 (read from byte 0x58): 0x{convertedLevel.levelVariables.off58:X}");
                Console.WriteLine($"LV Prop off7C (read from byte 0x7C if chunkCount=0): 0x{convertedLevel.levelVariables.off7C:X}");
                Console.WriteLine($"Moby Count: {(convertedLevel.mobs?.Count ?? 0)}");
                Console.WriteLine($"Grind Path Count in LV (lv.off68): {convertedLevel.levelVariables.off68} (should be 0)");
                Console.WriteLine($"Actual Grind Paths list count: {(convertedLevel.grindPaths?.Count ?? 0)} (should be 0)");
                Console.WriteLine($"Point Light Count: {(convertedLevel.pointLights?.Count ?? 0)}");
                if (convertedLevel.pointLights != null && convertedLevel.pointLights.Count > 0)
                {
                    var firstLight = convertedLevel.pointLights[0];
                    Console.WriteLine($"First reloaded Point Light - ID: {firstLight.id}, Pos: {firstLight.position}, Radius: {firstLight.radius}, Color: {firstLight.color}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ Error loading converted level for verification: {ex.Message}\n{ex.StackTrace}"); }

            Console.WriteLine("\n✅ Done! Test the output in RPCS3.");
        }
    }
}
