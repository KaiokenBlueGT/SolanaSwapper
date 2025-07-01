using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Models.Animations;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometrySwapper
{
    /// <summary>
    /// Specializes in handling the Vendor Moby replacement with RC1 Oltanis Vendor
    /// </summary>
    public static class VendorOltanisSwapper
    {
        /// <summary>
        /// Flags enum to control swap options
        /// </summary>
        [Flags]
        public enum VendorSwapOptions
        {
            None = 0,
            UseRC1Position = 1,
            UseRC1Model = 2,

            // Common combinations
            PositionOnly = UseRC1Position,
            FullReplacement = UseRC1Position | UseRC1Model,
            Default = FullReplacement
        }

        /// <summary>
        /// Swaps the RC2 Vendor with RC1 Oltanis Vendor model, animations, and textures
        /// </summary>
        /// <param name="targetLevel">RC2 level to modify</param>
        /// <param name="rc1OltanisLevel">RC1 Oltanis level to get the vendor model from</param>
        /// <param name="options">Options to control the swap behavior</param>
        /// <returns>True if operation was successful</returns>
        public static bool SwapVendorWithRC1Oltanis(Level targetLevel, Level rc1OltanisLevel, VendorSwapOptions options = VendorSwapOptions.Default)
        {
            if (targetLevel == null || rc1OltanisLevel == null)
            {
                Console.WriteLine("❌ Cannot swap vendor: Invalid level data");
                return false;
            }

            // Vendor model ID
            const int vendorModelId = 11;

            Console.WriteLine("\n==== Swapping RC2 Vendor with RC1 Oltanis Vendor ====");
            Console.WriteLine($"Options: Position={options.HasFlag(VendorSwapOptions.UseRC1Position)}, " +
                              $"RC1Model={options.HasFlag(VendorSwapOptions.UseRC1Model)}");

            // Find models in both levels
            var rc1VendorModel = rc1OltanisLevel.mobyModels?.FirstOrDefault(m => m.id == vendorModelId) as MobyModel;
            var targetVendorModel = targetLevel.mobyModels?.FirstOrDefault(m => m.id == vendorModelId) as MobyModel;

            if (rc1VendorModel == null)
            {
                Console.WriteLine("❌ Could not find vendor model in RC1 Oltanis level");
                return false;
            }

            if (targetVendorModel == null)
            {
                Console.WriteLine("❌ Could not find vendor model in target level");
                return false;
            }

            // Step 1: Find and remove duplicate vendor mobys in target level
            var targetVendorMobys = targetLevel.mobs?.Where(m => m.modelID == vendorModelId).ToList();
            if (targetVendorMobys == null || targetVendorMobys.Count == 0)
            {
                Console.WriteLine("❌ No vendor mobys found in target level");
                return false;
            }

            Console.WriteLine($"Found {targetVendorMobys.Count} vendor mobys in target level");

            if (targetVendorMobys.Count > 1)
            {
                // We are intentionally NOT removing duplicate vendors for testing purposes.
                Console.WriteLine($"Found multiple ({targetVendorMobys.Count}) vendor mobys - NOT removing duplicates for testing.");

                // The rest of the code will now operate on all found vendors.
                // For example, if UseRC1Model is true, ALL vendors will get the new model.
                // If UseRC1Position is true, only the FIRST vendor (targetVendorMobys[0]) will be moved.

                /*
                // Keep the first vendor moby and remove duplicates
                var vendorToKeep = targetVendorMobys[0];

                // Remove all duplicate vendors (except the first one)
                for (int i = 1; i < targetVendorMobys.Count; i++)
                {
                    targetLevel.mobs.Remove(targetVendorMobys[i]);
                    Console.WriteLine($"  Removed duplicate vendor at position {targetVendorMobys[i].position}");
                }

                // Update our reference to only include the one we kept
                targetVendorMobys = new List<Moby> { vendorToKeep };
                */
            }

            // Step 2: Reposition vendor if UseRC1Position is specified
            if (options.HasFlag(VendorSwapOptions.UseRC1Position))
            {
                var rc1VendorMobys = rc1OltanisLevel.mobs?.Where(m => m.modelID == vendorModelId).ToList();
                if (rc1VendorMobys == null || rc1VendorMobys.Count == 0)
                {
                    Console.WriteLine("⚠️ Could not find vendor moby in RC1 Oltanis level - will not reposition");
                }
                else
                {
                    Console.WriteLine($"Found {rc1VendorMobys.Count} vendor mobys in RC1 Oltanis level");

                    // Get the position of the first RC1 vendor
                    var rc1Vendor = rc1VendorMobys[0];

                    // Reposition the target vendor
                    var targetVendor = targetVendorMobys[0];
                    Console.WriteLine($"Repositioning vendor from {targetVendor.position} to {rc1Vendor.position}");
                    targetVendor.position = rc1Vendor.position;
                    targetVendor.rotation = rc1Vendor.rotation;
                    targetVendor.scale = rc1Vendor.scale;
                    targetVendor.UpdateTransformMatrix();
                }
            }
            else
            {
                Console.WriteLine("Keeping original vendor position (RC1 positioning disabled)");
            }

            // Set light value to 0 for all vendor mobys to match RC1 Oltanis
            foreach (var vendor in targetVendorMobys)
            {
                vendor.light = 0;
            }
            Console.WriteLine("✅ Set vendor light value to 0 to match RC1 Oltanis");

            // Step 3: Replace vendor model if UseRC1Model is specified
            if (options.HasFlag(VendorSwapOptions.UseRC1Model))
            {
                // Deep clone the RC1 vendor model
                var clonedRC1VendorModel = (MobyModel) MobySwapper.DeepCloneModel(rc1VendorModel);
                clonedRC1VendorModel.id = vendorModelId; // Ensure ID is maintained as 11

                // Preserve the RC2 vendor's specific values that must not change
                clonedRC1VendorModel.count3 = targetVendorModel.count3;  // Usually 0
                clonedRC1VendorModel.count4 = targetVendorModel.count4;  // Usually 0
                clonedRC1VendorModel.unk1 = targetVendorModel.unk1;      // Usually -0.000
                clonedRC1VendorModel.unk2 = targetVendorModel.unk2;      // Usually 0.001
                clonedRC1VendorModel.unk3 = targetVendorModel.unk3;      // Usually -0.000
                clonedRC1VendorModel.unk4 = targetVendorModel.unk4;      // Usually 25348.143
                clonedRC1VendorModel.color2 = targetVendorModel.color2;  // Usually -2139086816
                clonedRC1VendorModel.unk6 = targetVendorModel.unk6;      // Usually 1073807359

                Console.WriteLine($"Preserving RC2 vendor model properties:");
                Console.WriteLine($"  count3: {targetVendorModel.count3}");
                Console.WriteLine($"  count4: {targetVendorModel.count4}");
                Console.WriteLine($"  unk1: {targetVendorModel.unk1}");
                Console.WriteLine($"  unk2: {targetVendorModel.unk2}");
                Console.WriteLine($"  unk3: {targetVendorModel.unk3}");
                Console.WriteLine($"  unk4: {targetVendorModel.unk4}");
                Console.WriteLine($"  color2: {targetVendorModel.color2}");
                Console.WriteLine($"  unk6: {targetVendorModel.unk6}");

                // Import textures for the RC1 vendor model
                if (clonedRC1VendorModel.textureConfig != null && clonedRC1VendorModel.textureConfig.Count > 0)
                {
                    Console.WriteLine($"RC1 vendor model has {clonedRC1VendorModel.textureConfig.Count} texture configurations");
                    Console.WriteLine("Importing textures for RC1 vendor model...");

                    // Dictionary to map source texture indices to target texture indices
                    Dictionary<int, int> textureMapping = new Dictionary<int, int>();

                    foreach (var texConfig in clonedRC1VendorModel.textureConfig)
                    {
                        int originalTexId = texConfig.id;

                        // Validate texture index
                        if (originalTexId < 0 || originalTexId >= rc1OltanisLevel.textures.Count)
                        {
                            Console.WriteLine($"  ⚠️ Texture ID {originalTexId} is out of range for RC1 Oltanis textures");
                            continue;
                        }

                        var rc1Texture = rc1OltanisLevel.textures[originalTexId];

                        // Check if this texture already exists in the target level
                        int targetTexId = -1;
                        for (int i = 0; i < targetLevel.textures.Count; i++)
                        {
                            if (TextureEquals(rc1Texture, targetLevel.textures[i]))
                            {
                                targetTexId = i;
                                break;
                            }
                        }

                        // If not found, add the texture to the target level
                        if (targetTexId == -1)
                        {
                            // Deep copy the texture
                            var clonedTexture = DeepCloneTexture(rc1Texture);
                            targetLevel.textures.Add(clonedTexture);
                            targetTexId = targetLevel.textures.Count - 1;
                            Console.WriteLine($"  Added RC1 vendor texture at index {targetTexId}");
                        }
                        else
                        {
                            Console.WriteLine($"  Found matching texture at index {targetTexId}");
                        }

                        // Update the mapping and texture config
                        textureMapping[originalTexId] = targetTexId;
                        texConfig.id = targetTexId;
                    }

                    // Handle other texture configs if present
                    if (clonedRC1VendorModel.otherTextureConfigs != null && clonedRC1VendorModel.otherTextureConfigs.Count > 0)
                    {
                        foreach (var texConfig in clonedRC1VendorModel.otherTextureConfigs)
                        {
                            int originalTexId = texConfig.id;

                            // If we've already mapped this texture, reuse the mapping
                            if (textureMapping.TryGetValue(originalTexId, out int mappedId))
                            {
                                texConfig.id = mappedId;
                            }
                            else
                            {
                                // Otherwise map it just like we did above
                                if (originalTexId >= 0 && originalTexId < rc1OltanisLevel.textures.Count)
                                {
                                    var rc1Texture = rc1OltanisLevel.textures[originalTexId];

                                    // Check if this texture already exists
                                    int targetTexId = -1;
                                    for (int i = 0; i < targetLevel.textures.Count; i++)
                                    {
                                        if (TextureEquals(rc1Texture, targetLevel.textures[i]))
                                        {
                                            targetTexId = i;
                                            break;
                                        }
                                    }

                                    if (targetTexId == -1)
                                    {
                                        var clonedTexture = DeepCloneTexture(rc1Texture);
                                        targetLevel.textures.Add(clonedTexture);
                                        targetTexId = targetLevel.textures.Count - 1;
                                    }

                                    textureMapping[originalTexId] = targetTexId;
                                    texConfig.id = targetTexId;
                                }
                            }
                        }
                    }
                }

                // Replace the target vendor model
                targetLevel.mobyModels.Remove(targetVendorModel);
                targetLevel.mobyModels.Add(clonedRC1VendorModel);

                // Update the reference in vendor mobys
                foreach (var moby in targetVendorMobys)
                {
                    moby.model = clonedRC1VendorModel;
                }

                Console.WriteLine("✅ Replaced target vendor model with RC1 Oltanis vendor model");
            }
            else
            {
                Console.WriteLine("Keeping original vendor model (RC1 model replacement disabled)");
            }

            // Step 4: Validate and fix pVar indices
            MobySwapper.ValidateAndFixPvarIndices(targetLevel);

            // Step 5: Ensure vendor logo has correct properties if it exists
            EnsureVendorLogoProperties(targetLevel);

            // Summary
            Console.WriteLine("\n==== Vendor Swap Summary ====");
            Console.WriteLine("✅ Kept all vendor mobys for testing (did not remove duplicates)");
            Console.WriteLine("✅ Set vendor light value to 0 to match RC1 Oltanis");

            if (options.HasFlag(VendorSwapOptions.UseRC1Position))
                Console.WriteLine("✅ Repositioned vendor to match RC1 Oltanis");

            if (options.HasFlag(VendorSwapOptions.UseRC1Model))
                Console.WriteLine("✅ Replaced vendor model with RC1 Oltanis model");

            Console.WriteLine("✅ Fixed up all pVar indices");

            return true;
        }

        /// <summary>
        /// Interactive wrapper for vendor swapping function
        /// </summary>
        /// <returns>True if the operation was successful</returns>
        public static bool SwapVendorWithRC1OltanisInteractive()
        {
            Console.WriteLine("\n==== Swap RC2 Vendor with RC1 Oltanis Vendor ====");

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

            // Load target level
            Console.WriteLine($"\nLoading target level: {Path.GetFileName(targetPath)}...");
            Level targetLevel;

            try
            {
                targetLevel = new Level(targetPath);
                Console.WriteLine($"✅ Successfully loaded target level");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading target level: {ex.Message}");
                return false;
            }

            // Load RC1 Oltanis level
            Console.WriteLine($"\nLoading RC1 Oltanis level: {Path.GetFileName(rc1OltanisPath)}...");
            Level rc1OltanisLevel;

            try
            {
                rc1OltanisLevel = new Level(rc1OltanisPath);
                Console.WriteLine($"✅ Successfully loaded RC1 Oltanis level");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading RC1 Oltanis level: {ex.Message}");
                return false;
            }

            // Add option selection before performing the swap
            Console.WriteLine("\nSelect swap options:");
            Console.WriteLine("1. Full replacement (RC1 model and position)");
            Console.WriteLine("2. Position only (keep RC2 model but use RC1 position)");
            Console.WriteLine("3. Custom options");
            Console.Write("> ");

            string choice = Console.ReadLine()?.Trim() ?? "1";
            VendorSwapOptions options;

            switch (choice)
            {
                case "2":
                    options = VendorSwapOptions.PositionOnly;
                    break;
                case "3":
                    options = GetCustomOptions();
                    break;
                case "1":
                default:
                    options = VendorSwapOptions.FullReplacement;
                    break;
            }

            // Perform vendor swap with selected options
            bool success = SwapVendorWithRC1Oltanis(targetLevel, rc1OltanisLevel, options);

            if (success)
            {
                // Ask if the user wants to save
                Console.Write("\nSave changes to the target level? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    Console.WriteLine("Saving target level...");

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
        private static VendorSwapOptions GetCustomOptions()
        {
            VendorSwapOptions options = VendorSwapOptions.None;

            Console.WriteLine("\nCustomize swap options:");

            if (GetYesNoInput("Reposition vendor to match RC1 position? (y/n): "))
                options |= VendorSwapOptions.UseRC1Position;

            if (GetYesNoInput("Replace vendor model with RC1 model? (y/n): "))
                options |= VendorSwapOptions.UseRC1Model;

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

        /// <summary>
        /// Helper method to compare two textures for similarity
        /// </summary>
        private static bool TextureEquals(Texture tex1, Texture tex2)
        {
            if (tex1 == null || tex2 == null)
                return false;

            return tex1.width == tex2.width &&
                   tex1.height == tex2.height &&
                   tex1.vramPointer == tex2.vramPointer &&
                   tex1.data?.Length == tex2.data?.Length;
        }

        /// <summary>
        /// Helper method to create a deep copy of a texture
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
        /// Add a helper method to import textures for a model
        /// </summary>
        private static void ImportModelTextures(Level targetLevel, Level sourceLevel, MobyModel model)
        {
            if (model.textureConfig == null || model.textureConfig.Count == 0)
                return;

            Dictionary<int, int> textureMapping = new Dictionary<int, int>();

            foreach (var texConfig in model.textureConfig)
            {
                int originalTexId = texConfig.id;

                // Validate texture index
                if (originalTexId < 0 || originalTexId >= sourceLevel.textures.Count)
                {
                    Console.WriteLine($"  ⚠️ Texture ID {originalTexId} is out of range for source textures");
                    continue;
                }

                var sourceTexture = sourceLevel.textures[originalTexId];

                // Check if this texture already exists in the target level
                int targetTexId = -1;
                for (int i = 0; i < targetLevel.textures.Count; i++)
                {
                    if (TextureEquals(sourceTexture, targetLevel.textures[i]))
                    {
                        targetTexId = i;
                        break;
                    }
                }

                // If not found, add the texture to the target level
                if (targetTexId == -1)
                {
                    // Deep copy the texture
                    var clonedTexture = DeepCloneTexture(sourceTexture);
                    targetLevel.textures.Add(clonedTexture);
                    targetTexId = targetLevel.textures.Count - 1;
                    Console.WriteLine($"  Added texture at index {targetTexId}");
                }
                else
                {
                    Console.WriteLine($"  Found matching texture at index {targetTexId}");
                }

                // Update the mapping and texture config
                textureMapping[originalTexId] = targetTexId;
                texConfig.id = targetTexId;
            }

            // Handle other texture configs if present
            if (model.otherTextureConfigs != null && model.otherTextureConfigs.Count > 0)
            {
                foreach (var texConfig in model.otherTextureConfigs)
                {
                    int originalTexId = texConfig.id;

                    // If we've already mapped this texture, reuse the mapping
                    if (textureMapping.TryGetValue(originalTexId, out int mappedId))
                    {
                        texConfig.id = mappedId;
                    }
                    else if (originalTexId >= 0 && originalTexId < sourceLevel.textures.Count)
                    {
                        // Map it like we did above
                        var sourceTexture = sourceLevel.textures[originalTexId];

                        // Find or add the texture
                        int targetTexId = -1;
                        for (int i = 0; i < targetLevel.textures.Count; i++)
                        {
                            if (TextureEquals(sourceTexture, targetLevel.textures[i]))
                            {
                                targetTexId = i;
                                break;
                            }
                        }

                        if (targetTexId == -1)
                        {
                            var clonedTexture = DeepCloneTexture(sourceTexture);
                            targetLevel.textures.Add(clonedTexture);
                            targetTexId = targetLevel.textures.Count - 1;
                        }

                        textureMapping[originalTexId] = targetTexId;
                        texConfig.id = targetTexId;
                    }
                }
            }
        }

        /// <summary>
        /// Ensures the Vendor Logo model (ID 1143) has the correct property values
        /// </summary>
        /// <param name="level">The level containing the vendor logo model</param>
        /// <returns>True if the vendor logo was found and updated, otherwise false</returns>
        public static bool EnsureVendorLogoProperties(Level level)
        {
            // Vendor Logo model ID
            const int vendorLogoId = 1143;

            // Find vendor logo model in the level
            var vendorLogoModel = level.mobyModels?.FirstOrDefault(m => m.id == vendorLogoId) as MobyModel;

            if (vendorLogoModel == null)
            {
                Console.WriteLine("⚠️ Could not find vendor logo model in the level (ID: 1143)");
                return false;
            }

            Console.WriteLine("Setting correct properties for Vendor Logo model (ID: 1143)");

            // Set the required property values
            vendorLogoModel.count3 = 0;
            vendorLogoModel.count4 = 0;
            vendorLogoModel.unk1 = 0.002f;
            vendorLogoModel.unk2 = 0.002f;
            vendorLogoModel.unk3 = -3318.602f;
            vendorLogoModel.unk4 = 25448.279f;
            vendorLogoModel.unk6 = 1073807359;

            Console.WriteLine("✅ Updated vendor logo properties:");
            Console.WriteLine($"  count3: {vendorLogoModel.count3}");
            Console.WriteLine($"  count4: {vendorLogoModel.count4}");
            Console.WriteLine($"  unk1: {vendorLogoModel.unk1}");
            Console.WriteLine($"  unk2: {vendorLogoModel.unk2}");
            Console.WriteLine($"  unk3: {vendorLogoModel.unk3}");
            Console.WriteLine($"  unk4: {vendorLogoModel.unk4}");
            Console.WriteLine($"  unk6: {vendorLogoModel.unk6}");

            return true;
        }
    }
}
