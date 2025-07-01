using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography; // For SHA256
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Parsers;
using OpenTK.Mathematics;
using static LibReplanetizer.DataFunctions;

namespace GeometrySwapper
{
    // Add the new flag to the SwapOptions enum
    [Flags]
    public enum SwapOptions
    {
        None = 0,
        Terrain = 1 << 0,
        Collision = 1 << 1,
        Ties = 1 << 2,
        Shrubs = 1 << 3,
        Skybox = 1 << 4,
        GrindPaths = 1 << 5,
        PointLights = 1 << 6,
        SoundInstances = 1 << 7,
        Mobys = 1 << 8,
        All = Terrain | Collision | Ties | Shrubs | Skybox | GrindPaths | PointLights | SoundInstances,
        RC2SelfTest = 1 << 9,
        SwapLevelVariables = 1 << 10,
        TransferRatchetPosition = 1 << 11,
        RegisterPlanetInMap = 1 << 12, // New option for registering planets in the galactic map
        SwapVendorWithOltanis = 1 << 13, // <-- Add this line
        SwapCratesWithOltanis = 1 << 14, // Add this after the last value
        SwapSwingshots = 1 << 15,
        RunGrindPathDiagnostics = 1 << 16,
        UseMobyConverter = 1 << 17,
        CreateMobyInstances = 1 << 18,
    }

    class Program
    {
        // Make these static fields at the class level so they're accessible from all methods
        private static readonly string globalRc2Dir = @"D:\Projects\R&C1_to_R&C2_Planet_Format\Going_Commando_PSARC\rc2\ps3data\global\";
        private static string rc1SourceLevelDir = @"C:\Users\Ryan_\Downloads\temp\Oltanis_RaC1\";
        private static string rc2DonorLevelDir = @"C:\Users\Ryan_\Downloads\temp\Insomniac_Museum\"; // Using Smolg (formerly, Insomniac Museum) as donor
        private static string referenceGcPlanetDir = @"C:\Users\Ryan_\Downloads\temp\Damosel\";
        private static string outputDir = @"C:\Users\Ryan_\Downloads\temp\OltanisOnInsomniacBase\"; // Changed output name

        static void WriteUintBigEndian(FileStream fs, uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            fs.Write(b, 0, 4);
        }

        static string GetTextureSignature(Texture tex)
        {
            if (tex.data == null || tex.data.Length == 0)
            {
                // Use different properties or derive a name
                return $"NO_DATA_texture_{tex.width}x{tex.height}_{Guid.NewGuid()}";
            }
            using (var sha256 = SHA256.Create())
            {
                return Convert.ToBase64String(sha256.ComputeHash(tex.data));
            }
        }

        /// <summary>
        /// Remaps texture IDs in a model based on a texture ID mapping dictionary
        /// </summary>
        static void ReindexModelTextures(Model modelToUpdate, Dictionary<int, int> textureMapping, string modelContext)
        {
            if (modelToUpdate?.textureConfig == null) return;

            for (int i = 0; i < modelToUpdate.textureConfig.Count; i++)
            {
                var texConfig = modelToUpdate.textureConfig[i];
                int oldId = texConfig.id;

                // Try to map the texture ID
                if (textureMapping.TryGetValue(oldId, out int newId))
                {
                    texConfig.id = newId;
                    Console.WriteLine($"  Reindexed texture in {modelContext} (ID {modelToUpdate.id}): {oldId} → {newId}");
                }
                else
                {
                    Console.WriteLine($"  Warning: No mapping for texture ID {oldId} in model {modelContext} (ID {modelToUpdate.id})");
                }
            }
        }

        static void DumpTerrainInfo(Terrain? terrain, string label)
        {
            if (terrain == null)
            {
                Console.WriteLine($"\n===== TERRAIN DUMP for {label} =====");
                Console.WriteLine("Terrain is null");
                Console.WriteLine("=============================\n");
                return;
            }

            Console.WriteLine($"\n===== TERRAIN DUMP for {label} =====");
            Console.WriteLine($"Level Number: {terrain.levelNumber}");
            Console.WriteLine($"Fragment Count: {terrain.fragments.Count}");

            // Dump first few fragments for analysis
            int count = Math.Min(5, terrain.fragments.Count);
            for (int i = 0; i < count; i++)
            {
                var frag = terrain.fragments[i];
                Console.WriteLine($"\nFragment {i}:");
                Console.WriteLine($"  Culling Center: {frag.cullingCenter}");
                Console.WriteLine($"  Culling Size: {frag.cullingSize}");
                Console.WriteLine($"  Model ID: {frag.modelID}");
                Console.WriteLine($"  off1C: 0x{frag.off1C:X4}");
                Console.WriteLine($"  off1E (Fragment ID): 0x{frag.off1E:X4}");
                Console.WriteLine($"  off20: 0x{frag.off20:X4}");
                Console.WriteLine($"  off24: 0x{frag.off24:X8}");
                Console.WriteLine($"  off28: 0x{frag.off28:X8}");
                Console.WriteLine($"  off2C: 0x{frag.off2C:X8}");

                // Dump some model info if available
                if (frag.model != null)
                {
                    var model = frag.model;
                    Console.WriteLine($"  Model:");
                    Console.WriteLine($"    ID: {model.id}");
                    Console.WriteLine($"    Vertex Count: {model.vertexBuffer.Length / (model is SkyboxModel ? 6 : 8)}");
                    Console.WriteLine($"    Face Count: {model.indexBuffer.Length / 3}");
                    Console.WriteLine($"    Texture Configs: {model.textureConfig?.Count ?? 0}");
                }
            }
            Console.WriteLine("=============================\n");
        }

        static void LoadGlobalRCAssets(
            string globalRc2Dir,
            out List<Model> globalArmorModels,
            out List<List<Texture>> globalArmorTextures,
            out List<Model> globalGadgetModels,
            out List<Texture> globalGadgetTextures)
        {
            Console.WriteLine($"Loading global RC2 assets from {globalRc2Dir}...");

            // Initialize output parameters with empty collections
            globalArmorModels = new List<Model>();
            globalArmorTextures = new List<List<Texture>>();
            globalGadgetModels = new List<Model>();
            globalGadgetTextures = new List<Texture>();

            try
            {
                // Check if directory exists
                if (!Directory.Exists(globalRc2Dir))
                {
                    Console.WriteLine($"❌ Global RC2 directory not found: {globalRc2Dir}");
                    return;
                }

                // TODO: Load armor assets
                string armorDir = Path.Combine(globalRc2Dir, "armor");
                if (Directory.Exists(armorDir))
                {
                    // Load armor models and textures logic
                    // This would typically involve loading .ps3 files for each armor set
                    Console.WriteLine($"  Loading armor assets from {armorDir}...");
                    // Example implementation would parse files in this directory
                }
                else
                {
                    Console.WriteLine($"  ⚠️ Armor directory not found: {armorDir}");
                }

                // TODO: Load gadget assets
                string gadgetDir = Path.Combine(globalRc2Dir, "gadget");
                if (Directory.Exists(gadgetDir))
                {
                    // Load gadget models and textures logic
                    Console.WriteLine($"  Loading gadget assets from {gadgetDir}...");
                    // Example implementation would parse files in this directory
                }
                else
                {
                    Console.WriteLine($"  ⚠️ Gadget directory not found: {gadgetDir}");
                }

                Console.WriteLine($"✅ Global RC2 assets loaded: {globalArmorModels.Count} armor models, {globalGadgetModels.Count} gadget models");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading global RC2 assets: {ex.Message}");
            }
        }

        static void FinalizeLevelProcessing(
             Level targetLevel, // This is the rc2Level that's being modified
             Level rc1SourceLevel,
             Level referenceRc2PlanetLevel, // For Moby templates
             List<Model> globalArmorModels,
             List<List<Texture>> globalArmorTextures,
             List<Model> globalGadgetModels,
             List<Texture> globalGadgetTextures,
             SwapOptions options,
             Level rc2DonorLevel) // Added parameter to access rc2DonorLevel
        {
            Console.WriteLine("🧪 TEXTURE SYSTEM CHECKPOINT MODE 🧪");
            Console.WriteLine("Testing terrain swap with original texture IDs preserved");

            // ----- 1. BASIC SETUP - ALWAYS NEEDED -----
            Console.WriteLine("Setting up level variables...");
            if (rc2DonorLevel.levelVariables != null && targetLevel.levelVariables != null)
            {
                // Store original values we want to preserve
                float originalDeathPlaneZ = rc1SourceLevel.levelVariables.deathPlaneZ;

                // Ensure correct ByteSize - critically important
                targetLevel.levelVariables.ByteSize = 0x88;

                // Keep all values from donor except death plane Z
                targetLevel.levelVariables.deathPlaneZ = originalDeathPlaneZ;

                Console.WriteLine($"  LevelVariables preserved from donor, with death plane Z = {originalDeathPlaneZ}");

                // Check if we should swap level variables
                if ((options & SwapOptions.SwapLevelVariables) != 0 && rc1SourceLevel.levelVariables != null)
                {
                    Console.WriteLine("  Swapping selected level variables from RC1 source level...");

                    // Swap background color
                    targetLevel.levelVariables.backgroundColor = rc1SourceLevel.levelVariables.backgroundColor;
                    Console.WriteLine($"    Background Color swapped from RC1: R={rc1SourceLevel.levelVariables.backgroundColor.R}, G={rc1SourceLevel.levelVariables.backgroundColor.G}, B={rc1SourceLevel.levelVariables.backgroundColor.B}");

                    // Swap fog color
                    targetLevel.levelVariables.fogColor = rc1SourceLevel.levelVariables.fogColor;
                    Console.WriteLine($"    Fog Color swapped from RC1: R={rc1SourceLevel.levelVariables.fogColor.R}, G={rc1SourceLevel.levelVariables.fogColor.G}, B={rc1SourceLevel.levelVariables.fogColor.B}");

                    // Swap ship position
                    targetLevel.levelVariables.shipPosition = rc1SourceLevel.levelVariables.shipPosition;
                    Console.WriteLine($"    Ship Position swapped from RC1: X={rc1SourceLevel.levelVariables.shipPosition.X}, Y={rc1SourceLevel.levelVariables.shipPosition.Y}, Z={rc1SourceLevel.levelVariables.shipPosition.Z}");

                    // Swap ship rotation
                    targetLevel.levelVariables.shipRotation = rc1SourceLevel.levelVariables.shipRotation;
                    Console.WriteLine($"    Ship Rotation swapped from RC1: {rc1SourceLevel.levelVariables.shipRotation}");

                    Console.WriteLine("  ✅ Level variables swapped successfully");
                }
            }

            // Check if we should transfer Ratchet's position
            if ((options & SwapOptions.TransferRatchetPosition) != 0)
            {
                Console.WriteLine("  Transferring Ratchet moby position and rotation from RC1 source level...");

                // Find Ratchet in RC1 source level (typically mobyID = 0)
                Moby? rc1Ratchet = rc1SourceLevel.mobs?.FirstOrDefault(m => m.mobyID == 415);

                // Find Ratchet in target level
                Moby? targetRatchet = targetLevel.mobs?.FirstOrDefault(m => m.mobyID == 179); // 808 for Boldan, 5 for Damosel, 179 for Smolg.

                if (rc1Ratchet != null && targetRatchet != null)
                {
                    // Store original values for logging
                    Vector3 originalPosition = targetRatchet.position;
                    Quaternion originalRotation = targetRatchet.rotation;

                    // Transfer position from RC1 Ratchet to target Ratchet
                    targetRatchet.position = rc1Ratchet.position;

                    // Transfer rotation from RC1 Ratchet to target Ratchet
                    targetRatchet.rotation = rc1Ratchet.rotation;

                    // Make sure the transform matrix is updated with the new position and rotation
                    targetRatchet.UpdateTransformMatrix();

                    Console.WriteLine($"    Ratchet position transferred from RC1: {rc1Ratchet.position}");
                    Console.WriteLine($"    Original position was: {originalPosition}");
                    Console.WriteLine($"    Ratchet rotation transferred from RC1: {rc1Ratchet.rotation}");
                    Console.WriteLine($"    Original rotation was: {originalRotation}");
                    Console.WriteLine("  ✅ Ratchet position and rotation transferred successfully");
                }
                else
                {
                    Console.WriteLine("  ❌ Could not transfer Ratchet position and rotation:");
                    Console.WriteLine($"    RC1 Ratchet found: {rc1Ratchet != null}");
                    Console.WriteLine($"    Target Ratchet found: {targetRatchet != null}");

                    if (rc1Ratchet != null)
                    {
                        Console.WriteLine($"    RC1 Ratchet position was: {rc1Ratchet.position}");
                        Console.WriteLine($"    RC1 Ratchet rotation was: {rc1Ratchet.rotation}");
                    }
                }
            }

            // ----- 2. TERRAIN SWAP WITHOUT TEXTURE REINDEXING -----
            if ((options & SwapOptions.Terrain) != 0 && rc1SourceLevel.terrainEngine != null)
            {
                Console.WriteLine("  Starting terrain transfer (preserving original texture IDs)...");

                // Make a deep copy of the RC1 terrain engine to avoid messing with the original
                Terrain terrainToTransfer = rc1SourceLevel.terrainEngine;

                // Assign the RC1 terrain to the RC2 level without modification
                targetLevel.terrainEngine = terrainToTransfer;

                // RC2-specific terrain fragment properties that need to be set
                Console.WriteLine("  Setting RC2-specific values on terrain fragments...");
                if (targetLevel.terrainEngine?.fragments != null)
                {
                    for (int i = 0; i < targetLevel.terrainEngine.fragments.Count; i++)
                    {
                        var frag = targetLevel.terrainEngine.fragments[i];

                        // Get the fragment's byte array to modify offset 0x22 (chunk index)
                        byte[] fragBytes = frag.ToByteArray();

                        // Set chunk index (offset 0x22) to 0 since we're dealing with a single-chunk level
                        WriteShort(fragBytes, 0x22, 0);

                        // RC2 standard values (based on TerrainDiagnostics recommendations)
                        frag.off1C = 0xFFFF;  // Common RC2 value
                        frag.off20 = 0xFF00;  // Common RC2 value
                        frag.off24 = 0;       // Common RC2 value
                        frag.off28 = 0;       // Common RC2 value
                        frag.off2C = 0;       // Common RC2 value

                        // IMPORTANT: Do NOT change any texture IDs
                        // DO NOT reindex the fragment's model texture config

                        // Make sure transformation matrix is updated after these changes
                        frag.UpdateTransformMatrix();
                    }
                }

                if (targetLevel.terrainEngine?.fragments != null && targetLevel.terrainEngine.fragments.Count > 0)
                {
                    Console.WriteLine($"  Terrain swapped with {targetLevel.terrainEngine.fragments.Count} fragments (original texture IDs preserved).");
                }
                else
                {
                    Console.WriteLine("  Terrain swap skipped due to null terrain engine or fragments.");
                }

                // Optional: Dump the first few fragments to check their texture IDs
                if (targetLevel.terrainEngine?.fragments != null && targetLevel.terrainEngine.fragments.Count > 0)
                {
                    Console.WriteLine("\n  Texture ID check on first 5 terrain fragments:");
                    for (int i = 0; Math.Min(5, targetLevel.terrainEngine.fragments.Count) > i; i++)
                    {
                        var frag = targetLevel.terrainEngine.fragments[i];
                        if (frag.model?.textureConfig != null)
                        {
                            Console.WriteLine($"    Fragment {i}: {frag.model.textureConfig.Count} texture configs");
                            for (int j = 0; j < frag.model.textureConfig.Count; j++)
                            {
                                Console.WriteLine($"      TextureConfig {j} references texture ID: {frag.model.textureConfig[j].id}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"    Fragment {i}: No texture configs");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("  Terrain swap skipped.");
            }

            // ----- Add Collision Support -----
            if ((options & SwapOptions.Collision) != 0)
            {
                Console.WriteLine("📦 Copying RC1 collision data into RC2 level...");

                if (rc1SourceLevel.collisionEngine != null)
                {
                    // Copy the collision model from RC1 to RC2
                    Console.WriteLine("  Copying main collision model...");
                    targetLevel.collisionEngine = rc1SourceLevel.collisionEngine;

                    // Copy the collision bytes (raw data needed for serialization)
                    if (rc1SourceLevel.collBytesEngine != null && rc1SourceLevel.collBytesEngine.Length > 0)
                    {
                        targetLevel.collBytesEngine = rc1SourceLevel.collBytesEngine;
                        Console.WriteLine($"  Copied collision bytes: {targetLevel.collBytesEngine.Length} bytes");
                    }
                    else
                    {
                        Console.WriteLine("  Warning: No collision bytes found in RC1 source level");
                    }

                    Console.WriteLine("✅ Main collision model copied from RC1 to RC2");
                }
                else
                {
                    Console.WriteLine("❌ No collision model found in RC1 source level");
                }

                // Handle collision chunks if any exist
                if (rc1SourceLevel.collisionChunks != null && rc1SourceLevel.collisionChunks.Count > 0)
                {
                    Console.WriteLine($"  Found {rc1SourceLevel.collisionChunks.Count} collision chunks in RC1 source");

                    // Initialize collision chunks in target level if needed
                    if (targetLevel.collisionChunks == null)
                    {
                        targetLevel.collisionChunks = new List<Collision>();
                    }
                    else
                    {
                        targetLevel.collisionChunks.Clear();
                    }

                    // Initialize collision bytes chunks in target level if needed
                    if (targetLevel.collBytesChunks == null)
                    {
                        targetLevel.collBytesChunks = new List<byte[]>();
                    }
                    else
                    {
                        targetLevel.collBytesChunks.Clear();
                    }

                    // Copy each chunk's collision data
                    for (int i = 0; i < rc1SourceLevel.collisionChunks.Count; i++)
                    {
                        targetLevel.collisionChunks.Add(rc1SourceLevel.collisionChunks[i]);

                        if (rc1SourceLevel.collBytesChunks != null && i < rc1SourceLevel.collBytesChunks.Count)
                        {
                            targetLevel.collBytesChunks.Add(rc1SourceLevel.collBytesChunks[i]);
                        }

                        Console.WriteLine($"  Copied collision chunk {i}");
                    }
                }

                Console.WriteLine("✅ Collision data transfer complete");
            }
            else
            {
                Console.WriteLine("  Collision swap skipped.");
            }

            // ----- Handle Tie swapping -----
            if ((options & SwapOptions.Ties) != 0 && rc1SourceLevel.ties != null && rc1SourceLevel.ties.Count > 0)
            {
                Console.WriteLine("\n==== Swapping TIEs with RC1 Oltanis TIEs ====");

                // Configure options for TIE swapping
                TieSwapper.TieSwapOptions tieOptions = TieSwapper.TieSwapOptions.FullSwap;

                // Ask the user if they want to configure TIE swapping options
                if (GetYesNoInput("Would you like to configure TIE swapping options? (Y/N): "))
                {
                    Console.WriteLine("\nConfiguring TIE swap options:");

                    Console.WriteLine("1. Full swap (placements, models, and textures)");
                    Console.WriteLine("2. Placements and models only (no texture mapping)");
                    Console.WriteLine("3. Placements only (use RC2 models)");
                    Console.WriteLine("4. Custom options");
                    Console.Write("> ");

                    string choice = Console.ReadLine()?.Trim() ?? "1";

                    switch (choice)
                    {
                        case "1":
                            tieOptions = TieSwapper.TieSwapOptions.FullSwap;
                            break;
                        case "2":
                            tieOptions = TieSwapper.TieSwapOptions.PlacementsAndModels;
                            break;
                        case "3":
                            tieOptions = TieSwapper.TieSwapOptions.PlacementsOnly;
                            break;
                        case "4":
                            tieOptions = TieSwapper.TieSwapOptions.None;

                            if (GetYesNoInput("Use RC1 TIE placements? (Y/N): "))
                                tieOptions |= TieSwapper.TieSwapOptions.UseRC1Placements;

                            if (GetYesNoInput("Use RC1 TIE models? (Y/N): "))
                                tieOptions |= TieSwapper.TieSwapOptions.UseRC1Models;

                            if (GetYesNoInput("Map RC1 textures to RC2? (Y/N): "))
                                tieOptions |= TieSwapper.TieSwapOptions.MapTextures;
                            break;
                        default:
                            tieOptions = TieSwapper.TieSwapOptions.FullSwap;
                            break;
                    }
                }

                // Perform the TIE swap using our new TieSwapper class
                bool success = TieSwapper.SwapTiesWithRC1Oltanis(targetLevel, rc1SourceLevel, tieOptions);

                if (success)
                {
                    Console.WriteLine($"✅ Successfully swapped TIEs from RC1 Oltanis to RC2 level");
                    if (targetLevel.ties != null)
                    {
                        Console.WriteLine($"   Total TIEs in target level: {targetLevel.ties.Count}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to swap TIEs");
                }

                // Add TIE diagnostics after TIE swap is complete
                Console.WriteLine("\n==== Running TIE diagnostics after swap ====");

                // Create a diagnostics directory if it doesn't exist
                string tieDiagnosticsDir = Path.Combine(outputDir, "diagnostics");
                Directory.CreateDirectory(tieDiagnosticsDir);

                // Run analysis on the modified target level
                string targetLevelDiagFile = Path.Combine(tieDiagnosticsDir, "target_level_ties_analysis.txt");
                TieDiagnostics.AnalyzeTies(targetLevel, targetLevelDiagFile, "Target_Level_After_Swap");

                // Compare with the RC1 source level
                string comparisonFile = Path.Combine(tieDiagnosticsDir, "rc1_vs_target_ties_comparison.txt");
                TieDiagnostics.CompareTies(rc1SourceLevel, targetLevel, comparisonFile, "RC1_Source", "Target_Level");

                Console.WriteLine($"✅ TIE diagnostics complete. Reports saved to {tieDiagnosticsDir}");

                // Ask if user wants to view the diagnostics summary
                if (GetYesNoInput("\nWould you like to see a brief TIE diagnostics summary? (Y/N): "))
                {
                    try
                    {
                        // Display a brief summary from the comparison report
                        using (var reader = new StreamReader(comparisonFile))
                        {
                            // Skip the header lines
                            for (int i = 0; i < 5; i++) reader.ReadLine();

                            // Display basic statistics and risk assessment
                            Console.WriteLine("\n--- TIE DIAGNOSTICS SUMMARY ---");

                            // Read and display the basic statistics section
                            string line;
                            bool foundRiskSection = false;

                            while ((line = reader.ReadLine()) != null)
                            {
                                // Display basic statistics
                                if (line.Contains("BASIC STATISTICS"))
                                {
                                    Console.WriteLine("\n" + line);
                                    // Print next few lines of statistics
                                    for (int i = 0; i < 5 && (line = reader.ReadLine()) != null; i++)
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            Console.WriteLine(line);
                                    }
                                }

                                // Display risk assessment when we reach it
                                if (line.Contains("CRASH RISK ASSESSMENT"))
                                {
                                    foundRiskSection = true;
                                    Console.WriteLine("\n" + line);
                                    // Print risk level and recommendations
                                    for (int i = 0; i < 3 && (line = reader.ReadLine()) != null; i++)
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            Console.WriteLine(line);
                                    }
                                }

                                // Display most important recommendations
                                if (line.Contains("RECOMMENDATIONS"))
                                {
                                    Console.WriteLine("\n" + line);
                                    // Print first few recommendations
                                    for (int i = 0; i < 5 && (line = reader.ReadLine()) != null; i++)
                                    {
                                        if (line.StartsWith("•"))
                                            Console.WriteLine(line);
                                    }
                                }
                            }

                            if (!foundRiskSection)
                            {
                                Console.WriteLine("\nNo crash risk assessment found in the diagnostics report.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error displaying diagnostics summary: {ex.Message}");
                    }

                    Console.WriteLine("\nFull diagnostic reports are available in the diagnostics directory.");
                }
            }
            else
            {
                Console.WriteLine("  TIE swap skipped.");
            }

            // Add shrub swapping here
            SwapShrubsIfRequested(targetLevel, rc1SourceLevel, options);

            // Add skybox swapping here
            SwapSkyboxIfRequested(targetLevel, rc1SourceLevel, rc2DonorLevel, options);

            // Add point lights swapping here
            SwapPointLightsIfRequested(targetLevel, rc1SourceLevel, options);

            // Add sound instance swapping here
            SwapSoundInstancesIfRequested(targetLevel, rc1SourceLevel, options);

            // Add grind path swapping here
            SwapGrindPathsIfRequested(targetLevel, rc1SourceLevel, options);

            // Add moby swapping here
            SwapMobysIfRequested(targetLevel, rc1SourceLevel, referenceRc2PlanetLevel, options);

            // Add Swingshot swapping here
            SwapSwingshotsIfRequested(targetLevel, rc1SourceLevel, options);

            // Add vendor swapping here
            if ((options & SwapOptions.SwapVendorWithOltanis) != 0)
            {
                Console.WriteLine("\n==== Swapping Vendor with RC1 Oltanis Vendor ====");
                bool result = VendorOltanisSwapper.SwapVendorWithRC1Oltanis(targetLevel, rc1SourceLevel);
                Console.WriteLine(result ? "✅ Vendor swap complete" : "❌ Vendor swap failed");
            }

            if ((options & SwapOptions.SwapCratesWithOltanis) != 0)
            {
                Console.WriteLine("\n==== Swapping Crates with RC1 Oltanis Crates ====");
                bool result = CratesOltanisSwapper.SwapCratesWithRC1Oltanis(targetLevel, rc1SourceLevel);
                Console.WriteLine(result ? "✅ Crate swap complete" : "❌ Crate swap failed");
            }

            // ----- 3. IMPORT RC1 TEXTURES TO RC2 LEVEL -----
            if ((options & SwapOptions.Terrain) != 0)
            {
                Console.WriteLine("  Importing RC1 textures to preserve original visual appearance...");
                ImportRC1TexturesToRC2Level(targetLevel, rc1SourceLevel);
            }
            else
            {
                Console.WriteLine("  Texture import skipped (terrain swap not enabled).");
            }

            // ----- 4. NO CHANGES TO MODEL LISTS OR TEXTURE INDICES -----
            Console.WriteLine("  Preserving original texture list and model lists from donor level.");

            // ----- 5. NO EMPLACE COMMON DATA -----
            Console.WriteLine("  Skipping EmplaceCommonData() to avoid texture reindexing.");

            // ----- 6. REBUILD ID LISTS FOR HEADERS -----
            // This is needed for serialization to work properly
            if (targetLevel.mobs != null) targetLevel.mobyIds = targetLevel.mobs.Select(m => m.mobyID).ToList();
            if (targetLevel.ties != null) targetLevel.tieIds = targetLevel.ties.Select(t => t.modelID).ToList();
            if (targetLevel.shrubs != null) targetLevel.shrubIds = targetLevel.shrubs.Select(s => s.modelID).ToList();

            Console.WriteLine("🧪 TERRAIN SWAP COMPLETE - Original texture references preserved");

            // ----- 6. REBUILD ID LISTS FOR HEADERS AND ENSURE PROPER PADDING -----
            Console.WriteLine("\n🔄 Finalizing header tables and ensuring proper alignment...");

            // Update the tieIds collection in the header (references from ties to models)
            if (targetLevel.ties != null)
            {
                targetLevel.tieIds = targetLevel.ties.Select(t => t.modelID).ToList();
                Console.WriteLine($"  ✅ Updated tieIds header list with {targetLevel.tieIds.Count} entries");
            }

            // Make sure the tieGroupData ends on an 0x80 boundary for proper alignment
            if (targetLevel.tieGroupData != null && targetLevel.tieGroupData.Length > 0)
            {
                int colorOffset = targetLevel.tieGroupData.Length;
                if (colorOffset % 0x80 != 0)
                {
                    int newSize = ((colorOffset / 0x80) + 1) * 0x80;
                    Array.Resize(ref targetLevel.tieGroupData, newSize);
                    Console.WriteLine($"  ✅ Padded tieGroupData from {colorOffset} to {newSize} bytes for proper alignment");
                }
            }

            Console.WriteLine("✅ Header tables updated and proper alignment ensured");

            // Add planet registration if requested
            if ((options & SwapOptions.RegisterPlanetInMap) != 0)
            {
                Console.WriteLine("\n=== REGISTERING PLANET IN GALACTIC MAP ===");
                
                // Ask for planet details
                Console.Write("Enter planet name to display on the map: ");
                string planetName = Console.ReadLine() ?? "Oltanis";
                
                Console.Write("Enter city name to display on the map: ");
                string cityName = Console.ReadLine() ?? "Gorda City Ruins";
                
                Console.Write("Enter planet/level ID (numeric): ");
                if (!int.TryParse(Console.ReadLine() ?? "27", out int planetId))
                {
                    planetId = 27; // Default to 27 if invalid input
                }
                
                bool isAvailable = GetYesNoInput("Mark planet as available? (Y/N): ");
                
                // Ask if the user wants to update the all_text file
                bool patchAllText = GetYesNoInput("Update the all_text file with planet name? (Y/N): ");
                
                string? gamePath = null;
                if (patchAllText)
                {
                    Console.Write("Enter path to the game's ps3data directory: ");
                    gamePath = Console.ReadLine() ?? globalRc2Dir;
                    
                    // Make sure we have a valid path
                    if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                    {
                        Console.WriteLine("⚠️ Warning: The provided ps3data path does not exist. Using globalRc2Dir as fallback.");
                        gamePath = globalRc2Dir;
                    }
                }
                
                // Call the planet registration method with the additional parameters
                bool success = GalacticMapManager.AddPlanetToGalacticMap(
                    targetLevel, 
                    planetName, 
                    cityName, 
                    planetId, 
                    isAvailable, 
                    patchAllText,
                    gamePath ?? globalRc2Dir  // Make sure to provide a non-null value
                );
                
                if (success)
                {
                    Console.WriteLine($"✅ Planet '{planetName}' with city '{cityName}' registered successfully with ID {planetId}");
                    Console.WriteLine("\n==== Final pVar Index Validation ====");
                    MobySwapper.ValidateAndFixPvarIndices(targetLevel);
                    if ((options & SwapOptions.CreateMobyInstances) != 0)
                    {
                        Console.WriteLine("\n==== Creating Moby Instances with RC1 Oltanis Positions ====");

                        // Ask for model IDs
                        Console.WriteLine("Enter the model IDs you want to create instances for (comma-separated, e.g. '122,345'):");
                        Console.Write("> ");
                        string input = Console.ReadLine()?.Trim() ?? "";

                        // Parse model IDs
                        List<int> selectedModelIds = new List<int>();
                        foreach (string part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (int.TryParse(part.Trim(), out int modelId))
                            {
                                selectedModelIds.Add(modelId);
                            }
                        }

                        if (selectedModelIds.Count > 0)
                        {
                            // Ask for options before creating instances
                            Console.WriteLine("\nSelect options for Moby Instancer:");
                            Console.WriteLine("1. Default (light=0, use RC2 template) [default]");
                            Console.WriteLine("2. Custom options");
                            Console.Write("> ");
                            string choice = Console.ReadLine()?.Trim() ?? "1";

                            MobyOltanisInstancer.InstancerOptions instancerOptions;
                            if (choice == "2")
                            {
                                instancerOptions = MobyOltanisInstancer.InstancerOptions.None;

                                if (GetYesNoInput("Set light value to 0? (Y/N): "))
                                    instancerOptions |= MobyOltanisInstancer.InstancerOptions.SetLightToZero;

                                if (GetYesNoInput("Use existing moby as template for properties? (Y/N): "))
                                    instancerOptions |= MobyOltanisInstancer.InstancerOptions.UseRC2Template;

                                if (GetYesNoInput("Copy pVars from source level mobys? (Y/N): "))
                                    instancerOptions |= MobyOltanisInstancer.InstancerOptions.CopyPvars;
                            }
                            else
                            {
                                instancerOptions = MobyOltanisInstancer.InstancerOptions.Default;
                            }

                            bool result = MobyOltanisInstancer.CreateMobyInstancesFromLevel(
                                targetLevel,
                                rc1SourceLevel,
                                selectedModelIds.ToArray(),
                                instancerOptions);

                            Console.WriteLine(result ? "✅ Moby instances created successfully" : "❌ Failed to create moby instances");
                        }
                        else
                        {
                            Console.WriteLine("❌ No valid model IDs provided");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Failed to register planet '{planetName}' with city '{cityName}'");
                }
            }
        }

        static void SwapMobysIfRequested(Level targetLevel, Level rc1SourceLevel, Level referenceRc2PlanetLevel, SwapOptions options)
        {
            if ((options & SwapOptions.Mobys) != 0)
            {
                Console.WriteLine("\n==== Copying Special Mobys ====");

                if (referenceRc2PlanetLevel == null)
                {
                    Console.WriteLine("❌ Reference RC2 planet (Damosel) is required but not loaded");
                    return;
                }

                if (rc1SourceLevel == null)
                {
                    Console.WriteLine("❌ RC1 source level (Oltanis) is required but not loaded");
                    return;
                }

                // Ask user which approach they want to use
                Console.WriteLine("\nSelect moby transfer method:");
                Console.WriteLine("1. Copy RC1 mobys (traditional - may crash in-game)");
                Console.WriteLine("2. Copy RC2 mobys with RC1 models (all selected types)");
                Console.WriteLine("3. Copy RC2 mobys with selective model replacement (recommended)");
                Console.WriteLine("4. Cancel");
                Console.Write("> ");

                string choice = Console.ReadLine()?.Trim() ?? "";

                if (choice == "1")
                {
                    Console.WriteLine("\nCopying specific mobys from RC1 level...");

                    // Ask user which moby types to copy
                    List<string> mobyTypes = GetSelectedMobyTypes();

                    if (mobyTypes.Count > 0)
                    {
                        bool result = MobySwapper.CopyMobysToLevel(targetLevel, rc1SourceLevel, true, mobyTypes);
                        Console.WriteLine(result ? "✅ Mobys copied successfully" : "❌ Failed to copy mobys");
                    }
                    else
                    {
                        Console.WriteLine("❌ No moby types selected, skipping");
                    }
                }
                else if (choice == "2")
                {
                    Console.WriteLine("\nCopying RC2 mobys with RC1 models...");

                    // Ask user which moby types to replace with RC1 models
                    List<string> mobyTypes = GetSelectedMobyTypes();

                    if (mobyTypes.Count > 0)
                    {
                        bool result = MobySwapper.CopyRC2MobysWithRC1Models(
                            targetLevel,
                            referenceRc2PlanetLevel,
                            rc1SourceLevel,
                            mobyTypes);

                        Console.WriteLine(result ? "✅ Mobys copied successfully with RC1 models" : "❌ Failed to copy mobys");
                    }
                    else
                    {
                        Console.WriteLine("❌ No moby types selected, skipping");
                    }
                }
                else if (choice == "3")
                {
                    Console.WriteLine("\nCopying RC2 mobys with selective model replacement...");

                    // First, ask which moby types to include
                    List<string> selectedMobyTypes = GetSelectedMobyTypes();

                    if (selectedMobyTypes.Count == 0)
                    {
                        Console.WriteLine("❌ No moby types selected, skipping");
                        return;
                    }

                    // Dictionary to track which moby types should use RC1 models
                    Dictionary<string, bool> modelSourcePreference = new Dictionary<string, bool>();

                    Console.WriteLine("\nFor each selected moby type, choose whether to use RC1 or RC2 models:");

                    foreach (string mobyType in selectedMobyTypes)
                    {
                        // Display moby info
                        int[] modelIds = MobySwapper.MobyTypes[mobyType];
                        Console.WriteLine($"\n{mobyType} (Model IDs: {string.Join(", ", modelIds)})");

                        Console.Write("Use RC1 model for this moby type? (Y/N, default: Y): ");
                        string useRc1 = Console.ReadLine()?.Trim().ToUpper() ?? "Y";

                        // If Y or empty, use RC1 model; otherwise use RC2 model
                        modelSourcePreference[mobyType] = (useRc1 == "Y" || useRc1 == "");

                        Console.WriteLine($"Selected: {(modelSourcePreference[mobyType] ? "RC1 model" : "RC2 model")} for {mobyType}");
                    }

                    // Get only the moby types that should use RC1 models
                    List<string> rc1ModelTypes = modelSourcePreference
                        .Where(kvp => kvp.Value)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    // Get only the moby types that should use RC2 models
                    List<string> rc2ModelTypes = modelSourcePreference
                        .Where(kvp => !kvp.Value)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    Console.WriteLine("\nProcessing moby copy with selective model replacement...");

                    // Copy RC2 mobys with RC1 models for the selected types
                    if (rc1ModelTypes.Count > 0)
                    {
                        Console.WriteLine($"\nCopying {rc1ModelTypes.Count} moby types with RC1 models: {string.Join(", ", rc1ModelTypes)}");
                        bool rc1Result = MobySwapper.CopyRC2MobysWithRC1Models(
                            targetLevel,
                            referenceRc2PlanetLevel,
                            rc1SourceLevel,
                            rc1ModelTypes);

                        Console.WriteLine(rc1Result ? "✅ Mobys with RC1 models copied successfully" : "❌ Failed to copy mobys with RC1 models");
                    }

                    // Copy RC2 mobys with their original RC2 models for the remaining types
                    if (rc2ModelTypes.Count > 0)
                    {
                        Console.WriteLine($"\nCopying {rc2ModelTypes.Count} moby types with original RC2 models: {string.Join(", ", rc2ModelTypes)}");

                        // Pass rc1SourceLevel (Oltanis) as the position reference
                        bool rc2Result = MobySwapper.CopyRC2MobysPreservingModels(
                            targetLevel,
                            referenceRc2PlanetLevel,
                            rc1SourceLevel,  // Pass RC1 level for position data
                            rc2ModelTypes);

                        Console.WriteLine(rc2Result ? "✅ Mobys with RC2 models copied successfully" : "❌ Failed to copy mobys with RC2 models");
                    }

                    Console.WriteLine("\n✅ Selective model replacement completed");
                }
                else
                {
                    Console.WriteLine("⏭️ Moby copying skipped");
                }

                // Always ensure vendor support models are available, regardless of the option chosen above
                Console.WriteLine("\nEnsuring required vendor support models are available...");
                MobySwapper.ImportVendorSupportModels(targetLevel, referenceRc2PlanetLevel);
                Console.WriteLine("✅ Vendor support models checked");
            }
            else
            {
                Console.WriteLine("⏭️ Moby operations skipped (not enabled in options)");
            }

            Console.WriteLine("\n==== Fixing Swingshot Node pVars ====");
            MobySwapper.FixSwingshotNodePvars(targetLevel, rc1SourceLevel, MobySwapper.MobyTypes["SwingshotNode"]);

            Console.WriteLine("\n==== Validating pVar Indices ====");
            MobySwapper.ValidateAndFixPvarIndices(targetLevel);
        }

        static void SwapSwingshotsIfRequested(Level targetLevel, Level rc1SourceLevel, SwapOptions options)
        {
            if (options.HasFlag(SwapOptions.SwapSwingshots))
            {
                Console.WriteLine("\n=== Swapping Swingshots ===");

                // Use default options for the swingshot swapper
                bool success = SwingshotOltanisSwapper.SwapSwingshotsWithRC1Oltanis(
                    targetLevel,
                    rc1SourceLevel,
                    SwingshotOltanisSwapper.SwingshotSwapOptions.Default);

                if (success)
                    Console.WriteLine("✅ Successfully swapped swingshots");
                else
                    Console.WriteLine("❌ Failed to swap swingshots");
            }
        }

        static void SwapShrubsIfRequested(Level targetLevel, Level rc1SourceLevel, SwapOptions options)
        {
            if ((options & SwapOptions.Shrubs) != 0)
            {
                Console.WriteLine("\n==== Swapping Shrubs with RC1 Oltanis Shrubs ====");

                try
                {
                    // Ask for the specific options to use
                    Console.WriteLine("\nSelect shrub swap method:");
                    Console.WriteLine("1. Full replacement (RC1 models, positions, AND textures)");
                    Console.WriteLine("2. RC1 models and positions (no texture mapping)");
                    Console.WriteLine("3. Positions only (keep RC2 models but use RC1 positions)");
                    Console.WriteLine("4. Custom options");
                    Console.Write("> ");

                    string choice = Console.ReadLine()?.Trim() ?? "1";
                    ShrubSwapper.ShrubSwapOptions shrubOptions;

                    switch (choice)
                    {
                        case "1":
                            shrubOptions = ShrubSwapper.ShrubSwapOptions.FullSwap;
                            break;
                        case "2":
                            shrubOptions = ShrubSwapper.ShrubSwapOptions.PlacementsAndModels;
                            break;
                        case "3":
                            shrubOptions = ShrubSwapper.ShrubSwapOptions.PlacementsOnly;
                            break;
                        case "4":
                            shrubOptions = ShrubSwapper.ShrubSwapOptions.None;

                            if (GetYesNoInput("Use RC1 shrub placements? (Y/N): "))
                                shrubOptions |= ShrubSwapper.ShrubSwapOptions.UseRC1Placements;

                            if (GetYesNoInput("Use RC1 shrub models? (Y/N): "))
                                shrubOptions |= ShrubSwapper.ShrubSwapOptions.UseRC1Models;

                            if (GetYesNoInput("Map RC1 textures to RC2? (Y/N): "))
                                shrubOptions |= ShrubSwapper.ShrubSwapOptions.MapTextures;
                            break;
                        default:
                            shrubOptions = ShrubSwapper.ShrubSwapOptions.FullSwap;
                            break;
                    }

                    // Pre-swap diagnostics
                    Console.WriteLine("\n--- PRE-SWAP DIAGNOSTICS ---");
                    Console.WriteLine($"Target level has {targetLevel.shrubs?.Count ?? 0} shrubs");
                    Console.WriteLine($"RC1 source level has {rc1SourceLevel.shrubs?.Count ?? 0} shrubs");
                    Console.WriteLine($"Target level has {targetLevel.shrubModels?.Count ?? 0} shrub models");

                    // Ensure collections are initialized
                    if (targetLevel.shrubs == null)
                        targetLevel.shrubs = new List<Shrub>();

                    if (targetLevel.shrubModels == null)
                        targetLevel.shrubModels = new List<Model>();

                    // Call the non-interactive version directly with already-loaded levels
                    bool success = ShrubSwapper.SwapShrubsWithRC1Oltanis(
                        targetLevel,
                        rc1SourceLevel,
                        shrubOptions
                    );

                    // Additional checks after the swap
                    if (success)
                    {
                        // Final validation
                        Console.WriteLine("\n--- POST-SWAP VALIDATION ---");
                        Console.WriteLine($"Target level now has {targetLevel.shrubs.Count} shrubs");
                        Console.WriteLine($"Target level now has {targetLevel.shrubModels.Count} shrub models");

                        // Update shrub IDs list
                        if (targetLevel.shrubs != null)
                        {
                            targetLevel.shrubIds = targetLevel.shrubs.Select(s => s.modelID).ToList();
                            Console.WriteLine($"✅ Updated shrubIds list with {targetLevel.shrubIds.Count} entries");
                        }

                        Console.WriteLine("✅ Successfully transferred shrubs from RC1 to RC2 level");
                    }
                    else
                    {
                        Console.WriteLine("❌ Failed to transfer shrubs");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Critical error in shrub swapping: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        static List<string> GetSelectedMobyTypes()
        {
            List<string> allMobyTypes = MobySwapper.MobyTypes.Keys.ToList();
            List<string> selectedTypes = new List<string>();

            Console.WriteLine("\nAvailable moby types:");

            for (int i = 0; i < allMobyTypes.Count; i++)
            {
                string mobyType = allMobyTypes[i];
                int[] modelIds = MobySwapper.MobyTypes[mobyType];
                Console.WriteLine($"{i + 1}. {mobyType} (Model IDs: {string.Join(", ", modelIds)})");
            }

            Console.WriteLine($"{allMobyTypes.Count + 1}. All types");
            Console.WriteLine($"{allMobyTypes.Count + 2}. None");

            Console.Write("\nEnter numbers of moby types to include (comma-separated, e.g. '1,3,5'): ");
            string input = Console.ReadLine()?.Trim() ?? "";

            if (input == (allMobyTypes.Count + 1).ToString())
            {
                // User selected "All types"
                return allMobyTypes;
            }
            else if (input == (allMobyTypes.Count + 2).ToString() || string.IsNullOrWhiteSpace(input))
            {
                // User selected "None" or gave empty input
                return new List<string>();
            }

            // Parse the comma-separated list of numbers
            string[] parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string part in parts)
            {
                if (int.TryParse(part, out int index) && index >= 1 && index <= allMobyTypes.Count)
                {
                    selectedTypes.Add(allMobyTypes[index - 1]);
                }
            }

            Console.WriteLine($"\nSelected moby types: {string.Join(", ", selectedTypes)}");
            return selectedTypes;
        }

        static void SwapSkyboxIfRequested(Level targetLevel, Level rc1SourceLevel, Level rc2DonorLevel, SwapOptions options)
        {
            if ((options & SwapOptions.Skybox) != 0)
            {
                Console.WriteLine("\n==== Swapping Skybox ====");
                
                if (rc1SourceLevel.skybox == null)
                {
                    Console.WriteLine("❌ The RC1 source level doesn't have a skybox to transfer");
                    return;
                }
                
                if (targetLevel.skybox == null && rc2DonorLevel.skybox == null)
                {
                    Console.WriteLine("❌ Neither the target level nor the RC2 donor has a skybox structure to use as reference");
                    return;
                }
                
                bool success = SkyboxSwapper.SwapSkybox(targetLevel, rc1SourceLevel, rc2DonorLevel);
                
                if (success)
                {
                    Console.WriteLine("✅ Successfully swapped skybox from RC1 source to RC2 level");
                    
                    // If we have vertex data in the skybox, display some information about it
                    if (targetLevel.skybox != null && targetLevel.skybox.vertexBuffer != null)
                    {
                        int vertexCount = targetLevel.skybox.vertexBuffer.Length / SkyboxModel.VERTELEMSIZE;
                        Console.WriteLine($"  Skybox has {vertexCount} vertices and {targetLevel.skybox.textureConfig?.Count ?? 0} texture configs");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to swap skybox");
                }
            }
        }

        static void SwapGrindPathsIfRequested(Level targetLevel, Level rc1SourceLevel, SwapOptions options)
        {
            if ((options & SwapOptions.GrindPaths) != 0)
            {
                Console.WriteLine("\n==== Swapping Grind Paths ====");

                try
                {
                    // Ask for the specific options to use
                    Console.WriteLine("\nSelect grind path swap method:");
                    Console.WriteLine("1. Reposition existing paths only");
                    Console.WriteLine("2. Full replacement (reposition + add missing paths)");
                    Console.Write("> ");

                    string choice = Console.ReadLine()?.Trim() ?? "1";
                    GrindPathSwapper.GrindPathSwapOptions pathOptions;

                    if (choice == "1")
                        pathOptions = GrindPathSwapper.GrindPathSwapOptions.UseRC1Positions;
                    else
                        pathOptions = GrindPathSwapper.GrindPathSwapOptions.FullReplacement;

                    // Pre-swap diagnostics
                    Console.WriteLine("\n--- PRE-SWAP DIAGNOSTICS ---");
                    Console.WriteLine($"Target level has {targetLevel.grindPaths?.Count ?? 0} grind paths");
                    Console.WriteLine($"RC1 source level has {rc1SourceLevel.grindPaths?.Count ?? 0} grind paths");
                    Console.WriteLine($"Target level has {targetLevel.splines?.Count ?? 0} splines");

                    // Ensure collections are initialized
                    if (targetLevel.grindPaths == null)
                        targetLevel.grindPaths = new List<GrindPath>();

                    if (targetLevel.splines == null)
                        targetLevel.splines = new List<Spline>();

                    // Call the non-interactive version directly with already-loaded levels
                    bool success = GrindPathSwapper.SwapGrindPathsWithRC1Oltanis(
                        targetLevel,
                        rc1SourceLevel,
                        pathOptions
                    );

                    // Additional checks after the swap
                    if (success)
                    {
                        // Final validation
                        Console.WriteLine("\n--- POST-SWAP VALIDATION ---");
                        Console.WriteLine($"Target level now has {targetLevel.grindPaths.Count} grind paths");
                        Console.WriteLine($"Target level now has {targetLevel.splines.Count} splines");

                        // Check for duplicate spline IDs
                        var duplicateIds = targetLevel.splines
                            .GroupBy(s => s.id)
                            .Where(g => g.Count() > 1)
                            .ToList();

                        if (duplicateIds.Any())
                        {
                            Console.WriteLine($"⚠️ Found {duplicateIds.Count} duplicate spline ID groups. These will be fixed during save.");
                        }

                        Console.WriteLine($"✅ Successfully transferred grind paths from RC1 to RC2 level");

                        // Report on a few paths if available
                        if (targetLevel.grindPaths.Count > 0)
                        {
                            Console.WriteLine("   Sample of paths in target level:");
                            int sampleSize = Math.Min(3, targetLevel.grindPaths.Count);
                            for (int i = 0; i < sampleSize; i++)
                            {
                                var path = targetLevel.grindPaths[i];
                                Console.WriteLine($"     Path {i}: Position={path.position}, Radius={path.radius}, " +
                                                $"Spline points={(path.spline?.GetVertexCount() ?? 0)}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Failed to transfer grind paths");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Critical error in grind path swapping: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        static void SwapSoundInstancesIfRequested(Level targetLevel, Level rc1SourceLevel, SwapOptions options)
        {
            if ((options & SwapOptions.SoundInstances) != 0)
            {
                Console.WriteLine("\n==== Swapping Sound Instances ====");
                
                if (rc1SourceLevel.soundInstances == null || rc1SourceLevel.soundInstances.Count == 0)
                {
                    Console.WriteLine("❌ The RC1 source level doesn't have any sound instances to transfer");
                    return;
                }
                
                bool success = SoundInstanceSwapper.SwapSoundInstances(targetLevel, rc1SourceLevel);
                
                if (success)
                {
                    Console.WriteLine($"✅ Successfully transferred {rc1SourceLevel.soundInstances.Count} sound instances from RC1 to RC2 level");
                }
                else
                {
                    Console.WriteLine("❌ Failed to transfer sound instances");
                }
            }
        }

        static void SwapPointLightsIfRequested(Level targetLevel, Level rc1SourceLevel, SwapOptions options)
        {
            if ((options & SwapOptions.PointLights) != 0)
            {
                Console.WriteLine("\n==== Swapping Point Lights with RC1 Oltanis ====");
                bool success = PointLightsSwapper.SwapPointLights(targetLevel, rc1SourceLevel);
                Console.WriteLine(success ? "✅ Point lights swap complete" : "❌ Point lights swap failed");

                // Additionally, update Ratchet's light value to 0 to match RC1 Oltanis
                UpdateRatchetLightValue(targetLevel);
            }
        }

        static void UpdateRatchetLightValue(Level targetLevel)
        {
            // Find Ratchet moby - typically has mobyID 179 in RC2
            var ratchet = targetLevel.mobs?.FirstOrDefault(m => m.mobyID == 179);

            if (ratchet != null)
            {
                // Store original light value for logging
                int originalLight = ratchet.light;

                // Set light to 0 to match RC1 Oltanis
                ratchet.light = 0;

                Console.WriteLine($"✅ Updated Ratchet's light value from {originalLight} to 0");
            }
            else
            {
                Console.WriteLine("⚠️ Could not find Ratchet moby (ID 179) to update light value");
            }
        }

        static SwapOptions DisplayMenuAndGetOptions()
        {
            Console.WriteLine("\n=== R&C1 to R&C2 Geometry Swap Options ===");
            Console.WriteLine("1. Swap Terrain/Geometry ONLY");
            Console.WriteLine("2. Swap Collision ONLY");
            Console.WriteLine("3. Swap TIEs ONLY");
            Console.WriteLine("4. Swap Shrubs ONLY");
            Console.WriteLine("5. Swap Skybox ONLY");
            Console.WriteLine("6. Swap Grind Paths ONLY");
            Console.WriteLine("7. Swap Point Lights ONLY");
            Console.WriteLine("8. Swap Sound Instances ONLY");
            Console.WriteLine("9. Copy Special Mobys ONLY");
            Console.WriteLine("10. Import Common Mobys from Reference RC2 Levels");
            Console.WriteLine("11. ALL of the above (except 10)");
            Console.WriteLine("12. Swap Level Variables ONLY (Background/Fog Color, Ship Position/Rotation)");
            Console.WriteLine("13. Transfer Ratchet Position ONLY");
            Console.WriteLine("14. Register Planet in Galactic Map ONLY");
            Console.WriteLine("15. Swap Vendor with RC1 Oltanis Vendor ONLY");
            Console.WriteLine("16. Swap Crates with RC1 Oltanis Crates ONLY");
            Console.WriteLine("17. Run TIE Diagnostics ONLY");
            Console.WriteLine("18. Run Grind Path Diagnostics ONLY");
            Console.WriteLine("19. Swap Swingshots with RC1 Oltanis ONLY");
            Console.WriteLine("20. Moby Import Tool");
            Console.WriteLine("21. Moby Export Tool");
            Console.WriteLine("22. Convert RC1 Mobys to RC2 Mobys!");
            Console.WriteLine("23. Create Moby Instances with RC1 Positions");
            Console.WriteLine("24. Custom Selection");
            Console.Write("\nEnter your choice (1-24): ");

            string input = Console.ReadLine() ?? "11"; // Default to ALL if empty
            if (!int.TryParse(input.Trim(), out int choice))
                choice = 11; // Default to ALL if invalid input

            SwapOptions options;
            switch (choice)
            {
                case 1: options = SwapOptions.Terrain; break;
                case 2: options = SwapOptions.Collision; break;
                case 3: options = SwapOptions.Ties; break;
                case 4: options = SwapOptions.Shrubs; break;
                case 5: options = SwapOptions.Skybox; break;
                case 6: options = SwapOptions.GrindPaths; break;
                case 7: options = SwapOptions.PointLights; break;
                case 8: options = SwapOptions.SoundInstances; break;
                case 9: options = SwapOptions.Mobys; break;
                case 10: // Import Common Mobys - This is a special case
                    ImportCommonMobysFromUser();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 11: options = SwapOptions.All; break;
                case 12: options = SwapOptions.SwapLevelVariables; break;
                case 13: options = SwapOptions.TransferRatchetPosition; break;
                case 14: options = SwapOptions.RegisterPlanetInMap; break;
                case 15: options = SwapOptions.SwapVendorWithOltanis; break;
                case 16: options = SwapOptions.SwapCratesWithOltanis; break;
                case 17: // Run TIE Diagnostics
                    RunTieDiagnosticsOnly();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 18: // Run Grind Path Diagnostics - Added this case
                    RunGrindPathDiagnosticsOnly();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 19:
                    options = SwapOptions.SwapSwingshots; break;
                case 20:
                    GeometrySwapper.MobyExporter.ImportMobysInteractive();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 21:
                    GeometrySwapper.MobyExporter.ExportMobysInteractive();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 22: // Convert RC1 Mobys to RC2 Mobys
                    RunMobyConverter();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 23: // Create Moby Instances - This is the new option
                    MobyOltanisInstancer.CreateMobyInstancesInteractive();
                    return DisplayMenuAndGetOptions(); // Return to menu after completion
                case 24: options = GetCustomOptions(); break;
                default: options = SwapOptions.All; break;
            }

            return options;
        }

        static void RunMobyConverter()
        {
            Console.WriteLine("\n==== RC1 to RC2 Moby Converter ====");

            // Get input file path
            Console.WriteLine("Enter path to the RC1 level engine.ps3 file:");
            Console.Write("> ");
            string enginePath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(enginePath) || !File.Exists(enginePath))
            {
                Console.WriteLine("❌ Invalid RC1 engine.ps3 path");
                return;
            }

            // Get output directory
            Console.WriteLine("\nEnter output directory for converted mobys:");
            Console.Write("> ");
            string outputDir = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = Path.Combine(Path.GetDirectoryName(enginePath) ?? "", "converted_mobys");
            }

            // Ensure output directory exists
            Directory.CreateDirectory(outputDir);

            Console.WriteLine($"\nConverting mobys from {enginePath} to {outputDir}...");

            try
            {
                // Call the MobyConverter's Main method with the arguments
                string[] converterArgs = { enginePath, outputDir };
                MobyConverter.Program.RunMobyConverter(converterArgs);

                Console.WriteLine($"\n✅ Moby conversion complete. Converted mobys saved to {outputDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during moby conversion: {ex.Message}");
            }

            Console.WriteLine("\nPress Enter to return to main menu...");
            Console.ReadLine();
        }

        static void ImportCommonMobysFromUser()
        {
            Console.WriteLine("\n==== Import Common Mobys from Reference RC2 Levels ====");
            
            // Ask for target level path (default to existing RC2 donor level if available)
            Console.WriteLine("\nEnter path to the target level engine.ps3 file (or leave empty to use current RC2 donor level):");
            Console.Write("> ");
            string targetPath = Console.ReadLine()?.Trim() ?? "";
            
            // If no path provided, use the RC2 donor level path
            if (string.IsNullOrEmpty(targetPath))
            {
                targetPath = Path.Combine(rc2DonorLevelDir, "engine.ps3");
                Console.WriteLine($"Using RC2 donor level: {targetPath}");
            }
            
            if (!File.Exists(targetPath))
            {
                Console.WriteLine("❌ Invalid target level path. Returning to main menu.");
                return;
            }

            // Get reference level paths
            List<string> referenceEnginePaths = new List<string>();
            
            Console.WriteLine("\nHow many reference RC2 levels do you want to analyze? (2-5)");
            Console.Write("> ");
            
            if (!int.TryParse(Console.ReadLine()?.Trim() ?? "2", out int referenceCount))
            {
                referenceCount = 2;
            }
            
            referenceCount = Math.Max(2, Math.Min(5, referenceCount)); // Clamp between 2 and 5
            
            Console.WriteLine($"\nEnter paths to {referenceCount} reference RC2 engine.ps3 files:");
            
            // Default to standard RC2 level and the reference GC planet as first two options
            string defaultReferenceLevel = Path.Combine(referenceGcPlanetDir, "engine.ps3");
            
            for (int i = 0; i < referenceCount; i++)
            {
                string defaultPrompt = "";
                if (i == 0 && File.Exists(defaultReferenceLevel))
                {
                    defaultPrompt = $" (or leave empty to use {Path.GetFileName(defaultReferenceLevel)})";
                }
                
                Console.Write($"Reference #{i + 1}{defaultPrompt}> ");
                string path = Console.ReadLine()?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(path) && i == 0 && File.Exists(defaultReferenceLevel))
                {
                    path = defaultReferenceLevel;
                    Console.WriteLine($"Using default reference: {path}");
                }
                
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    referenceEnginePaths.Add(path);
                }
                else
                {
                    Console.WriteLine("❌ Invalid path, skipping");
                }
            }
            
            if (referenceEnginePaths.Count < 2)
            {
                Console.WriteLine("❌ Need at least 2 valid reference paths to continue. Returning to main menu.");
                return;
            }

            // Ask about overwriting existing mobys
            Console.Write("\nOverwrite existing mobys in the target level? (y/n): ");
            bool allowOverwrite = Console.ReadLine()?.Trim().ToLower() == "y";

            // Load target level
            Console.WriteLine($"\nLoading target level: {Path.GetFileName(targetPath)}...");
            Level targetLevel;
            
            try
            {
                targetLevel = new Level(targetPath);
                Console.WriteLine($"✅ Successfully loaded target level with {targetLevel.mobyModels?.Count ?? 0} moby models");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading target level: {ex.Message}");
                return;
            }

            // Perform the import
            bool success = MobyImporter.ImportCommonMobysFromReferenceLevels(
                targetLevel, 
                referenceEnginePaths, 
                allowOverwrite
            );

            // Save the target level if successful
            if (success)
            {
                Console.Write("\nSave changes to the target level? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    Console.WriteLine("Saving target level...");
                    
                    try
                    {
                        PrepareLevelForSave(targetLevel);
                        Console.WriteLine($"Saving level to {targetPath}...");
                        targetLevel.Save(targetPath);
                        Console.WriteLine("✅ Target level saved successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error saving target level: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine("\nPress Enter to return to main menu...");
            Console.ReadLine();
        }

        static SwapOptions GetCustomOptions()
        {
            SwapOptions options = SwapOptions.None;

            Console.WriteLine("\n=== Custom Selection ===");
            Console.WriteLine("Enter Y/N for each option:");

            if (GetYesNoInput("Swap Terrain/Geometry? (Y/N): "))
                options |= SwapOptions.Terrain;

            if (GetYesNoInput("Swap Collision? (Y/N): "))
                options |= SwapOptions.Collision;

            if (GetYesNoInput("Swap TIEs? (Y/N): "))
                options |= SwapOptions.Ties;

            if (GetYesNoInput("Swap Shrubs? (Y/N): "))
                options |= SwapOptions.Shrubs;

            if (GetYesNoInput("Swap Skybox? (Y/N): "))
                options |= SwapOptions.Skybox;

            if (GetYesNoInput("Swap Grind Paths? (Y/N): "))
                options |= SwapOptions.GrindPaths;

            if (GetYesNoInput("Swap Point Lights? (Y/N): "))
                options |= SwapOptions.PointLights;

            if (GetYesNoInput("Swap Sound Instances? (Y/N): "))
                options |= SwapOptions.SoundInstances;

            if (GetYesNoInput("Copy Special Mobys (Vendors, Crates)? (Y/N): "))
                options |= SwapOptions.Mobys;

            if (GetYesNoInput("Import Common Mobys from reference RC2 levels? (Y/N): "))
            {
                ImportCommonMobysFromUser();
            }

            if (GetYesNoInput("Swap Level Variables (Background/Fog Color, Ship Position/Rotation)? (Y/N): "))
                options |= SwapOptions.SwapLevelVariables;

            if (GetYesNoInput("Transfer Ratchet position from RC1 Oltanis? (Y/N): "))
                options |= SwapOptions.TransferRatchetPosition;

            if (GetYesNoInput("Swap Vendor with RC1 Oltanis Vendor? (Y/N): "))
                options |= SwapOptions.SwapVendorWithOltanis;

            if (GetYesNoInput("Swap Crates with RC1 Oltanis Crates? (Y/N): "))
                options |= SwapOptions.SwapCratesWithOltanis;

            if (GetYesNoInput("Swap swingshots from RC1 to RC2? (y/n): "))
                options |= SwapOptions.SwapSwingshots;

            if (GetYesNoInput("Create moby instances with RC1 Oltanis positions? (Y/N): "))
                options |= SwapOptions.CreateMobyInstances;

            if (GetYesNoInput("Register planet in the Galactic Map? (Y/N): "))
                options |= SwapOptions.RegisterPlanetInMap;

            if (GetYesNoInput("Run Grind Path Diagnostics? (Y/N): "))
            {
                RunGrindPathDiagnosticsOnly();
            }

            return options;
        }

        static bool GetYesNoInput(string prompt)
        {
            Console.Write(prompt);
            string input = Console.ReadLine() ?? "n";
            return input.Trim().ToUpper().StartsWith("Y");
        }

        /// <summary>
        /// Analyzes texture usage in RC1 and RC2 levels to find potential correspondences
        /// </summary>
        static void AnalyzeAndMapTextures(Level rc1Level, Level rc2Level, string outputDir)
        {
            Console.WriteLine("🔍 TEXTURE CORRESPONDENCE ANALYSIS 🔍");
            Console.WriteLine("Analyzing RC1 and RC2 textures to find potential matches...");

            // Create output directory for texture analysis
            string textureAnalysisDir = Path.Combine(outputDir, "texture_analysis");
            Directory.CreateDirectory(textureAnalysisDir);

            // Extract the RC1 texture usage by terrain models
            var rc1TextureUsage = new Dictionary<int, List<int>>();
            var rc1TextureInfo = new Dictionary<int, (int width, int height)>();

            if (rc1Level.terrainEngine?.fragments != null && rc1Level.textures != null)
            {
                foreach (var fragment in rc1Level.terrainEngine.fragments)
                {
                    if (fragment.model?.textureConfig != null)
                    {
                        foreach (var texConfig in fragment.model.textureConfig)
                        {
                            int texId = texConfig.id;
                            if (!rc1TextureUsage.ContainsKey(texId))
                            {
                                rc1TextureUsage[texId] = new List<int>();
                                // Record texture dimensions if available
                                if (texId >= 0 && texId < rc1Level.textures.Count)
                                {
                                    var tex = rc1Level.textures[texId];
                                    rc1TextureInfo[texId] = (tex.width, tex.height);
                                }
                            }

                            if (!rc1TextureUsage[texId].Contains(fragment.model.id))
                                rc1TextureUsage[texId].Add(fragment.model.id);
                        }
                    }
                }
            }

            // Extract the RC2 texture usage by terrain models
            var rc2TextureUsage = new Dictionary<int, List<int>>();
            var rc2TextureInfo = new Dictionary<int, (int width, int height)>();

            if (rc2Level.terrainEngine?.fragments != null && rc2Level.textures != null)
            {
                foreach (var fragment in rc2Level.terrainEngine.fragments)
                {
                    if (fragment.model?.textureConfig != null)
                    {
                        foreach (var texConfig in fragment.model.textureConfig)
                        {
                            int texId = texConfig.id;
                            if (!rc2TextureUsage.ContainsKey(texId))
                            {
                                rc2TextureUsage[texId] = new List<int>();
                                // Record texture dimensions if available
                                if (texId >= 0 && texId < rc2Level.textures.Count)
                                {
                                    var tex = rc2Level.textures[texId];
                                    rc2TextureInfo[texId] = (tex.width, tex.height);
                                }
                            }

                            if (!rc2TextureUsage[texId].Contains(fragment.model.id))
                                rc2TextureUsage[texId].Add(fragment.model.id);
                        }
                    }
                }
            }

            // Generate texture correspondence report
            using (var writer = new StreamWriter(Path.Combine(textureAnalysisDir, "texture_correspondence.txt")))
            {
                writer.WriteLine("=== TEXTURE CORRESPONDENCE ANALYSIS ===");
                writer.WriteLine($"RC1 Level: {rc1Level.path}");
                writer.WriteLine($"RC2 Level: {rc2Level.path}");
                writer.WriteLine($"Total RC1 Textures: {rc1Level.textures?.Count ?? 0}");
                writer.WriteLine($"Total RC2 Textures: {rc2Level.textures?.Count ?? 0}");
                writer.WriteLine("\n=== RC1 TEXTURES USED BY TERRAIN ===");

                foreach (var kvp in rc1TextureUsage.OrderBy(k => k.Key))
                {
                    string dimensions = rc1TextureInfo.ContainsKey(kvp.Key)
                        ? $"{rc1TextureInfo[kvp.Key].width}x{rc1TextureInfo[kvp.Key].height}"
                        : "unknown";

                    writer.WriteLine($"Texture {kvp.Key} ({dimensions}): Used by {kvp.Value.Count} models: {string.Join(", ", kvp.Value)}");
                }

                writer.WriteLine("\n=== RC2 TEXTURES USED BY TERRAIN ===");
                foreach (var kvp in rc2TextureUsage.OrderBy(k => k.Key))
                {
                    string dimensions = rc2TextureInfo.ContainsKey(kvp.Key)
                        ? $"{rc2TextureInfo[kvp.Key].width}x{rc2TextureInfo[kvp.Key].height}"
                        : "unknown";

                    writer.WriteLine($"Texture {kvp.Key} ({dimensions}): Used by {kvp.Value.Count} models: {string.Join(", ", kvp.Value)}");
                }

                // Attempt to match textures based on dimensions and usage patterns
                writer.WriteLine("\n=== POTENTIAL TEXTURE MATCHES (DIMENSION-BASED) ===");

                var potentialMatches = new Dictionary<int, List<(int id, float score)>>();

                foreach (var rc1Tex in rc1TextureInfo)
                {
                    var matches = new List<(int id, float score)>();

                    foreach (var rc2Tex in rc2TextureInfo)
                    {
                        float score = 0;

                        // Score based on dimension match
                        if (rc1Tex.Value.width == rc2Tex.Value.width && rc1Tex.Value.height == rc2Tex.Value.height)
                        {
                            score += 10;  // Strong match for same dimensions
                        }
                        else
                        {
                            // Partial match for similar dimensions (within 25%)
                            float widthRatio = (float) rc1Tex.Value.width / rc2Tex.Value.width;
                            float heightRatio = (float) rc1Tex.Value.height / rc2Tex.Value.height;

                            if (widthRatio >= 0.75f && widthRatio <= 1.25f &&
                                heightRatio >= 0.75f && heightRatio <= 1.25f)
                            {
                                score += 5;  // Partial match for similar dimensions
                            }
                        }

                        // Only include matches with some score
                        if (score > 0)
                        {
                            matches.Add((rc2Tex.Key, score));
                        }
                    }

                    if (matches.Count > 0)
                    {
                        potentialMatches[rc1Tex.Key] = matches.OrderByDescending(m => m.score).ToList();
                    }
                }

                // Write potential matches
                foreach (var kvp in potentialMatches)
                {
                    writer.WriteLine($"RC1 Texture {kvp.Key} ({rc1TextureInfo[kvp.Key].width}x{rc1TextureInfo[kvp.Key].height}):");

                    foreach (var match in kvp.Value)
                    {
                        writer.WriteLine($"  -> RC2 Texture {match.id} ({rc2TextureInfo[match.id].width}x{rc2TextureInfo[match.id].height}), Score: {match.score}");
                    }
                }

                // Generate texture mapping suggestions
                writer.WriteLine("\n=== RECOMMENDED TEXTURE MAPPING (RC1 -> RC2) ===");
                writer.WriteLine("// Use this as a starting point for your texture mapping");
                writer.WriteLine("var textureMapping = new Dictionary<int, int>() {");

                foreach (var kvp in potentialMatches)
                {
                    if (kvp.Value.Count > 0)
                    {
                        writer.WriteLine($"    {{ {kvp.Key}, {kvp.Value[0].id} }}, // RC1 -> RC2");
                    }
                }

                writer.WriteLine("};");
            }

            Console.WriteLine($"✅ Texture analysis complete. Results saved to {textureAnalysisDir}");
            Console.WriteLine("Review the texture_correspondence.txt file for mapping suggestions");
        }

        /// <summary>
        /// Tests texture ID remapping on a limited number of fragments
        /// </summary>
        static void TestTextureReindexing(Level targetLevel, Dictionary<int, int> textureMapping, int fragmentCount = 5)
        {
            Console.WriteLine($"🧪 TESTING TEXTURE REINDEXING ON {fragmentCount} FRAGMENTS 🧪");

            if (targetLevel.terrainEngine?.fragments == null || targetLevel.terrainEngine.fragments.Count == 0)
            {
                Console.WriteLine("❌ No terrain fragments to reindex");
                return;
            }

            // Limit to requested fragment count
            int count = Math.Min(fragmentCount, targetLevel.terrainEngine.fragments.Count);
            Console.WriteLine($"Reindexing texture IDs for first {count} fragments...");

            // Keep track of statistics
            int totalConfigs = 0;
            int mappedConfigs = 0;
            int unmappedConfigs = 0;

            // Process the fragments
            for (int i = 0; i < count; i++)
            {
                var fragment = targetLevel.terrainEngine.fragments[i];
                if (fragment.model?.textureConfig != null)
                {
                    Console.WriteLine($"  Fragment {i} (Model ID {fragment.model.id}):");

                    for (int j = 0; j < fragment.model.textureConfig.Count; j++)
                    {
                        var texConfig = fragment.model.textureConfig[j];
                        int oldTextureId = texConfig.id;
                        totalConfigs++;

                        Console.WriteLine($"    TextureConfig {j}: ID {oldTextureId}");

                        if (textureMapping.TryGetValue(oldTextureId, out int newTextureId))
                        {
                            // Remap texture ID
                            texConfig.id = newTextureId;
                            mappedConfigs++;
                            Console.WriteLine($"      → Mapped to RC2 texture ID {newTextureId}");
                        }
                        else
                        {
                            unmappedConfigs++;
                            Console.WriteLine($"      → No mapping found, keeping original ID");
                        }
                    }
                }
            }

            Console.WriteLine("\nReindexing Statistics:");
            Console.WriteLine($"  Total TextureConfigs processed: {totalConfigs}");
            Console.WriteLine($"  Successfully mapped: {mappedConfigs} ({(totalConfigs > 0 ? (float) mappedConfigs / totalConfigs * 100 : 0):F1}%)");
            Console.WriteLine($"  No mapping found: {unmappedConfigs} ({(totalConfigs > 0 ? (float) unmappedConfigs / totalConfigs * 100 : 0):F1}%)");

            Console.WriteLine("\n⚠️ WARNING: This is a test run only. The texture IDs have been changed but will not be saved unless you explicitly save the level.");
        }

        /// <summary>
        /// Imports textures from the RC1 level into the target RC2 level,
        /// preserving the original texture IDs used by terrain fragments
        /// </summary>
        static void ImportRC1TexturesToRC2Level(Level targetLevel, Level rc1SourceLevel)
        {
            Console.WriteLine("📥 Importing RC1 textures to RC2 level...");

            // Create a HashSet of texture IDs used by terrain fragments
            var usedTextureIds = new HashSet<int>();

            // Find all texture IDs used by terrain fragments
            if (targetLevel.terrainEngine?.fragments != null)
            {
                foreach (var fragment in targetLevel.terrainEngine.fragments)
                {
                    if (fragment.model?.textureConfig != null)
                    {
                        foreach (var texConfig in fragment.model.textureConfig)
                        {
                            usedTextureIds.Add(texConfig.id);
                        }
                    }
                }
            }

            // Keep track of stats
            int totalImported = 0;
            int skippedOutOfRange = 0;
            int skippedNoData = 0;

            Console.WriteLine($"  Found {usedTextureIds.Count} unique texture IDs referenced by terrain fragments");

            // Ensure the target texture list is initialized
            if (targetLevel.textures == null)
            {
                targetLevel.textures = new List<Texture>();
            }

            // If we need to expand the texture list in the target level
            int highestTextureId = usedTextureIds.Count > 0 ? usedTextureIds.Max() : -1;
            if (highestTextureId >= targetLevel.textures.Count)
            {
                int originalSize = targetLevel.textures.Count;
                int newSize = highestTextureId + 1;

                Console.WriteLine($"  Expanding RC2 texture list from {originalSize} to {newSize} entries");

                // Create empty placeholder textures to fill the list
                for (int i = originalSize; i < newSize; i++)
                {
                    targetLevel.textures.Add(new Texture(0, 0, 0, new byte[0])); // Default empty texture with valid arguments
                }
            }

            // Import the used RC1 textures to the right positions in RC2 texture list
            foreach (int textureId in usedTextureIds.OrderBy(id => id))
            {
                if (textureId < 0 || textureId >= rc1SourceLevel.textures?.Count)
                {
                    Console.WriteLine($"  ⚠️ Texture ID {textureId} is out of range for RC1 texture list");
                    skippedOutOfRange++;
                    continue;
                }

                var rc1Texture = rc1SourceLevel.textures[textureId];

                if (rc1Texture.data == null || rc1Texture.data.Length == 0)
                {
                    Console.WriteLine($"  ⚠️ Texture ID {textureId} has no data in RC1 source");
                    skippedNoData++;
                    continue;
                }

                // Replace the placeholder or original texture with the RC1 texture
                targetLevel.textures[textureId] = rc1Texture;
                totalImported++;

                // Log some information about every 20th texture to avoid flooding the console
                if (totalImported % 20 == 0 || totalImported <= 5)
                {
                    Console.WriteLine($"  Imported texture {textureId} ({rc1Texture.width}x{rc1Texture.height})");
                }
            }

            Console.WriteLine($"✅ Imported {totalImported} RC1 textures to RC2 level");
            if (skippedOutOfRange > 0 || skippedNoData > 0)
            {
                Console.WriteLine($"  ⚠️ Skipped {skippedOutOfRange} out-of-range IDs and {skippedNoData} textures with no data");
            }
        }

        /// <summary>
        /// Verifies that all textures used by terrain fragments exist in the texture list
        /// </summary>
        static void VerifyTexturesForTerrainFragments(Level level, string levelName)
        {
            Console.WriteLine($"\n=== TEXTURE VERIFICATION FOR {levelName} ===");

            if (level.terrainEngine?.fragments == null || level.terrainEngine.fragments.Count == 0)
            {
                Console.WriteLine("No terrain fragments to verify");
                return;
            }

            if (level.textures == null || level.textures.Count == 0)
            {
                Console.WriteLine("No textures in level");
                return;
            }

            var usedTextureIds = new HashSet<int>();
            var missingTextureIds = new HashSet<int>();
            var emptyTextureIds = new HashSet<int>();

            // Check texture references in fragments
            foreach (var fragment in level.terrainEngine.fragments)
            {
                if (fragment.model?.textureConfig != null)
                {
                    foreach (var texConfig in fragment.model.textureConfig)
                    {
                        int texId = texConfig.id;
                        usedTextureIds.Add(texId);

                        // Check if the ID is within range
                        if (texId < 0 || texId >= level.textures.Count)
                        {
                            missingTextureIds.Add(texId);
                            continue;
                        }

                        // Check if texture has data
                        if (level.textures[texId].data == null || level.textures[texId].data.Length == 0)
                        {
                            emptyTextureIds.Add(texId);
                        }
                    }
                }
            }

            // Report findings
            Console.WriteLine($"Total unique texture IDs used by terrain: {usedTextureIds.Count}");
            Console.WriteLine($"Texture list size: {level.textures.Count}");

            if (missingTextureIds.Count > 0)
            {
                Console.WriteLine($"⚠️ {missingTextureIds.Count} texture IDs are out of range: {string.Join(", ", missingTextureIds.Take(10))}{(missingTextureIds.Count > 10 ? "..." : "")}");
            }
            else
            {
                Console.WriteLine("✅ All texture IDs are within range");
            }

            if (emptyTextureIds.Count > 0)
            {
                Console.WriteLine($"⚠️ {emptyTextureIds.Count} textures have no data: {string.Join(", ", emptyTextureIds.Take(10))}{(emptyTextureIds.Count > 10 ? "..." : "")}");
            }
            else
            {
                Console.WriteLine("✅ All referenced textures have data");
            }

            // Sample some textures
            Console.WriteLine("\nSample of textures used by terrain:");
            foreach (var texId in usedTextureIds.OrderBy(id => id).Take(5))
            {
                if (texId >= 0 && texId < level.textures.Count)
                {
                    var tex = level.textures[texId];
                    Console.WriteLine($"  Texture {texId}: {tex.width}x{tex.height}, {(tex.data?.Length ?? 0)} bytes");
                }
                else
                {
                    Console.WriteLine($"  Texture {texId}: Out of range");
                }
            }
        }

        /// <summary>
        /// Checks if a tie object is likely to be garbage/unused
        /// </summary>
        static bool IsLikelyGarbageTie(Tie tie)
        {
            // Check for unreasonably large coordinates or extreme positions
            if (float.IsNaN(tie.position.X) || float.IsNaN(tie.position.Y) || float.IsNaN(tie.position.Z))
                return true;

            if (Math.Abs(tie.position.X) > 10000 || Math.Abs(tie.position.Y) > 10000 || Math.Abs(tie.position.Z) > 10000)
                return true;

            // Position under far below level bounds often indicates unused ties
            if (tie.position.Y < -999)
                return true;

            // Extremely small or large scale can indicate junk data
            if (tie.scale.X < 0.001f || tie.scale.Y < 0.001f || tie.scale.Z < 0.001f ||
                tie.scale.X > 1000 || tie.scale.Y > 1000 || tie.scale.Z > 1000)
                return true;

            return false;
        }

        /// <summary>
        /// Imports RC1 tie models to RC2 level, ensuring proper model compatibility with unique IDs
        /// </summary>
        static Dictionary<int, int> ImportRC1TieModelsToRC2Level(Level targetLevel, Level rc1SourceLevel)
        {
            Console.WriteLine($"🔄 Importing RC1 tie models to RC2 level...");

            var modelIdMapping = new Dictionary<int, int>();

            if (rc1SourceLevel.tieModels == null || rc1SourceLevel.tieModels.Count == 0)
            {
                Console.WriteLine("  ⚠️ No tie models found in RC1 source level");
                return modelIdMapping;
            }

            // Ensure the target has a tie model list
            if (targetLevel.tieModels == null)
            {
                targetLevel.tieModels = new List<Model>();
            }

            // Find the highest existing model ID to avoid conflicts
            short nextModelId = 1000; // Start with a safe base value
            foreach (var existingModel in targetLevel.tieModels)
            {
                if (existingModel != null && existingModel.id >= nextModelId)
                {
                    nextModelId = (short) (existingModel.id + 1);
                }
            }

            Console.WriteLine($"  Starting model ID assignment from {nextModelId}");

            // Log all RC1 model IDs for diagnostic purposes
            var rc1ModelIds = new HashSet<int>();
            if (rc1SourceLevel.tieModels != null)
            {
                foreach (var model in rc1SourceLevel.tieModels.Where(m => m != null))
                {
                    rc1ModelIds.Add(model.id);
                }
            }
            Console.WriteLine($"  Found {rc1ModelIds.Count} unique model IDs in RC1 source");

            // Create a deep copy of each RC1 model to avoid reference issues
            foreach (var origModel in rc1SourceLevel.tieModels?.Where(m => m != null) ?? Enumerable.Empty<Model>())
            {
                // Skip if not actually a TieModel (shouldn't happen but just to be safe)
                if (!(origModel is TieModel rc1Model))
                {
                    Console.WriteLine($"  ⚠️ Model {origModel.id} is not a TieModel, skipping");
                    continue;
                }

                // Store the original RC1 model ID
                int originalId = rc1Model.id;

                // Create a clone of the model
                try
                {
                    TieModel newModel = TieModel(rc1Model);

                    // Assign a new unique ID to avoid conflicts with existing RC2 models
                    newModel.id = nextModelId++;

                    // Add the model with its new ID to the RC2 level
                    targetLevel.tieModels.Add(newModel);

                    // Record the mapping from original RC1 ID to new assigned RC2 ID
                    modelIdMapping[originalId] = newModel.id;

                    Console.WriteLine($"  Added RC1 tie model {originalId} as RC2 model {newModel.id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error copying RC1 tie model {originalId}: {ex.Message}");
                }
            }

            // Identify any RC1 tie model IDs that weren't imported
            var usedModelIds = new HashSet<int>();
            if (rc1SourceLevel.ties != null)
            {
                foreach (var tie in rc1SourceLevel.ties)
                {
                    usedModelIds.Add(tie.modelID);
                }
            }

            // Find model IDs that are referenced by ties but weren't imported
            var missingModelIds = usedModelIds.Where(id => !modelIdMapping.ContainsKey(id)).ToList();
            if (missingModelIds.Count > 0)
            {
                Console.WriteLine($"  ⚠️ Found {missingModelIds.Count} model IDs referenced by RC1 ties but not imported:");
                foreach (var id in missingModelIds.Take(10))
                {
                    Console.WriteLine($"     - Model ID {id}");
                }
                if (missingModelIds.Count > 10)
                {
                    Console.WriteLine($"     - ... and {missingModelIds.Count - 10} more");
                }
            }

            // CRITICAL FIX: Update both tieIds AND tieModelIds collections in the header
            if (targetLevel.tieModels != null)
            {
                // Update the mapping of model IDs used by the engine header
                targetLevel.tieIds = targetLevel.tieModels.Select(m => (int) m.id).ToList();

                Console.WriteLine($"  ✅ Updated tieIds header list with {targetLevel.tieIds.Count} model IDs");
            }

            Console.WriteLine($"  ✅ Imported {modelIdMapping.Count} RC1 tie models to RC2 level with unique IDs");
            return modelIdMapping;
        }

        /// <summary>
        /// Creates a copy of a TieModel with new ID assignment
        /// </summary>
        static TieModel TieModel(TieModel sourceModel)
        {
            // Create a minimal byte array that we can pass to construct a new TieModel
            byte[] dummyBlock = new byte[0x40];

            // Copy basic properties that would be read from the byte array
            WriteFloat(dummyBlock, 0x00, sourceModel.cullingX);
            WriteFloat(dummyBlock, 0x04, sourceModel.cullingY);
            WriteFloat(dummyBlock, 0x08, sourceModel.cullingZ);
            WriteFloat(dummyBlock, 0x0C, sourceModel.cullingRadius);
            WriteUint(dummyBlock, 0x20, sourceModel.off20);
            WriteShort(dummyBlock, 0x2A, sourceModel.wiggleMode);
            WriteFloat(dummyBlock, 0x2C, sourceModel.off2C);
            WriteShort(dummyBlock, 0x30, (short) 0); // We'll set the ID later
            WriteUint(dummyBlock, 0x34, sourceModel.off34);
            WriteUint(dummyBlock, 0x38, sourceModel.off38);
            WriteUint(dummyBlock, 0x3C, sourceModel.off3C);

            // Since the constructor requires a FileStream, we need to create one from our dummy block
            // We'll create a temporary file for this purpose
            string tempFilePath = Path.GetTempFileName();
            try
            {
                // Write the dummy block to the temp file
                File.WriteAllBytes(tempFilePath, dummyBlock);

                // Open the temp file as a FileStream
                using (FileStream fs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read))
                {
                    // Create a new TieModel using the dummy data
                    TieModel newModel = new TieModel(fs, dummyBlock, 0);

                    // Now manually copy over the remaining important data
                    newModel.size = sourceModel.size;

                    // Deep copy vertex and index buffers
                    newModel.vertexBuffer = sourceModel.vertexBuffer != null
                        ? (float[]) sourceModel.vertexBuffer.Clone()
                        : new float[0];

                    newModel.indexBuffer = sourceModel.indexBuffer != null
                        ? (ushort[]) sourceModel.indexBuffer.Clone()
                        : new ushort[0];

                    // Deep copy texture configs to avoid shared references
                    newModel.textureConfig = new List<TextureConfig>();
                    if (sourceModel.textureConfig != null)
                    {
                        foreach (var texConfig in sourceModel.textureConfig)
                        {
                            TextureConfig newConfig = new TextureConfig();
                            newConfig.id = texConfig.id;
                            newConfig.start = texConfig.start;
                            newConfig.size = texConfig.size;
                            newConfig.mode = texConfig.mode;
                            newModel.textureConfig.Add(newConfig);
                        }
                    }

                    return newModel;
                }
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch { /* Ignore delete errors */ }
                }
            }
        }

        static void RunTieDiagnosticsOnly()
        {
            Console.WriteLine("\n==== Running TIE Diagnostics Tool ====");
            TieDiagnostics.RunTieDiagnosticsInteractive();
        }

        static void RunGrindPathDiagnosticsOnly()
        {
            Console.WriteLine("\n==== Running Grind Path Diagnostics Tool ====");
            GrindPathDiagnostics.RunGrindPathDiagnosticsInteractive();
        }

        static void PrepareLevelForSave(Level level)
        {
            Console.WriteLine("\n=== Preparing Level For Save ===");

            // Add grind path validation
            if (level.grindPaths != null && level.grindPaths.Count > 0)
            {
                Console.WriteLine("Validating grind paths...");
                bool validPaths = GrindPathSwapper.ValidateGrindPathReferences(level);

                if (!validPaths)
                {
                    Console.WriteLine("⚠️ WARNING: Invalid grind path references detected. Attempting to fix...");

                    // Fix dangling references
                    foreach (var path in level.grindPaths.ToList())
                    {
                        if (path.spline != null && !level.splines.Contains(path.spline))
                        {
                            Console.WriteLine($"  Fixing invalid reference in path {path.id}");
                            path.spline = null;
                        }
                    }

                    // Fix duplicate spline IDs
                    var duplicateSplineIds = level.splines
                        .GroupBy(s => s.id)
                        .Where(g => g.Count() > 1)
                        .ToList();

                    if (duplicateSplineIds.Any())
                    {
                        Console.WriteLine($"  Fixing {duplicateSplineIds.Count} duplicate spline ID groups");
                        int highestId = level.splines.Max(s => s.id);

                        foreach (var group in duplicateSplineIds)
                        {
                            bool first = true;
                            foreach (var spline in level.splines.Where(s => s.id == group.Key).ToList())
                            {
                                if (first)
                                {
                                    first = false;
                                    continue;
                                }

                                // Assign new ID
                                int oldId = spline.id;
                                spline.id = ++highestId;
                                Console.WriteLine($"    Changed spline ID {oldId} to {spline.id}");
                            }
                        }
                    }
                }
            }

            // Validate TIE data
            if (level.tieModels != null)
            {
                Console.WriteLine($"• Level has {level.tieModels.Count} TIE models");
            }
            
            if (level.ties != null)
            {
                Console.WriteLine($"• Level has {level.ties.Count} TIE instances");
            }
            
            if (level.tieIds != null)
            {
                Console.WriteLine($"• Level has {level.tieIds.Count} TIE IDs in header list");
                
                // Critical check: tieIds must match model count, not instance count
                if (level.tieModels != null && level.tieIds.Count != level.tieModels.Count)
                {
                    Console.WriteLine($"⚠️ FIXING CRITICAL ISSUE: tieIds count ({level.tieIds.Count}) doesn't match model count ({level.tieModels.Count})");
                    level.tieIds = level.tieModels.Select(m => (int)m.id).ToList();
                    Console.WriteLine($"✅ Updated tieIds list with {level.tieIds.Count} entries");
                }
            }
            
            // Check tieGroupData alignment
            if (level.tieGroupData != null)
            {
                Console.WriteLine($"• Level has {level.tieGroupData.Length} bytes of tieGroupData");
                
                // Check alignment
                if (level.tieGroupData.Length % 0x80 != 0)
                {
                    Console.WriteLine($"⚠️ FIXING CRITICAL ISSUE: tieGroupData size ({level.tieGroupData.Length}) is not aligned to 0x80 boundaries");
                    TieSwapper.CreateTieSerializationData(level);
                    Console.WriteLine($"✅ Recreated tieGroupData, now {level.tieGroupData.Length} bytes (aligned to 0x80)");
                }
            }
            
            // Check tieData size
            if (level.tieData != null && level.ties != null)
            {
                Console.WriteLine($"• Level has {level.tieData.Length} bytes of tieData");
                
                int expectedSize = level.ties.Count * 0x70;
                if (level.tieData.Length != expectedSize)
                {
                    Console.WriteLine($"⚠️ FIXING CRITICAL ISSUE: tieData size mismatch: {level.tieData.Length} bytes (expected {expectedSize})");
                    TieSwapper.CreateTieSerializationData(level);
                    Console.WriteLine($"✅ Recreated tieData, now {level.tieData.Length} bytes");
                }
            }
            
            Console.WriteLine($"Saving level with {level.tieIds?.Count ?? 0} tieIds and tieGroupData size {level.tieGroupData?.Length ?? 0} bytes");
        }

        public static void Main(string[] args)
        {
            Console.WriteLine(">>> R&C1 to R&C2 Geometry Swapper <<<");

            string rc1SourceLevelDir = @"C:\Users\Ryan_\Downloads\temp\Oltanis_RaC1\";
            string rc2DonorLevelDir = @"C:\Users\Ryan_\Downloads\temp\Insomniac_Museum\"; // Using Insomniac Museum as donor
            string referenceGcPlanetDir = @"C:\Users\Ryan_\Downloads\temp\Insomniac_Museum\";
            string globalRc2Dir = @"D:\Projects\R&C1_to_R&C2_Planet_Format\Going_Commando_PSARC\rc2\ps3data\global\";
            string outputDir = @"C:\Users\Ryan_\Downloads\temp\OltanisOnInsomniacBase\"; // Changed output name

            // Display menu and get user options
            SwapOptions options = DisplayMenuAndGetOptions();
            Console.WriteLine($"\nSelected options: {options}");

            Directory.CreateDirectory(outputDir);

            string rc1EnginePath = Path.Combine(rc1SourceLevelDir, "engine.ps3");
            string rc2EnginePath = Path.Combine(rc2DonorLevelDir, "engine.ps3");
            string referenceGcPlanetEnginePath = Path.Combine(referenceGcPlanetDir, "engine.ps3");

            Console.WriteLine($"📂 Loading R&C1 Source Level (for geometry): {rc1EnginePath}");
            Level? rc1Level = null;
            try { rc1Level = new Level(rc1EnginePath); }
            catch (Exception ex) { Console.WriteLine($"❌ Error loading R&C1 source: {ex.Message}"); return; }

            Level? rc2DonorLevel = null; // Renamed for clarity
            try { rc2DonorLevel = new Level(rc2EnginePath); }
            catch (Exception ex) { Console.WriteLine($"❌ Error loading R&C2 donor: {ex.Message}"); return; }

            Level? referenceRc2PlanetLevel = null;
            if (File.Exists(referenceGcPlanetEnginePath))
            {
                try
                {
                    referenceRc2PlanetLevel = new Level(referenceGcPlanetEnginePath);
                    Console.WriteLine("  ✅ Reference R&C2 planet loaded.");
                }
                catch (Exception ex) { Console.WriteLine($"  ❌ Error loading reference R&C2 planet: {ex.Message}"); }
            }
            else { Console.WriteLine($"  Warning: Reference R&C2 planet not found at {referenceGcPlanetEnginePath}."); }

            LoadGlobalRCAssets(globalRc2Dir, out var globalArmorModels, out var globalArmorTextures, out var globalGadgetModels, out var globalGadgetTextures);

            if (rc1Level == null || rc2DonorLevel == null)
            {
                Console.WriteLine("Critical error: Source or Donor level failed to load. Exiting.");
                return;
            }

            rc2DonorLevel.game = GameType.RaC2; // Explicitly set target game type

            // Add diagnostic info about donor level before attempting to modify it
            Console.WriteLine("\n=== RC2 DONOR LEVEL DIAGNOSTIC INFO ===");
            Console.WriteLine($"Game Type: {rc2DonorLevel.game.num}");
            Console.WriteLine($"Total textures: {rc2DonorLevel.textures?.Count ?? 0}");
            Console.WriteLine($"Total mobys: {rc2DonorLevel.mobs?.Count ?? 0}");
            Console.WriteLine($"Total moby models: {rc2DonorLevel.mobyModels?.Count ?? 0}");
            Console.WriteLine($"Level Variables ByteSize: 0x{rc2DonorLevel.levelVariables?.ByteSize.ToString("X") ?? "??"}");
            Console.WriteLine($"Terrain fragments: {rc2DonorLevel.terrainEngine?.fragments?.Count ?? 0}");

            // Perform terrain analysis
            Console.WriteLine("\n=== ANALYZING TERRAIN STRUCTURES ===");
            DumpTerrainInfo(rc1Level.terrainEngine, "RC1 Source Level");
            DumpTerrainInfo(rc2DonorLevel.terrainEngine, "RC2 Donor Level");

            if (referenceRc2PlanetLevel != null)
            {
                DumpTerrainInfo(referenceRc2PlanetLevel.terrainEngine, "RC2 Reference Level");
            }

            // Ask if user wants to continue after analysis
            Console.WriteLine("\nAnalysis complete. Continue with geometry swap? (Y/N):");
            if (!GetYesNoInput(""))
            {
                Console.WriteLine("Operation cancelled by user.");
                return;
            }

            // --- Call the main processing function with user-selected options ---
            FinalizeLevelProcessing(rc2DonorLevel, rc1Level, referenceRc2PlanetLevel!,
                      globalArmorModels, globalArmorTextures,
                      globalGadgetModels, globalGadgetTextures,
                      options,
                      rc2DonorLevel);

            // Analyze texture correspondence between RC1 and RC2
            Console.WriteLine("\n=== TEXTURE MAPPING ANALYSIS ===");
            AnalyzeAndMapTextures(rc1Level, rc2DonorLevel, outputDir);

            // After the analysis is complete, you can define a manual mapping:
            // This is just a placeholder - you should replace it with values from the analysis
            var textureMapping = new Dictionary<int, int>()
            {
                { 0, 0 },  // Just placeholders for now
                { 1, 1 },
                // Add more mappings based on the analysis results
            };

            // Test how texture reindexing would work on a small sample
            Console.WriteLine("\n=== TEXTURE REINDEXING TEST ===");
            if (GetYesNoInput("Would you like to test texture reindexing on 5 fragments? (Y/N): "))
            {
                TestTextureReindexing(rc1Level, textureMapping, 5);
            }

            // Import RC1 textures into RC2 level
            Console.WriteLine("\n=== IMPORTING RC1 TEXTURES INTO RC2 LEVEL ===");
            ImportRC1TexturesToRC2Level(rc2DonorLevel, rc1Level);

            // Verify textures for terrain fragments
            Console.WriteLine("\n=== VERIFYING TEXTURES FOR TERRAIN FRAGMENTS ===");
            VerifyTexturesForTerrainFragments(rc2DonorLevel, "RC2 Donor Level");

            // --- Save, Patch, Verify ---
            Console.WriteLine($"\n💾 Saving geometry-swapped level to: {outputDir}");

            // Ensure ByteSize is correct before saving
            if (rc2DonorLevel.levelVariables != null)
            {
                if (rc2DonorLevel.levelVariables.ByteSize != 0x88)
                {
                    Console.WriteLine($"⚠️ Correcting ByteSize from 0x{rc2DonorLevel.levelVariables.ByteSize:X} to 0x88");
                    rc2DonorLevel.levelVariables.ByteSize = 0x88;
                }
                else
                {
                    Console.WriteLine("✅ ByteSize correctly set to 0x88");
                }
            }

            PrepareLevelForSave(rc2DonorLevel);
            Console.WriteLine($"Saving level to {outputDir}...");
            rc2DonorLevel.Save(outputDir);


            string outputEngineFile = Path.Combine(outputDir, "engine.ps3");
            Console.WriteLine("Patching engine.ps3 header values...");
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
            catch (Exception ex) { Console.WriteLine($"❌ Error while patching engine.ps3: {ex.Message}"); }

            // Declare verificationLevel so it's accessible later
            Level? verificationLevel = null;

            Console.WriteLine("\n=== Post-Processing Verification ===");
            try
            {
                verificationLevel = new Level(outputEngineFile);
                Console.WriteLine($"GameType reported by loaded level: {verificationLevel.game.num}");
                Console.WriteLine($"Terrain fragments: {verificationLevel.terrainEngine?.fragments?.Count ?? 0}");
                Console.WriteLine($"Total mobys: {verificationLevel.mobs?.Count ?? 0}");
                Console.WriteLine($"Total ties: {verificationLevel.ties?.Count ?? 0}");
                Console.WriteLine($"Total shrubs: {verificationLevel.shrubs?.Count ?? 0}");

                // Fragment verification
                Console.WriteLine("\n=== Terrain Fragment ID Verification ===");
                var fragments = verificationLevel.terrainEngine?.fragments;
                if (fragments != null && fragments.Count > 0)
                {
                    var fragmentIds = fragments.Select(f => f.off1E).ToList();
                    var uniqueIds = new HashSet<ushort>(fragmentIds);
                    Console.WriteLine($"Fragment count: {fragmentIds.Count}");
                    Console.WriteLine($"Unique IDs: {uniqueIds.Count}");

                    bool properSequence = true;
                    for (int i = 0; i < fragmentIds.Count; i++)
                    {
                        if (fragmentIds[i] != i)
                        {
                            properSequence = false;
                            Console.WriteLine($"Sequence break: Fragment at index {i} has ID {fragmentIds[i]}");
                        }
                    }

                    if (properSequence)
                        Console.WriteLine("✅ Fragment IDs are in perfect sequential order");
                    else
                        Console.WriteLine("⚠️ Fragment IDs are not in sequential order");

                    Console.WriteLine("\nSample of first few fragments:");
                    for (int i = 0; i < Math.Min(5, fragments.Count); i++)
                    {
                        var frag = fragments[i];
                        Console.WriteLine($"  Fragment {i}: ID={frag.off1E}, ModelID={frag.modelID}, HasVertices={frag.model?.vertexBuffer?.Length > 0}");
                    }
                }
                else
                {
                    Console.WriteLine("No terrain fragments found in verification level");
                }

                var ratchet = verificationLevel.mobs?.FirstOrDefault(m => m.mobyID == 0);
                if (ratchet != null)
                {
                    Console.WriteLine($"Ratchet found (oClass {ratchet.mobyID}) - pos: {ratchet.position}, modelID: {ratchet.modelID}");
                }
                else
                {
                    Console.WriteLine("Warning: Ratchet (ID 0) not found in verification load!");
                }

                // Verify textures in the saved level
                VerifyTexturesForTerrainFragments(verificationLevel, "Verification Level");
            }
            catch (Exception ex) { Console.WriteLine($"❌ Error during verification: {ex.Message}"); }

            // Save result examination
            Console.WriteLine("\n=== SAVE RESULT EXAMINATION ===");
            Console.WriteLine($"Output directory exists: {Directory.Exists(outputDir)}");
            Console.WriteLine($"Engine file exists: {File.Exists(outputEngineFile)}");
            Console.WriteLine($"Engine file size: {(File.Exists(outputEngineFile) ? new FileInfo(outputEngineFile).Length : 0)} bytes");
            Console.WriteLine($"Gameplay file exists: {File.Exists(Path.Combine(outputDir, "gameplay_ntsc"))}");
            Console.WriteLine($"Gameplay file size: {(File.Exists(Path.Combine(outputDir, "gameplay_ntsc")) ? new FileInfo(Path.Combine(outputDir, "gameplay_ntsc")).Length : 0)} bytes");

            // Try to open the files directly to see if there's a basic format issue
            try
            {
                using (var fs = File.OpenRead(outputEngineFile))
                {
                    byte[] header = new byte[16];
                    fs.Read(header, 0, 16);
                    Console.WriteLine($"  Engine header first 16 bytes: {BitConverter.ToString(header)}");
                }

                string gameplayPath = Path.Combine(outputDir, "gameplay_ntsc");
                if (File.Exists(gameplayPath))
                {
                    using (var fs = File.OpenRead(gameplayPath))
                    {
                        byte[] header = new byte[16];
                        fs.Read(header, 0, 16);
                        Console.WriteLine($"  Gameplay header first 16 bytes: {BitConverter.ToString(header)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error examining saved files: {ex.Message}");
            }

            // === Diagnostics ===
            string diagnosticsDir = Path.Combine(outputDir, "diagnostics");
            Directory.CreateDirectory(diagnosticsDir);
            Console.WriteLine("\n=== Running Detailed Terrain Diagnostics ===");

            if (rc1Level != null)
                TerrainDiagnostics.AnalyzeLevel(rc1Level, diagnosticsDir, "RC1_Source");

            if (rc2DonorLevel != null)
                TerrainDiagnostics.AnalyzeLevel(rc2DonorLevel, diagnosticsDir, "RC2_Donor");

            if (referenceRc2PlanetLevel != null)
            {
                TerrainDiagnostics.AnalyzeLevel(referenceRc2PlanetLevel, diagnosticsDir, "RC2_Reference");
            }

            if (verificationLevel != null)
            {
                TerrainDiagnostics.AnalyzeLevel(verificationLevel, diagnosticsDir, "Output_Level");

                if (rc2DonorLevel != null)
                    TerrainDiagnostics.CompareLevels(rc2DonorLevel, verificationLevel, diagnosticsDir, "RC2_Donor", "Output_Level");

                if (rc1Level != null)
                    TerrainDiagnostics.CompareLevels(rc1Level, verificationLevel, diagnosticsDir, "RC1_Source", "Output_Level");
            }
            else
            {
                Console.WriteLine("⚠️ Skipping verification level analysis as the level could not be loaded");
            }

            // Ask if user wants to analyze all RC2 levels
            if (GetYesNoInput("\nWould you like to analyze all Going Commando levels for comparison? (Y/N): "))
            {
                string? rc2DataPath = Path.GetDirectoryName(Path.GetDirectoryName(globalRc2Dir)); // Should point to ps3data
                string allLevelsOutputDir = Path.Combine(outputDir, "all_rc2_levels_analysis");

                Console.WriteLine("\n=== Running Analysis on All Going Commando Levels ===");
                Console.WriteLine($"Looking in: {rc2DataPath ?? "Unknown path"}");

                // Check if the directory exists and has levels
                if (string.IsNullOrEmpty(rc2DataPath) || !Directory.Exists(rc2DataPath))
                {
                    Console.WriteLine($"❌ RC2 data directory not found: {rc2DataPath ?? "Unknown path"}");
                }
                else
                {
                    // Generate a comprehensive analysis of patterns across all RC2 levels
                    TerrainDiagnostics.GenerateRC2PatternsReport(rc2DataPath, allLevelsOutputDir);

                    // Analyze individual levels if user wants detailed reports
                    if (GetYesNoInput("Would you also like detailed reports for each individual level? (Y/N): "))
                    {
                        int maxLevels = 0;
                        Console.Write("Enter maximum number of levels to analyze (0 for all): ");
                        int.TryParse(Console.ReadLine() ?? "0", out maxLevels);

                        TerrainDiagnostics.AnalyzeAllRC2Levels(rc2DataPath, allLevelsOutputDir, maxLevels);
                    }

                    Console.WriteLine($"✅ All RC2 levels analysis complete. Results in {allLevelsOutputDir}");
                }
            }

            Console.WriteLine("\n✅ Geometry Swapper Done! Test the output in RPCS3.");
        }
    }
}
