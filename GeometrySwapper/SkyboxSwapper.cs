using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibReplanetizer;
using LibReplanetizer.Models;
using SixLabors.ImageSharp.PixelFormats;
using static LibReplanetizer.DataFunctions;

namespace GeometrySwapper
{
    /// <summary>
    /// Handles the swapping of skybox models between levels, specifically from RC1 to RC2
    /// </summary>
    public class SkyboxSwapper
    {
        /// <summary>
        /// Copies and adapts a skybox from an RC1 level to an RC2 level
        /// </summary>
        /// <param name="targetLevel">The RC2 level where the skybox will be replaced</param>
        /// <param name="rc1SourceLevel">The RC1 level containing the source skybox</param>
        /// <param name="rc2DonorLevel">Optional RC2 level to use for structure reference</param>
        /// <returns>True if the operation was successful</returns>
        public static bool SwapSkybox(Level targetLevel, Level rc1SourceLevel, Level rc2DonorLevel = null)
        {
            Console.WriteLine($"🌠 Swapping skybox from RC1 level to RC2 level...");

            if (targetLevel == null)
            {
                Console.WriteLine("  ❌ Error: Target level is null");
                return false;
            }

            if (rc1SourceLevel == null || rc1SourceLevel.skybox == null)
            {
                Console.WriteLine("  ❌ Error: RC1 source level or its skybox is null");
                return false;
            }

            try
            {
                // Create a deep clone of the RC1 skybox
                SkyboxModel rc1Skybox = rc1SourceLevel.skybox;
                SkyboxModel newSkybox = CloneSkyboxModel(rc1Skybox);
                
                // Update the game type to RC2
                newSkybox.game = GameType.RaC2;

                // If we have a donor RC2 level, use it for reference values that might be game-specific
                if (rc2DonorLevel != null && rc2DonorLevel.skybox != null)
                {
                    SkyboxModel donorSkybox = rc2DonorLevel.skybox;
                    
                    // Copy RC2-specific fields that might need to be preserved
                    newSkybox.off04 = donorSkybox.off04;
                    newSkybox.off08 = donorSkybox.off08;
                    newSkybox.off0A = donorSkybox.off0A;
                    newSkybox.off0C = donorSkybox.off0C;
                    
                    Console.WriteLine("  Applied RC2 donor skybox field values for compatibility");
                }

                // Import the textures used by the skybox
                Dictionary<int, int> textureIdMapping = ImportSkyboxTextures(targetLevel, rc1SourceLevel, newSkybox);

                // Update texture references in the skybox
                UpdateSkyboxTextureReferences(newSkybox, textureIdMapping);
                
                // Replace the target skybox with our modified RC1 skybox
                targetLevel.skybox = newSkybox;
                
                Console.WriteLine("  ✅ Skybox successfully swapped");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error during skybox swap: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a deep clone of a SkyboxModel
        /// </summary>
        /// <param name="sourceSkybox">The source skybox to clone</param>
        /// <returns>A new SkyboxModel instance with copied data</returns>
        private static SkyboxModel CloneSkyboxModel(SkyboxModel sourceSkybox)
        {
            Console.WriteLine("  Creating deep clone of source skybox...");
            
            // Create a minimal skybox model
            SkyboxModel newSkybox = new SkyboxModel(null, sourceSkybox.game, 0);
            
            // Copy basic properties
            newSkybox.id = sourceSkybox.id;
            newSkybox.size = sourceSkybox.size;
            typeof(Model).GetProperty("vertexStride", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(newSkybox, sourceSkybox.vertexStride);
            newSkybox.someColor = sourceSkybox.someColor;
            newSkybox.off04 = sourceSkybox.off04;
            newSkybox.off08 = sourceSkybox.off08;
            newSkybox.off0A = sourceSkybox.off0A;
            newSkybox.off0C = sourceSkybox.off0C;
            
            // Deep copy vertex buffer
            if (sourceSkybox.vertexBuffer != null)
            {
                newSkybox.vertexBuffer = new float[sourceSkybox.vertexBuffer.Length];
                Array.Copy(sourceSkybox.vertexBuffer, newSkybox.vertexBuffer, sourceSkybox.vertexBuffer.Length);
            }
            
            // Deep copy index buffer
            if (sourceSkybox.indexBuffer != null)
            {
                newSkybox.indexBuffer = new ushort[sourceSkybox.indexBuffer.Length];
                Array.Copy(sourceSkybox.indexBuffer, newSkybox.indexBuffer, sourceSkybox.indexBuffer.Length);
            }
            
            // Deep copy texture configurations
            newSkybox.textureConfig = new List<TextureConfig>();
            if (sourceSkybox.textureConfig != null)
            {
                foreach (var texConfig in sourceSkybox.textureConfig)
                {
                    TextureConfig newConfig = new TextureConfig();
                    newConfig.id = texConfig.id;
                    newConfig.start = texConfig.start;
                    newConfig.size = texConfig.size;
                    newConfig.mode = texConfig.mode;
                    newConfig.wrapModeS = texConfig.wrapModeS;
                    newConfig.wrapModeT = texConfig.wrapModeT;
                    newSkybox.textureConfig.Add(newConfig);
                }
            }
            
            // Deep copy textureConfigs structure
            newSkybox.textureConfigs = new List<List<TextureConfig>>();
            if (sourceSkybox.textureConfigs != null)
            {
                foreach (var texConfigList in sourceSkybox.textureConfigs)
                {
                    var newConfigList = new List<TextureConfig>();
                    foreach (var texConfig in texConfigList)
                    {
                        TextureConfig newConfig = new TextureConfig();
                        newConfig.id = texConfig.id;
                        newConfig.start = texConfig.start;
                        newConfig.size = texConfig.size;
                        newConfig.mode = texConfig.mode;
                        newConfig.wrapModeS = texConfig.wrapModeS;
                        newConfig.wrapModeT = texConfig.wrapModeT;
                        newConfigList.Add(newConfig);
                    }
                    newSkybox.textureConfigs.Add(newConfigList);
                }
            }
            
            return newSkybox;
        }

        /// <summary>
        /// Imports textures used by the skybox from the RC1 source to the RC2 target level
        /// </summary>
        /// <param name="targetLevel">The RC2 target level</param>
        /// <param name="rc1SourceLevel">The RC1 source level</param>
        /// <param name="skybox">The skybox model</param>
        /// <returns>Mapping from RC1 texture IDs to RC2 texture IDs</returns>
        private static Dictionary<int, int> ImportSkyboxTextures(Level targetLevel, Level rc1SourceLevel, SkyboxModel skybox)
        {
            Console.WriteLine("  Importing skybox textures...");
            
            var textureIdMapping = new Dictionary<int, int>();
            
            // Ensure target texture list exists
            if (targetLevel.textures == null)
            {
                targetLevel.textures = new List<Texture>();
            }
            
            // Identify texture IDs used by the skybox
            HashSet<int> usedTextureIds = new HashSet<int>();
            if (skybox.textureConfig != null)
            {
                foreach (var texConfig in skybox.textureConfig)
                {
                    usedTextureIds.Add(texConfig.id);
                }
            }
            
            if (skybox.textureConfigs != null)
            {
                foreach (var configList in skybox.textureConfigs)
                {
                    foreach (var texConfig in configList)
                    {
                        usedTextureIds.Add(texConfig.id);
                    }
                }
            }
            
            Console.WriteLine($"  Found {usedTextureIds.Count} unique texture IDs used by skybox");
            
            // Import each texture
            foreach (int rc1TextureId in usedTextureIds)
            {
                // Skip invalid texture IDs
                if (rc1TextureId < 0 || rc1TextureId >= rc1SourceLevel.textures.Count)
                {
                    Console.WriteLine($"  ⚠️ Warning: Skybox references texture ID {rc1TextureId} which is out of range for RC1 level");
                    continue;
                }
                
                // Find the texture in RC1 level
                Texture rc1Texture = rc1SourceLevel.textures[rc1TextureId];
                if (rc1Texture == null)
                {
                    Console.WriteLine($"  ⚠️ Warning: Skybox references texture ID {rc1TextureId} which was not found in RC1 level");
                    continue;
                }
                
                // Check if this texture already exists in the target level
                int targetTexId = -1;
                for (int i = 0; i < targetLevel.textures.Count; i++)
                {
                    // Compare key properties to determine if textures are equivalent
                    if (TextureEquals(rc1Texture, targetLevel.textures[i]))
                    {
                        targetTexId = i;
                        Console.WriteLine($"  Found matching texture at index {targetTexId} for source {rc1TextureId}");
                        break;
                    }
                }
                
                // If not found, add the texture to the target level
                if (targetTexId == -1)
                {
                    // Deep copy the texture
                    Texture newTexture = DeepCloneTexture(rc1Texture);
                    targetLevel.textures.Add(newTexture);
                    targetTexId = targetLevel.textures.Count - 1;
                    Console.WriteLine($"  Imported texture {rc1TextureId} -> {targetTexId} ({newTexture.width}x{newTexture.height})");
                }
                
                // Record mapping
                textureIdMapping[rc1TextureId] = targetTexId;
            }
            
            return textureIdMapping;
        }

        /// <summary>
        /// Checks if two textures are effectively equal
        /// </summary>
        private static bool TextureEquals(Texture tex1, Texture tex2)
        {
            if (tex1 == null || tex2 == null)
                return false;
            
            // Compare key properties to determine if textures are equivalent
            return tex1.width == tex2.width &&
                   tex1.height == tex2.height &&
                   tex1.vramPointer == tex2.vramPointer &&
                   tex1.data?.Length == tex2.data?.Length;
        }

        /// <summary>
        /// Creates a deep copy of a texture with all properties preserved
        /// </summary>
        private static Texture DeepCloneTexture(Texture sourceTexture)
        {
            if (sourceTexture == null)
                throw new ArgumentNullException(nameof(sourceTexture), "Source texture cannot be null.");
            
            // Create a new texture with the same properties
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
            
            // Copy remaining properties
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
        /// Updates texture references in the skybox based on the texture ID mapping
        /// </summary>
        /// <param name="skybox">The skybox to update</param>
        /// <param name="textureIdMapping">Mapping from RC1 texture IDs to RC2 texture IDs</param>
        private static void UpdateSkyboxTextureReferences(SkyboxModel skybox, Dictionary<int, int> textureIdMapping)
        {
            Console.WriteLine("  Updating skybox texture references...");
            
            // Update main texture config
            if (skybox.textureConfig != null)
            {
                foreach (var texConfig in skybox.textureConfig)
                {
                    if (textureIdMapping.TryGetValue(texConfig.id, out int newId))
                    {
                        texConfig.id = newId;
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ Warning: No mapping found for texture ID {texConfig.id}");
                    }
                }
            }
            
            // Update textureConfigs structure
            if (skybox.textureConfigs != null)
            {
                foreach (var texConfigList in skybox.textureConfigs)
                {
                    foreach (var texConfig in texConfigList)
                    {
                        if (textureIdMapping.TryGetValue(texConfig.id, out int newId))
                        {
                            texConfig.id = newId;
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠️ Warning: No mapping found for texture ID {texConfig.id}");
                        }
                    }
                }
            }
        }
    }
}
