using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometrySwapper
{
    /// <summary>
    /// Handles manipulation of point lights and directional lights between RC1 and RC2 levels
    /// </summary>
    public static class PointLightsSwapper
    {
        /// <summary>
        /// Swaps point lights from RC1 level to RC2 level
        /// </summary>
        /// <param name="targetLevel">RC2 level to modify</param>
        /// <param name="rc1SourceLevel">RC1 level to get light data from</param>
        /// <returns>True if operation was successful</returns>
        public static bool SwapPointLights(Level targetLevel, Level rc1SourceLevel)
        {
            if (targetLevel == null || rc1SourceLevel == null)
            {
                Console.WriteLine("? Cannot swap point lights: Invalid level data");
                return false;
            }

            Console.WriteLine("\n==== Swapping Point Lights to match RC1 Oltanis ====");

            // Process the point lights
            ProcessPointLights(targetLevel, rc1SourceLevel);

            // Process directional lights (the ones shown in your screenshot)
            ProcessDirectionalLights(targetLevel, rc1SourceLevel);

            return true;
        }

        /// <summary>
        /// Process the point lights from the levels
        /// </summary>
        private static void ProcessPointLights(Level targetLevel, Level rc1SourceLevel)
        {
            if (targetLevel.pointLights == null)
            {
                targetLevel.pointLights = new List<PointLight>();
                Console.WriteLine("Created new point lights list for target level");
            }

            if (rc1SourceLevel.pointLights == null || rc1SourceLevel.pointLights.Count == 0)
            {
                Console.WriteLine("?? RC1 level has no point lights to copy");
                
                // Clear any existing point lights in target level
                if (targetLevel.pointLights.Count > 0)
                {
                    targetLevel.pointLights.Clear();
                    Console.WriteLine("? Removed all point lights from target level");
                }
                
                return;
            }

            // Clear existing point lights
            int removedCount = targetLevel.pointLights.Count;
            targetLevel.pointLights.Clear();
            Console.WriteLine($"? Removed {removedCount} existing point lights from target level");

            // Copy point lights from RC1 level
            foreach (var rc1Light in rc1SourceLevel.pointLights)
            {
                // Create a clone of the RC1 light
                PointLight newLight = new PointLight(GameType.RaC2, new byte[PointLight.GetElementSize(GameType.RaC2)], targetLevel.pointLights.Count);
                newLight.position = rc1Light.position;
                newLight.color = rc1Light.color;
                newLight.radius = rc1Light.radius;
                
                targetLevel.pointLights.Add(newLight);
                Console.WriteLine($"? Added point light: Position={newLight.position}, Color={newLight.color}, Radius={newLight.radius}");
            }

            Console.WriteLine($"? Added {targetLevel.pointLights.Count} point lights from RC1 Oltanis");
        }

        /// <summary>
        /// Process directional lights from the levels (the ones shown in the screenshot)
        /// </summary>
        private static void ProcessDirectionalLights(Level targetLevel, Level rc1SourceLevel)
        {
            if (targetLevel.lights == null)
            {
                targetLevel.lights = new List<Light>();
                Console.WriteLine("Created new directional lights list for target level");
            }

            if (rc1SourceLevel.lights == null || rc1SourceLevel.lights.Count == 0)
            {
                Console.WriteLine("?? RC1 level has no directional lights to copy");
                return;
            }

            // Keep track of how many lights we started with
            int initialLightCount = targetLevel.lights.Count;
            Console.WriteLine($"Target level has {initialLightCount} directional lights initially");

            // We only want to keep one light (Light 0) to match RC1 Oltanis
            if (initialLightCount > 1)
            {
                // Remove all but the first light
                targetLevel.lights = targetLevel.lights.Take(1).ToList();
                Console.WriteLine($"? Kept 1 directional light and removed {initialLightCount - 1} excess lights");
            }
            else if (initialLightCount == 0)
            {
                // Create a new light if none exists
                Light newLight = CreateDefaultLight();
                targetLevel.lights.Add(newLight);
                Console.WriteLine("? Added new default directional light");
            }

            // Now we have exactly one light (Light 0) in the target level
            // Set its properties to match the RC1 Oltanis light
            if (rc1SourceLevel.lights.Count > 0)
            {
                Light rc1Light = rc1SourceLevel.lights[0];
                Light targetLight = targetLevel.lights[0];

                // Update the light properties
                targetLight.color1 = rc1Light.color1;
                targetLight.direction1 = rc1Light.direction1;
                targetLight.color2 = rc1Light.color2;
                targetLight.direction2 = rc1Light.direction2;

                Console.WriteLine($"? Updated directional light 0 to match RC1 Oltanis values:");
                Console.WriteLine($"  Color 1: ({rc1Light.color1.X}, {rc1Light.color1.Y}, {rc1Light.color1.Z}, {rc1Light.color1.W})");
                Console.WriteLine($"  Direction 1: ({rc1Light.direction1.X}, {rc1Light.direction1.Y}, {rc1Light.direction1.Z}, {rc1Light.direction1.W})");
                Console.WriteLine($"  Color 2: ({rc1Light.color2.X}, {rc1Light.color2.Y}, {rc1Light.color2.Z}, {rc1Light.color2.W})");
                Console.WriteLine($"  Direction 2: ({rc1Light.direction2.X}, {rc1Light.direction2.Y}, {rc1Light.direction2.Z}, {rc1Light.direction2.W})");
            }
            else
            {
                // Based on your screenshot, set Light 0 properties manually
                Light targetLight = targetLevel.lights[0];
                
                // Values from the screenshot
                targetLight.color1 = new Vector4(68/255.0f, 73/255.0f, 73/255.0f, 1.0f); 
                targetLight.direction1 = new Vector4(0.677f, 0.556f, -0.482f, 1.0f);
                targetLight.color2 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                targetLight.direction2 = new Vector4(0.77f, 0.421f, -0.479f, 1.0f);
                
                Console.WriteLine("? Set directional light 0 to values from screenshot:");
                Console.WriteLine($"  Color 1: ({targetLight.color1.X}, {targetLight.color1.Y}, {targetLight.color1.Z}, {targetLight.color1.W})");
                Console.WriteLine($"  Direction 1: ({targetLight.direction1.X}, {targetLight.direction1.Y}, {targetLight.direction1.Z}, {targetLight.direction1.W})");
                Console.WriteLine($"  Color 2: ({targetLight.color2.X}, {targetLight.color2.Y}, {targetLight.color2.Z}, {targetLight.color2.W})");
                Console.WriteLine($"  Direction 2: ({targetLight.direction2.X}, {targetLight.direction2.Y}, {targetLight.direction2.Z}, {targetLight.direction2.W})");
            }

            // Update all light references in mobys, ties and shrubs to use Light 0
            UpdateLightReferences(targetLevel);
        }

        /// <summary>
        /// Updates all objects in the level to reference Light 0
        /// </summary>
        private static void UpdateLightReferences(Level level)
        {
            int mobyCount = 0;
            int shrubCount = 0;
            int tieCount = 0;

            // Update all mobys to use Light 0
            if (level.mobs != null)
            {
                foreach (var moby in level.mobs)
                {
                    if (moby.light != 0)
                    {
                        moby.light = 0;
                        mobyCount++;
                    }
                }
            }

            // Update all shrubs to use Light 0
            if (level.shrubs != null)
            {
                foreach (var shrub in level.shrubs)
                {
                    if (shrub.light != 0)
                    {
                        shrub.light = 0;
                        shrubCount++;
                    }
                }
            }

            // Update all ties to use Light 0
            if (level.ties != null)
            {
                foreach (var tie in level.ties)
                {
                    if (tie.light != 0)
                    {
                        tie.light = 0;
                        tieCount++;
                    }
                }
            }

            Console.WriteLine($"? Updated light references to use Light 0: {mobyCount} mobys, {shrubCount} shrubs, {tieCount} ties");
        }

        /// <summary>
        /// Creates a default light with standard values
        /// </summary>
        private static Light CreateDefaultLight()
        {
            // Create a byte array with default values
            byte[] defaultLightData = new byte[0x40];
            
            // Fill with default values (mostly zeros)
            for (int i = 0; i < defaultLightData.Length; i++)
            {
                defaultLightData[i] = 0;
            }
            
            // Create the light from the default data
            Light light = new Light(defaultLightData, 0);
            
            // Set some reasonable default values
            light.color1 = new Vector4(0.267f, 0.286f, 0.286f, 1.0f);  // Light gray
            light.direction1 = new Vector4(0.677f, 0.556f, -0.482f, 1.0f);  // From top right
            light.color2 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);  // Black (no secondary light)
            light.direction2 = new Vector4(0.770f, 0.421f, -0.479f, 1.0f);  // Similar direction
            
            return light;
        }

        /// <summary>
        /// Interactive wrapper for point light swapping function
        /// </summary>
        /// <returns>True if the operation was successful</returns>
        public static bool SwapPointLightsInteractive()
        {
            Console.WriteLine("\n==== Swap RC2 Point Lights with RC1 Oltanis Point Lights ====");

            // Get target level path
            Console.WriteLine("\nEnter path to the target RC2 level engine.ps3 file:");
            Console.Write("> ");
            string targetPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                Console.WriteLine("? Invalid target level path");
                return false;
            }

            // Get RC1 Oltanis level path
            Console.WriteLine("\nEnter path to the RC1 Oltanis level engine.ps3 file:");
            Console.Write("> ");
            string rc1OltanisPath = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(rc1OltanisPath) || !File.Exists(rc1OltanisPath))
            {
                Console.WriteLine("? Invalid RC1 Oltanis level path");
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
                Console.WriteLine($"? Error loading levels: {ex.Message}");
                return false;
            }

            // Perform point lights swap
            bool success = SwapPointLights(targetLevel, rc1OltanisLevel);

            if (success)
            {
                Console.Write("\nSave changes to the target level? (y/n): ");
                if (Console.ReadLine()?.Trim().ToLower() == "y")
                {
                    try
                    {
                        targetLevel.Save(targetPath);
                        Console.WriteLine("? Target level saved successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? Error saving target level: {ex.Message}");
                        return false;
                    }
                }
            }
            
            return success;
        }
    }
}
