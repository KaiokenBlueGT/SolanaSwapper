using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using SixLabors.ImageSharp.PixelFormats;

// Use explicit aliases to avoid conflicts
using LibTexture = LibReplanetizer.Texture;
using LibVector3 = OpenTK.Mathematics.Vector3;
using LibVector2 = OpenTK.Mathematics.Vector2;
using LibQuaternion = OpenTK.Mathematics.Quaternion;
using static LibReplanetizer.DataFunctions;

// Add LibLunacy using directive with specific aliases
using LibLunacy;
using LunacyVector2 = System.Numerics.Vector2; // ReLunacy uses System.Numerics

namespace GeometrySwapper
{
    /// <summary>
    /// Converts R&C Future Series levels (ToD/ACiT) to UYA format
    /// Uses ReLunacy parsing techniques for Future level data
    /// </summary>
    public static class FutureToUyaConverter
    {
        // Add these fields to store ReLunacy data
        private static AssetLoader? _currentAssetLoader;
        private static FileManager? _currentFileManager;
        private static FutureLevel? _storedFutureLevel; // Add this line

        public static void ConvertFutureLevelToUya(
            string futureLevelPath,
            string uyaDonorPath,
            string outputPath,
            FutureSwapOptions options)
        {
            Console.WriteLine("🌙 Starting ToD/ACiT → UYA Level Conversion");
            Console.WriteLine($"📂 Future Level Path: {futureLevelPath}");
            Console.WriteLine($"📂 UYA Donor Path: {uyaDonorPath}");
            Console.WriteLine($"📂 Output Path: {outputPath}");
            Console.WriteLine($"⚙️ Options: {options}");

            try
            {
                // Step 1: Parse Future level using ReLunacy-style parsing
                Console.WriteLine("\n=== Step 1: Parsing Future Level ===");
                var futureLevel = ParseFutureLevel(futureLevelPath);

                if (futureLevel == null)
                {
                    Console.WriteLine("❌ Failed to parse Future level data");
                    return;
                }

                // Step 2: Load UYA donor using existing Replanetizer
                Console.WriteLine("\n=== Step 2: Loading UYA Donor Level ===");
                string uyaEnginePath = Path.Combine(uyaDonorPath, "engine.ps3");
                if (!File.Exists(uyaEnginePath))
                {
                    Console.WriteLine($"❌ UYA engine.ps3 not found at: {uyaEnginePath}");
                    return;
                }

                var uyaDonor = new Level(uyaEnginePath);
                Console.WriteLine($"✅ Loaded UYA donor level with {uyaDonor.terrainEngine?.fragments?.Count ?? 0} terrain fragments");

                // Step 3: Convert assets using existing conversion pipeline
                Console.WriteLine("\n=== Step 3: Converting Future Assets to UYA ===");
                ConvertFutureAssetsToUya(futureLevel, uyaDonor, options);

                // Step 4: Save using existing infrastructure
                Console.WriteLine("\n=== Step 4: Saving Converted Level ===");
                Directory.CreateDirectory(outputPath);
                uyaDonor.Save(outputPath);

                Console.WriteLine("🎉 Future → UYA conversion completed successfully!");
                Console.WriteLine($"📁 Output saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during Future → UYA conversion: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Update the ParseFutureLevel method to actually call ReLunacy's AssetExtractor
        private static FutureLevel? ParseFutureLevel(string levelPath)
        {
            Console.WriteLine($"🔍 Parsing Future level from: {levelPath}");

            // Step 1: Validate ReLunacy input requirements
            if (!ValidateReLunacyInput(levelPath))
                return null;

            // Step 2: Use ReLunacy AssetExtractor to extract models
            Console.WriteLine("📦 Calling ReLunacy AssetExtractor...");
            if (!ExtractFutureAssetsWithReLunacy(levelPath))
            {
                Console.WriteLine("❌ Failed to extract Future assets with ReLunacy");
                return null;
            }

            // Step 3: Parse extracted assets into our Future data structures
            var futureLevel = LoadExtractedFutureAssets(levelPath);

            return futureLevel;
        }

        private static void ConvertFutureAssetsToUya(FutureLevel future, Level uya, FutureSwapOptions options)
        {
            Console.WriteLine("🔄 Converting Future assets to UYA format...");

            // STEP 1: Clear existing terrain fragments from donor level
            Console.WriteLine("\n=== Step 1: Clearing Donor Level Terrain ===");
            ClearDonorTerrain(uya);

            if (options.HasFlag(FutureSwapOptions.UFrags))
            {
                ConvertUFragsToTerrain(future, uya);
            }

            if (options.HasFlag(FutureSwapOptions.Mobys))
            {
                ConvertMobysToUya(future, uya);
            }

            if (options.HasFlag(FutureSwapOptions.Ties))
            {
                ConvertTiesToUya(future, uya);
            }

            if (options.HasFlag(FutureSwapOptions.Textures))
            {
                ConvertTexturesToUya(future, uya);
            }

            if (options.HasFlag(FutureSwapOptions.Collision))
            {
                ConvertCollisionToUya(future, uya);
            }

            // STEP 2: Apply coordinate transformations and position mappings
            if (options.HasFlag(FutureSwapOptions.PositionMappings))
            {
                Console.WriteLine("\n=== Step 2: Applying Position Mappings ===");
                ApplyPositionMappings(future, uya);
            }
        }

        /// <summary>
        /// Enhanced UFrag to Terrain conversion with debug export
        /// </summary>
        private static void ConvertUFragsToTerrain(FutureLevel future, Level uya)
        {
            Console.WriteLine("🏔️ Converting FILTERED Future UFrags to UYA terrain...");

            if (future.UFrags == null || future.UFrags.Count == 0)
            {
                Console.WriteLine("  ⚠️ No UFrags found in Future level");
                return;
            }

            if (uya.terrainEngine == null)
            {
                var fragments = new List<TerrainFragment>();
                uya.terrainEngine = new Terrain(fragments, 0);
            }

            Console.WriteLine($"  📊 Converting {future.UFrags.Count} FILTERED UFrags to UYA TerrainFragments...");

            // DIAGNOSTIC: Export first few UFrags to OBJ for verification
            string debugOutputPath = Path.Combine(Path.GetTempPath(), "UFrag_Debug");
            Directory.CreateDirectory(debugOutputPath);

            int convertedFragments = 0;
            int totalModels = 0;
            int skippedCount = 0;

            foreach (var ufrag in future.UFrags.Take(5)) // Export first 5 for verification
            {
                ExportUFragToOBJ(ufrag, debugOutputPath);
            }

            foreach (var ufrag in future.UFrags)
            {
                try
                {
                    if (ufrag.VertexData == null || ufrag.IndexData == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Create multiple terrain models from this UFrag (may split large meshes)
                    var terrainModels = CreateSplitTerrainModelsFromUFrag(ufrag);

                    foreach (var terrainModel in terrainModels)
                    {
                        // Create a TerrainFragment for each model
                        var terrainFragment = CreateTerrainFragmentFromModel(ufrag, terrainModel);
                        if (terrainFragment != null)
                        {
                            uya.terrainEngine.fragments.Add(terrainFragment);
                            convertedFragments++;
                        }
                    }

                    totalModels += terrainModels.Count;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ Failed to convert UFrag {ufrag.ID}: {ex.Message}");
                    skippedCount++;
                }
            }

            Console.WriteLine($"  ✅ Converted {future.UFrags.Count - skippedCount}/{future.UFrags.Count} FILTERED UFrags into {convertedFragments} TerrainFragments ({totalModels} models total)");
            Console.WriteLine($"  📁 Debug OBJs exported to: {debugOutputPath}");
            if (skippedCount > 0)
            {
                Console.WriteLine($"  ⚠️ Skipped {skippedCount} UFrags due to missing data");
            }
        }

        /// <summary>
        /// Creates a TerrainFragment from a TerrainModel
        /// </summary>
        private static TerrainFragment? CreateTerrainFragmentFromModel(FutureUFrag ufrag, TerrainModel terrainModel)
        {
            try
            {
                const float SCALE_FACTOR = 0.1f;

                var boundingBox = ufrag.BoundingBox;
                var cullingCenter = boundingBox?.center ?? LibVector3.Zero;
                var cullingSize = boundingBox?.size.Length ?? 1.0f;

                cullingCenter *= SCALE_FACTOR;
                cullingSize *= SCALE_FACTOR;

                var fragment = (TerrainFragment) System.Runtime.Serialization.FormatterServices
                    .GetUninitializedObject(typeof(TerrainFragment));

                fragment.position = new OpenTK.Mathematics.Vector3(cullingCenter.X, cullingCenter.Y, cullingCenter.Z);
                fragment.scale = OpenTK.Mathematics.Vector3.One;
                fragment.rotation = OpenTK.Mathematics.Quaternion.Identity;
                fragment.modelMatrix = OpenTK.Mathematics.Matrix4.Identity;
                fragment.reflection = OpenTK.Mathematics.Matrix4.Identity;

                fragment.model = terrainModel;
                fragment.modelID = terrainModel.id;

                fragment.cullingCenter = new OpenTK.Mathematics.Vector3(cullingCenter.X, cullingCenter.Y, cullingCenter.Z);
                fragment.cullingSize = cullingSize;

                fragment.off1C = 0xFFFF;
                fragment.off1E = (ushort) ufrag.ID;
                fragment.off20 = 0xFF00;
                fragment.off24 = 0;
                fragment.off28 = 0;
                fragment.off2C = 0;

                return fragment;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ Error creating TerrainFragment: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets Ratchet's position to match a Future moby instance
        /// </summary>
        private static void ApplyRatchetPosition(Dictionary<string, LibVector3> futurePositions, Level uya)
        {
            Console.WriteLine("🤖 Setting Ratchet's position...");

            // Look for Ratchet-like instances
            var ratchetKeys = futurePositions.Keys.Where(k =>
                k.Contains("Ratchet", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Player", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("0030", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (ratchetKeys.Count == 0)
            {
                Console.WriteLine("  ⚠️ No Future Ratchet position found, skipping");
                return;
            }

            var ratchetPos = futurePositions[ratchetKeys.First()];

            // Find Ratchet in the UYA level (usually Model ID 0)
            var ratchet = uya.mobs?.FirstOrDefault(m => m.modelID == 0);
            if (ratchet == null)
            {
                Console.WriteLine("  ⚠️ Ratchet (Model ID 0) not found in UYA level");
                return;
            }

            var oldPos = ratchet.position;
            ratchet.position = new OpenTK.Mathematics.Vector3(ratchetPos.X, ratchetPos.Y, ratchetPos.Z);
            ratchet.UpdateTransformMatrix();

            Console.WriteLine($"  ✅ Moved Ratchet from {oldPos} to {ratchet.position}");
        }

        /// <summary>
        /// Sets the ship position in level variables
        /// </summary>
        private static void ApplyShipPosition(Dictionary<string, LibVector3> futurePositions, Level uya)
        {
            Console.WriteLine("🚀 Setting ship position...");

            // Look for ship-like instances
            var shipKeys = futurePositions.Keys.Where(k =>
                k.Contains("Ship", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Aphelion", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("0039", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (shipKeys.Count == 0)
            {
                Console.WriteLine("  ⚠️ No Future ship position found, skipping");
                return;
            }

            var shipPos = futurePositions[shipKeys.First()];

            if (uya.levelVariables == null)
            {
                Console.WriteLine("  ⚠️ Level variables not available");
                return;
            }

            var oldShipPos = uya.levelVariables.shipPosition;
            uya.levelVariables.shipPosition = new OpenTK.Mathematics.Vector3(shipPos.X, shipPos.Y, shipPos.Z);

            Console.WriteLine($"  ✅ Moved ship from {oldShipPos} to {uya.levelVariables.shipPosition}");
        }

        /// <summary>
        /// Maps crate positions from Future mobys to UYA crates
        /// </summary>
        private static void ApplyCratePositions(Dictionary<string, LibVector3> futurePositions, Level uya)
        {
            Console.WriteLine("📦 Mapping crate positions...");

            // Look for crate-like instances
            var crateKeys = futurePositions.Keys.Where(k =>
                k.Contains("Crate", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Box", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var crateMapping = new Dictionary<int, string>
            {
                { 505, "Explosive" }, // Explosive Crate
                { 500, "Standard" }, // Standard Crate  
                { 501, "Nanotech" }, // Nanotech Crate
                { 511, "Ammo" }      // Ammo Crate
            };

            int cratesFound = 0;
            foreach (var (uyaModelId, crateType) in crateMapping)
            {
                // Find a matching future crate
                var matchingKey = crateKeys.FirstOrDefault(k =>
                    k.Contains(crateType, StringComparison.OrdinalIgnoreCase));

                if (matchingKey == null) continue;

                var cratePos = futurePositions[matchingKey];

                // Find existing crates of this type in UYA level
                var existingCrates = uya.mobs?.Where(m => m.modelID == uyaModelId).ToList();
                if (existingCrates == null || existingCrates.Count == 0) continue;

                // Move the first crate of this type to the Future position
                var crate = existingCrates[0];
                var oldPos = crate.position;
                crate.position = new OpenTK.Mathematics.Vector3(cratePos.X, cratePos.Y, cratePos.Z);
                crate.UpdateTransformMatrix();

                Console.WriteLine($"  ✅ Moved {crateType} crate (Model {uyaModelId}) from {oldPos} to {crate.position}");
                cratesFound++;
            }

            Console.WriteLine($"  📦 Mapped {cratesFound} crate positions");
        }

        /// <summary>
        /// Maps vendor position from Future to UYA Gadgetron Vendor
        /// </summary>
        private static void ApplyVendorPosition(Dictionary<string, LibVector3> futurePositions, Level uya)
        {
            Console.WriteLine("🏪 Setting vendor position...");

            // Look for vendor-like instances
            var vendorKeys = futurePositions.Keys.Where(k =>
                k.Contains("Vendor", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("GrummelNet", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("Shop", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (vendorKeys.Count == 0)
            {
                Console.WriteLine("  ⚠️ No Future vendor position found, skipping");
                return;
            }

            var vendorPos = futurePositions[vendorKeys.First()];

            // Find Gadgetron Vendor in UYA level (Model ID 11)
            var vendor = uya.mobs?.FirstOrDefault(m => m.modelID == 11);
            if (vendor == null)
            {
                Console.WriteLine("  ⚠️ Gadgetron Vendor (Model ID 11) not found in UYA level");
                return;
            }

            var oldPos = vendor.position;
            vendor.position = new OpenTK.Mathematics.Vector3(vendorPos.X, vendorPos.Y, vendorPos.Z);
            vendor.UpdateTransformMatrix();

            Console.WriteLine($"  ✅ Moved vendor from {oldPos} to {vendor.position}");
        }

        /// <summary>
        /// REMOVED: GetMobyPosition - no longer used (we get world transforms directly)
        /// </summary>
        private static LibVector3? GetMobyPosition(object moby)
        {
            // This method is deprecated - we now extract world transforms directly
            return null;
        }

        /// <summary>
        /// Validates mesh integrity before processing
        /// </summary>
        private static bool ValidateMeshIntegrity(FutureUFrag ufrag, int vertexCount, int indexCount)
        {
            // Check for basic sanity
            if (vertexCount <= 0 || indexCount <= 0)
            {
                Console.WriteLine($"    ❌ UFrag {ufrag.ID}: Invalid counts - vertices: {vertexCount}, indices: {indexCount}");
                return false;
            }

            if (indexCount % 3 != 0)
            {
                Console.WriteLine($"    ❌ UFrag {ufrag.ID}: Index count {indexCount} not divisible by 3");
                return false;
            }

            // Check index bounds
            if (ufrag.IndexData == null)
            {
                Console.WriteLine($"    ❌ UFrag {ufrag.ID}: IndexData is null");
                return false;
            }
            int maxIndex = ufrag.IndexData.Max();
            if (maxIndex >= vertexCount)
            {
                Console.WriteLine($"    ❌ UFrag {ufrag.ID}: Max index {maxIndex} >= vertex count {vertexCount}");
                return false;
            }

            // Check for NaN vertices
            if (ufrag.VertexData == null)
            {
                Console.WriteLine($"    ❌ UFrag {ufrag.ID}: VertexData is null");
                return false;
            }
            for (int i = 0; i < ufrag.VertexData.Length; i++)
            {
                if (float.IsNaN(ufrag.VertexData[i]) || float.IsInfinity(ufrag.VertexData[i]))
                {
                    Console.WriteLine($"    ❌ UFrag {ufrag.ID}: Invalid vertex data at index {i}");
                    return false;
                }
            }

            // Check bounding box validity
            if (ufrag.BoundingBox.HasValue)
            {
                var bbox = ufrag.BoundingBox.Value;
                float volume = bbox.size.X * bbox.size.Y * bbox.size.Z;
                if (volume <= 0)
                {
                    Console.WriteLine($"    ⚠️ UFrag {ufrag.ID}: Zero or negative bounding box volume");
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a single terrain model - NO SCALING (already done in vertex conversion)
        /// </summary>
        private static TerrainModel? CreateSingleTerrainModelFixed(FutureUFrag ufrag, int vertexStart, int vertexCount, int indexStart, int indexCount, float scaleFactor, int[]? customIndices = null, int[]? customVertexMapping = null)
        {
            try
            {
                // Create TerrainModel using reflection
                var terrainModel = (TerrainModel) System.Runtime.Serialization.FormatterServices
                    .GetUninitializedObject(typeof(TerrainModel));

                terrainModel.id = GetNextTerrainModelId();
                terrainModel.size = 1.0f; // FIXED: Don't use scaleFactor here anymore
                typeof(TerrainModel).GetProperty("vertexStride", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.SetValue(terrainModel, 8);

                // Use custom vertex mapping if provided, otherwise use sequential range
                var actualVertexCount = customVertexMapping?.Length ?? vertexCount;
                if (ufrag.VertexData == null)
                {
                    Console.WriteLine($"    ❌ UFrag {ufrag.ID}: VertexData is null");
                    return null;
                }
                terrainModel.vertexBuffer = new float[actualVertexCount * 8];

                for (int i = 0; i < actualVertexCount; i++)
                {
                    int sourceVertexIndex = customVertexMapping?[i] ?? (vertexStart + i);
                    int srcIndex = sourceVertexIndex * 8;
                    int dstIndex = i * 8;

                    // Position (x, y, z) - NO SCALING (already done in ConvertToInterleavedVertexDataEnhanced)
                    terrainModel.vertexBuffer[dstIndex + 0] = ufrag.VertexData[srcIndex + 0];
                    terrainModel.vertexBuffer[dstIndex + 1] = ufrag.VertexData[srcIndex + 1];
                    terrainModel.vertexBuffer[dstIndex + 2] = ufrag.VertexData[srcIndex + 2];

                    // Normals (nx, ny, nz) - Keep original normals for proper lighting
                    terrainModel.vertexBuffer[dstIndex + 3] = ufrag.VertexData[srcIndex + 5];
                    terrainModel.vertexBuffer[dstIndex + 4] = ufrag.VertexData[srcIndex + 6];
                    terrainModel.vertexBuffer[dstIndex + 5] = ufrag.VertexData[srcIndex + 7];

                    // UVs (u, v) - Use original UVs without debug scaling
                    terrainModel.vertexBuffer[dstIndex + 6] = ufrag.VertexData[srcIndex + 3];
                    terrainModel.vertexBuffer[dstIndex + 7] = ufrag.VertexData[srcIndex + 4];
                }

                // Use custom indices if provided, otherwise use sequential range
                var actualIndexCount = customIndices?.Length ?? indexCount;
                if (ufrag.IndexData == null)
                {
                    Console.WriteLine($"    ❌ UFrag {ufrag.ID}: IndexData is null");
                    return null;
                }
                terrainModel.indexBuffer = new ushort[actualIndexCount];

                if (customIndices != null)
                {
                    for (int i = 0; i < actualIndexCount; i++)
                    {
                        terrainModel.indexBuffer[i] = (ushort) customIndices[i];
                    }
                }
                else
                {
                    for (int i = 0; i < actualIndexCount; i++)
                    {
                        terrainModel.indexBuffer[i] = (ushort) ufrag.IndexData[indexStart + i];
                    }
                }

                // Create DEBUG RGBA data - color by chunk ID for easier identification
                terrainModel.rgbas = new byte[actualVertexCount * 4];
                terrainModel.lights = new List<int>();

                // Generate a unique color per UFrag for debugging
                var debugColor = GenerateDebugColor(ufrag.ID);

                for (int i = 0; i < actualVertexCount; i++)
                {
                    terrainModel.rgbas[i * 4 + 0] = debugColor.R; // R
                    terrainModel.rgbas[i * 4 + 1] = debugColor.G; // G  
                    terrainModel.rgbas[i * 4 + 2] = debugColor.B; // B
                    terrainModel.rgbas[i * 4 + 3] = 255; // A - full opacity
                    terrainModel.lights.Add(0); // Flat lighting for debug
                }

                // Use debug material - checkerboard texture for UV visualization
                terrainModel.textureConfig = new List<TextureConfig>();
                var textureConfig = new TextureConfig
                {
                    id = 261, // Use UYA texture, but this should be a checkerboard debug texture
                    start = 0,
                    size = terrainModel.indexBuffer.Length / 3,
                    wrapModeS = TextureConfig.WrapMode.Repeat,
                    wrapModeT = TextureConfig.WrapMode.Repeat
                };
                terrainModel.textureConfig.Add(textureConfig);

                return terrainModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ Error creating single terrain model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Split mesh by material (group triangles by texture)
        /// </summary>
        private static List<TerrainModel> SplitMeshByMaterial(FutureUFrag ufrag, int maxVerts, int maxIndices, float scaleFactor)
        {
            var models = new List<TerrainModel>();

            try
            {
                // For now, assume single material per UFrag
                // TODO: Implement proper material detection from texture IDs

                // Just use triangle splitting for now
                return SplitMeshByTriangles(ufrag, maxVerts, maxIndices, scaleFactor);
            }
            catch
            {
                return new List<TerrainModel>();
            }
        }

        /// <summary>
        /// Splits mesh by preserving complete triangles - best quality
        /// </summary>
        private static List<TerrainModel> SplitMeshByTriangles(FutureUFrag ufrag, int maxVerts, int maxIndices, float scaleFactor)
        {
            var models = new List<TerrainModel>();

            try
            {
                if (ufrag.IndexData == null)
                    return models; // Prevent null dereference

                int triangleCount = ufrag.IndexData.Length / 3;
                var usedVertices = new HashSet<int>();
                var currentChunkVertices = new HashSet<int>();
                var currentChunkTriangles = new List<int>();

                for (int triIndex = 0; triIndex < triangleCount; triIndex++)
                {
                    int baseIdx = triIndex * 3;
                    var triVerts = new int[] {
                ufrag.IndexData[baseIdx],
                ufrag.IndexData[baseIdx + 1],
                ufrag.IndexData[baseIdx + 2]
            };

                    // Check if adding this triangle would exceed limits
                    var newVertices = triVerts.Where(v => !currentChunkVertices.Contains(v)).ToList();

                    if (currentChunkVertices.Count + newVertices.Count > maxVerts ||
                        currentChunkTriangles.Count * 3 + 3 > maxIndices)
                    {
                        // Finish current chunk
                        if (currentChunkTriangles.Count > 0)
                        {
                            var model = CreateModelFromTriangleChunk(ufrag, currentChunkVertices, currentChunkTriangles, scaleFactor);
                            if (model != null) models.Add(model);
                        }

                        // Start new chunk
                        currentChunkVertices.Clear();
                        currentChunkTriangles.Clear();
                    }

                    // Add triangle to current chunk
                    foreach (var v in triVerts)
                        currentChunkVertices.Add(v);
                    currentChunkTriangles.Add(triIndex);
                }

                // Finish last chunk
                if (currentChunkTriangles.Count > 0)
                {
                    var model = CreateModelFromTriangleChunk(ufrag, currentChunkVertices, currentChunkTriangles, scaleFactor);
                    if (model != null) models.Add(model);
                }

                return models;
            }
            catch
            {
                return new List<TerrainModel>(); // Return empty on failure
            }
        }

        /// <summary>
        /// Clears existing terrain fragments from the donor level to avoid conflicts
        /// </summary>
        private static void ClearDonorTerrain(Level uya)
        {
            Console.WriteLine("🧹 Clearing existing terrain from donor level...");

            if (uya.terrainEngine?.fragments != null)
            {
                int originalCount = uya.terrainEngine.fragments.Count;
                uya.terrainEngine.fragments.Clear();
                Console.WriteLine($"  ✅ Removed {originalCount} existing terrain fragments");
            }
            else
            {
                Console.WriteLine("  ℹ️ No existing terrain fragments to remove");
            }

            // Also clear terrain chunks if they exist
            if (uya.terrainChunks != null && uya.terrainChunks.Count > 0)
            {
                int chunkCount = uya.terrainChunks.Count;
                uya.terrainChunks.Clear();
                Console.WriteLine($"  ✅ Removed {chunkCount} terrain chunks");
            }
        }

        /// <summary>
        /// Applies coordinate transformations and maps Future moby positions to UYA equivalents
        /// </summary>
        private static void ApplyPositionMappings(FutureLevel future, Level uya)
        {
            Console.WriteLine("🎯 Applying position mappings from Future level to UYA level...");

            // Extract Future moby positions using ReLunacy data
            var futurePositions = ExtractFutureMobyPositions();

            if (futurePositions.Count == 0)
            {
                Console.WriteLine("  ⚠️ No Future moby positions available for mapping");
                return;
            }

            Console.WriteLine($"  📍 Found {futurePositions.Count} Future moby positions to map");

            // Apply coordinate transformations and position mappings
            ApplyRatchetPosition(futurePositions, uya);
            ApplyShipPosition(futurePositions, uya);
            ApplyCratePositions(futurePositions, uya);
            ApplyVendorPosition(futurePositions, uya);
        }

        /// <summary>
        /// Validates that the input path contains the files ReLunacy needs
        /// </summary>
        private static bool ValidateReLunacyInput(string levelPath)
        {
            string mainDatPath = Path.Combine(levelPath, "main.dat");
            string assetLookupPath = Path.Combine(levelPath, "assetlookup.dat");
            string highMipsPath = Path.Combine(levelPath, "highmips.dat");

            Console.WriteLine($"  🔍 Looking for main.dat: {File.Exists(mainDatPath)}");
            Console.WriteLine($"  🔍 Looking for assetlookup.dat: {File.Exists(assetLookupPath)}");
            Console.WriteLine($"  🔍 Looking for highmips.dat: {File.Exists(highMipsPath)}");

            bool hasMainDat = File.Exists(mainDatPath);
            bool hasAssetLookup = File.Exists(assetLookupPath);
            bool hasHighMips = File.Exists(highMipsPath);

            if (!hasMainDat && !hasAssetLookup)
            {
                Console.WriteLine("❌ Neither main.dat nor assetlookup.dat found");
                return false;
            }

            if (hasAssetLookup && !hasHighMips)
            {
                Console.WriteLine("❌ assetlookup.dat found but highmips.dat is missing");
                Console.WriteLine("   ReLunacy requires highmips.dat when using assetlookup.dat");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calls ReLunacy's AssetExtractor to extract models from Future level
        /// </summary>
        private static bool ExtractFutureAssetsWithReLunacy(string levelPath)
        {
            // For Future to UYA conversion, we need direct access to the loaded data
            // The subprocess AssetExtractor can't share its memory with our process
            Console.WriteLine("🔧 For Future→UYA conversion, using direct ReLunacy integration to access data...");
            return ExtractFutureAssetsDirectly(levelPath);

            /* COMMENTED OUT - Keep for reference if we need subprocess later
            try
            {
                // Try multiple potential paths for AssetExtractor.exe
                string[] possiblePaths = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AssetExtractor.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ReLunacy", "AssetExtractor.exe"),
                    Path.Combine(levelPath, "..", "..", "AssetExtractor.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AssetExtractor.exe")
                };

                string relunacyPath = "";
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        relunacyPath = path;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(relunacyPath))
                {
                    Console.WriteLine("❌ AssetExtractor.exe not found - trying direct ReLunacy integration");
                    // Try to use ReLunacy directly instead of subprocess
                    return ExtractFutureAssetsDirectly(levelPath);
                }

                Console.WriteLine($"🔧 Running: {relunacyPath} \"{levelPath}\"");

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = relunacyPath,
                    Arguments = levelPath, // Remove --export-all flag that doesn't exist
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine("❌ Failed to start AssetExtractor process");
                        return ExtractFutureAssetsDirectly(levelPath);
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine("✅ AssetExtractor completed successfully");
                        if (!string.IsNullOrEmpty(output))
                            Console.WriteLine($"📝 Output: {output}");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"❌ AssetExtractor failed with exit code: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(error))
                            Console.WriteLine($"❌ Error: {error}");
                        Console.WriteLine("⚠️ Trying direct ReLunacy integration");
                        return ExtractFutureAssetsDirectly(levelPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception running AssetExtractor: {ex.Message}");
                Console.WriteLine("⚠️ Trying direct ReLunacy integration");
                return ExtractFutureAssetsDirectly(levelPath);
            }
            */
        }

        /// <summary>
        /// Use ReLunacy library directly instead of subprocess
        /// </summary>
        private static bool ExtractFutureAssetsDirectly(string levelPath)
        {
            try
            {
                Console.WriteLine("🔗 Using ReLunacy library directly...");

                var fileManager = new FileManager();
                fileManager.LoadFolder(levelPath);

                var assetLoader = new AssetLoader(fileManager);
                var progress = new LunacyVector2(); // Use System.Numerics.Vector2 for ReLunacy
                float totalProgress = 0;
                string status = "";

                assetLoader.LoadAssets(ref progress, ref totalProgress, ref status);

                Console.WriteLine($"✅ ReLunacy loaded assets: {status}");
                Console.WriteLine($"📊 Loaded: {assetLoader.mobys.Count} mobys, {assetLoader.ties.Count} ties, {assetLoader.textures.Count} textures, {assetLoader.zones.Count} zones");

                // Count UFrags from all zones
                int totalUFrags = 0;
                foreach (var zoneGroup in assetLoader.ufrags)
                {
                    totalUFrags += zoneGroup.Value.Count;
                }
                Console.WriteLine($"📊 Total UFrags across all zones: {totalUFrags}");

                // Store the asset loader for later use
                _currentAssetLoader = assetLoader;
                _currentFileManager = fileManager;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to use ReLunacy directly: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Loads the assets extracted by ReLunacy into our Future data structures
        /// </summary>
        private static FutureLevel LoadExtractedFutureAssets(string levelPath)
        {
            Console.WriteLine("📁 Loading extracted Future assets...");

            var futureLevel = new FutureLevel
            {
                LevelPath = levelPath,
                HasMainDat = File.Exists(Path.Combine(levelPath, "main.dat")),
                HasAssetLookup = File.Exists(Path.Combine(levelPath, "assetlookup.dat")),
                HasHighMips = File.Exists(Path.Combine(levelPath, "highmips.dat")),
                HasTexStream = File.Exists(Path.Combine(levelPath, "texstream.dat")),
                HasDebugData = File.Exists(Path.Combine(levelPath, "debug.dat")),
                UFrags = new List<FutureUFrag>(),
                Mobys = new List<FutureMoby>(),
                Ties = new List<FutureTie>(),
                Textures = new List<FutureTexture>()
            };

            // Try to load from ReLunacy data first
            if (_currentAssetLoader != null)
            {
                LoadFromReLunacyData(futureLevel, _currentAssetLoader);
            }
            else
            {
                // Fallback to file-based loading
                LoadExtractedModels(levelPath, futureLevel);
                LoadExtractedTextures(levelPath, futureLevel);
            }

            Console.WriteLine($"✅ Loaded {futureLevel.UFrags.Count} UFrags, {futureLevel.Mobys.Count} Mobys, {futureLevel.Ties.Count} Ties, {futureLevel.Textures.Count} Textures");

            return futureLevel;
        }

        /// <summary>
        /// Load data directly from ReLunacy's AssetLoader
        /// </summary>
        private static void LoadFromReLunacyData(FutureLevel futureLevel, AssetLoader assetLoader)
        {
            Console.WriteLine("📦 Loading data from ReLunacy AssetLoader...");

            // Store the Future level for position access
            _storedFutureLevel = futureLevel;

            // Load UFrags from zones
            LoadUFragsFromReLunacy(futureLevel, assetLoader);

            // Load Mobys
            LoadMobysFromReLunacy(futureLevel, assetLoader);

            // Load Ties  
            LoadTiesFromReLunacy(futureLevel, assetLoader);

            // Load Textures
            LoadTexturesFromReLunacy(futureLevel, assetLoader);
        }

        /// <summary>
        /// Enhanced UFrag loading with PROPER filtering for terrain-only geometry
        /// </summary>
        private static void LoadUFragsFromReLunacy(FutureLevel futureLevel, AssetLoader assetLoader)
        {
            Console.WriteLine("🏔️ Loading UFrags from ReLunacy with PROPER vertex decode and TERRAIN FILTERING...");

            int ufragCount = 0;
            int loggedDetails = 0;
            int filteredOut = 0;
            const int maxDetailedLogs = 5;

            foreach (var zoneGroup in assetLoader.ufrags)
            {
                Console.WriteLine($"  📍 Processing zone {zoneGroup.Key} with {zoneGroup.Value.Count} UFrags");

                foreach (var ufragPair in zoneGroup.Value)
                {
                    var ufrag = ufragPair.Value;

                    try
                    {
                        // Extract vertex data from ReLunacy UFrag with PROPER decode
                        float[] vertexPositions = ufrag.GetVertPositions();
                        float[] uvCoords = ufrag.GetUVs();
                        uint[] indices = ufrag.GetIndices();

                        // Calculate number of vertices
                        int vertexCount = vertexPositions?.Length / 3 ?? 0;
                        int uvCount = uvCoords?.Length / 2 ?? 0;
                        int indexCount = indices?.Length ?? 0;

                        // CRITICAL: Filter out non-terrain UFrags BEFORE processing
                        if (vertexPositions == null || indices == null || !IsTerrainUFrag(ufrag, vertexPositions, indices, ufragCount))
                        {
                            filteredOut++;
                            ufragCount++;
                            continue;
                        }

                        // DIAGNOSTIC: Print raw data for first UFrag to verify decode
                        if (loggedDetails == 0 && vertexPositions != null && indices != null)
                        {
                            Console.WriteLine($"    🔍 DIAGNOSTIC UFrag {ufragCount} (TERRAIN):");
                            Console.WriteLine($"      Vertex count: {vertexCount}, UV count: {uvCount}, Index count: {indexCount}");

                            if (vertexPositions.Length >= 15)
                            {
                                Console.WriteLine($"      First 5 vertices (raw):");
                                for (int i = 0; i < Math.Min(5, vertexCount); i++)
                                {
                                    int idx = i * 3;
                                    Console.WriteLine($"        V{i}: ({vertexPositions[idx]:F3}, {vertexPositions[idx + 1]:F3}, {vertexPositions[idx + 2]:F3})");
                                }
                            }

                            if (indices.Length >= 9)
                            {
                                Console.WriteLine($"      First 3 triangles (indices):");
                                for (int i = 0; i < Math.Min(9, indices.Length); i += 3)
                                {
                                    Console.WriteLine($"        T{i / 3}: [{indices[i]}, {indices[i + 1]}, {indices[i + 2]}]");
                                }
                            }
                        }

                        // DETECT AND CONVERT TRIANGLE STRIPS
                        int[] processedIndices = indices != null ? ConvertTriangleStripToList(indices) : Array.Empty<int>();

                        // Add null checks before calling ValidateUFragGeometry
                        if (vertexPositions == null || processedIndices == null)
                        {
                            if (loggedDetails < maxDetailedLogs)
                            {
                                Console.WriteLine($"    ❌ UFrag {ufragCount} has null vertex or index data");
                                loggedDetails++;
                            }
                            ufragCount++;
                            continue;
                        }

                        // SANITY CHECKS
                        if (!ValidateUFragGeometry(vertexPositions, processedIndices, ufragCount))
                        {
                            if (loggedDetails < maxDetailedLogs)
                            {
                                Console.WriteLine($"    ❌ UFrag {ufragCount} FAILED geometry validation");
                                loggedDetails++;
                            }
                            ufragCount++;
                            continue;
                        }

                        // Calculate bounding box from vertices
                        var boundingBox = CalculateBoundingBoxFromPositions(vertexPositions);

                        // Convert to our intermediate format with PROPER interleaved data
                        var futureUFrag = new FutureUFrag
                        {
                            ID = ufragCount++,
                            VertexData = ConvertToInterleavedVertexDataEnhanced(vertexPositions, uvCoords),
                            IndexData = processedIndices,
                            TextureIds = new List<int>(), // TODO: Extract from shader
                            BoundingBox = boundingBox
                        };

                        futureLevel.UFrags.Add(futureUFrag);

                        if (loggedDetails < maxDetailedLogs)
                        {
                            Console.WriteLine($"    ✅ UFrag {futureUFrag.ID}: {vertexCount} verts, {processedIndices.Length} indices (TERRAIN)");
                            loggedDetails++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (loggedDetails < maxDetailedLogs)
                        {
                            Console.WriteLine($"    ❌ Failed to process UFrag {ufragCount}: {ex.Message}");
                            loggedDetails++;
                        }
                        ufragCount++;
                    }
                }

            }

            Console.WriteLine($"  ✅ Loaded {futureLevel.UFrags.Count} TERRAIN UFrags from {assetLoader.ufrags.Count} zones");
            Console.WriteLine($"  🗑️ Filtered out {filteredOut} non-terrain UFrags (foliage/occluders/alpha cards)");
            if (loggedDetails >= maxDetailedLogs)
            {
                Console.WriteLine($"  📝 (Detailed logging limited to first {maxDetailedLogs} items)");
            }
        }

        /// <summary>
        /// Determines if a UFrag represents actual terrain geometry vs foliage/occluders/alpha cards
        /// </summary>
        private static bool IsTerrainUFrag(object ufrag, float[] vertexPositions, uint[] indices, int ufragId)
        {
            if (vertexPositions == null || indices == null || vertexPositions.Length < 9 || indices.Length < 3)
                return false;

            int vertexCount = vertexPositions.Length / 3;
            int triangleCount = indices.Length / 3;

            // HEURISTIC 1: Skip very small meshes (likely detail objects, not terrain)
            if (vertexCount < 6 || triangleCount < 2)
            {
                if (ufragId < 10)
                    Console.WriteLine($"    🗑️ UFrag {ufragId}: Too small ({vertexCount} verts, {triangleCount} tris) - likely detail object");
                return false;
            }

            // HEURISTIC 2: Check for billboard/card geometry (very flat quads)
            if (IsLikelyBillboard(vertexPositions, indices))
            {
                if (ufragId < 10)
                    Console.WriteLine($"    🗑️ UFrag {ufragId}: Billboard/card geometry detected - likely foliage");
                return false;
            }

            // HEURISTIC 3: Check triangle density - terrain should have reasonable density
            float averageTriangleArea = CalculateAverageTriangleArea(vertexPositions, indices);
            if (averageTriangleArea < 0.001f || averageTriangleArea > 10000.0f)
            {
                if (ufragId < 10)
                    Console.WriteLine($"    🗑️ UFrag {ufragId}: Extreme triangle area ({averageTriangleArea:F6}) - likely occluder/detail");
                return false;
            }

            // HEURISTIC 4: Check bounding box aspect ratio - terrain shouldn't be too thin
            var bbox = CalculateBoundingBoxFromPositions(vertexPositions);
            if (bbox.HasValue)
            {
                var size = bbox.Value.size;
                float minDimension = Math.Min(Math.Min(size.X, size.Y), size.Z);
                float maxDimension = Math.Max(Math.Max(size.X, size.Y), size.Z);

                if (minDimension > 0 && (maxDimension / minDimension) > 100.0f)
                {
                    if (ufragId < 10)
                        Console.WriteLine($"    🗑️ UFrag {ufragId}: Extreme aspect ratio ({maxDimension:F1}/{minDimension:F3}) - likely card/plane");
                    return false;
                }
            }

            // HEURISTIC 5: Try to get shader/material info for alpha detection
            try
            {
                var shader = ufrag.GetType().GetMethod("GetShader")?.Invoke(ufrag, null);
                if (shader != null)
                {
                    // Check if this is an alpha/transparent material
                    var shaderType = shader.GetType();
                    var nameField = shaderType.GetField("name");
                    var nameProperty = shaderType.GetProperty("name");
                    string shaderName = "";
                    if (nameField != null)
                    {
                        shaderName = nameField.GetValue(shader)?.ToString()?.ToLower() ?? "";
                    }
                    else if (nameProperty != null)
                    {
                        shaderName = nameProperty.GetValue(shader)?.ToString()?.ToLower() ?? "";
                    }

                    if (shaderName.Contains("alpha") || shaderName.Contains("transparent") ||
                        shaderName.Contains("foliage") || shaderName.Contains("billboard"))
                    {
                        if (ufragId < 10)
                            Console.WriteLine($"    🗑️ UFrag {ufragId}: Alpha/transparent shader '{shaderName}' - likely foliage");
                        return false;
                    }
                }
            }
            catch
            {
                // Shader info not available, continue with other heuristics
            }

            // Passed all filters - likely terrain
            return true;
        }

        /// <summary>
        /// Detects billboard/card geometry (flat quads)
        /// </summary>
        private static bool IsLikelyBillboard(float[] vertexPositions, uint[] indices)
        {
            int vertexCount = vertexPositions.Length / 3;
            int triangleCount = indices.Length / 3;

            // Check if this is a simple quad (4 vertices, 2 triangles)
            if (vertexCount == 4 && triangleCount == 2)
            {
                // Calculate if the 4 vertices are roughly coplanar
                var v0 = new LibVector3(vertexPositions[0], vertexPositions[1], vertexPositions[2]);
                var v1 = new LibVector3(vertexPositions[3], vertexPositions[4], vertexPositions[5]);
                var v2 = new LibVector3(vertexPositions[6], vertexPositions[7], vertexPositions[8]);
                var v3 = new LibVector3(vertexPositions[9], vertexPositions[10], vertexPositions[11]);

                // Calculate normal of the plane formed by first 3 vertices
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var normal = LibVector3.Cross(edge1, edge2).Normalized();

                // Check if 4th vertex is also on this plane
                var edge3 = v3 - v0;
                float planarity = Math.Abs(LibVector3.Dot(normal, edge3));

                return planarity < 0.1f; // Very flat = likely billboard
            }

            return false;
        }

        /// <summary>
        /// Calculates average triangle area to detect degenerate geometry
        /// </summary>
        private static float CalculateAverageTriangleArea(float[] vertexPositions, uint[] indices)
        {
            int triangleCount = indices.Length / 3;
            if (triangleCount == 0) return 0;

            float totalArea = 0;
            int validTriangles = 0;

            for (int i = 0; i < triangleCount; i++)
            {
                int baseIdx = i * 3;
                var i1 = (int) indices[baseIdx] * 3;
                var i2 = (int) indices[baseIdx + 1] * 3;
                var i3 = (int) indices[baseIdx + 2] * 3;

                if (i1 + 2 < vertexPositions.Length && i2 + 2 < vertexPositions.Length && i3 + 2 < vertexPositions.Length)
                {
                    var v1 = new LibVector3(vertexPositions[i1], vertexPositions[i1 + 1], vertexPositions[i1 + 2]);
                    var v2 = new LibVector3(vertexPositions[i2], vertexPositions[i2 + 1], vertexPositions[i2 + 2]);
                    var v3 = new LibVector3(vertexPositions[i3], vertexPositions[i3 + 1], vertexPositions[i3 + 2]);

                    var edge1 = v2 - v1;
                    var edge2 = v3 - v1;
                    var cross = LibVector3.Cross(edge1, edge2);
                    float area = cross.Length * 0.5f;

                    totalArea += area;
                    validTriangles++;
                }
            }

            return validTriangles > 0 ? totalArea / validTriangles : 0;
        }

        /// <summary>
        /// Converts triangle strips to triangle lists, handling restart indices
        /// </summary>
        private static int[] ConvertTriangleStripToList(uint[] indices)
        {
            if (indices == null || indices.Length == 0)
                return new int[0];

            // Check if this looks like a triangle strip (has restart indices)
            bool hasRestartIndices = indices.Any(idx => idx == 0xFFFF || idx == 0xFFFFFFFF);

            if (!hasRestartIndices)
            {
                // Assume it's already a triangle list
                return indices.Select(idx => (int) idx).ToArray();
            }

            Console.WriteLine("    🔄 Converting triangle strip to triangle list...");

            var triangleList = new List<int>();
            var currentStrip = new List<uint>();

            foreach (uint index in indices)
            {
                // Check for restart index
                if (index == 0xFFFF || index == 0xFFFFFFFF)
                {
                    // Process current strip
                    ProcessTriangleStrip(currentStrip, triangleList);
                    currentStrip.Clear();
                }
                else
                {
                    currentStrip.Add(index);
                }
            }

            // Process final strip
            if (currentStrip.Count > 0)
            {
                ProcessTriangleStrip(currentStrip, triangleList);
            }

            Console.WriteLine($"    ✅ Converted strip with {indices.Length} indices to list with {triangleList.Count} indices");
            return triangleList.ToArray();
        }

        /// <summary>
        /// Converts a single triangle strip to triangles
        /// </summary>
        private static void ProcessTriangleStrip(List<uint> strip, List<int> triangleList)
        {
            if (strip.Count < 3) return;

            for (int i = 0; i < strip.Count - 2; i++)
            {
                uint v1 = strip[i];
                uint v2 = strip[i + 1];
                uint v3 = strip[i + 2];

                // Skip degenerate triangles
                if (v1 == v2 || v2 == v3 || v1 == v3)
                    continue;

                // Maintain winding order (alternate for each triangle in strip)
                if (i % 2 == 0)
                {
                    // Even triangle: normal order
                    triangleList.Add((int) v1);
                    triangleList.Add((int) v2);
                    triangleList.Add((int) v3);
                }
                else
                {
                    // Odd triangle: swap to maintain winding order
                    triangleList.Add((int) v1);
                    triangleList.Add((int) v3);
                    triangleList.Add((int) v2);
                }
            }
        }

        /// <summary>
        /// Validates UFrag geometry with proper sanity checks
        /// </summary>
        private static bool ValidateUFragGeometry(float[] vertexPositions, int[] indices, int ufragId)
        {
            if (vertexPositions == null || indices == null)
                return false;

            int vertexCount = vertexPositions.Length / 3;

            // Basic sanity checks
            if (vertexCount <= 0 || indices.Length <= 0)
                return false;

            if (indices.Length % 3 != 0)
            {
                Console.WriteLine($"    ❌ UFrag {ufragId}: Index count {indices.Length} not divisible by 3");
                return false;
            }

            // Check index bounds
            int maxIndex = indices.Max();
            if (maxIndex >= vertexCount)
            {
                Console.WriteLine($"    ❌ UFrag {ufragId}: Max index {maxIndex} >= vertex count {vertexCount}");
                return false;
            }

            // Check for NaN vertices
            for (int i = 0; i < vertexPositions.Length; i++)
            {
                if (float.IsNaN(vertexPositions[i]) || float.IsInfinity(vertexPositions[i]))
                {
                    Console.WriteLine($"    ❌ UFrag {ufragId}: Invalid vertex data at index {i}: {vertexPositions[i]}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Helper methods for data conversion and processing
        /// </summary>
        private static float[]? ConvertToInterleavedVertexDataEnhanced(float[]? positions, float[]? uvs)
        {
            if (positions == null) return null;

            int vertexCount = positions.Length / 3;
            bool hasUVs = uvs != null && uvs.Length >= vertexCount * 2;

            // CRITICAL FIX: Apply scale factor IMMEDIATELY when converting from Future to UYA
            const float FUTURE_TO_UYA_SCALE = 0.1f; // Scale down from Future world space to UYA world space

            // Create interleaved vertex data: [x, y, z, u, v, nx, ny, nz] per vertex
            var result = new float[vertexCount * 8];

            for (int i = 0; i < vertexCount; i++)
            {
                int srcPosIndex = i * 3;
                int srcUVIndex = i * 2;
                int dstIndex = i * 8;

                // Position - APPLY SCALE FACTOR HERE, NOT LATER
                result[dstIndex + 0] = positions[srcPosIndex + 0] * FUTURE_TO_UYA_SCALE;
                result[dstIndex + 1] = positions[srcPosIndex + 1] * FUTURE_TO_UYA_SCALE;
                result[dstIndex + 2] = positions[srcPosIndex + 2] * FUTURE_TO_UYA_SCALE;

                // UVs
                if (hasUVs && uvs != null)
                {
                    result[dstIndex + 3] = uvs[srcUVIndex + 0];
                    result[dstIndex + 4] = uvs[srcUVIndex + 1];
                }
                else
                {
                    result[dstIndex + 3] = 0.0f;
                    result[dstIndex + 4] = 0.0f;
                }

                // Normals (pointing up - will be calculated properly later)
                result[dstIndex + 5] = 0.0f;
                result[dstIndex + 6] = 1.0f;
                result[dstIndex + 7] = 0.0f;
            }

            return result;
        }

        private static void LoadMobysFromReLunacy(FutureLevel futureLevel, AssetLoader assetLoader)
        {
            Console.WriteLine("🤖 Loading Mobys from ReLunacy...");

            int mobyCount = 0;
            int positionsFound = 0;

            foreach (var mobyPair in assetLoader.mobys)
            {
                var moby = mobyPair.Value;

                // Extract position from the moby if available
                var position = GetMobyPosition(moby);
                if (position.HasValue)
                {
                    positionsFound++;
                }

                var futureMoby = new FutureMoby
                {
                    MobyID = mobyCount,
                    ModelID = mobyCount,
                    Position = position ?? LibVector3.Zero, // Use extracted position or default
                    Rotation = LibQuaternion.Identity, // TODO: Extract rotation if available
                    Scale = LibVector3.One,
                    VertexData = null, // TODO: Extract using moby.GetBuffers() or similar
                    IndexData = null,
                    TextureIds = new List<int>()
                };

                futureLevel.Mobys.Add(futureMoby);

                // Log position for first few mobys only
                if (mobyCount < 5 && position.HasValue)
                {
                    string mobyName = $"Moby_{mobyPair.Key:X4}";
                    Console.WriteLine($"    📍 {mobyName}: {position} (index {mobyCount})");
                }

                mobyCount++;
            }

            Console.WriteLine($"  ✅ Loaded {mobyCount} Mobys, found {positionsFound} valid positions");
        }

        private static void LoadTiesFromReLunacy(FutureLevel futureLevel, AssetLoader assetLoader)
        {
            Console.WriteLine("🏗️ Loading Ties from ReLunacy...");

            int tieCount = 0;
            foreach (var tiePair in assetLoader.ties)
            {
                var tie = tiePair.Value;

                var futureTie = new FutureTie
                {
                    ModelID = tieCount,
                    Position = LibVector3.Zero, // TODO: Extract position if available
                    Rotation = LibQuaternion.Identity, // TODO: Extract rotation if available  
                    Scale = LibVector3.One,
                    VertexData = null, // TODO: Extract vertex data
                    IndexData = null,
                    TextureIds = new List<int>()
                };

                futureLevel.Ties.Add(futureTie);
                tieCount++;
            }

            Console.WriteLine($"  ✅ Loaded {tieCount} Ties");
        }

        private static void LoadTexturesFromReLunacy(FutureLevel futureLevel, AssetLoader assetLoader)
        {
            Console.WriteLine("🎨 Loading Textures from ReLunacy...");

            int textureCount = 0;
            foreach (var texturePair in assetLoader.textures)
            {
                var texture = texturePair.Value;

                var futureTexture = new FutureTexture
                {
                    ID = textureCount,
                    Width = (int) texture.width,
                    Height = (int) texture.height,
                    Data = null, // TODO: Extract texture data
                    Format = ".dds" // ReLunacy uses DDS
                };

                futureLevel.Textures.Add(futureTexture);
                textureCount++;
            }

            Console.WriteLine($"  ✅ Loaded {textureCount} Textures");
        }

        /// <summary>
        /// Enhanced version that handles more position types
        /// </summary>
        private static LibVector3? ConvertToLibVector3(object position)
        {
            if (position == null) return null;

            var posType = position.GetType();
            Console.WriteLine($"    🔍 Converting position type: {posType.FullName}");

            // Handle System.Numerics.Vector3
            if (posType.Name == "Vector3" && posType.Namespace == "System.Numerics")
            {
                dynamic vec = position;
                var result = new LibVector3(vec.X, vec.Y, vec.Z);
                Console.WriteLine($"    ✅ Converted System.Numerics.Vector3: {result}");
                return result;
            }

            // Handle OpenTK Vector3
            if (position is LibVector3 libVec)
            {
                Console.WriteLine($"    ✅ Already LibVector3: {libVec}");
                return libVec;
            }

            // Handle float arrays
            if (position is float[] floatArray && floatArray.Length >= 3)
            {
                var result = new LibVector3(floatArray[0], floatArray[1], floatArray[2]);
                Console.WriteLine($"    ✅ Converted float array: {result}");
                return result;
            }

            // Handle double arrays (convert to float)
            if (position is double[] doubleArray && doubleArray.Length >= 3)
            {
                var result = new LibVector3((float)doubleArray[0], (float)doubleArray[1], (float)doubleArray[2]);
                Console.WriteLine($"    ✅ Converted double array: {result}");
                return result;
            }

            // Try reflection for X, Y, Z properties
            try
            {
                var xProp = posType.GetProperty("X") ?? posType.GetProperty("x");
                var yProp = posType.GetProperty("Y") ?? posType.GetProperty("y");
                var zProp = posType.GetProperty("Z") ?? posType.GetProperty("z");
                
                if (xProp != null && yProp != null && zProp != null)
                {
                    var x = Convert.ToSingle(xProp.GetValue(position));
                    var y = Convert.ToSingle(yProp.GetValue(position));
                    var z = Convert.ToSingle(zProp.GetValue(position));
                    
                    var result = new LibVector3(x, y, z);
                    Console.WriteLine($"    ✅ Converted via X,Y,Z properties: {result}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ⚠️ Reflection conversion failed: {ex.Message}");
            }

            Console.WriteLine($"    ❌ Could not convert position type: {posType.FullName}");
            return null;
        }

        /// <summary>
        /// Enhanced position extraction using proper world transforms from zone instances
        /// </summary>
        private static Dictionary<string, LibVector3> ExtractFutureMobyPositions()
        {
            var positions = new Dictionary<string, LibVector3>();

            Console.WriteLine("  🔍 Extracting WORLD TRANSFORMS from ReLunacy zone data...");

            // Single global scale factor - applied once at the end
            const float SCALE_FUTURE_TO_UYA = 0.1f;

            // Strategy 1: Extract from gameplay regions (the right way)
            Console.WriteLine("  📦 Strategy 1: Gameplay region instance transforms...");
            var regionPositions = ExtractGameplayRegionPositions(SCALE_FUTURE_TO_UYA);
            positions = positions.Union(regionPositions).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Strategy 2: Fall back to zone tie instances if needed
            if (positions.Count < 5)
            {
                Console.WriteLine("  📦 Strategy 2: Zone tie instance transforms...");
                var zonePositions = ExtractZoneInstancePositions(SCALE_FUTURE_TO_UYA);
                positions = positions.Union(zonePositions).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // Strategy 3: NO MORE BOUNDING SPHERE FALLBACK - that's local space junk

            Console.WriteLine($"  ✅ Final result: {positions.Count} WORLD TRANSFORM positions extracted");

            // Validate that we got real world positions (should be in hundreds/thousands range)
            if (positions.Count > 0)
            {
                var samplePos = positions.Values.First();
                float magnitude = samplePos.Length;
                if (magnitude < 10.0f)
                {
                    Console.WriteLine($"  ⚠️ WARNING: Sample position magnitude {magnitude:F2} - still looks like local space!");
                }
                else
                {
                    Console.WriteLine($"  ✅ Sample position magnitude {magnitude:F2} - looks like world space");
                }
            }

            return positions;
        }

        /// <summary>
        /// Extract positions from gameplay region data with PROPER transform matrices
        /// </summary>
        private static Dictionary<string, LibVector3> ExtractGameplayRegionPositions(float scaleFactor)
        {
            var positions = new Dictionary<string, LibVector3>();

            // Check if AssetLoader has gameplay property using reflection
            var gameplayField = _currentAssetLoader?.GetType().GetField("gameplay");
            var gameplayProperty = _currentAssetLoader?.GetType().GetProperty("gameplay");

            object? gameplay = null;
            if (gameplayField != null)
            {
                gameplay = gameplayField.GetValue(_currentAssetLoader);
            }
            else if (gameplayProperty != null)
            {
                gameplay = gameplayProperty.GetValue(_currentAssetLoader);
            }

            if (gameplay == null)
            {
                Console.WriteLine("  ⚠️ No gameplay data available in AssetLoader");
                return positions;
            }

            Console.WriteLine("  🔍 Extracting PROPER world transforms from gameplay regions...");

            try
            {
                // Get regions from gameplay using reflection
                var gameplayType = gameplay.GetType();
                var regionsField = gameplayType.GetField("regions");
                var regionsProperty = gameplayType.GetProperty("regions");

                object? regions = null;
                if (regionsField != null)
                {
                    regions = regionsField.GetValue(gameplay);
                }
                else if (regionsProperty != null)
                {
                    regions = regionsProperty.GetValue(gameplay);
                }

                if (regions is System.Collections.IEnumerable regionEnumerable)
                {
                    foreach (var region in regionEnumerable)
                    {
                        if (region == null) continue;

                        // Get mobyInstances from region
                        var regionType = region.GetType();
                        var mobyInstancesField = regionType.GetField("mobyInstances");
                        var mobyInstancesProperty = regionType.GetProperty("mobyInstances");

                        object? mobyInstances = null;
                        if (mobyInstancesField != null)
                        {
                            mobyInstances = mobyInstancesField.GetValue(region);
                        }
                        else if (mobyInstancesProperty != null)
                        {
                            mobyInstances = mobyInstancesProperty.GetValue(region);
                        }

                        if (mobyInstances is System.Collections.IDictionary mobyDict)
                        {
                            Console.WriteLine($"  📦 Processing region with {mobyDict.Count} moby instances");

                            foreach (System.Collections.DictionaryEntry entry in mobyDict)
                            {
                                var instanceId = entry.Key;
                                var mobyInstance = entry.Value; // THIS is the actual instance, not the KeyValuePair

                                if (mobyInstance == null) continue;

                                try
                                {
                                    // Get WORLD position from the instance (not bounding sphere!)
                                    var mobyInstanceType = mobyInstance.GetType();

                                    // Look for position field/property
                                    var positionField = mobyInstanceType.GetField("position");
                                    var positionProperty = mobyInstanceType.GetProperty("position");

                                    object? positionObj = null;
                                    if (positionField != null)
                                    {
                                        positionObj = positionField.GetValue(mobyInstance);
                                    }
                                    else if (positionProperty != null)
                                    {
                                        positionObj = positionProperty.GetValue(mobyInstance);
                                    }

                                    if (positionObj != null)
                                    {
                                        var worldPos = ConvertToLibVector3(positionObj);
                                        if (worldPos.HasValue)
                                        {
                                            // Apply SINGLE global scale factor
                                            var scaledPos = worldPos.Value * scaleFactor;

                                            // Get name from mobyInstance
                                            var nameField = mobyInstanceType.GetField("name");
                                            var nameProperty = mobyInstanceType.GetProperty("name");

                                            string instanceName = $"MobyInst_{instanceId}";
                                            if (nameField != null)
                                            {
                                                var nameValue = nameField.GetValue(mobyInstance) as string;
                                                if (!string.IsNullOrEmpty(nameValue))
                                                    instanceName = nameValue;
                                            }
                                            else if (nameProperty != null)
                                            {
                                                var nameValue = nameProperty.GetValue(mobyInstance) as string;
                                                if (!string.IsNullOrEmpty(nameValue))
                                                    instanceName = nameValue;
                                            }

                                            positions[instanceName] = scaledPos;

                                            // Log REAL world positions (should be in hundreds/thousands)
                                            if (positions.Count <= 10)
                                            {
                                                Console.WriteLine($"    📍 {instanceName}: WORLD {worldPos.Value} → UYA {scaledPos} (magnitude: {worldPos.Value.Length:F1})");
                                            }
                                        }
                                    }

                                    // Also try matrix-based transform if available
                                    var transformField = mobyInstanceType.GetField("transform") ?? mobyInstanceType.GetField("transformation");
                                    var transformProperty = mobyInstanceType.GetProperty("transform") ?? mobyInstanceType.GetProperty("transformation");

                                    object? transformObj = null;
                                    if (transformField != null)
                                    {
                                        transformObj = transformField.GetValue(mobyInstance);
                                    }
                                    else if (transformProperty != null)
                                    {
                                        transformObj = transformProperty.GetValue(mobyInstance);
                                    }

                                    if (transformObj != null && positionObj == null)
                                    {
                                        var matrixPos = ExtractPositionFromMatrix(transformObj);
                                        if (matrixPos.HasValue)
                                        {
                                            var scaledPos = matrixPos.Value * scaleFactor;
                                            string instanceName = $"MatrixInst_{instanceId}";
                                            positions[instanceName] = scaledPos;

                                            if (positions.Count <= 5)
                                            {
                                                Console.WriteLine($"    📍 {instanceName}: MATRIX {matrixPos.Value} → UYA {scaledPos} (magnitude: {matrixPos.Value.Length:F1})");
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"    ❌ Error processing moby instance {instanceId}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error extracting gameplay region positions: {ex.Message}");
            }

            Console.WriteLine($"  ✅ Extracted {positions.Count} WORLD positions from gameplay regions");
            return positions;
        }

        /// <summary>
        /// FIXED: Extract positions from zone TIE instances with PROPER .Value access
        /// </summary>
        private static Dictionary<string, LibVector3> ExtractZoneInstancePositions(float scaleFactor)
        {
            var positions = new Dictionary<string, LibVector3>();

            if (_currentAssetLoader?.zones == null)
            {
                Console.WriteLine("  ⚠️ No ReLunacy zone data available");
                return positions;
            }

            Console.WriteLine("  🔍 Extracting from zone instances with PROPER .Value access...");

            foreach (var zonePair in _currentAssetLoader.zones)
            {
                var zone = zonePair.Value;

                try
                {
                    // Look for tieInstances field in the zone
                    var zoneType = zone.GetType();
                    var tieInstancesField = zoneType.GetField("tieInstances");
                    var tieInstancesProperty = zoneType.GetProperty("tieInstances");

                    object? tieInstances = null;
                    if (tieInstancesField != null)
                    {
                        tieInstances = tieInstancesField.GetValue(zone);
                    }
                    else if (tieInstancesProperty != null)
                    {
                        tieInstances = tieInstancesProperty.GetValue(zone);
                    }

                    // CRITICAL FIX: Check if this is a Dictionary and properly access entries
                    if (tieInstances is System.Collections.IDictionary dictionary)
                    {
                        Console.WriteLine($"  📦 Processing zone {zonePair.Key} with {dictionary.Count} tie instance groups");

                        foreach (System.Collections.DictionaryEntry entry in dictionary)
                        {
                            var instanceKey = entry.Key;
                            var instanceValue = entry.Value; // This should be a List<TieInstance>

                            if (instanceValue is System.Collections.IEnumerable instanceList)
                            {
                                int instanceCount = 0;
                                foreach (var instance in instanceList)
                                {
                                    if (instance == null) continue;

                                    try
                                    {
                                        var instanceType = instance.GetType();

                                        // Look for transformation Matrix4x4 field
                                        var transformationField = instanceType.GetField("transformation");
                                        var transformationProperty = instanceType.GetProperty("transformation");

                                        object? transformationObj = null;
                                        if (transformationField != null)
                                        {
                                            transformationObj = transformationField.GetValue(instance);
                                        }
                                        else if (transformationProperty != null)
                                        {
                                            transformationObj = transformationProperty.GetValue(instance);
                                        }

                                        if (transformationObj != null)
                                        {
                                            // Extract position from Matrix4x4 transformation
                                            var worldPos = ExtractPositionFromMatrix(transformationObj);
                                            if (worldPos.HasValue)
                                            {
                                                // Apply single global scale factor
                                                var scaledPos = worldPos.Value * scaleFactor;

                                                string instanceName = $"TieInst_{instanceKey}_{instanceCount:X4}";
                                                positions[instanceName] = scaledPos;

                                                if (positions.Count <= 10)
                                                {
                                                    Console.WriteLine($"    📍 {instanceName}: WORLD {worldPos.Value} → UYA {scaledPos} (magnitude: {worldPos.Value.Length:F1})");
                                                }
                                            }
                                        }

                                        instanceCount++;
                                    }
                                    catch (Exception ex)
                                    {
                                        if (instanceCount < 3)
                                            Console.WriteLine($"    ❌ Error processing instance {instanceCount}: {ex.Message}");
                                        instanceCount++;
                                    }
                                }

                                if (instanceCount > 0)
                                {
                                    Console.WriteLine($"  📦 Processed {instanceCount} instances for key {instanceKey} in zone {zonePair.Key}");
                                }
                            }
                        }
                    }
                    else if (tieInstances is System.Collections.IEnumerable enumerable)
                    {
                        // Fallback: treat as simple enumerable
                        int instanceCount = 0;
                        foreach (var instance in enumerable)
                        {
                            if (instance == null) continue;

                            var worldPos = ExtractInstancePosition(instance, instanceCount);
                            if (worldPos.HasValue)
                            {
                                var scaledPos = worldPos.Value * scaleFactor;
                                string instanceName = $"TieInst_{zonePair.Key}_{instanceCount:X4}";
                                positions[instanceName] = scaledPos;

                                if (instanceCount < 5)
                                {
                                    Console.WriteLine($"    📍 {instanceName}: WORLD {worldPos.Value} → UYA {scaledPos} (magnitude: {worldPos.Value.Length:F1})");
                                }
                            }
                            instanceCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ Error processing zone {zonePair.Key}: {ex.Message}");
                }
            }

            Console.WriteLine($"  ✅ Extracted {positions.Count} positions from zone instances");
            return positions;
        }

        /// <summary>
        /// Extract WORLD position from a zone instance (not bounding sphere!)
        /// </summary>
        private static LibVector3? ExtractWorldPositionFromInstance(object instance, int instanceIndex)
        {
            try
            {
                var instanceType = instance.GetType();
                bool shouldLog = instanceIndex < 3;

                if (shouldLog)
                {
                    Console.WriteLine($"    🔍 Instance {instanceIndex} type: {instanceType.Name}");
                }

                // Look for transform matrix first (most reliable)
                var matrixField = instanceType.GetField("transform") ?? instanceType.GetField("matrix");
                var matrixProperty = instanceType.GetProperty("transform") ?? instanceType.GetProperty("matrix");

                object? matrix = null;
                if (matrixField != null)
                {
                    matrix = matrixField.GetValue(instance);
                }
                else if (matrixProperty != null)
                {
                    matrix = matrixProperty.GetValue(instance);
                }

                if (matrix != null)
                {
                    var position = ExtractPositionFromMatrix(matrix);
                    if (position.HasValue && shouldLog)
                    {
                        Console.WriteLine($"    ✅ Extracted world position from transform matrix: {position.Value}");
                    }
                    return position;
                }

                // Try world position field names (not local ones!)
                var worldPositionFields = new[] { "worldPosition", "position", "pos" };

                foreach (var fieldName in worldPositionFields)
                {
                    var field = instanceType.GetField(fieldName);
                    if (field != null)
                    {
                        var value = field.GetValue(instance);
                        if (value != null)
                        {
                            var converted = ConvertToLibVector3(value);
                            if (converted.HasValue && shouldLog)
                            {
                                Console.WriteLine($"    ✅ Found world position field '{fieldName}': {converted.Value}");
                            }
                            return converted;
                        }
                    }

                    var property = instanceType.GetProperty(fieldName);
                    if (property != null)
                    {
                        var value = property.GetValue(instance);
                        if (value != null)
                        {
                            var converted = ConvertToLibVector3(value);
                            if (converted.HasValue && shouldLog)
                            {
                                Console.WriteLine($"    ✅ Found world position property '{fieldName}': {converted.Value}");
                            }
                            return converted;
                        }
                    }
                }

                if (shouldLog)
                {
                    var fields = instanceType.GetFields();
                    Console.WriteLine($"    🔍 Available fields: {string.Join(", ", fields.Take(8).Select(f => f.Name))}");
                }

                return null;
            }
            catch (Exception ex)
            {
                if (instanceIndex < 3)
                {
                    Console.WriteLine($"    ❌ Error extracting world position from instance {instanceIndex}: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// NEW METHOD: Extracts positions from zone instance data with consistent scaling
        /// </summary>
        private static Dictionary<string, LibVector3> ExtractZoneInstancePositions()
        {
            var positions = new Dictionary<string, LibVector3>();

            if (_currentAssetLoader?.zones == null)
            {
                Console.WriteLine("  ⚠️ No ReLunacy zone data available");
                return positions;
            }

            Console.WriteLine("  🔍 Extracting moby instance positions from zone data...");

            // Use the SAME scale factor as terrain conversion
            const float POSITION_SCALE_FACTOR = 0.1f; // Match terrain scaling

            foreach (var zonePair in _currentAssetLoader.zones)
            {
                var zone = zonePair.Value;

                try
                {
                    // Try to get the tieInstances field which should contain positioned objects
                    var zoneType = zone.GetType();
                    var tieInstancesField = zoneType.GetField("tieInstances");

                    if (tieInstancesField != null)
                    {
                        var tieInstances = tieInstancesField.GetValue(zone);

                        if (tieInstances is System.Collections.IEnumerable enumerable)
                        {
                            int instanceCount = 0;
                            foreach (var instance in enumerable)
                            {
                                var position = ExtractInstancePosition(instance, instanceCount);
                                if (position.HasValue)
                                {
                                    string mobyName = $"TieInst_{instanceCount:X4}";
                                    // Apply consistent scale factor
                                    var scaledPos = position.Value * POSITION_SCALE_FACTOR;
                                    positions[mobyName] = scaledPos;

                                    if (instanceCount < 10) // Log first 10 for debugging
                                    {
                                        Console.WriteLine($"    📍 {mobyName}: {position.Value} → {scaledPos} (scaled x{POSITION_SCALE_FACTOR})");
                                    }
                                }
                                instanceCount++;
                            }

                            if (instanceCount > 0)
                            {
                                Console.WriteLine($"  📦 Found {instanceCount} tie instances in zone {zonePair.Key}");
                            }
                        }
                    }

                    // Also check for direct moby instances or placement data
                    var mobyInstancesField = zoneType.GetField("mobyInstances") ??
                                           zoneType.GetField("instances") ??
                                           zoneType.GetField("objects");

                    if (mobyInstancesField != null)
                    {
                        var mobyInstances = mobyInstancesField.GetValue(zone);

                        if (mobyInstances is System.Collections.IEnumerable enumerable)
                        {
                            int instanceCount = 0;
                            foreach (var instance in enumerable)
                            {
                                var position = ExtractInstancePosition(instance, instanceCount);
                                if (position.HasValue)
                                {
                                    string mobyName = $"MobyInst_{instanceCount:X4}";
                                    // Apply consistent scale factor
                                    var scaledPos = position.Value * POSITION_SCALE_FACTOR;
                                    positions[mobyName] = scaledPos;

                                    if (instanceCount < 5)
                                    {
                                        Console.WriteLine($"    📍 {mobyName}: {position.Value} → {scaledPos} (scaled x{POSITION_SCALE_FACTOR})");
                                    }
                                }
                                instanceCount++;
                            }

                            if (instanceCount > 0)
                            {
                                Console.WriteLine($"  📦 Found {instanceCount} moby instances in zone {zonePair.Key}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ❌ Error processing zone {zonePair.Key}: {ex.Message}");
                }
            }

            Console.WriteLine($"  ✅ Extracted {positions.Count} world positions from zones");
            return positions;
        }

        /// <summary>
        /// Extracts world position from a zone instance object
        /// </summary>
        private static LibVector3? ExtractInstancePosition(object instance, int instanceIndex)
        {
            try
            {
                var instanceType = instance.GetType();

                // Log detailed info for first few instances
                bool shouldLog = instanceIndex < 3;

                if (shouldLog)
                {
                    Console.WriteLine($"    🔍 Instance {instanceIndex} type: {instanceType.Name}");
                }

                // Try common position field names for placed objects
                var positionFields = new[] { "position", "pos", "transform", "worldPosition", "placement" };

                foreach (var fieldName in positionFields)
                {
                    // Try field first
                    var field = instanceType.GetField(fieldName);
                    if (field != null)
                    {
                        var value = field.GetValue(instance);
                        if (value != null)
                        {
                            var converted = ConvertToLibVector3(value);
                            if (converted.HasValue && shouldLog)
                            {
                                Console.WriteLine($"    ✅ Found field '{fieldName}': {converted.Value}");
                            }
                            return converted;
                        }
                    }

                    // Try property second
                    var property = instanceType.GetProperty(fieldName);
                    if (property != null)
                    {
                        var value = property.GetValue(instance);
                        if (value != null)
                        {
                            var converted = ConvertToLibVector3(value);
                            if (converted.HasValue && shouldLog)
                            {
                                Console.WriteLine($"    ✅ Found property '{fieldName}': {converted.Value}");
                            }
                            return converted;
                        }
                    }
                }

                // Try to get matrix transform and extract translation
                var matrixField = instanceType.GetField("matrix");
                var matrixProperty = instanceType.GetProperty("matrix");

                object? matrix = null;
                if (matrixField != null)
                {
                    matrix = matrixField.GetValue(instance);
                }
                else if (matrixProperty != null)
                {
                    matrix = matrixProperty.GetValue(instance);
                }

                if (matrix != null)
                {
                    var position = ExtractPositionFromMatrix(matrix);
                    if (position.HasValue && shouldLog)
                    {
                        Console.WriteLine($"    ✅ Extracted from matrix: {position.Value}");
                    }
                    return position;
                }

                // Try X, Y, Z fields directly
                var xField = instanceType.GetField("x") ?? instanceType.GetField("X");
                var yField = instanceType.GetField("y") ?? instanceType.GetField("Y");
                var zField = instanceType.GetField("z") ?? instanceType.GetField("Z");

                if (xField != null && yField != null && zField != null)
                {
                    var x = Convert.ToSingle(xField.GetValue(instance));
                    var y = Convert.ToSingle(yField.GetValue(instance));
                    var z = Convert.ToSingle(zField.GetValue(instance));

                    var result = new LibVector3(x, y, z);
                    if (shouldLog)
                    {
                        Console.WriteLine($"    ✅ Found X,Y,Z fields: {result}");
                    }
                    return result;
                }

                // Try X, Y, Z properties as fallback
                var xProperty = instanceType.GetProperty("x") ?? instanceType.GetProperty("X");
                var yProperty = instanceType.GetProperty("y") ?? instanceType.GetProperty("Y");
                var zProperty = instanceType.GetProperty("z") ?? instanceType.GetProperty("Z");

                if (xProperty != null && yProperty != null && zProperty != null)
                {
                    var x = Convert.ToSingle(xProperty.GetValue(instance));
                    var y = Convert.ToSingle(yProperty.GetValue(instance));
                    var z = Convert.ToSingle(zProperty.GetValue(instance));

                    var result = new LibVector3(x, y, z);
                    if (shouldLog)
                    {
                        Console.WriteLine($"    ✅ Found X,Y,Z properties: {result}");
                    }
                    return result;
                }

                // Debug: list all available fields for first few instances
                if (shouldLog)
                {
                    var fields = instanceType.GetFields();
                    var properties = instanceType.GetProperties();
                    Console.WriteLine($"    🔍 Available fields: {string.Join(", ", fields.Take(10).Select(f => f.Name))}");
                    Console.WriteLine($"    🔍 Available properties: {string.Join(", ", properties.Take(10).Select(p => p.Name))}");
                }

                return null;
            }
            catch (Exception ex)
            {
                if (instanceIndex < 3)
                {
                    Console.WriteLine($"    ❌ Error extracting position from instance {instanceIndex}: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// Attempts to extract position from a transformation matrix
        /// </summary>
        private static LibVector3? ExtractPositionFromMatrix(object matrix)
        {
            if (matrix == null) return null;

            try
            {
                var matrixType = matrix.GetType();

                // Handle different matrix types
                if (matrixType.Name.Contains("Matrix4") || matrixType.Name.Contains("Matrix"))
                {
                    // Try to get translation component - typically M41, M42, M43 or similar
                    var m41Field = matrixType.GetField("M41");
                    var m42Field = matrixType.GetField("M42");
                    var m43Field = matrixType.GetField("M43");

                    var m41Property = matrixType.GetProperty("M41");
                    var m42Property = matrixType.GetProperty("M42");
                    var m43Property = matrixType.GetProperty("M43");

                    // Try fields first
                    if (m41Field != null && m42Field != null && m43Field != null)
                    {
                        var x = Convert.ToSingle(m41Field.GetValue(matrix));
                        var y = Convert.ToSingle(m42Field.GetValue(matrix));
                        var z = Convert.ToSingle(m43Field.GetValue(matrix));

                        return new LibVector3(x, y, z);
                    }

                    // Try properties second
                    if (m41Property != null && m42Property != null && m43Property != null)
                    {
                        var x = Convert.ToSingle(m41Property.GetValue(matrix));
                        var y = Convert.ToSingle(m42Property.GetValue(matrix));
                        var z = Convert.ToSingle(m43Property.GetValue(matrix));

                        return new LibVector3(x, y, z);
                    }

                    // Try translation field
                    var translationField = matrixType.GetField("translation");
                    if (translationField != null)
                    {
                        var translation = translationField.GetValue(matrix);
                        if (translation != null)
                        {
                            return ConvertToLibVector3(translation);
                        }
                    }

                    // Try translation property
                    var translationProperty = matrixType.GetProperty("Translation");
                    if (translationProperty != null)
                    {
                        var translation = translationProperty.GetValue(matrix);
                        if (translation != null)
                        {
                            return ConvertToLibVector3(translation);
                        }
                    }
                }
            }
            catch
            {
                // Swallow exceptions and fall through to return null
            }
            return null;
        }

        /// <summary>
        /// Generates a unique debug color for each UFrag chunk
        /// </summary>
        private static (byte R, byte G, byte B) GenerateDebugColor(int ufragId)
        {
            // Generate distinct colors for different chunks
            // Use HSV to RGB conversion for better color distribution
            float hue = (ufragId * 137.508f) % 360.0f; // Golden angle for good distribution
            float saturation = 0.7f; // Vibrant but not oversaturated
            float value = 0.9f; // Bright

            return HsvToRgb(hue, saturation, value);
        }

        /// <summary>
        /// Creates a terrain model from a specific triangle chunk
        /// </summary>
        private static TerrainModel? CreateModelFromTriangleChunk(FutureUFrag ufrag, HashSet<int> vertexSet, List<int> triangles, float scaleFactor)
        {
            try
            {
                var vertexList = vertexSet.OrderBy(v => v).ToList();
                var vertexMap = vertexList.Select((v, i) => new { OriginalIndex = v, NewIndex = i }).ToDictionary(x => x.OriginalIndex, x => x.NewIndex);

                // Ensure ufrag.IndexData is not null before dereferencing
                if (ufrag.IndexData == null)
                {
                    Console.WriteLine($"    ❌ UFrag {ufrag.ID}: IndexData is null in CreateModelFromTriangleChunk");
                    return null;
                }

                // Create remapped indices
                var newIndices = new List<int>();
                foreach (var triIndex in triangles)
                {
                    int baseIdx = triIndex * 3;
                    newIndices.Add(vertexMap[ufrag.IndexData[baseIdx]]);
                    newIndices.Add(vertexMap[ufrag.IndexData[baseIdx + 1]]);
                    newIndices.Add(vertexMap[ufrag.IndexData[baseIdx + 2]]);
                }

                return CreateSingleTerrainModelFixed(ufrag, 0, vertexList.Count, 0, newIndices.Count, scaleFactor, newIndices.ToArray(), vertexList.ToArray());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts HSV color to RGB
        /// </summary>
        private static (byte R, byte G, byte B) HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60.0f) % 2 - 1));
            float m = v - c;

            float r, g, b;

            if (h >= 0 && h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h >= 60 && h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h >= 120 && h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h >= 180 && h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h >= 240 && h < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            return (
                (byte) ((r + m) * 255),
                (byte) ((g + m) * 255),
                (byte) ((b + m) * 255)
            );
        }

        private static short _nextTerrainModelId = 1000; // Start at 1000 to avoid conflicts
        
        private static short GetNextTerrainModelId()
        {
            return _nextTerrainModelId++;
        }
        private static void ConvertMobysToUya(FutureLevel future, Level uya)
        {
            Console.WriteLine("🤖 Converting Future Mobys to UYA format...");

            if (future.Mobys == null || future.Mobys.Count == 0)
            {
                Console.WriteLine("  ⚠️ No Mobys found in Future level");
                return;
            }

            Console.WriteLine($"  📊 Found {future.Mobys.Count} Mobys - conversion not yet implemented");
            // TODO: Implement Moby conversion
            // This would involve converting Future Moby data to UYA MobyInstance format
        }

        private static void ConvertTiesToUya(FutureLevel future, Level uya)
        {
            Console.WriteLine("🏗️ Converting Future Ties to UYA format...");

            if (future.Ties == null || future.Ties.Count == 0)
            {
                Console.WriteLine("  ⚠️ No Ties found in Future level");
                return;
            }

            Console.WriteLine($"  📊 Found {future.Ties.Count} Ties - conversion not yet implemented");
            // TODO: Implement Tie conversion
            // This would involve converting Future Tie data to UYA Tie format
        }

        private static void ConvertTexturesToUya(FutureLevel future, Level uya)
        {
            Console.WriteLine("🎨 Converting Future textures to UYA format...");

            if (future.Textures == null || future.Textures.Count == 0)
            {
                Console.WriteLine("  ⚠️ No textures found in Future level");
                return;
            }

            Console.WriteLine($"  📊 Found {future.Textures.Count} textures - conversion not yet implemented");
            // TODO: Implement texture conversion
            // This would involve converting Future texture data to UYA LibReplanetizer.Texture format
        }

        private static void ConvertCollisionToUya(FutureLevel future, Level uya)
        {
            Console.WriteLine("🛡️ Converting Future collision to UYA format...");
            Console.WriteLine("  ⚠️ Collision conversion not yet implemented");
            // TODO: Implement collision conversion
            // This would involve extracting collision data from ReLunacy and converting to UYA format
        }

        /// <summary>
        /// Loads extracted model files and converts them to our Future data structures
        /// </summary>
        private static void LoadExtractedModels(string levelPath, FutureLevel futureLevel)
        {
            Console.WriteLine("🔍 Scanning for extracted model files...");

            // Look for common model file extensions that ReLunacy might export
            string[] modelExtensions = { "*.obj", "*.dae", "*.gltf", "*.glb" };

            foreach (string pattern in modelExtensions)
            {
                var files = Directory.GetFiles(levelPath, pattern, SearchOption.AllDirectories);
                Console.WriteLine($"  Found {files.Length} {pattern} files");

                foreach (string file in files)
                {
                    try
                    {
                        var model = LoadModelFile(file);
                        if (model != null)
                        {
                            // Categorize the model based on filename or other heuristics
                            CategorizeAndAddModel(model, file, futureLevel);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Failed to load model {file}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Loads extracted texture files into the Future level structure
        /// </summary>
        private static void LoadExtractedTextures(string levelPath, FutureLevel futureLevel)
        {
            Console.WriteLine("🔍 Scanning for extracted texture files...");

            // Look for common texture file extensions that ReLunacy might export
            string[] textureExtensions = { "*.png", "*.jpg", "*.jpeg", "*.tga", "*.bmp", "*.dds" };

            foreach (string pattern in textureExtensions)
            {
                var files = Directory.GetFiles(levelPath, pattern, SearchOption.AllDirectories);
                Console.WriteLine($"  Found {files.Length} {pattern} files");

                foreach (string file in files)
                {
                    try
                    {
                        var texture = LoadTextureFile(file, futureLevel.Textures.Count);
                        if (texture != null)
                        {
                            futureLevel.Textures.Add(texture);
                            Console.WriteLine($"  ✅ Added texture: {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Failed to load texture {file}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Loads a texture file into our Future texture format (placeholder implementation)
        /// </summary>
        private static FutureTexture? LoadTextureFile(string filePath, int textureId)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);

                // For now, create a placeholder texture
                // TODO: Implement proper texture format detection and conversion
                return new FutureTexture
                {
                    ID = textureId,
                    Width = 256, // Placeholder - would need to read from image
                    Height = 256, // Placeholder - would need to read from image
                    Data = fileData,
                    Format = Path.GetExtension(filePath).ToLower()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading texture file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// DEBUG: Export a specific UFrag to OBJ file for external verification
        /// </summary>
        private static void ExportUFragToOBJ(FutureUFrag ufrag, string outputPath)
        {
            if (ufrag.VertexData == null || ufrag.IndexData == null)
                return;

            try
            {
                var objPath = Path.Combine(outputPath, $"debug_ufrag_{ufrag.ID}.obj");
                var objDir = Path.GetDirectoryName(objPath);
                if (!string.IsNullOrEmpty(objDir))
                {
                    Directory.CreateDirectory(objDir);
                }

                using (var writer = new StreamWriter(objPath))
                {
                    writer.WriteLine($"# UFrag {ufrag.ID} exported for debugging");
                    writer.WriteLine($"# Vertices: {ufrag.VertexData.Length / 8}");
                    writer.WriteLine($"# Indices: {ufrag.IndexData.Length}");

                    // Write vertices
                    for (int i = 0; i < ufrag.VertexData.Length; i += 8)
                    {
                        float x = ufrag.VertexData[i + 0];
                        float y = ufrag.VertexData[i + 1];
                        float z = ufrag.VertexData[i + 2];
                        writer.WriteLine($"v {x:F6} {y:F6} {z:F6}");
                    }

                    // Write UV coordinates
                    for (int i = 0; i < ufrag.VertexData.Length; i += 8)
                    {
                        float u = ufrag.VertexData[i + 6];
                        float v = ufrag.VertexData[i + 7];
                        writer.WriteLine($"vt {u:F6} {v:F6}");
                    }

                    // Write faces (OBJ uses 1-based indexing)
                    for (int i = 0; i < ufrag.IndexData.Length; i += 3)
                    {
                        int v1 = ufrag.IndexData[i] + 1;
                        int v2 = ufrag.IndexData[i + 1] + 1;
                        int v3 = ufrag.IndexData[i + 2] + 1;
                        writer.WriteLine($"f {v1}/{v1} {v2}/{v2} {v3}/{v3}");
                    }
                }

                Console.WriteLine($"    📁 Exported UFrag {ufrag.ID} to {objPath} for verification");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ Failed to export UFrag {ufrag.ID}: {ex.Message}");
            }
        }

        /// <summary>
        /// Uses the existing Replanetizer export logic in reverse to load a model file
        /// </summary>
        private static FutureModelData? LoadModelFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".obj":
                    return LoadWavefrontModel(filePath);
                case ".dae":
                    return LoadColladaModel(filePath);
                case ".gltf":
                case ".glb":
                    return LoadGLTFModel(filePath);
                default:
                    Console.WriteLine($"⚠️ Unsupported model format: {extension}");
                    return null;
            }
        }

        /// <summary>
        /// Simplified Wavefront OBJ loader (reverse of WavefrontExporter)
        /// </summary>
        private static FutureModelData? LoadWavefrontModel(string filePath)
        {
            Console.WriteLine($"📦 Loading Wavefront model: {Path.GetFileName(filePath)}");

            var vertices = new List<LibVector3>();
            var normals = new List<LibVector3>();
            var uvs = new List<LibVector2>();
            var indices = new List<int>();
            var textureIds = new List<int>();

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                foreach (string line in lines)
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    switch (parts[0])
                    {
                        case "v": // Vertex
                            if (parts.Length >= 4)
                            {
                                vertices.Add(new LibVector3(
                                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                                    float.Parse(parts[3], CultureInfo.InvariantCulture)
                                ));
                            }
                            break;

                        case "vn": // Normal
                            if (parts.Length >= 4)
                            {
                                normals.Add(new LibVector3(
                                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                                    float.Parse(parts[3], CultureInfo.InvariantCulture)
                                ));
                            }
                            break;

                        case "vt": // UV
                            if (parts.Length >= 3)
                            {
                                uvs.Add(new LibVector2(
                                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                                    float.Parse(parts[2], CultureInfo.InvariantCulture)
                                ));
                            }
                            break;

                        case "f": // Face
                            if (parts.Length >= 4)
                            {
                                // Simple triangulation (assumes triangular faces or quads)
                                for (int i = 1; i < parts.Length; i++)
                                {
                                    string[] vertexData = parts[i].Split('/');
                                    if (vertexData.Length > 0 && int.TryParse(vertexData[0], out int vertexIndex))
                                    {
                                        indices.Add(vertexIndex - 1); // OBJ uses 1-based indexing
                                    }
                                }
                            }
                            break;
                    }
                }

                if (vertices.Count == 0)
                {
                    Console.WriteLine("⚠️ No vertices found in model");
                    return null;
                }

                return new FutureModelData
                {
                    Vertices = vertices.ToArray(),
                    Normals = normals.ToArray(),
                    UVs = uvs.ToArray(),
                    Indices = indices.ToArray(),
                    TextureIds = textureIds
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading Wavefront model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Placeholder for other model loaders
        /// </summary>
        private static FutureModelData? LoadColladaModel(string filePath)
        {
            Console.WriteLine($"⚠️ Collada loader not yet implemented: {Path.GetFileName(filePath)}");
            return null;
        }

        private static FutureModelData? LoadGLTFModel(string filePath)
        {
            Console.WriteLine($"⚠️ glTF loader not yet implemented: {Path.GetFileName(filePath)}");
            return null;
        }

        /// <summary>
        /// Data structure for loaded model data before categorization
        /// </summary>
        private class FutureModelData
        {
            public LibVector3[] Vertices { get; set; } = Array.Empty<LibVector3>();
            public LibVector3[] Normals { get; set; } = Array.Empty<LibVector3>();
            public LibVector2[] UVs { get; set; } = Array.Empty<LibVector2>();
            public int[] Indices { get; set; } = Array.Empty<int>();
            public List<int> TextureIds { get; set; } = new();
        }

        /// <summary>
        /// Categorizes loaded models into UFrags, Mobys, or Ties based on heuristics
        /// </summary>
        private static void CategorizeAndAddModel(FutureModelData modelData, string filePath, FutureLevel futureLevel)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();

            // Simple heuristics for categorization
            if (fileName.Contains("terrain") || fileName.Contains("ufrag"))
            {
                // Convert to UFrag
                var ufrag = new FutureUFrag
                {
                    ID = futureLevel.UFrags.Count,
                    VertexData = ConvertVertexArrayToFloatArray(modelData.Vertices),
                    IndexData = modelData.Indices,
                    TextureIds = modelData.TextureIds,
                    BoundingBox = CalculateBoundingBoxFromPositions(ConvertVertexArrayToFloatArray(modelData.Vertices))
                };
                futureLevel.UFrags.Add(ufrag);
                Console.WriteLine($"  ✅ Added UFrag: {fileName}");
            }
            else if (fileName.Contains("moby") || fileName.Contains("object"))
            {
                // Convert to Moby
                var moby = new FutureMoby
                {
                    MobyID = futureLevel.Mobys.Count,
                    ModelID = futureLevel.Mobys.Count,
                    Position = LibVector3.Zero,
                    Rotation = LibQuaternion.Identity,
                    Scale = LibVector3.One,
                    VertexData = ConvertVertexArrayToFloatArray(modelData.Vertices),
                    IndexData = modelData.Indices,
                    TextureIds = modelData.TextureIds
                };
                futureLevel.Mobys.Add(moby);
                Console.WriteLine($"  ✅ Added Moby: {fileName}");
            }
            else
            {
                // Default to Tie (static geometry)
                var tie = new FutureTie
                {
                    ModelID = futureLevel.Ties.Count,
                    Position = LibVector3.Zero,
                    Rotation = LibQuaternion.Identity,
                    Scale = LibVector3.One,
                    VertexData = ConvertVertexArrayToFloatArray(modelData.Vertices),
                    IndexData = modelData.Indices,
                    TextureIds = modelData.TextureIds
                };
                futureLevel.Ties.Add(tie);
                Console.WriteLine($"  ✅ Added Tie: {fileName}");
            }
        }
        private static float[] ConvertVertexArrayToFloatArray(LibVector3[] vertices)
        {
            var result = new float[vertices.Length * 3];
            for (int i = 0; i < vertices.Length; i++)
            {
                result[i * 3 + 0] = vertices[i].X;
                result[i * 3 + 1] = vertices[i].Y;
                result[i * 3 + 2] = vertices[i].Z;
            }
            return result;
        }

        private static int[]? ConvertUIntArrayToIntArray(uint[]? uintArray)
        {
            if (uintArray == null) return null;

            var result = new int[uintArray.Length];
            for (int i = 0; i < uintArray.Length; i++)
            {
                result[i] = (int) uintArray[i];
            }
            return result;
        }

        private static (LibVector3 center, LibVector3 size)? CalculateBoundingBoxFromPositions(float[]? positions)
        {
            if (positions == null || positions.Length < 3) return null;

            int vertexCount = positions.Length / 3;
            if (vertexCount == 0) return null;

            var min = new LibVector3(positions[0], positions[1], positions[2]);
            var max = new LibVector3(positions[0], positions[1], positions[2]);

            for (int i = 1; i < vertexCount; i++)
            {
                int index = i * 3;
                var vertex = new LibVector3(positions[index], positions[index + 1], positions[index + 2]);

                min = LibVector3.ComponentMin(min, vertex);
                max = LibVector3.ComponentMax(max, vertex);
            }

            var center = (min + max) * 0.5f;
            var size = max - min;

            // CRITICAL FIX: Ensure bounding box has minimum size to avoid zero volume warnings
            const float MIN_SIZE = 0.001f;
            if (size.X < MIN_SIZE) size = new LibVector3(MIN_SIZE, size.Y, size.Z);
            if (size.Y < MIN_SIZE) size = new LibVector3(size.X, MIN_SIZE, size.Z);
            if (size.Z < MIN_SIZE) size = new LibVector3(size.X, size.Y, MIN_SIZE);

            return (center, size);
        }

        // Data structures for Future level parsing
        public class FutureLevel
        {
            public string LevelPath { get; set; } = "";
            public bool HasMainDat { get; set; }
            public bool HasAssetLookup { get; set; }
            public bool HasHighMips { get; set; }
            public bool HasTexStream { get; set; }
            public bool HasDebugData { get; set; }

            public List<FutureUFrag> UFrags { get; set; } = new();
            public List<FutureMoby> Mobys { get; set; } = new();
            public List<FutureTie> Ties { get; set; } = new();
            public List<FutureTexture> Textures { get; set; } = new();
        }

        public class FutureUFrag
        {
            public int ID { get; set; }
            public float[]? VertexData { get; set; }
            public int[]? IndexData { get; set; }
            public List<int> TextureIds { get; set; } = new();
            public (LibVector3 center, LibVector3 size)? BoundingBox { get; set; }
        }

        public class FutureMoby
        {
            public int MobyID { get; set; }
            public int ModelID { get; set; }
            public LibVector3 Position { get; set; }
            public LibQuaternion Rotation { get; set; }
            public LibVector3 Scale { get; set; } = LibVector3.One;
            public float[]? VertexData { get; set; }
            public int[]? IndexData { get; set; }
            public List<int> TextureIds { get; set; } = new();
        }

        public class FutureTie
        {
            public int ModelID { get; set; }
            public LibVector3 Position { get; set; }
            public LibQuaternion Rotation { get; set; }
            public LibVector3 Scale { get; set; } = LibVector3.One;
            public float[]? VertexData { get; set; }
            public int[]? IndexData { get; set; }
            public List<int> TextureIds { get; set; } = new();
        }

        public class FutureTexture
        {
            public int ID { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[]? Data { get; set; }
            public string Format { get; set; } = "";
        }

        [Flags]
        public enum FutureSwapOptions
        {
            None = 0,
            UFrags = 1 << 0,
            Mobys = 1 << 1,
            Ties = 1 << 2,
            Textures = 1 << 3,
            Collision = 1 << 4,
            PositionMappings = 1 << 5,
            All = UFrags | Mobys | Ties | Textures | Collision | PositionMappings
        }

        /// <summary>
        /// Creates multiple TerrainModels with PROPER splitting (no truncation)
        /// </summary>
        private static List<TerrainModel> CreateSplitTerrainModelsFromUFrag(FutureUFrag ufrag)
        {
            var terrainModels = new List<TerrainModel>();

            try
            {
                if (ufrag.VertexData == null || ufrag.IndexData == null)
                    return terrainModels;

                const float SCALE_FACTOR = 0.1f;

                // UYA LIMITS: Conservative for 16-bit indices
                const int MAX_VERTICES_PER_CHUNK = 32000;  // Leave headroom
                const int MAX_INDICES_PER_CHUNK = 45000;   // Leave headroom
                //const ushort MAX_INDEX_VALUE = 65535;      // 16-bit limit

                int originalVertexCount = ufrag.VertexData.Length / 8;
                int originalIndexCount = ufrag.IndexData.Length;

                // Validate mesh integrity first
                if (!ValidateMeshIntegrity(ufrag, originalVertexCount, originalIndexCount))
                {
                    Console.WriteLine($"    ❌ UFrag {ufrag.ID} failed integrity check - skipping");
                    return terrainModels;
                }

                // If mesh is small enough, create single model
                if (originalVertexCount <= MAX_VERTICES_PER_CHUNK &&
                    originalIndexCount <= MAX_INDICES_PER_CHUNK)
                {
                    // Use the original (first) CreateSingleTerrainModel method
                    var singleModel = CreateSingleTerrainModelFixed(ufrag, 0, originalVertexCount, 0, originalIndexCount, SCALE_FACTOR);
                    if (singleModel != null)
                        terrainModels.Add(singleModel);
                    return terrainModels;
                }

                Console.WriteLine($"    🔧 UFrag {ufrag.ID} needs splitting: {originalVertexCount} verts, {originalIndexCount} indices");

                // STRATEGY 1: Split by material first (group triangles by texture)
                var materialSplits = SplitMeshByMaterial(ufrag, MAX_VERTICES_PER_CHUNK, MAX_INDICES_PER_CHUNK, SCALE_FACTOR);
                if (materialSplits.Count > 0)
                {
                    terrainModels.AddRange(materialSplits);
                    Console.WriteLine($"    ✅ Split into {materialSplits.Count} material-based chunks");
                    return terrainModels;
                }

                // STRATEGY 2: Split by triangles (preserve complete triangles)
                var triangleSplits = SplitMeshByTriangles(ufrag, MAX_VERTICES_PER_CHUNK, MAX_INDICES_PER_CHUNK, SCALE_FACTOR);
                if (triangleSplits.Count > 0)
                {
                    terrainModels.AddRange(triangleSplits);
                    Console.WriteLine($"    ✅ Split into {triangleSplits.Count} triangle-based chunks");
                    return terrainModels;
                }

                Console.WriteLine($"    ❌ Failed to split UFrag {ufrag.ID} properly - may be degenerate");
                return terrainModels;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ❌ Error splitting UFrag {ufrag.ID}: {ex.Message}");
                return terrainModels;
            }
        }

        /// <summary>
        /// Remove degenerate triangles (zero area, needle-like shapes)
        /// </summary>
        private static int[] RemoveDegenerateTriangles(FutureUFrag ufrag, int[] indices)
        {
            const float MIN_TRIANGLE_AREA = 0.001f; // Minimum triangle area
            var validIndices = new List<int>();

            // Add null check for VertexData
            if (ufrag.VertexData == null)
            {
                return validIndices.ToArray();
            }

            for (int i = 0; i < indices.Length; i += 3)
            {
                if (i + 2 >= indices.Length) break;

                int i1 = indices[i];
                int i2 = indices[i + 1];
                int i3 = indices[i + 2];

                // Skip if any index is invalid
                if (i1 < 0 || i2 < 0 || i3 < 0 ||
                    i1 * 8 + 2 >= ufrag.VertexData.Length ||
                    i2 * 8 + 2 >= ufrag.VertexData.Length ||
                    i3 * 8 + 2 >= ufrag.VertexData.Length)
                {
                    continue;
                }

                // Get triangle vertices
                var v1 = new LibVector3(ufrag.VertexData[i1 * 8], ufrag.VertexData[i1 * 8 + 1], ufrag.VertexData[i1 * 8 + 2]);
                var v2 = new LibVector3(ufrag.VertexData[i2 * 8], ufrag.VertexData[i2 * 8 + 1], ufrag.VertexData[i2 * 8 + 2]);
                var v3 = new LibVector3(ufrag.VertexData[i3 * 8], ufrag.VertexData[i3 * 8 + 1], ufrag.VertexData[i3 * 8 + 2]);

                // Calculate triangle area using cross product
                var edge1 = v2 - v1;
                var edge2 = v3 - v1;
                var cross = LibVector3.Cross(edge1, edge2);
                float area = cross.Length * 0.5f;

                // Skip degenerate triangles
                if (area < MIN_TRIANGLE_AREA)
                {
                    continue;
                }

                // Triangle is valid
                validIndices.Add(i1);
                validIndices.Add(i2);
                validIndices.Add(i3);
            }

            return validIndices.ToArray();
        }
    }
}
