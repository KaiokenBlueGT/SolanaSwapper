using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using OpenTK.Mathematics;

namespace GeometrySwapper
{
    /// <summary>
    /// Diagnostic tool for analyzing terrain and level structure differences between RC1 and RC2
    /// </summary>
    public static class TerrainDiagnostics
    {
        /// <summary>
        /// Performs comprehensive analysis of a level's terrain and exports findings to files
        /// </summary>
        public static void AnalyzeLevel(Level level, string outputPath, string label)
        {
            Directory.CreateDirectory(outputPath);
            string baseFileName = Path.Combine(outputPath, $"{label}_analysis");

            // Export terrain fragment details
            using (var writer = new StreamWriter($"{baseFileName}_fragments.csv"))
            {
                writer.WriteLine("Index,FragmentID,ModelID,ChunkIndex,off1C,off1E,off20,off22,off24,off28,off2C,HasModel,VertexCount,TextureConfigCount,Position");
                
                if (level.terrainEngine?.fragments != null)
                {
                    for (int i = 0; i < level.terrainEngine.fragments.Count; i++)
                    {
                        var frag = level.terrainEngine.fragments[i];
                        var fragBytes = frag.ToByteArray();
                        
                        // Extract chunk index from byte array at offset 0x22
                        short chunkIndex = 0;
                        if (fragBytes.Length >= 0x24)
                        {
                            chunkIndex = BitConverter.ToInt16(fragBytes, 0x22);
                        }
                        
                        // Handle position which might be a Vector3 or individual floats
                        string positionStr;
                        try
                        {
                            // Try to access as Vector3
                            positionStr = $"{frag.position.X},{frag.position.Y},{frag.position.Z}";
                        }
                        catch (Exception)
                        {
                            // Fallback if position is not a Vector3
                            positionStr = $"{frag.position}";
                        }
                        
                        writer.WriteLine(
                            $"{i}," +
                            $"{frag.off1E}," +
                            $"{frag.modelID}," +
                            $"{chunkIndex}," +
                            $"0x{frag.off1C:X4}," +
                            $"0x{frag.off1E:X4}," +
                            $"0x{frag.off20:X4}," +
                            $"0x{chunkIndex:X4}," +
                            $"0x{frag.off24:X8}," +
                            $"0x{frag.off28:X8}," +
                            $"0x{frag.off2C:X8}," +
                            $"{(frag.model != null)}," +
                            $"{(frag.model?.vertexBuffer?.Length ?? 0) / 8}," +
                            $"{frag.model?.textureConfig?.Count ?? 0}," +
                            $"{positionStr}"
                        );
                    }
                }
            }

            // Export terrain model details
            using (var writer = new StreamWriter($"{baseFileName}_models.csv"))
            {
                writer.WriteLine("ModelID,Size,VertexCount,IndexCount,TextureConfigCount,TextureIDs");
                
                var uniqueModels = new HashSet<int>();
                if (level.terrainEngine?.fragments != null)
                {
                    foreach (var frag in level.terrainEngine.fragments)
                    {
                        if (frag.model != null && !uniqueModels.Contains(frag.model.id))
                        {
                            uniqueModels.Add(frag.model.id);
                            
                            // Gather texture IDs used by this model
                            string textureIds = "none";
                            if (frag.model.textureConfig != null && frag.model.textureConfig.Count > 0)
                            {
                                textureIds = string.Join(",", frag.model.textureConfig.Select(tc => tc.id));
                            }
                            
                            writer.WriteLine(
                                $"{frag.model.id}," +
                                $"{frag.model.size}," +
                                $"{frag.model.vertexBuffer.Length / 8}," +
                                $"{frag.model.indexBuffer.Length}," +
                                $"{frag.model.textureConfig?.Count ?? 0}," +
                                $"{textureIds}"
                            );
                        }
                    }
                }
            }

            // Export level variables
            using (var writer = new StreamWriter($"{baseFileName}_levelVars.txt"))
            {
                if (level.levelVariables != null)
                {
                    var lv = level.levelVariables;
                    writer.WriteLine($"Game: {level.game.num}");
                    writer.WriteLine($"ChunkCount: {lv.chunkCount}");
                    writer.WriteLine($"ByteSize: {lv.ByteSize}");
                    writer.WriteLine($"DeathPlaneZ: {lv.deathPlaneZ}");
                    writer.WriteLine($"FogStart: {lv.fogNearDistance}");
                    writer.WriteLine($"FogEnd: {lv.fogFarDistance}");
                    writer.WriteLine($"FogNearIntensity: {lv.fogNearIntensity}");
                    writer.WriteLine($"FogFarIntensity: {lv.fogFarIntensity}");
                    
                    // Handle shipPosition which might be a Vector3 or individual components
                    try
                    {
                        // Try to access as Vector3
                        writer.WriteLine($"ShipPosition: {lv.shipPosition.X}, {lv.shipPosition.Y}, {lv.shipPosition.Z}");
                    }
                    catch (Exception)
                    {
                        // Fallback if not a Vector3
                        writer.WriteLine($"ShipPosition: {lv.shipPosition}");
                    }
                    
                    writer.WriteLine($"ShipRotation: {lv.shipRotation}");
                    
                    writer.WriteLine($"ShipPathID: {lv.shipPathID}");
                    writer.WriteLine($"ShipCameraStartID: {lv.shipCameraStartID}");
                    writer.WriteLine($"ShipCameraEndID: {lv.shipCameraEndID}");
                    writer.WriteLine($"off58: 0x{lv.off58:X8}");
                    writer.WriteLine($"off68: 0x{lv.off68:X8}");
                    writer.WriteLine($"off6C: 0x{lv.off6C:X8}");
                    writer.WriteLine($"off78: 0x{lv.off78:X8}");
                    writer.WriteLine($"off7C: 0x{lv.off7C:X8}");
                }
            }

            // Export texture usage analysis
            using (var writer = new StreamWriter($"{baseFileName}_textures.csv"))
            {
                writer.WriteLine("TextureID,Width,Height,UsedByModels");
                
                if (level.textures != null)
                {
                    // Build a map of which textures are used by which models
                    var textureToModelMap = new Dictionary<int, List<int>>();
                    
                    if (level.terrainEngine?.fragments != null)
                    {
                        foreach (var frag in level.terrainEngine.fragments)
                        {
                            if (frag.model?.textureConfig != null)
                            {
                                foreach (var texConfig in frag.model.textureConfig)
                                {
                                    if (!textureToModelMap.ContainsKey(texConfig.id))
                                    {
                                        textureToModelMap[texConfig.id] = new List<int>();
                                    }
                                    
                                    if (!textureToModelMap[texConfig.id].Contains(frag.model.id))
                                    {
                                        textureToModelMap[texConfig.id].Add(frag.model.id);
                                    }
                                }
                            }
                        }
                    }
                    
                    for (int i = 0; i < level.textures.Count; i++)
                    {
                        var tex = level.textures[i];
                        var usedBy = textureToModelMap.ContainsKey(i) 
                            ? string.Join(",", textureToModelMap[i]) 
                            : "none";
                        
                        writer.WriteLine(
                            $"{i}," +
                            $"{tex.width}," +
                            $"{tex.height}," +
                            $"{usedBy}"
                        );
                    }
                }
            }

            // Export chunk data analysis (if any)
            using (var writer = new StreamWriter($"{baseFileName}_chunks.txt"))
            {
                writer.WriteLine($"LevelVariables.chunkCount: {level.levelVariables?.chunkCount ?? 0}");
                writer.WriteLine($"terrainChunks.Count: {level.terrainChunks?.Count ?? 0}");
                writer.WriteLine($"collisionChunks.Count: {level.collisionChunks?.Count ?? 0}");
                writer.WriteLine($"collBytesChunks.Count: {level.collBytesChunks?.Count ?? 0}");
                
                if (level.terrainChunks != null)
                {
                    for (int i = 0; i < level.terrainChunks.Count; i++)
                    {
                        writer.WriteLine($"\nChunk {i}:");
                        writer.WriteLine($"  levelNumber: {level.terrainChunks[i].levelNumber}");
                        writer.WriteLine($"  fragmentCount: {level.terrainChunks[i].fragments?.Count ?? 0}");
                    }
                }
            }

            Console.WriteLine($"✅ Level analysis exported to {outputPath}");
        }

        /// <summary>
        /// Compare two levels and highlight differences in their terrain structures
        /// </summary>
        public static void CompareLevels(Level level1, Level level2, string outputPath, string label1, string label2)
        {
            Directory.CreateDirectory(outputPath);
            string comparisonFile = Path.Combine(outputPath, $"{label1}_vs_{label2}_comparison.txt");
            
            using (var writer = new StreamWriter(comparisonFile))
            {
                writer.WriteLine($"Comparing {label1} vs {label2}");
                writer.WriteLine("=======================================================");
                
                // Game type comparison
                writer.WriteLine($"Game type: {level1.game.num} vs {level2.game.num}");
                
                // Level variables comparison
                writer.WriteLine("\nLEVEL VARIABLES COMPARISON:");
                if (level1.levelVariables != null && level2.levelVariables != null)
                {
                    CompareAndWrite(writer, "chunkCount", level1.levelVariables.chunkCount, level2.levelVariables.chunkCount);
                    CompareAndWrite(writer, "ByteSize", level1.levelVariables.ByteSize, level2.levelVariables.ByteSize);
                    CompareAndWrite(writer, "deathPlaneZ", level1.levelVariables.deathPlaneZ, level2.levelVariables.deathPlaneZ);
                    CompareAndWrite(writer, "fogNearDistance", level1.levelVariables.fogNearDistance, level2.levelVariables.fogNearDistance);
                    CompareAndWrite(writer, "fogFarDistance", level1.levelVariables.fogFarDistance, level2.levelVariables.fogFarDistance);
                    CompareAndWrite(writer, "off7C", $"0x{level1.levelVariables.off7C:X8}", $"0x{level2.levelVariables.off7C:X8}");
                }
                else
                {
                    writer.WriteLine("One or both levels missing levelVariables");
                }
                
                // Terrain engine comparison
                writer.WriteLine("\nTERRAIN ENGINE COMPARISON:");
                if (level1.terrainEngine != null && level2.terrainEngine != null)
                {
                    CompareAndWrite(writer, "levelNumber", level1.terrainEngine.levelNumber, level2.terrainEngine.levelNumber);
                    CompareAndWrite(writer, "fragmentCount", level1.terrainEngine.fragments?.Count ?? 0, level2.terrainEngine.fragments?.Count ?? 0);
                    
                    // Fragment ID sequence check
                    writer.WriteLine("\nFRAGMENT ID SEQUENCE:");
                    bool level1Sequential = CheckSequentialFragmentIds(level1, out string level1Breaks);
                    bool level2Sequential = CheckSequentialFragmentIds(level2, out string level2Breaks);
                    
                    writer.WriteLine($"{label1} sequential: {level1Sequential}");
                    if (!level1Sequential) writer.WriteLine($"{label1} breaks: {level1Breaks}");
                    
                    writer.WriteLine($"{label2} sequential: {level2Sequential}");
                    if (!level2Sequential) writer.WriteLine($"{label2} breaks: {level2Breaks}");
                }
                else
                {
                    writer.WriteLine("One or both levels missing terrainEngine");
                }
                
                // Chunk usage comparison
                writer.WriteLine("\nCHUNK USAGE COMPARISON:");
                CompareAndWrite(writer, "terrainChunks.Count", level1.terrainChunks?.Count ?? 0, level2.terrainChunks?.Count ?? 0);
                CompareAndWrite(writer, "collisionChunks.Count", level1.collisionChunks?.Count ?? 0, level2.collisionChunks?.Count ?? 0);
                
                // Texture stats comparison
                writer.WriteLine("\nTEXTURE STATS COMPARISON:");
                CompareAndWrite(writer, "textureCount", level1.textures?.Count ?? 0, level2.textures?.Count ?? 0);
                
                // Model stats
                writer.WriteLine("\nMODEL STATS COMPARISON:");
                int level1ModelCount = level1.terrainEngine?.fragments?.Select(f => f.model?.id ?? -1)
                    .Where(id => id >= 0).Distinct().Count() ?? 0;
                int level2ModelCount = level2.terrainEngine?.fragments?.Select(f => f.model?.id ?? -1)
                    .Where(id => id >= 0).Distinct().Count() ?? 0;
                
                CompareAndWrite(writer, "uniqueTerrainModelCount", level1ModelCount, level2ModelCount);
                
                // Analyze off22 (chunk index) values
                writer.WriteLine("\nCHUNK INDEX (off22) ANALYSIS:");
                AnalyzeChunkIndices(level1, label1, writer);
                AnalyzeChunkIndices(level2, label2, writer);
            }
            
            Console.WriteLine($"✅ Level comparison exported to {comparisonFile}");
        }
        
        /// <summary>
        /// Analyze all Going Commando levels in a directory
        /// </summary>
        /// <param name="rc2DataPath">Path to the RC2 ps3data directory</param>
        /// <param name="outputPath">Where to save the analysis files</param>
        /// <param name="maxLevels">Maximum number of levels to analyze (0 for all)</param>
        public static void AnalyzeAllRC2Levels(string rc2DataPath, string outputPath, int maxLevels = 0)
        {
            Console.WriteLine($"Analyzing RC2 levels in {rc2DataPath}...");
            
            Directory.CreateDirectory(outputPath);
            
            // Summary file for quick reference
            using (var summaryWriter = new StreamWriter(Path.Combine(outputPath, "rc2_levels_summary.csv")))
            {
                summaryWriter.WriteLine("LevelNumber,LevelName,GameType,FragmentCount,TerrainChunks,CollisionChunks,ChunkCountInVars,SequentialFragmentIDs,UniqueModels,TextureCount");
                
                // Look for level directories (level0 through level26)
                List<string> levelPaths = new List<string>();
                for (int i = 0; i <= 26; i++)
                {
                    string levelDir = Path.Combine(rc2DataPath, $"level{i}");
                    if (Directory.Exists(levelDir))
                    {
                        levelPaths.Add(levelDir);
                    }
                }
                
                // Apply limit if specified
                if (maxLevels > 0 && maxLevels < levelPaths.Count)
                {
                    levelPaths = levelPaths.Take(maxLevels).ToList();
                }
                
                Console.WriteLine($"Found {levelPaths.Count} level directories to analyze");
                
                // Now process each level
                foreach (string levelDir in levelPaths)
                {
                    string levelName = Path.GetFileName(levelDir);
                    string enginePath = Path.Combine(levelDir, "engine.ps3");
                    
                    if (!File.Exists(enginePath))
                    {
                        Console.WriteLine($"⚠️ No engine.ps3 found in {levelDir} - skipping");
                        continue;
                    }
                    
                    try
                    {
                        Console.WriteLine($"Loading {levelName}...");
                        Level level = new Level(enginePath);
                        
                        // Create level-specific output directory
                        string levelOutputDir = Path.Combine(outputPath, levelName);
                        Directory.CreateDirectory(levelOutputDir);
                        
                        // Analyze this level
                        AnalyzeLevel(level, levelOutputDir, levelName);
                        
                        // Add to summary
                        int uniqueModels = level.terrainEngine?.fragments?.Select(f => f.model?.id ?? -1)
                            .Where(id => id >= 0).Distinct().Count() ?? 0;
                        
                        bool sequentialIds = CheckSequentialFragmentIds(level, out _);
                        
                        summaryWriter.WriteLine(
                            $"{level.terrainEngine?.levelNumber ?? -1}," +
                            $"{levelName}," +
                            $"{level.game.num}," +
                            $"{level.terrainEngine?.fragments?.Count ?? 0}," +
                            $"{level.terrainChunks?.Count ?? 0}," +
                            $"{level.collisionChunks?.Count ?? 0}," +
                            $"{level.levelVariables?.chunkCount ?? 0}," +
                            $"{sequentialIds}," +
                            $"{uniqueModels}," +
                            $"{level.textures?.Count ?? 0}"
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error analyzing {levelName}: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine($"✅ Analysis of all RC2 levels complete. Results saved to {outputPath}");
        }
        
        /// <summary>
        /// Perform a comparative analysis across all RC2 levels to identify patterns and best practices
        /// </summary>
        public static void GenerateRC2PatternsReport(string rc2DataPath, string outputPath)
        {
            Console.WriteLine("Generating RC2 terrain patterns report...");
            
            Directory.CreateDirectory(outputPath);
            string reportPath = Path.Combine(outputPath, "rc2_terrain_patterns_report.txt");
            
            // Load all available RC2 levels first
            List<Tuple<string, Level>> levels = new List<Tuple<string, Level>>();
            
            for (int i = 0; i <= 26; i++)
            {
                string levelDir = Path.Combine(rc2DataPath, $"level{i}");
                string enginePath = Path.Combine(levelDir, "engine.ps3");
                
                if (Directory.Exists(levelDir) && File.Exists(enginePath))
                {
                    try
                    {
                        Level level = new Level(enginePath);
                        levels.Add(new Tuple<string, Level>($"level{i}", level));
                        Console.WriteLine($"Loaded {levelDir}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not load {levelDir}: {ex.Message}");
                    }
                }
            }
            
            using (var writer = new StreamWriter(reportPath))
            {
                writer.WriteLine("Ratchet & Clank: Going Commando (RC2) Terrain Structure Analysis");
                writer.WriteLine("=================================================================");
                writer.WriteLine($"Date: {DateTime.Now}");
                writer.WriteLine($"Total levels analyzed: {levels.Count}");
                writer.WriteLine();
                
                // 1. Fragment ID Sequencing Pattern
                writer.WriteLine("1. FRAGMENT ID SEQUENCING PATTERN");
                writer.WriteLine("------------------------------");
                int sequentialCount = 0;
                foreach (var (name, level) in levels)
                {
                    bool isSequential = CheckSequentialFragmentIds(level, out _);
                    if (isSequential) sequentialCount++;
                    writer.WriteLine($"{name}: {(isSequential ? "Sequential" : "Non-sequential")}");
                }
                writer.WriteLine($"\nSummary: {sequentialCount} out of {levels.Count} levels have sequential fragment IDs");
                writer.WriteLine($"Pattern: {(sequentialCount == levels.Count ? "ALL RC2 levels use sequential fragment IDs" : "Sequential fragment IDs are common but not universal")}");
                writer.WriteLine();
                
                // 2. Chunk Count Pattern
                writer.WriteLine("2. CHUNK USAGE PATTERN");
                writer.WriteLine("----------------------");
                var chunkCounts = levels.Select(l => l.Item2.terrainChunks?.Count ?? 0).GroupBy(c => c)
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key} chunks: {g.Count()} levels")
                    .ToList();
                
                foreach (var countGroup in chunkCounts)
                {
                    writer.WriteLine(countGroup);
                }
                
                writer.WriteLine("\nChunk count in LevelVariables vs actual terrain chunks:");
                foreach (var (name, level) in levels)
                {
                    int varsCount = level.levelVariables?.chunkCount ?? 0;
                    int actualCount = level.terrainChunks?.Count ?? 0;
                    writer.WriteLine($"{name}: LevelVars.chunkCount={varsCount}, actual chunks={actualCount}");
                }
                writer.WriteLine();
                
                // 3. Fragment to Chunk Index Distribution
                writer.WriteLine("3. FRAGMENT TO CHUNK INDEX MAPPING");
                writer.WriteLine("----------------------------------");
                writer.WriteLine("Analysis of how off22 (chunk index) values are distributed in fragments:");
                
                foreach (var (name, level) in levels.Where(l => l.Item2.terrainEngine?.fragments?.Count > 0))
                {
                    writer.WriteLine($"\n{name}:");
                    AnalyzeChunkIndices(level, name, writer);
                }
                
                // 4. Common Values for Important Fields
                writer.WriteLine("\n4. COMMON FRAGMENT FIELD VALUES");
                writer.WriteLine("------------------------------");
                writer.WriteLine("Analysis of common values for off1C, off20, off24, off28, off2C:");
                
                var off1CValues = new Dictionary<ushort, int>();
                var off20Values = new Dictionary<ushort, int>();
                var off24Values = new Dictionary<uint, int>();
                var off28Values = new Dictionary<uint, int>();
                var off2CValues = new Dictionary<uint, int>();
                
                foreach (var (_, level) in levels)
                {
                    if (level.terrainEngine?.fragments == null) continue;
                    
                    foreach (var frag in level.terrainEngine.fragments)
                    {
                        // off1C
                        if (!off1CValues.ContainsKey(frag.off1C))
                            off1CValues[frag.off1C] = 0;
                        off1CValues[frag.off1C]++;
                        
                        // off20
                        if (!off20Values.ContainsKey(frag.off20))
                            off20Values[frag.off20] = 0;
                        off20Values[frag.off20]++;
                        
                        // off24
                        if (!off24Values.ContainsKey(frag.off24))
                            off24Values[frag.off24] = 0;
                        off24Values[frag.off24]++;
                        
                        // off28
                        if (!off28Values.ContainsKey(frag.off28))
                            off28Values[frag.off28] = 0;
                        off28Values[frag.off28]++;
                        
                        // off2C
                        if (!off2CValues.ContainsKey(frag.off2C))
                            off2CValues[frag.off2C] = 0;
                        off2CValues[frag.off2C]++;
                    }
                }
                
                writer.WriteLine("\nMost common values for off1C:");
                foreach (var kvp in off1CValues.OrderByDescending(kv => kv.Value).Take(5))
                {
                    writer.WriteLine($"  0x{kvp.Key:X4}: {kvp.Value} occurrences ({(double)kvp.Value / levels.Sum(l => l.Item2.terrainEngine?.fragments?.Count ?? 0) * 100:F1}%)");
                }
                
                writer.WriteLine("\nMost common values for off20:");
                foreach (var kvp in off20Values.OrderByDescending(kv => kv.Value).Take(5))
                {
                    writer.WriteLine($"  0x{kvp.Key:X4}: {kvp.Value} occurrences ({(double)kvp.Value / levels.Sum(l => l.Item2.terrainEngine?.fragments?.Count ?? 0) * 100:F1}%)");
                }
                
                writer.WriteLine("\nMost common values for off24:");
                foreach (var kvp in off24Values.OrderByDescending(kv => kv.Value).Take(5))
                {
                    writer.WriteLine($"  0x{kvp.Key:X8}: {kvp.Value} occurrences ({(double)kvp.Value / levels.Sum(l => l.Item2.terrainEngine?.fragments?.Count ?? 0) * 100:F1}%)");
                }
                
                // 5. Recommendations for Oltanis port
                writer.WriteLine("\n5. RECOMMENDATIONS FOR RC1 TO RC2 TERRAIN PORTING");
                writer.WriteLine("----------------------------------------------");
                writer.WriteLine("Based on the analysis of RC2 levels, here are recommendations for porting RC1 terrain:");
                writer.WriteLine("1. Ensure fragment IDs (off1E) are sequential (0, 1, 2, 3...)");
                writer.WriteLine("2. Set all fragments' chunk index (off22) to 0 for single-chunk levels");
                writer.WriteLine("3. Use standard values for other offsets:");
                writer.WriteLine($"   - off1C: 0x{off1CValues.OrderByDescending(kv => kv.Value).First().Key:X4}");
                writer.WriteLine($"   - off20: 0x{off20Values.OrderByDescending(kv => kv.Value).First().Key:X4}");
                writer.WriteLine($"   - off24: 0x{off24Values.OrderByDescending(kv => kv.Value).First().Key:X8}");
                writer.WriteLine($"   - off28: 0x{off28Values.OrderByDescending(kv => kv.Value).First().Key:X8}");
                writer.WriteLine($"   - off2C: 0x{off2CValues.OrderByDescending(kv => kv.Value).First().Key:X8}");
                writer.WriteLine("4. Set LevelVariables.chunkCount to match the number of actual chunks (1 for single-chunk levels)");
                writer.WriteLine("5. Ensure proper texture index remapping to avoid visual glitches");
            }
            
            Console.WriteLine($"✅ RC2 terrain patterns report generated at {reportPath}");
        }
        
        private static void CompareAndWrite<T>(StreamWriter writer, string propertyName, T value1, T value2)
        {
            bool matches = EqualityComparer<T>.Default.Equals(value1, value2);
            string matchIndicator = matches ? "✅ " : "❌ ";
            
            writer.WriteLine($"{matchIndicator}{propertyName}: {value1} vs {value2}");
        }
        
        private static bool CheckSequentialFragmentIds(Level level, out string breakDetails)
        {
            var fragments = level.terrainEngine?.fragments;
            var sb = new StringBuilder();
            bool isSequential = true;
            
            if (fragments != null && fragments.Count > 0)
            {
                for (int i = 0; i < fragments.Count; i++)
                {
                    if (fragments[i].off1E != i)
                    {
                        isSequential = false;
                        sb.AppendLine($"Fragment at index {i} has ID {fragments[i].off1E}");
                    }
                }
            }
            
            breakDetails = sb.ToString();
            return isSequential;
        }
        
        private static void AnalyzeChunkIndices(Level level, string label, StreamWriter writer)
        {
            if (level.terrainEngine?.fragments == null || level.terrainEngine.fragments.Count == 0)
            {
                writer.WriteLine($"{label}: No fragments to analyze");
                return;
            }
            
            var chunkIndices = new Dictionary<short, int>();
            
            foreach (var frag in level.terrainEngine.fragments)
            {
                var fragBytes = frag.ToByteArray();
                short chunkIndex = 0;
                
                if (fragBytes.Length >= 0x24)
                {
                    chunkIndex = BitConverter.ToInt16(fragBytes, 0x22);
                }
                
                if (!chunkIndices.ContainsKey(chunkIndex))
                {
                    chunkIndices[chunkIndex] = 0;
                }
                
                chunkIndices[chunkIndex]++;
            }
            
            writer.WriteLine($"{label} chunk indices distribution:");
            foreach (var kvp in chunkIndices.OrderBy(k => k.Key))
            {
                writer.WriteLine($"  Chunk {kvp.Key}: {kvp.Value} fragments");
            }
        }
    }
}
