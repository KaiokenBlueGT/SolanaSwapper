using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using OpenTK.Mathematics;
using static LibReplanetizer.DataFunctions;

namespace GeometrySwapper
{
    /// <summary>
    /// Handles the transfer of Shrub models and instances from RC1 levels to RC2 levels
    /// </summary>
    public class ShrubSwapper
    {
        /// <summary>
        /// Options for controlling Shrub swapping behavior
        /// </summary>
        [Flags]
        public enum ShrubSwapOptions
        {
            None = 0,
            UseRC1Placements = 1,
            UseRC1Models = 2,
            MapTextures = 4,

            PlacementsOnly = UseRC1Placements,
            PlacementsAndModels = UseRC1Placements | UseRC1Models,
            FullSwap = UseRC1Placements | UseRC1Models | MapTextures,
            Default = PlacementsAndModels
        }

        /// <summary>
        /// Swaps Shrub objects from an RC1 level to an RC2 level
        /// </summary>
        /// <param name="targetLevel">The RC2 level where Shrubs will be replaced</param>
        /// <param name="rc1SourceLevel">The RC1 level containing the source Shrubs</param>
        /// <param name="options">Options to control the swap behavior</param>
        /// <returns>True if the operation was successful</returns>
        public static bool SwapShrubsWithRC1Oltanis(Level targetLevel, Level rc1SourceLevel, ShrubSwapOptions options = ShrubSwapOptions.Default)
        {
            if (targetLevel == null || rc1SourceLevel == null)
            {
                Console.WriteLine("  ❌ Error: One of the levels is null");
                return false;
            }

            try
            {
                Console.WriteLine("\n==== Swapping Shrubs to match RC1 Oltanis ====");
                Console.WriteLine($"Options: Placements={options.HasFlag(ShrubSwapOptions.UseRC1Placements)}, " +
                                 $"Models={options.HasFlag(ShrubSwapOptions.UseRC1Models)}, " +
                                 $"MapTextures={options.HasFlag(ShrubSwapOptions.MapTextures)}");

                // Step 1: Remove all existing Shrubs from the target level
                RemoveAllShrubs(targetLevel);

                // Step 2: Import Shrub models from RC1 if needed
                Dictionary<int, int> shrubModelIdMapping = new Dictionary<int, int>();
                if (options.HasFlag(ShrubSwapOptions.UseRC1Models))
                {
                    shrubModelIdMapping = ImportRC1ShrubModelsToRC2Level(targetLevel, rc1SourceLevel);
                }

                // Step 3: Import Shrubs from RC1 to RC2
                TransferShrubInstances(targetLevel, rc1SourceLevel, shrubModelIdMapping);

                // Step 4: Map textures if required
                if (options.HasFlag(ShrubSwapOptions.MapTextures))
                {
                    Console.WriteLine("\n--- Texture Mapping Details ---");
                    Console.WriteLine($"RC1 Level has {rc1SourceLevel.textures?.Count ?? 0} textures");
                    Console.WriteLine($"Target Level has {targetLevel.textures?.Count ?? 0} textures");
                    Console.WriteLine($"Target Level has {targetLevel.shrubModels?.Count ?? 0} shrub models");

                    if (targetLevel.textures != null && targetLevel.shrubModels != null && targetLevel.shrubModels.Count > 0)
                    {
                        MapShrubTextures(targetLevel, rc1SourceLevel);

                        // Validate texture references to ensure they're in range
                        ValidateShrubTextureReferences(targetLevel);
                    }
                    else
                    {
                        Console.WriteLine("  ❌ Cannot map textures: Missing required data in target level");
                    }
                }

                // Step 5: Resolve any ID conflicts
                ResolveMobyShrubIdConflicts(targetLevel);

                // Step 6: Update light references to use Light 0
                UpdateShrubLightReferences(targetLevel);

                // Step 7: Update the shrubIds list to match the models
                UpdateShrubIds(targetLevel);

                // Validate the final state
                Console.WriteLine("\n--- POST-SWAP VALIDATION ---");
                Console.WriteLine($"Target level now has {targetLevel.shrubs.Count} shrubs");
                Console.WriteLine($"Target level now has {targetLevel.shrubModels.Count} shrub models");

                // Make sure shrubIds has entries for each shrub instance
                if (targetLevel.shrubIds.Count != targetLevel.shrubs.Count)
                {
                    Console.WriteLine($"⚠️ Warning: shrubIds count ({targetLevel.shrubIds.Count}) doesn't match shrub count ({targetLevel.shrubs.Count})");
                    targetLevel.shrubIds.Clear();
                    foreach (var shrub in targetLevel.shrubs)
                    {
                        targetLevel.shrubIds.Add(shrub.modelID);
                    }
                    Console.WriteLine($"✅ Updated shrubIds list with {targetLevel.shrubIds.Count} entries");
                }
                else
                {
                    Console.WriteLine($"✅ shrubIds list correctly contains {targetLevel.shrubIds.Count} entries");
                }

                // Validate that all shrubs reference valid models
                bool allValid = true;
                foreach (var shrub in targetLevel.shrubs)
                {
                    if (shrub.model == null || targetLevel.shrubModels.All(m => m.id != shrub.modelID))
                    {
                        Console.WriteLine($"⚠️ Shrub references invalid model ID: {shrub.modelID}");
                        allValid = false;
                    }
                }

                if (allValid)
                {
                    Console.WriteLine("✅ All shrubs reference valid models");
                }

                Console.WriteLine("✅ Successfully transferred shrubs from RC1 to RC2 level");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during shrub transfer: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Removes all shrubs from the target level
        /// </summary>
        private static void RemoveAllShrubs(Level targetLevel)
        {
            int count = targetLevel.shrubs.Count;
            targetLevel.shrubs.Clear();
            Console.WriteLine($"  ✅ Removed {count} existing shrubs from target level");
        }

        /// <summary>
        /// Maps shrub textures from RC1 to RC2 level
        /// </summary>
        private static void MapShrubTextures(Level targetLevel, Level rc1SourceLevel)
        {
            Console.WriteLine("  Mapping shrub textures from RC1 to RC2 level...");
            
            int mappedCount = 0;
            int addedCount = 0;

            // Dictionary to map source texture indices to target texture indices
            Dictionary<int, int> textureIdMapping = new Dictionary<int, int>();
            
            // Debugging: Get info about texture 864 in the target level
            if (864 < targetLevel.textures.Count)
            {
                var tex864 = targetLevel.textures[864];
                Console.WriteLine($"\n  DEBUG: Texture 864 properties: {tex864.width}x{tex864.height}, MipMapCount: {tex864.mipMapCount}");
                Console.WriteLine($"         Data length: {(tex864.data != null ? tex864.data.Length.ToString() : "null")}");
            }

            // First pass: Find and collect all texture IDs used by RC1 shrub models
            HashSet<int> usedTextureIdsInRC1 = new HashSet<int>();
            foreach (var model in targetLevel.shrubModels?.OfType<ShrubModel>() ?? Enumerable.Empty<ShrubModel>())
            {
                if (model.textureConfig != null)
                {
                    foreach (var texConfig in model.textureConfig)
                    {
                        usedTextureIdsInRC1.Add(texConfig.id);
                    }
                }
            }

            Console.WriteLine($"  Found {usedTextureIdsInRC1.Count} unique texture IDs used by shrub models");

            // Create signatures for all target level textures for faster matching
            var targetTextureLookup = new Dictionary<string, int>();
            for (int i = 0; i < targetLevel.textures.Count; i++)
            {
                var tex = targetLevel.textures[i];
                string signature = GetTextureSignature(tex);
                if (!string.IsNullOrEmpty(signature))
                {
                    targetTextureLookup[signature] = i;
                }
            }
            
            Console.WriteLine($"  Indexed {targetTextureLookup.Count} target textures for matching");

            // Second pass: Process each unique texture ID from RC1
            foreach (int rc1TexId in usedTextureIdsInRC1)
            {
                // Skip invalid texture IDs
                if (rc1TexId < 0 || rc1TexId >= rc1SourceLevel.textures.Count)
                {
                    Console.WriteLine($"  ⚠️ Texture ID {rc1TexId} is out of range for RC1 source level");
                    continue;
                }

                // Skip if we've already processed this texture ID
                if (textureIdMapping.ContainsKey(rc1TexId))
                {
                    continue;
                }

                Texture rc1Tex = rc1SourceLevel.textures[rc1TexId];
                
                // Check if this texture is valid
                if (rc1Tex.data == null || rc1Tex.data.Length == 0)
                {
                    Console.WriteLine($"  ⚠️ RC1 texture {rc1TexId} has no texture data");
                    continue;
                }
                
                // Get signature for this RC1 texture
                string rc1TexSignature = GetTextureSignature(rc1Tex);
                
                // Try to find a matching texture in the target level
                int matchingTexId = -1;
                
                // First try signature-based matching
                if (!string.IsNullOrEmpty(rc1TexSignature) && targetTextureLookup.TryGetValue(rc1TexSignature, out int texId))
                {
                    matchingTexId = texId;
                    Console.WriteLine($"  Signature match found for texture {rc1TexId} -> {matchingTexId}");
                }
                else
                {
                    // Fall back to property-based matching
                    matchingTexId = FindMatchingTexture(targetLevel, rc1Tex);
                    if (matchingTexId >= 0)
                    {
                        Console.WriteLine($"  Property match found for texture {rc1TexId} -> {matchingTexId}");
                    }
                }

                if (matchingTexId >= 0)
                {
                    textureIdMapping[rc1TexId] = matchingTexId;
                    mappedCount++;
                    Console.WriteLine($"  ✓ Mapped texture {rc1TexId} -> {matchingTexId}");
                }
                else
                {
                    // No matching texture found, add the RC1 texture to the target level
                    Texture newTexture = DeepCloneTexture(rc1Tex);
                    targetLevel.textures.Add(newTexture);
                    int newTexId = targetLevel.textures.Count - 1;
                    textureIdMapping[rc1TexId] = newTexId;
                    addedCount++;
                    Console.WriteLine($"  + Added new texture at index {newTexId} from RC1 texture {rc1TexId}");
                }
            }

            // Third pass: Update all shrub models with the new texture mappings
            int modelsUpdated = 0;
            foreach (var model in targetLevel.shrubModels?.OfType<ShrubModel>() ?? Enumerable.Empty<ShrubModel>())
            {
                if (model.textureConfig == null || model.textureConfig.Count == 0)
                    continue;

                bool modelUpdated = false;
                foreach (var texConfig in model.textureConfig)
                {
                    // Only update if we have a mapping for this texture ID
                    if (textureIdMapping.TryGetValue(texConfig.id, out int newId))
                    {
                        int oldId = texConfig.id;
                        texConfig.id = newId;
                        modelUpdated = true;
                        
                        // Add special tracking for texture 864
                        if (newId == 864)
                        {
                            Console.WriteLine($"    ⚠️ WARNING: Mapped RC1 texture {oldId} to problematic texture 864");
                        }
                        else
                        {
                            Console.WriteLine($"    Updated texture ref in model {model.id}: {oldId} → {newId}");
                        }
                    }
                }

                if (modelUpdated)
                    modelsUpdated++;
            }

            Console.WriteLine($"  ✅ Updated texture references in {modelsUpdated} shrub models");
            Console.WriteLine($"  ✅ Mapped {mappedCount} textures and added {addedCount} new textures");
        }

        /// <summary>
        /// Validates texture references in all Shrub models to ensure they're within valid range
        /// </summary>
        /// <param name="level">The level to validate</param>
        private static void ValidateShrubTextureReferences(Level level)
        {
            if (level.shrubModels == null || level.shrubModels.Count == 0 || level.textures == null)
                return;

            Console.WriteLine("\n🔍 Validating texture references in Shrub models...");

            int modelsFixed = 0;
            int texturesFixed = 0;

            // Maximum valid texture index
            int maxValidTextureId = level.textures.Count - 1;

            foreach (var model in level.shrubModels.OfType<ShrubModel>())
            {
                if (model.textureConfig == null || model.textureConfig.Count == 0)
                    continue;

                bool modelFixed = false;

                for (int i = 0; i < model.textureConfig.Count; i++)
                {
                    var texConfig = model.textureConfig[i];

                    // Check if texture ID is out of range
                    if (texConfig.id < 0 || texConfig.id > maxValidTextureId)
                    {
                        // Clamp the texture ID to a valid value
                        int oldId = texConfig.id;
                        texConfig.id = Math.Clamp(texConfig.id, 0, maxValidTextureId);

                        Console.WriteLine($"  ⚠️ Fixed invalid texture ID in model {model.id}: {oldId} → {texConfig.id}");
                        texturesFixed++;
                        modelFixed = true;
                    }
                }

                if (modelFixed)
                    modelsFixed++;
            }

            if (modelsFixed > 0)
                Console.WriteLine($"  ✅ Fixed texture references in {modelsFixed} models (corrected {texturesFixed} texture IDs)");
            else
                Console.WriteLine("  ✅ All shrub texture references are valid");
        }

        /// <summary>
        /// Updates the level's shrubIds list to include both model IDs and instance data needed for serialization
        /// </summary>
        /// <param name="level">The level to update</param>
        private static void UpdateShrubIds(Level level)
        {
            if (level.shrubModels == null)
                return;
            
            // First, ensure we have a shrubIds list
            if (level.shrubIds == null)
            {
                level.shrubIds = new List<int>();
            }
            else
            {
                level.shrubIds.Clear();
            }
            
            // Add the actual shrub instance IDs
            // This is crucial - the serializer uses this list during save
            if (level.shrubs != null)
            {
                foreach (var shrub in level.shrubs)
                {
                    level.shrubIds.Add(shrub.modelID);
                }
            }
            
            Console.WriteLine($"  ✅ Updated shrubIds list with {level.shrubIds.Count} entries");
            
            // Ensure the shrub data byte array is recreated to match the correct shrub count
            if (level.shrubs != null)
            {
                // Create or update the shrub data byte array
                level.shrubData = new byte[level.shrubs.Count * Shrub.ELEMENTSIZE];
                int offset = 0;
                
                // Write each shrub's data into the byte array
                foreach (var shrub in level.shrubs)
                {
                    byte[] shrubBytes = shrub.ToByteArray();
                    Array.Copy(shrubBytes, 0, level.shrubData, offset, shrubBytes.Length);
                    offset += shrubBytes.Length;
                }
                
                Console.WriteLine($"  ✅ Created shrub data array with {level.shrubs.Count} entries");
            }
            
            // Also initialize shrubGroupData if it doesn't exist
            if (level.shrubGroupData == null || level.shrubGroupData.Length == 0)
            {
                // Create minimal valid shrub group data
                level.shrubGroupData = new byte[8];
                Console.WriteLine("  ✅ Created minimal shrub group data");
            }
        }

        /// <summary>
        /// Generates a unique signature for a texture to help with matching
        /// </summary>
        private static string GetTextureSignature(Texture tex)
        {
            if (tex == null || tex.data == null || tex.data.Length == 0)
                return string.Empty;
                
            // Use dimensions, mipmap count, and a better sampling of the data
            StringBuilder signature = new StringBuilder();
            signature.Append($"{tex.width}x{tex.height}_{tex.mipMapCount}_");
            
            // Calculate a more robust hash by sampling from different parts of the texture
            int dataLength = tex.data.Length;
            const int MAX_SAMPLES = 5;
            int[] samplePoints = new int[MAX_SAMPLES];
            
            // Sample from beginning, 25%, 50%, 75%, and near end
            for (int i = 0; i < MAX_SAMPLES; i++)
            {
                samplePoints[i] = (int)((float)i / (MAX_SAMPLES - 1) * dataLength * 0.95f);
                if (samplePoints[i] < dataLength)
                {
                    signature.Append(tex.data[samplePoints[i]].ToString("X2"));
                }
            }
            
            // Add additional sampling if data is large enough
            if (dataLength > 1000)
            {
                // Add 16-byte samples from different positions
                for (int i = 0; i < 3; i++)
                {
                    int pos = (dataLength / 4) * (i + 1);
                    if (pos + 16 < dataLength)
                    {
                        for (int j = 0; j < 16; j += 4)
                        {
                            signature.Append(tex.data[pos + j].ToString("X2"));
                        }
                    }
                }
            }
            
            return signature.ToString();
        }

        /// <summary>
        /// Finds a matching texture in the target level
        /// </summary>
        private static int FindMatchingTexture(Level targetLevel, Texture sourceTexture)
        {
            // First try a more strict comparison to avoid incorrect matches
            for (int i = 0; i < targetLevel.textures.Count; i++)
            {
                var targetTexture = targetLevel.textures[i];
                
                // Skip invalid textures
                if (targetTexture == null || targetTexture.data == null || 
                    sourceTexture == null || sourceTexture.data == null)
                    continue;
                    
                // Do a very strict comparison of basic properties
                if (targetTexture.width == sourceTexture.width && 
                    targetTexture.height == sourceTexture.height &&
                    targetTexture.mipMapCount == sourceTexture.mipMapCount)
                {
                    // Do a deeper data comparison using more sample points
                    int sampleSize = Math.Min(2000, Math.Min(targetTexture.data.Length, sourceTexture.data.Length));
                    bool matches = true;
                    
                    // Sample from beginning, middle and end of the texture data
                    for (int offset = 0; offset < sampleSize && matches; offset += 500)
                    {
                        if (offset < targetTexture.data.Length && offset < sourceTexture.data.Length)
                        {
                            // Check sequences of bytes for more accuracy
                            for (int j = 0; j < 10 && offset + j < sampleSize; j++)
                            {
                                if (targetTexture.data[offset + j] != sourceTexture.data[offset + j])
                                {
                                    matches = false;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (matches)
                        return i;
                }
            }
            
            // No strong match found
            return -1;
        }

        /// <summary>
        /// Compares two textures for equality
        /// </summary>
        private static bool TextureEquals(Texture tex1, Texture tex2)
        {
            if (tex1 == null || tex2 == null)
                return false;

            // Check basic properties first
            if (tex1.width != tex2.width ||
                tex1.height != tex2.height ||
                tex1.mipMapCount != tex2.mipMapCount)
                return false;

            // If data is available, do a deeper comparison
            if (tex1.data != null && tex2.data != null && tex1.data.Length == tex2.data.Length)
            {
                // Check a sample of bytes for efficiency
                int sampleSize = Math.Min(1000, tex1.data.Length);
                for (int i = 0; i < sampleSize; i++)
                {
                    if (tex1.data[i] != tex2.data[i])
                        return false;
                }
                return true;
            }

            // If no data to compare, assume equal if dimensions match
            return true;
        }

        /// <summary>
        /// Creates a deep clone of a texture
        /// </summary>
        private static Texture DeepCloneTexture(Texture sourceTexture)
        {
            byte[] newData = new byte[0];
            if (sourceTexture.data != null)
            {
                newData = new byte[sourceTexture.data.Length];
                Array.Copy(sourceTexture.data, newData, sourceTexture.data.Length);
            }

            Texture newTexture = new Texture(
                sourceTexture.id,
                sourceTexture.width,
                sourceTexture.height,
                newData
            );

            newTexture.mipMapCount = sourceTexture.mipMapCount;
            newTexture.off06 = sourceTexture.off06;
            newTexture.off08 = sourceTexture.off08;
            newTexture.off0C = sourceTexture.off0C;
            newTexture.off10 = sourceTexture.off10;
            newTexture.off14 = sourceTexture.off14;
            newTexture.off1C = sourceTexture.off1C;
            newTexture.off20 = sourceTexture.off20;
            newTexture.vramPointer = sourceTexture.vramPointer;

            return newTexture;
        }

        /// <summary>
        /// Imports RC1 shrub models into the RC2 level
        /// </summary>
        public static Dictionary<int, int> ImportRC1ShrubModelsToRC2Level(Level targetLevel, Level rc1SourceLevel)
        {
            var modelIdMapping = new Dictionary<int, int>();
            int initialModelCount = targetLevel.shrubModels.Count;

            // Build a list of existing IDs
            HashSet<short> existingIds = new HashSet<short>();
            foreach (var model in targetLevel.shrubModels)
            {
                existingIds.Add(model.id);
            }

            // Track the next available ID to use
            short nextId = 0;
            while (existingIds.Contains(nextId)) nextId++;

            // Process each RC1 shrub model
            foreach (var sourceModel in rc1SourceLevel.shrubModels)
            {
                // Clone the RC1 model
                ShrubModel newModel = CloneShrubModel((ShrubModel) sourceModel);

                // Assign a new ID if there's a conflict
                short originalId = newModel.id;
                if (existingIds.Contains(newModel.id))
                {
                    newModel.id = nextId++;
                    while (existingIds.Contains(nextId)) nextId++;
                }

                existingIds.Add(newModel.id);
                targetLevel.shrubModels.Add(newModel);
                modelIdMapping[originalId] = newModel.id;
            }

            int addedCount = targetLevel.shrubModels.Count - initialModelCount;
            Console.WriteLine($"  ✅ Added {addedCount} shrub models");
            return modelIdMapping;
        }

        /// <summary>
        /// Creates a complete clone of a ShrubModel
        /// </summary>
        public static ShrubModel CloneShrubModel(ShrubModel sourceModel)
        {
            if (sourceModel == null)
                throw new ArgumentNullException(nameof(sourceModel), "Source model cannot be null");

            Console.WriteLine($"  Cloning shrub model {sourceModel.id}");
            
            // Create a temporary byte array with the minimum required data
            byte[] shrubBlock = new byte[0x40]; // Standard size for shrub model header
            
            // Write key values into the shrub block
            WriteShort(shrubBlock, 0x30, sourceModel.id);
            WriteFloat(shrubBlock, 0x00, sourceModel.cullingX);
            WriteFloat(shrubBlock, 0x04, sourceModel.cullingY);
            WriteFloat(shrubBlock, 0x08, sourceModel.cullingZ);
            WriteFloat(shrubBlock, 0x0C, sourceModel.cullingRadius);
            WriteUint(shrubBlock, 0x20, sourceModel.off20);
            WriteShort(shrubBlock, 0x2A, sourceModel.off2A);
            WriteUint(shrubBlock, 0x2C, sourceModel.off2C);
            WriteUint(shrubBlock, 0x34, sourceModel.off34);
            WriteUint(shrubBlock, 0x38, sourceModel.off38);
            WriteUint(shrubBlock, 0x3C, sourceModel.off3C);
            
            // Create a new instance using the minimal data
            // Note: This approach means the constructor will initialize with empty
            // vertex/index buffers and texture configs, which we'll overwrite next
            ShrubModel newModel = new ShrubModel(null, shrubBlock, 0);
            
            // Clone vertex buffer
            if (sourceModel.vertexBuffer != null && sourceModel.vertexBuffer.Length > 0)
            {
                newModel.vertexBuffer = new float[sourceModel.vertexBuffer.Length];
                Array.Copy(sourceModel.vertexBuffer, newModel.vertexBuffer, sourceModel.vertexBuffer.Length);
                Console.WriteLine($"    Copied {sourceModel.vertexBuffer.Length / 8} vertices");
            }
            else
            {
                Console.WriteLine("    Warning: Source model has no vertex data");
                newModel.vertexBuffer = new float[0];
            }

            // Clone index buffer
            if (sourceModel.indexBuffer != null && sourceModel.indexBuffer.Length > 0)
            {
                newModel.indexBuffer = new ushort[sourceModel.indexBuffer.Length];
                Array.Copy(sourceModel.indexBuffer, newModel.indexBuffer, sourceModel.indexBuffer.Length);
                Console.WriteLine($"    Copied {sourceModel.indexBuffer.Length / 3} triangles");
            }
            else
            {
                Console.WriteLine("    Warning: Source model has no index data");
                newModel.indexBuffer = new ushort[0];
            }

            // Clone texture configuration
            newModel.textureConfig = new List<TextureConfig>();
            if (sourceModel.textureConfig != null)
            {
                foreach (var texConfig in sourceModel.textureConfig)
                {
                    TextureConfig newConfig = new TextureConfig
                    {
                        id = texConfig.id,
                        start = texConfig.start,
                        size = texConfig.size,
                        mode = texConfig.mode,
                        wrapModeS = texConfig.wrapModeS,
                        wrapModeT = texConfig.wrapModeT
                    };
                    newModel.textureConfig.Add(newConfig);
                }
                Console.WriteLine($"    Copied {sourceModel.textureConfig.Count} texture configurations");
            }
            else
            {
                Console.WriteLine("    Warning: Source model has no texture configs");
            }

            Console.WriteLine($"  ✅ Successfully cloned shrub model {sourceModel.id}");
            return newModel;
        }

        /// <summary>
        /// Transfers shrub instances from RC1 to RC2 level
        /// </summary>
        public static bool TransferShrubInstances(Level targetLevel, Level rc1SourceLevel, Dictionary<int, int> modelIdMapping)
        {
            int count = 0;

            // Process each shrub from RC1
            foreach (var rc1Shrub in rc1SourceLevel.shrubs)
            {
                // Create a new shrub instance based on the RC1 shrub
                Shrub newShrub = new Shrub(rc1Shrub);

                // Map to the new model ID if needed
                if (modelIdMapping.ContainsKey(newShrub.modelID))
                {
                    newShrub.modelID = modelIdMapping[newShrub.modelID];
                }

                // Find the corresponding model
                newShrub.model = targetLevel.shrubModels.Find(m => m.id == newShrub.modelID);

                // Only add if we found a model
                if (newShrub.model != null)
                {
                    targetLevel.shrubs.Add(newShrub);
                    count++;
                }
                else
                {
                    Console.WriteLine($"  ⚠️ Could not find model ID {newShrub.modelID} for shrub");
                }
            }

            Console.WriteLine($"  ✅ Transferred {count} shrubs from RC1 level");
            return count > 0;
        }

        /// <summary>
        /// Resolves conflicts between Moby and Shrub model IDs
        /// </summary>
        public static int ResolveMobyShrubIdConflicts(Level level)
        {
            int changedCount = 0;
            HashSet<short> mobyIds = new HashSet<short>();

            // Collect all moby IDs
            foreach (var model in level.mobyModels)
            {
                mobyIds.Add(model.id);
            }

            // Find shrub models with conflicting IDs
            var conflictIds = level.shrubModels
                .Where(model => mobyIds.Contains(model.id))
                .ToList();

            if (conflictIds.Count == 0)
            {
                Console.WriteLine("  ✅ No ID conflicts detected between shrub and moby models");
                return 0;
            }

            // Find the highest existing ID
            short maxId = level.shrubModels.Max(model => model.id);
            short nextId = (short) (maxId + 1);

            // Resolve conflicts by assigning new IDs
            foreach (var model in conflictIds)
            {
                short oldId = model.id;
                model.id = nextId++;
                changedCount++;
                Console.WriteLine($"  ✅ Changed shrub model ID from {oldId} to {model.id} to resolve conflict");

                // Update any shrub instances using this model
                foreach (var shrub in level.shrubs.Where(s => s.modelID == oldId))
                {
                    shrub.modelID = model.id;
                    shrub.model = model;
                }
            }

            Console.WriteLine($"  ✅ Resolved {changedCount} ID conflicts between shrub and moby models");
            return changedCount;
        }

        /// <summary>
        /// Updates all shrub light references to use Light 0
        /// </summary>
        private static void UpdateShrubLightReferences(Level level)
        {
            int count = 0;
            if (level.shrubs != null)
            {
                foreach (var shrub in level.shrubs)
                {
                    if (shrub.light != 0)
                    {
                        shrub.light = 0;
                        count++;
                    }
                }
            }
            Console.WriteLine($"  ✅ Updated {count} shrubs to use Light 0");
        }

        /// <summary>
        /// Interactive wrapper for shrub swapping function
        /// </summary>
        /// <returns>True if the operation was successful</returns>
        public static bool SwapShrubsWithRC1OltanisInteractive()
        {
            Console.WriteLine("\n==== Swap RC2 Shrubs with RC1 Oltanis Shrubs ====");

            // Get target level path
            Console.WriteLine("\nEnter path to the target RC2 level engine.ps3 file:");
            Console.Write("> ");
            string targetPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                Console.WriteLine("❌ Invalid target level path");
                return false;
            }

            // Get RC1 Oltanis level path
            Console.WriteLine("\nEnter path to the RC1 Oltanis level engine.ps3 file:");
            Console.Write("> ");
            string rc1OltanisPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(rc1OltanisPath) || !File.Exists(rc1OltanisPath))
            {
                Console.WriteLine("❌ Invalid RC1 Oltanis level path");
                return false;
            }

            // Load levels
            Level targetLevel, rc1OltanisLevel;
            try
            {
                targetLevel = new Level(targetPath);
                rc1OltanisLevel = new Level(rc1OltanisPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading levels: {ex.Message}");
                return false;
            }

            // Option selection
            Console.WriteLine("\nSelect swap options:");
            Console.WriteLine("1. Full replacement (RC1 models and positions)");
            Console.WriteLine("2. Placements only (keep RC2 models but use RC1 positions)");
            Console.WriteLine("3. Custom options");
            Console.Write("> ");
            string choice = Console.ReadLine()?.Trim() ?? "1";
            ShrubSwapOptions options;
            switch (choice)
            {
                case "2":
                    options = ShrubSwapOptions.PlacementsOnly;
                    break;
                case "3":
                    options = GetCustomOptions();
                    break;
                case "1":
                default:
                    options = ShrubSwapOptions.FullSwap;
                    break;
            }

            bool success = SwapShrubsWithRC1Oltanis(targetLevel, rc1OltanisLevel, options);

            if (success)
            {
                Console.Write("\nSave changes to the target level? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    try
                    {
                        targetLevel.Save(targetPath);
                        Console.WriteLine("✅ Target level saved successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error saving target level: {ex.Message}");
                        return false;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Helper method to get custom options
        /// </summary>
        private static ShrubSwapOptions GetCustomOptions()
        {
            ShrubSwapOptions options = ShrubSwapOptions.None;

            Console.WriteLine("\nCustomize swap options:");

            if (GetYesNoInput("Use RC1 shrub placements? (y/n): "))
                options |= ShrubSwapOptions.UseRC1Placements;

            if (GetYesNoInput("Use RC1 shrub models? (y/n): "))
                options |= ShrubSwapOptions.UseRC1Models;

            if (GetYesNoInput("Map RC1 textures to RC2 level? (y/n): "))
                options |= ShrubSwapOptions.MapTextures;

            return options;
        }

        /// <summary>
        /// Helper method for yes/no input
        /// </summary>
        private static bool GetYesNoInput(string prompt)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()?.Trim().ToLower() ?? "";
            return input == "y" || input == "yes";
        }
    }
}
