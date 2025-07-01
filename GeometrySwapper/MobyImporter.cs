using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Models.Animations; // Add this line to import Animation
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometrySwapper
{
    /// <summary>
    /// Handles importing mobys from reference RC2 levels into a target level
    /// </summary>
    public static class MobyImporter
    {
        // Class to store moby data for comparison across levels
        private class MobyIdentifier
        {
            public int ModelId { get; set; }
            public string ModelName { get; set; }
            public int AnimationCount { get; set; }
            public Level SourceLevel { get; set; }
            public string LevelName => Path.GetFileNameWithoutExtension(SourceLevel.path);
            public MobyModel SourceModel { get; set; }
            public List<Animation> Animations => SourceModel?.animations ?? new List<Animation>();

            public bool HasAnimations => AnimationCount > 0;

            public MobyIdentifier(int modelId, MobyModel model, Level level)
            {
                ModelId = modelId;
                ModelName = GetFriendlyName(modelId);
                AnimationCount = model?.animations?.Count ?? 0;
                SourceLevel = level;
                SourceModel = model;
            }

            private string GetFriendlyName(int modelId)
            {
                // Try to find in the MobyTypes dictionary first
                foreach (var entry in MobySwapper.MobyTypes)
                {
                    if (entry.Value.Contains(modelId))
                    {
                        return entry.Key;
                    }
                }

                // If not found, just use the model ID
                return $"Model {modelId}";
            }
        }

        // Represents a moby that appears in multiple reference levels
        private class CommonMoby
        {
            public int ModelId { get; set; }
            public string ModelName { get; set; }
            public List<MobyIdentifier> Instances { get; set; } = new List<MobyIdentifier>();
            public int ReferenceCount => Instances.Count;
            public bool HasAnimations => Instances.Any(i => i.HasAnimations);
            public MobyIdentifier BestInstance => GetBestInstance();

            public CommonMoby(int modelId, string name)
            {
                ModelId = modelId;
                ModelName = name;
            }

            public void AddInstance(MobyIdentifier instance)
            {
                if (!Instances.Any(i => i.SourceLevel == instance.SourceLevel))
                {
                    Instances.Add(instance);
                }
            }

            // Get the best instance (one with animations preferred)
            private MobyIdentifier GetBestInstance()
            {
                // First try to find one with animations
                var withAnimations = Instances.FirstOrDefault(i => i.HasAnimations);
                if (withAnimations != null)
                    return withAnimations;

                // Otherwise return any instance
                return Instances.FirstOrDefault();
            }
        }

        /// <summary>
        /// Import common mobys from multiple RC2 reference levels into a target level
        /// </summary>
        /// <param name="targetLevel">The target level to import mobys into</param>
        /// <param name="referenceEnginePaths">Paths to reference RC2 engine.ps3 files</param>
        /// <param name="allowOverwrite">Whether to overwrite existing mobys</param>
        /// <returns>True if the operation was successful</returns>
        public static bool ImportCommonMobysFromReferenceLevels(Level targetLevel, List<string> referenceEnginePaths, bool allowOverwrite = false)
        {
            if (targetLevel == null || referenceEnginePaths == null || referenceEnginePaths.Count < 2)
            {
                Console.WriteLine("❌ Cannot import common mobys: Invalid parameters");
                return false;
            }

            Console.WriteLine("\n==== Importing Common Mobys from Reference Levels ====");

            // 1. Load all reference levels
            List<Level> referenceLevels = new List<Level>();

            foreach (string enginePath in referenceEnginePaths)
            {
                try
                {
                    Console.WriteLine($"Loading reference level: {Path.GetFileName(enginePath)}...");
                    Level level = new Level(enginePath);
                    referenceLevels.Add(level);
                    Console.WriteLine($"✅ Successfully loaded level with {level.mobyModels?.Count ?? 0} moby models");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error loading reference level {enginePath}: {ex.Message}");
                }
            }

            if (referenceLevels.Count < 2)
            {
                Console.WriteLine("❌ Failed to load enough reference levels (minimum 2 required)");
                return false;
            }

            // 2. Analyze mobys across all levels to find common ones
            Dictionary<int, CommonMoby> allMobys = new Dictionary<int, CommonMoby>();
            Dictionary<int, CommonMoby> commonMobys = new Dictionary<int, CommonMoby>();

            Console.WriteLine("\nAnalyzing mobys across reference levels...");

            foreach (Level level in referenceLevels)
            {
                if (level.mobyModels == null) continue;

                foreach (Model model in level.mobyModels)
                {
                    if (model == null || !(model is MobyModel mobyModel)) continue;

                    int modelId = model.id;
                    var identifier = new MobyIdentifier(modelId, mobyModel, level);

                    if (!allMobys.ContainsKey(modelId))
                    {
                        allMobys[modelId] = new CommonMoby(modelId, identifier.ModelName);
                    }

                    allMobys[modelId].AddInstance(identifier);
                }
            }

            // Find mobys that appear in multiple levels (common mobys)
            foreach (var entry in allMobys)
            {
                if (entry.Value.ReferenceCount >= 2)
                {
                    commonMobys[entry.Key] = entry.Value;
                }
            }

            int commonCount = commonMobys.Count;
            int uncommonCount = allMobys.Count - commonCount;

            Console.WriteLine($"Found {commonCount} common mobys (present in at least 2 reference levels)");
            Console.WriteLine($"Found {uncommonCount} uncommon mobys (present in only 1 reference level)");

            // 3. Present options to the user
            Console.WriteLine("\nOptions:");
            Console.WriteLine("1. Import common mobys only (present in multiple reference levels)");
            Console.WriteLine("2. Import uncommon mobys only (present in only one reference level)");
            Console.WriteLine("3. Import all mobys from reference levels");
            Console.WriteLine("4. Select specific mobys to import manually");
            Console.Write("\nEnter your choice (1-4): ");

            string choice = Console.ReadLine()?.Trim() ?? "1";
            List<CommonMoby> mobysToImport = new List<CommonMoby>();

            switch (choice)
            {
                case "1": // Common mobys only
                    mobysToImport = commonMobys.Values.ToList();
                    Console.WriteLine($"Selected {mobysToImport.Count} common mobys for import");
                    break;

                case "2": // Uncommon mobys only 
                    mobysToImport = allMobys.Values.Where(m => m.ReferenceCount == 1).ToList();
                    Console.WriteLine($"Selected {mobysToImport.Count} uncommon mobys for import");
                    break;

                case "3": // All mobys
                    mobysToImport = allMobys.Values.ToList();
                    Console.WriteLine($"Selected all {mobysToImport.Count} mobys for import");
                    break;

                case "4": // Manual selection
                    mobysToImport = SelectMobysManually(allMobys.Values.ToList());
                    Console.WriteLine($"Selected {mobysToImport.Count} mobys for import");
                    break;

                default:
                    Console.WriteLine("Invalid choice. Defaulting to common mobys only.");
                    mobysToImport = commonMobys.Values.ToList();
                    break;
            }

            // 4. Check for conflicts with existing mobys in the target level
            HashSet<int> targetModelIds = new HashSet<int>();
            HashSet<int> specialMobyIds = GetSpecialMobyIds(); // Get IDs of mobys handled by MobySwapper

            if (targetLevel.mobyModels != null)
            {
                foreach (var model in targetLevel.mobyModels)
                {
                    if (model != null)
                    {
                        targetModelIds.Add(model.id);
                    }
                }
            }

            // Filter out special mobys that are managed by MobySwapper to avoid conflicts
            mobysToImport = mobysToImport
                .Where(m => !specialMobyIds.Contains(m.ModelId))
                .ToList();

            Console.WriteLine($"Filtered out {specialMobyIds.Count} special mobys that are managed by the Special Moby Swapper");

            List<CommonMoby> conflictingMobys = mobysToImport
                .Where(m => targetModelIds.Contains(m.ModelId))
                .ToList();

            if (conflictingMobys.Count > 0)
            {
                Console.WriteLine($"\nFound {conflictingMobys.Count} mobys that already exist in the target level:");
                foreach (var moby in conflictingMobys.Take(10))
                {
                    Console.WriteLine($"- {moby.ModelName} (ID: {moby.ModelId})");
                }

                if (conflictingMobys.Count > 10)
                {
                    Console.WriteLine($"- ... and {conflictingMobys.Count - 10} more");
                }

                if (!allowOverwrite)
                {
                    Console.Write("\nOverwrite existing mobys? (y/n): ");
                    allowOverwrite = Console.ReadLine()?.Trim().ToLower() == "y";
                }

                if (!allowOverwrite)
                {
                    Console.WriteLine("Skipping mobys that already exist in the target level");
                    mobysToImport = mobysToImport
                        .Where(m => !targetModelIds.Contains(m.ModelId))
                        .ToList();
                }
            }

            // Create a global texture mapping to track all texture transformations
            Dictionary<string, int> globalTextureMap = new Dictionary<string, int>();

            // 5. Perform the import
            int importedCount = 0;
            int skippedCount = 0;
            int texturesImported = 0;

            // Track which models were successfully imported
            List<int> successfullyImportedModelIds = new List<int>();

            Console.WriteLine("\nImporting selected mobys to target level...");

            foreach (var moby in mobysToImport)
            {
                try
                {
                    // Get the best instance (preferably one with animations)
                    MobyIdentifier bestInstance = moby.BestInstance;
                    if (bestInstance == null || bestInstance.SourceModel == null)
                    {
                        Console.WriteLine($"⚠️ No valid instance found for {moby.ModelName} (ID: {moby.ModelId})");
                        skippedCount++;
                        continue;
                    }

                    // Check if target already has this model and we're not overwriting
                    if (targetModelIds.Contains(moby.ModelId) && !allowOverwrite)
                    {
                        Console.WriteLine($"⏭️ Skipping {moby.ModelName} (ID: {moby.ModelId}) - already exists in target");
                        skippedCount++;
                        continue;
                    }

                    // Import the model
                    MobyModel sourceModel = bestInstance.SourceModel;
                    Level sourceLevel = bestInstance.SourceLevel;

                    // Deep clone the model
                    MobyModel clonedModel = (MobyModel) MobySwapper.DeepCloneModel(sourceModel);
                    clonedModel.id = sourceModel.id;  // Preserve original ID

                    // Create a texture mapping dictionary for this specific model
                    Dictionary<int, int> textureMapping = new Dictionary<int, int>();

                    // Handle texture dependencies
                    if (clonedModel.textureConfig != null && clonedModel.textureConfig.Count > 0)
                    {
                        Console.WriteLine($"  Processing {clonedModel.textureConfig.Count} texture configs for {moby.ModelName} (ID: {moby.ModelId})");

                        foreach (var texConfig in clonedModel.textureConfig)
                        {
                            int originalTexId = texConfig.id;

                            // Skip if texture index is out of bounds
                            if (originalTexId >= sourceLevel.textures.Count)
                            {
                                Console.WriteLine($"  ⚠️ Texture ID {originalTexId} is out of bounds for source level");
                                continue;
                            }

                            // Get the texture from source level
                            var sourceTexture = sourceLevel.textures[originalTexId];
                            string textureKey = GetTextureSignature(sourceTexture);

                            // Check if we've already imported this texture (global tracking)
                            int targetTexId = -1;
                            if (globalTextureMap.TryGetValue(textureKey, out targetTexId))
                            {
                                // We've already handled this texture
                                textureMapping[originalTexId] = targetTexId;
                            }
                            else
                            {
                                bool foundMatch = false;

                                // First, check if the exact same texture already exists in the target level
                                for (int i = 0; i < targetLevel.textures.Count; i++)
                                {
                                    if (CompareTextures(sourceTexture, targetLevel.textures[i]))
                                    {
                                        targetTexId = i;
                                        foundMatch = true;
                                        Console.WriteLine($"    Found matching texture at index {targetTexId}");
                                        break;
                                    }
                                }

                                // If not found, add it
                                if (!foundMatch)
                                {
                                    // Create a deep copy of the texture
                                    Texture newTexture = DeepCloneTexture(sourceTexture);
                                    targetLevel.textures.Add(newTexture);
                                    targetTexId = targetLevel.textures.Count - 1;
                                    texturesImported++;
                                    Console.WriteLine($"    Added new texture at index {targetTexId}");
                                }

                                // Add to our mapping dictionaries
                                globalTextureMap[textureKey] = targetTexId;
                                textureMapping[originalTexId] = targetTexId;
                            }

                            // Update the texture ID in the config
                            texConfig.id = targetTexId;
                        }

                        Console.WriteLine($"  Mapped {textureMapping.Count} textures for {moby.ModelName}");
                    }

                    // Also handle textures in otherTextureConfigs if present
                    if (clonedModel.otherTextureConfigs != null && clonedModel.otherTextureConfigs.Count > 0)
                    {
                        Console.WriteLine($"  Processing {clonedModel.otherTextureConfigs.Count} secondary texture configs for {moby.ModelName}");

                        foreach (var texConfig in clonedModel.otherTextureConfigs)
                        {
                            int originalTexId = texConfig.id;

                            // Skip if texture index is out of bounds
                            if (originalTexId >= sourceLevel.textures.Count)
                            {
                                Console.WriteLine($"  ⚠️ Texture ID {originalTexId} is out of bounds for source level");
                                continue;
                            }

                            // If we've already mapped this texture ID, reuse the mapping
                            if (textureMapping.TryGetValue(originalTexId, out int mappedId))
                            {
                                texConfig.id = mappedId;
                                Console.WriteLine($"    Reusing mapped texture ID {originalTexId} → {mappedId}");
                            }
                            else
                            {
                                // Otherwise process it the same way as primary textures
                                var sourceTexture = sourceLevel.textures[originalTexId];
                                string textureKey = GetTextureSignature(sourceTexture);

                                int targetTexId = -1;
                                if (globalTextureMap.TryGetValue(textureKey, out targetTexId))
                                {
                                    textureMapping[originalTexId] = targetTexId;
                                }
                                else
                                {
                                    bool foundMatch = false;

                                    // Check if this texture already exists in the target level
                                    for (int i = 0; i < targetLevel.textures.Count; i++)
                                    {
                                        if (CompareTextures(sourceTexture, targetLevel.textures[i]))
                                        {
                                            targetTexId = i;
                                            foundMatch = true;
                                            break;
                                        }
                                    }

                                    // If not found, add it
                                    if (!foundMatch)
                                    {
                                        Texture newTexture = DeepCloneTexture(sourceTexture);
                                        targetLevel.textures.Add(newTexture);
                                        targetTexId = targetLevel.textures.Count - 1;
                                        texturesImported++;
                                    }

                                    // Add to our mapping dictionaries
                                    globalTextureMap[textureKey] = targetTexId;
                                    textureMapping[originalTexId] = targetTexId;
                                }

                                texConfig.id = targetTexId;
                            }
                        }
                    }

                    // Remove any existing model with the same ID if overwriting
                    if (allowOverwrite)
                    {
                        targetLevel.mobyModels.RemoveAll(m => m != null && m.id == moby.ModelId);
                    }

                    // Add the new model to the target level
                    targetLevel.mobyModels.Add(clonedModel);
                    targetModelIds.Add(clonedModel.id);
                    successfullyImportedModelIds.Add(clonedModel.id);

                    Console.WriteLine($"✅ Imported {moby.ModelName} (ID: {moby.ModelId}) from {bestInstance.LevelName}");
                    if (moby.HasAnimations)
                    {
                        Console.WriteLine($"  - With {bestInstance.AnimationCount} animations");
                    }

                    importedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error importing {moby.ModelName} (ID: {moby.ModelId}): {ex.Message}");
                    skippedCount++;
                }
            }

            // Handle special dependencies that might be needed for certain mobys
            ImportSpecialDependencies(targetLevel, referenceLevels);

            // 6. Print summary
            Console.WriteLine($"\n==== Import Summary ====");
            Console.WriteLine($"✅ Successfully imported: {importedCount} moby models");
            Console.WriteLine($"🖼️ Textures imported: {texturesImported}");
            Console.WriteLine($"⏭️ Skipped: {skippedCount} moby models");

            if (importedCount > 0)
            {
                // After successful import, validate and fix PVAR indices
                MobySwapper.ValidateAndFixPvarIndices(targetLevel);

                // Ask user if they want to create instances of the imported mobys
                if (successfullyImportedModelIds.Count > 0)
                {
                    Console.WriteLine("\nWould you like to create instances of the newly imported mobys using RC1 Oltanis positions? (y/n)");
                    Console.Write("> ");
                    if (Console.ReadLine()?.Trim().ToLower() == "y")
                    {
                        return CreateInstancesForImportedMobys(targetLevel, successfullyImportedModelIds);
                    }
                }
            }

            return importedCount > 0;
        }

        /// <summary>
        /// Create instances for the newly imported Moby models using RC1 Oltanis positions
        /// </summary>
        /// <param name="targetLevel">The target level with imported Moby models</param>
        /// <param name="importedModelIds">List of successfully imported model IDs</param>
        /// <returns>True if instance creation was initiated successfully</returns>
        private static bool CreateInstancesForImportedMobys(Level targetLevel, List<int> importedModelIds)
        {
            if (targetLevel == null || importedModelIds == null || importedModelIds.Count == 0)
            {
                return false;
            }

            // Ask for RC1 Oltanis level path
            Console.WriteLine("\nEnter path to the RC1 Oltanis level engine.ps3 file for position references:");
            Console.Write("> ");
            string rc1OltanisPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(rc1OltanisPath) || !File.Exists(rc1OltanisPath))
            {
                Console.WriteLine("❌ Invalid RC1 Oltanis level path");
                return false;
            }

            // Load RC1 Oltanis level
            Level rc1OltanisLevel;
            try
            {
                Console.WriteLine("Loading RC1 Oltanis level...");
                rc1OltanisLevel = new Level(rc1OltanisPath);
                Console.WriteLine("✅ RC1 Oltanis level loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading RC1 Oltanis level: {ex.Message}");
                return false;
            }

            // Show list of imported models and ask user to select which ones to create instances for
            Console.WriteLine("\nAvailable Imported Models:");
            Console.WriteLine("-------------------------");

            for (int i = 0; i < importedModelIds.Count; i++)
            {
                int modelId = importedModelIds[i];
                var model = targetLevel.mobyModels.FirstOrDefault(m => m.id == modelId);
                string modelName = GetFriendlyModelName(modelId);
                int existingInstances = targetLevel.mobs?.Count(m => m.modelID == modelId) ?? 0;

                Console.WriteLine($"{i + 1}. {modelName} (ID: {modelId}) - {existingInstances} existing instances");
            }

            // Let user select models for instancing
            Console.WriteLine("\nSelect the models to create instances for (comma-separated numbers or 'all'):");
            Console.Write("> ");
            string selection = Console.ReadLine()?.Trim().ToLower() ?? "";

            List<int> selectedModelIds = new List<int>();

            if (selection == "all")
            {
                selectedModelIds = importedModelIds;
            }
            else
            {
                foreach (string part in selection.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), out int index) && index >= 1 && index <= importedModelIds.Count)
                    {
                        selectedModelIds.Add(importedModelIds[index - 1]);
                    }
                }
            }

            if (selectedModelIds.Count == 0)
            {
                Console.WriteLine("❌ No models selected for instancing");
                return false;
            }

            Console.WriteLine($"Selected {selectedModelIds.Count} models for instancing");

            // Set up instancer options
            Console.WriteLine("\nInstance creation options:");
            Console.WriteLine("1. Default (light=0, use RC2 template) [default]");
            Console.WriteLine("2. Custom options");
            Console.Write("> ");
            string optionsChoice = Console.ReadLine()?.Trim() ?? "1";

            MobyOltanisInstancer.InstancerOptions options;
            if (optionsChoice == "2")
            {
                options = MobyOltanisInstancer.InstancerOptions.None;

                Console.Write("Set light value to 0? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                    options |= MobyOltanisInstancer.InstancerOptions.SetLightToZero;

                Console.Write("Use existing moby as template for properties? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                    options |= MobyOltanisInstancer.InstancerOptions.UseRC2Template;

                Console.Write("Copy pVars from source level mobys? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                    options |= MobyOltanisInstancer.InstancerOptions.CopyPvars;
            }
            else
            {
                options = MobyOltanisInstancer.InstancerOptions.Default;
            }

            // Create instances
            bool success = MobyOltanisInstancer.CreateMobyInstancesFromLevel(
                targetLevel,
                rc1OltanisLevel,
                selectedModelIds.ToArray(),
                options);

            return success;
        }

        /// <summary>
        /// Present a list of mobys and allow the user to select which ones to import
        /// </summary>
        private static List<CommonMoby> SelectMobysManually(List<CommonMoby> allMobys)
        {
            List<CommonMoby> selectedMobys = new List<CommonMoby>();

            // Sort by common first, then by model ID
            var sortedMobys = allMobys
                .OrderByDescending(m => m.ReferenceCount)
                .ThenBy(m => m.ModelId)
                .ToList();

            Console.WriteLine("\nAvailable Mobys:");
            Console.WriteLine("----------------");
            Console.WriteLine("[ID]\t[Name]\t\t[Count]\t[Animations]\t[Source Level(s)]");

            for (int i = 0; i < sortedMobys.Count; i++)
            {
                var moby = sortedMobys[i];
                string sourceLevels = string.Join(", ", moby.Instances.Select(inst => inst.LevelName));
                string animStatus = moby.HasAnimations ? "Yes" : "No";

                // Pad name to at least 16 characters
                string paddedName = moby.ModelName.PadRight(16);

                Console.WriteLine($"{i + 1,3}. {moby.ModelId,5} {paddedName} {moby.ReferenceCount,4}x\t{animStatus,-5}\t[{sourceLevels}]");
            }

            Console.WriteLine("\nEnter the numbers of the mobys to import, separated by commas");
            Console.WriteLine("Or enter 'c' for common only, 'u' for uncommon only, 'a' for all");
            Console.Write("> ");

            string input = Console.ReadLine()?.Trim().ToLower() ?? "";

            if (input == "c")
            {
                // Common mobys (appear in multiple levels)
                return sortedMobys.Where(m => m.ReferenceCount >= 2).ToList();
            }
            else if (input == "u")
            {
                // Uncommon mobys (appear in only one level)
                return sortedMobys.Where(m => m.ReferenceCount == 1).ToList();
            }
            else if (input == "a")
            {
                // All mobys
                return sortedMobys;
            }
            else
            {
                // Parse individual selections
                string[] selections = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (string selection in selections)
                {
                    if (int.TryParse(selection.Trim(), out int index) && index >= 1 && index <= sortedMobys.Count)
                    {
                        selectedMobys.Add(sortedMobys[index - 1]);
                    }
                }
            }

            return selectedMobys;
        }

        /// <summary>
        /// Import any special dependencies that may be needed for certain types of mobys
        /// </summary>
        private static void ImportSpecialDependencies(Level targetLevel, List<Level> referenceLevels)
        {
            // Check if any vendor-related mobys were imported
            bool hasVendor = false;
            bool hasVendorLogo = false;

            if (targetLevel.mobyModels != null)
            {
                hasVendor = targetLevel.mobyModels.Any(m => m?.id == 11);
                hasVendorLogo = targetLevel.mobyModels.Any(m => m?.id == 1143);
            }

            // If we have a vendor, make sure we have the vendor support models
            if (hasVendor)
            {
                // Find the best reference level with vendor support models
                Level bestReferenceLevel = referenceLevels.FirstOrDefault(l =>
                    l.mobyModels != null &&
                    l.mobyModels.Any(m => m?.id == 1007) &&
                    l.mobyModels.Any(m => m?.id == 1137) &&
                    l.mobyModels.Any(m => m?.id == 1204) &&
                    l.mobyModels.Any(m => m?.id == 1502)
                );

                if (bestReferenceLevel != null)
                {
                    Console.WriteLine("\nImporting vendor support models...");
                    MobySwapper.ImportVendorSupportModels(targetLevel, bestReferenceLevel);
                }
            }

            // If we have a vendor logo, ensure its textures are properly set up
            if (hasVendorLogo)
            {
                // Find reference level with vendor logo
                Level referenceWithLogo = referenceLevels.FirstOrDefault(l =>
                    l.mobyModels != null && l.mobyModels.Any(m => m?.id == 1143));

                if (referenceWithLogo != null)
                {
                    Console.WriteLine("\nEnsuring vendor logo textures are properly set up...");
                    MobySwapper.EnsureVendorLogoTextures(targetLevel, referenceWithLogo);
                }
            }
        }

        // Add this helper method to get a friendly name for a model ID
        private static string GetFriendlyModelName(int modelId)
        {
            // Try to find this model ID in MobySwapper.MobyTypes
            foreach (var entry in MobySwapper.MobyTypes)
            {
                if (entry.Value.Contains(modelId))
                {
                    return entry.Key;
                }
            }

            // If not found, return a generic name
            return $"Model {modelId}";
        }

        // Add this helper method to use the MobySwapper's TextureEquals implementation
        private static bool CompareTextures(Texture sourceTexture, Texture targetTexture)
        {
            if (sourceTexture == null || targetTexture == null)
                return false;

            // Compare key identifying properties
            return sourceTexture.width == targetTexture.width &&
                   sourceTexture.height == targetTexture.height &&
                   sourceTexture.vramPointer == targetTexture.vramPointer &&
                   sourceTexture.data?.Length == targetTexture.data?.Length;
        }

        // Helper method to generate a unique signature for a texture
        private static string GetTextureSignature(Texture texture)
        {
            if (texture == null) return "null";

            // Create a signature based on texture properties including width, height, vramPointer
            // and a hash of the first few bytes of data (if available) for better uniqueness
            string dataHash = "";
            if (texture.data != null && texture.data.Length > 0)
            {
                // Use up to first 16 bytes (if available) to create a simple hash
                int bytesToHash = Math.Min(16, texture.data.Length);
                int hashValue = 0;
                for (int i = 0; i < bytesToHash; i++)
                {
                    hashValue = (hashValue * 31) + texture.data[i];
                }
                dataHash = hashValue.ToString("X8");
            }

            return $"{texture.width}x{texture.height}-{texture.vramPointer}-{dataHash}";
        }

        // Helper method to deep clone a texture
        private static Texture DeepCloneTexture(Texture sourceTexture)
        {
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

        // Get IDs of special mobys that should be handled by MobySwapper
        private static HashSet<int> GetSpecialMobyIds()
        {
            HashSet<int> specialIds = new HashSet<int>();

            // Add known special moby IDs that should be managed by MobySwapper
            // Vendor
            specialIds.Add(11);
            // Vendor logo
            specialIds.Add(1143);
            // Vendor support models
            specialIds.Add(1007);
            specialIds.Add(1137);
            specialIds.Add(1204);
            specialIds.Add(1502);

            // Add any other special mobys from MobySwapper.MobyTypes that are specially handled
            foreach (var entry in MobySwapper.MobyTypes)
            {
                // Check if this is a special moby type that requires special handling
                if (entry.Key.Contains("Vendor") ||
                    entry.Key.Contains("Gadgetron") ||
                    entry.Key.Contains("Ratchet") ||
                    entry.Key.Contains("Clank") ||
                    entry.Key.Contains("Ship") ||
                    entry.Key.Contains("Skid"))
                {
                    foreach (var id in entry.Value)
                    {
                        specialIds.Add(id);
                    }
                }
            }

            return specialIds;
        }

        /// <summary>
        /// Interactive UI for importing common mobys from RC2 reference levels
        /// </summary>
        public static bool ImportCommonMobysInteractive()
        {
            Console.WriteLine("\n==== Import Common Mobys from RC2 Reference Levels ====");

            // 1. Get target level path
            Console.WriteLine("\nEnter path to the target level engine.ps3 file:");
            Console.Write("> ");
            string targetPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                Console.WriteLine("❌ Invalid target level path");
                return false;
            }

            // 2. Get reference level paths
            List<string> referenceEnginePaths = new List<string>();

            Console.WriteLine("\nHow many reference RC2 levels do you want to analyze? (2-5)");
            Console.Write("> ");

            if (!int.TryParse(Console.ReadLine()?.Trim() ?? "2", out int referenceCount))
            {
                referenceCount = 2;
            }

            referenceCount = Math.Max(2, Math.Min(5, referenceCount)); // Clamp between 2 and 5

            Console.WriteLine($"\nEnter paths to {referenceCount} reference RC2 engine.ps3 files:");

            for (int i = 0; i < referenceCount; i++)
            {
                Console.Write($"Reference #{i + 1}> ");
                string path = Console.ReadLine()?.Trim() ?? "";

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
                Console.WriteLine("❌ Need at least 2 valid reference paths to continue");
                return false;
            }

            // 3. Ask about overwriting existing mobys
            Console.Write("\nOverwrite existing mobys in the target level? (y/n): ");
            bool allowOverwrite = Console.ReadLine()?.Trim().ToLower() == "y";

            // 4. Load target level
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
                return false;
            }

            // 5. Perform the import
            bool success = ImportCommonMobysFromReferenceLevels(
                targetLevel,
                referenceEnginePaths,
                allowOverwrite
            );

            // 6. Save the target level if successful
            if (success)
            {
                Console.Write("\nSave changes to the target level? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    // Use the robust save method to preserve all level data
                    SaveLevelWithGrindPathValidation(targetLevel);
                }
            }

            return success;
        }

        /// <summary>
        /// Saves the level with proper validation for grind paths and other critical data.
        /// </summary>
        public static void SaveLevelWithGrindPathValidation(Level level)
        {
            if (level == null || string.IsNullOrEmpty(level.path))
            {
                Console.WriteLine("❌ Cannot save level: Invalid level data or path.");
                return;
            }

            try
            {
                Console.WriteLine("\n=== Preparing Level For Final Save (Moby Importer) ===");

                // 1. THIS IS THE FIX: Restore grind paths and splines from the original file
                //    to completely undo any in-memory corruption that happened during the import process.
                GrindPathSwapper.RestoreAndValidateGrindPaths(level);

                // 2. Ensure other critical collections are not null
                if (level.pVars == null) level.pVars = new List<byte[]>();

                // 3. Update transform matrices for all mobys (this is safe)
                if (level.mobs != null)
                {
                    foreach (var moby in level.mobs)
                    {
                        moby.UpdateTransformMatrix();
                    }
                }

                Console.WriteLine("\nProceeding to save the level...");
                Console.WriteLine($"   Saving to: {level.path}");
                level.Save(level.path);
                Console.WriteLine("✅ Target level saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FATAL ERROR during save process: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
