// Copyright (C) 2018-2025, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Serializers;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometrySwapper
{
    /// <summary>
    /// Creates instances/placements for specified Moby models using positions from a source level.
    /// </summary>
    public static class MobyOltanisInstancer
    {
        /// <summary>
        /// Options for creating Moby instances
        /// </summary>
        [Flags]
        public enum InstancerOptions
        {
            None = 0,
            SetLightToZero = 1,
            UseRC2Template = 2,
            CopyPvars = 4,

            Default = SetLightToZero | UseRC2Template
        }

        /// <summary>
        /// Creates instances of a specified Moby model using positions from a source level.
        /// </summary>
        /// <param name="targetLevel">RC2 level to modify</param>
        /// <param name="sourceLevel">The level to get positions from (can be RC1 or a converted RC2 level)</param>
        /// <param name="targetModelIds">IDs of models to create instances for</param>
        /// <param name="options">Options for creating instances</param>
        /// <returns>True if operation was successful</returns>
        public static bool CreateMobyInstancesFromLevel(
            Level targetLevel,
            Level sourceLevel,
            int[] targetModelIds,
            InstancerOptions options = InstancerOptions.Default)
        {
            if (targetLevel == null || sourceLevel == null)
            {
                Console.WriteLine("❌ Cannot create moby instances: Invalid level data");
                return false;
            }

            if (targetModelIds == null || targetModelIds.Length == 0)
            {
                Console.WriteLine("❌ No target model IDs provided");
                return false;
            }

            Console.WriteLine("\n==== Creating Moby Instances Using Source Level Positions ====");
            Console.WriteLine($"Target model IDs: {string.Join(", ", targetModelIds)}");

            // Track results
            bool anySuccessful = false;
            int totalCreated = 0;

            // Process each model ID
            foreach (int targetModelId in targetModelIds)
            {
                // Find the model in the target level
                var mobyModel = targetLevel.mobyModels?.FirstOrDefault(m => m.id == targetModelId);
                if (mobyModel == null)
                {
                    Console.WriteLine($"❌ Model ID {targetModelId} not found in target level");
                    continue;
                }

                // Find positions in the source level to use for placement, but only for the current model ID
                var positionSources = FindPositionSources(sourceLevel, targetModelId);
                if (positionSources.Count == 0)
                {
                    Console.WriteLine($"❌ No suitable position sources found in the source level for model ID {targetModelId}");
                    continue;
                }

                Console.WriteLine($"\nCreating instances of model ID {targetModelId}");
                Console.WriteLine($"Found {positionSources.Count} potential position sources in the source level");

                // Find template moby (if enabled)
                Moby? templateMoby = null;
                if (options.HasFlag(InstancerOptions.UseRC2Template))
                {
                    // Try to find a template moby of the same model ID in the target level
                    templateMoby = targetLevel.mobs?.FirstOrDefault(m => m.modelID == targetModelId);

                    if (templateMoby == null)
                    {
                        // If not found, use any moby as a fallback template
                        templateMoby = targetLevel.mobs?.FirstOrDefault();
                    }

                    if (templateMoby != null)
                    {
                        Console.WriteLine($"Using template moby with oClass {templateMoby.mobyID} for properties");
                    }
                }

                // Find the highest moby ID to ensure we create unique IDs
                int nextMobyId = 1000; // Start with a safe base value
                if (targetLevel.mobs != null && targetLevel.mobs.Count > 0)
                {
                    nextMobyId = targetLevel.mobs.Max(m => m.mobyID) + 1;
                }
                Console.WriteLine($"Next moby ID will start at: {nextMobyId}");

                // Ensure mobs list is initialized
                if (targetLevel.mobs == null)
                {
                    targetLevel.mobs = new List<Moby>();
                }

                // Create mobys using the source positions
                int createdCount = 0;
                foreach (var positionSource in positionSources)
                {
                    try
                    {
                        // Create a new moby with a unique ID
                        var newMoby = new Moby(GameType.RaC2, mobyModel, positionSource.position, positionSource.rotation, positionSource.scale)
                        {
                            mobyID = nextMobyId++,

                            // Copy from template or use reasonable defaults
                            updateDistance = templateMoby?.updateDistance ?? 200,
                            drawDistance = templateMoby?.drawDistance ?? 200,
                            spawnBeforeDeath = true,
                            spawnType = templateMoby?.spawnType ?? new Bitmask(),
                            light = options.HasFlag(InstancerOptions.SetLightToZero) ? 0 : (templateMoby?.light ?? 0),

                            // Additional properties that might be important
                            missionID = templateMoby?.missionID ?? 0,
                            dataval = templateMoby?.dataval ?? 0,

                            // Set required unknown values
                            unk7A = 8192,
                            unk7B = 0,
                            unk8A = 16384,
                            unk8B = 0,
                            unk12A = 256,
                            unk12B = 0
                        };
                        newMoby.modelID = mobyModel.id; //If this single line addition fixes my problem, I swear to fucking god lmfao. EDIT: IT DID, FOR FUCK'S SAKE LMAOOOO

                        // Update transform matrix
                        newMoby.UpdateTransformMatrix();

                        // Handle pVars
                        if (options.HasFlag(InstancerOptions.UseRC2Template) && templateMoby?.pVars != null && templateMoby.pVars.Length > 0)
                        {
                            // Copy pVars from template
                            newMoby.pVars = (byte[]) templateMoby.pVars.Clone();
                        }
                        else if (options.HasFlag(InstancerOptions.CopyPvars) && positionSource.pVars != null && positionSource.pVars.Length > 0)
                        {
                            // Copy pVars from the source moby
                            newMoby.pVars = (byte[]) positionSource.pVars.Clone();
                        }
                        else
                        {
                            // Create default empty pVars
                            newMoby.pVars = Array.Empty<byte>();
                        }

                        // Add the new moby to the level
                        targetLevel.mobs.Add(newMoby);
                        createdCount++;

                        if (createdCount % 10 == 0 || createdCount <= 5)
                        {
                            Console.WriteLine($"  Created new moby with ID {newMoby.mobyID} at position {newMoby.position}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error creating new moby: {ex.Message}");
                    }
                }

                Console.WriteLine($"✅ Created {createdCount} instances of model ID {targetModelId}");
                totalCreated += createdCount;

                if (createdCount > 0)
                {
                    anySuccessful = true;

                    // Update moby IDs list in the level
                    if (targetLevel.mobyIds != null)
                    {
                        // Make sure mobyIds reflects all mobys in the level
                        targetLevel.mobyIds = targetLevel.mobs.Select(m => m.mobyID).ToList();
                    }
                }
            }

            if (anySuccessful)
            {
                Console.WriteLine($"\n✅ Successfully created {totalCreated} moby instances across all selected models");

                // Run the pvar index validation
                Console.WriteLine("\n==== Final pVar Index Validation ====");
                MobySwapper.ValidateAndFixPvarIndices(targetLevel);

                return true;
            }
            else
            {
                Console.WriteLine("\n❌ Failed to create any moby instances");
                return false;
            }
        }

        /// <summary>
        /// Prepares the level for saving by fixing inconsistencies.
        /// </summary>
        private static void PrepareLevelForSave(Level level)
        {
            Console.WriteLine("\n=== Preparing Level For Save (Moby Instancer) ===");

            // 1. Make sure mobyIds are properly synced
            if (level.mobs != null)
            {
                level.mobyIds = level.mobs.Select(m => m.mobyID).ToList();
                Console.WriteLine($"  ✅ Updated mobyIds list with {level.mobyIds.Count} entries");
            }

            // 2. Fix model references for each moby
            if (level.mobs != null && level.mobyModels != null)
            {
                int fixedRefs = 0;
                foreach (var moby in level.mobs)
                {
                    if (moby.model == null || moby.model.id != moby.modelID)
                    {
                        var correctModel = level.mobyModels.FirstOrDefault(m => m.id == moby.modelID);
                        if (correctModel != null)
                        {
                            moby.model = correctModel;
                            fixedRefs++;
                        }
                    }
                }
                if (fixedRefs > 0) Console.WriteLine($"  ✅ Fixed {fixedRefs} invalid model references");
            }

            // 3. Fix pVar indices and references
            Console.WriteLine("  Running pVar index validation before save...");
            MobySwapper.ValidateAndFixPvarIndices(level);

            // 4. Ensure critical collections are not null
            if (level.pVars == null) level.pVars = new List<byte[]>();
            if (level.splines == null) level.splines = new List<Spline>();
            if (level.grindPaths == null) level.grindPaths = new List<GrindPath>();

            // 5. Clear chunk data to prevent saving them
            level.terrainChunks = new List<Terrain>();
            level.collisionChunks = new List<Collision>();
            level.collBytesChunks = new List<byte[]>();
            if (level.levelVariables != null)
            {
                level.levelVariables.chunkCount = 0;
            }

            // 6. Update transform matrices
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
        /// Safely saves the level using the robust method from the geometry swapper.
        /// </summary>
        /// <param name="level">The level to save</param>
        /// <param name="outputPath">Path where the level should be saved</param>
        /// <returns>True if save was successful</returns>
        private static bool SaveLevelSafely(Level level, string outputPath)
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

                // Save the level using the standard Level.Save method, which handles serialization correctly
                Console.WriteLine($"Saving level to {directory}...");
                level.Save(directory);

                // Patch the engine header with required values for RC2, mimicking the successful save process
                string outputEngineFile = Path.Combine(directory, "engine.ps3");
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
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error while patching engine.ps3: {ex.Message}");
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

        /// <summary>
        /// Finds suitable position sources in the source level.
        /// </summary>
        private static List<Moby> FindPositionSources(Level sourceLevel, int modelIdToFind)
        {
            if (sourceLevel.mobs == null)
            {
                return new List<Moby>();
            }

            // Use all mobys from the source level as potential positions,
            // excluding special mobys like the player or vendors that might not be suitable.
            return sourceLevel.mobs.Where(m => m.modelID == modelIdToFind).ToList();
        }

        /// <summary>
        /// Interactive wrapper for moby instancing function
        /// </summary>
        /// <returns>True if the operation was successful</returns>
        public static bool CreateMobyInstancesInteractive()
        {
            Console.WriteLine("\n==== Create Moby Instances from a Source Level ====");

            // Get target level path
            Console.WriteLine("\nEnter path to the target RC2 level's engine.ps3 file:");
            Console.Write("> ");
            string targetPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                Console.WriteLine("❌ Invalid target level path");
                return false;
            }

            // Get source level path (the converted level)
            Console.WriteLine("\nEnter path to the source level for placements (e.g., your converted level's engine.ps3):");
            Console.Write("> ");
            string sourceLevelPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(sourceLevelPath) || !File.Exists(sourceLevelPath))
            {
                Console.WriteLine("❌ Invalid source level path");
                return false;
            }

            // Load levels
            Level targetLevel, sourceLevel;
            try
            {
                Console.WriteLine("Loading target level...");
                targetLevel = new Level(targetPath);
                Console.WriteLine("Loading source level for positions...");
                sourceLevel = new Level(sourceLevelPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading levels: {ex.Message}");
                return false;
            }

            // Display available models in the target level
            Console.WriteLine("\nAvailable Moby Models in target level:");
            Console.WriteLine("----------------------------------");

            if (targetLevel.mobyModels == null || targetLevel.mobyModels.Count == 0)
            {
                Console.WriteLine("No moby models found in target level!");
                return false;
            }

            // Group models by category using MobySwapper.MobyTypes if available
            var modelsByCategory = new Dictionary<string, List<int>>();
            var uncategorizedModels = new List<int>();

            foreach (var model in targetLevel.mobyModels)
            {
                bool found = false;
                foreach (var category in MobySwapper.MobyTypes)
                {
                    if (category.Value.Contains(model.id))
                    {
                        if (!modelsByCategory.ContainsKey(category.Key))
                        {
                            modelsByCategory[category.Key] = new List<int>();
                        }
                        modelsByCategory[category.Key].Add(model.id);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    uncategorizedModels.Add(model.id);
                }
            }

            // Display all categorized models first
            foreach (var category in modelsByCategory.OrderBy(c => c.Key))
            {
                Console.WriteLine($"\n{category.Key}:");
                foreach (var modelId in category.Value.OrderBy(id => id))
                {
                    int instanceCount = targetLevel.mobs?.Count(m => m.modelID == modelId) ?? 0;
                    Console.WriteLine($"  ID {modelId}: {instanceCount} existing instances");
                }
            }

            // Display uncategorized models
            if (uncategorizedModels.Count > 0)
            {
                Console.WriteLine("\nUncategorized Models:");
                foreach (var modelId in uncategorizedModels.OrderBy(id => id))
                {
                    int instanceCount = targetLevel.mobs?.Count(m => m.modelID == modelId) ?? 0;
                    Console.WriteLine($"  ID {modelId}: {instanceCount} existing instances");
                }
            }

            // Get user selection
            Console.WriteLine("\nEnter the model IDs you want to create instances for (comma-separated, e.g. '122,345'):");
            Console.Write("> ");
            string input = Console.ReadLine()?.Trim() ?? "";

            // Parse model IDs
            List<int> selectedModelIds = new List<int>();
            foreach (string part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int modelId))
                {
                    // Verify the model exists
                    if (targetLevel.mobyModels.Any(m => m.id == modelId))
                    {
                        selectedModelIds.Add(modelId);
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Warning: Model ID {modelId} not found in target level, skipping");
                    }
                }
            }

            if (selectedModelIds.Count == 0)
            {
                Console.WriteLine("❌ No valid model IDs selected");
                return false;
            }

            // Ask for options
            Console.WriteLine("\nSelect options:");
            Console.WriteLine("1. Default (light=0, use RC2 template) [default]");
            Console.WriteLine("2. Custom options");
            Console.Write("> ");
            string choice = Console.ReadLine()?.Trim() ?? "1";

            InstancerOptions options;
            if (choice == "2")
            {
                options = InstancerOptions.None;

                if (GetYesNoInput("Set light value to 0? (Y/N): "))
                    options |= InstancerOptions.SetLightToZero;

                if (GetYesNoInput("Use existing moby as template for properties? (Y/N): "))
                    options |= InstancerOptions.UseRC2Template;

                if (GetYesNoInput("Copy pVars from source level mobys? (Y/N): "))
                    options |= InstancerOptions.CopyPvars;
            }
            else
            {
                options = InstancerOptions.Default;
            }

            // Create instances
            bool success = CreateMobyInstancesFromLevel(
                targetLevel,
                sourceLevel,
                selectedModelIds.ToArray(),
                options);

            if (success)
            {
                // Ask how they want to save the modified level
                Console.WriteLine("\nHow do you want to save the modified level?");
                Console.WriteLine("1. Save changes to the target level (overwrite)");
                Console.WriteLine("2. Save as a new level file");
                Console.WriteLine("3. Don't save changes");
                Console.Write("> ");

                string saveChoice = Console.ReadLine()?.Trim() ?? "3";

                if (saveChoice == "1") // Overwrite
                {
                    Console.WriteLine($"Saving changes to the target level: {targetPath}");
                    return SaveLevelSafely(targetLevel, targetPath);
                }
                else if (saveChoice == "2") // Save as new file
                {
                    Console.WriteLine("\nEnter path for the new level file (e.g. 'C:\\path\\to\\new_level\\engine.ps3'):");
                    Console.Write("> ");
                    string newPath = Console.ReadLine()?.Trim() ?? "";

                    if (string.IsNullOrEmpty(newPath))
                    {
                        Console.WriteLine("❌ Invalid path provided");
                        return false;
                    }

                    Console.WriteLine($"Saving level to: {newPath}");
                    return SaveLevelSafely(targetLevel, newPath);
                }
                else
                {
                    Console.WriteLine("Changes not saved.");
                    return true;
                }
            }

            return success;
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
