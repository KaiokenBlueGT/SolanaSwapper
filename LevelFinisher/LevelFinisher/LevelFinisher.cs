using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using OpenTK.Mathematics;
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Serializers;
using LibReplanetizer.Parsers; // For ArmorParser, GadgetParser
using static LibReplanetizer.DataFunctions;

namespace LevelFinisher
{
    class FinisherProgram
    {
        static void WriteUintBigEndian(FileStream fs, uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            fs.Write(b, 0, 4);
        }

        // No SanitizeRatchetMoby needed if we fully replace with GC template.

        static void FinalizeLevel(
            Level portedLevel,
            Level referenceRc2PlanetLevel, // Full R&C2 planet for templates
            List<Model> globalArmorModels,
            List<List<Texture>> globalArmorTextures,
            List<Model> globalGadgetModels,
            List<Texture> globalGadgetTextures)
        {
            Console.WriteLine("🚀 Starting Part 2: Finalizing level conversion...");

            // --- 1. Integrate R&C2 Global Armors ---
            if (globalArmorModels != null && globalArmorModels.Any())
            {
                Console.WriteLine("Integrating R&C2 global armors...");
                portedLevel.armorModels = globalArmorModels; // Already loaded list
                portedLevel.armorTextures = globalArmorTextures;
                Console.WriteLine($"  Integrated {portedLevel.armorModels.Count} armor models and {portedLevel.armorTextures.Count} armor texture sets.");
            }
            else
            {
                Console.WriteLine("  Warning: No global armor model data provided. Ratchet may not appear correctly.");
            }

            // --- 2. Integrate R&C2 Global Gadgets (Weapons) ---
            if (globalGadgetModels != null && globalGadgetModels.Any())
            {
                Console.WriteLine("Integrating R&C2 global gadgets...");
                portedLevel.gadgetModels = globalGadgetModels; // Already loaded list
                portedLevel.gadgetTextures = globalGadgetTextures;
                Console.WriteLine($"  Integrated {portedLevel.gadgetModels.Count} gadget models and {portedLevel.gadgetTextures.Count} gadget textures.");
            }
            else
            {
                Console.WriteLine("  Warning: No global gadget model data provided.");
            }

            // --- 3. EmplaceCommonData ---
            // This merges armor/gadget models into portedLevel.mobyModels and their textures into portedLevel.textures
            Console.WriteLine("Emplacing common data (merging gadget/armor textures and models)...");
            try
            {
                portedLevel.EmplaceCommonData();
                Console.WriteLine("  Common data emplaced successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error during EmplaceCommonData: {ex.Message}. Texture/model linking for gadgets/armor might be incorrect.");
            }

            // --- 4. Replace/Refine Ratchet Instance ---
            // NOTE: Ratchet moby varies by level! For Oltanis, it's 415. For Yeedil, GC template is moby 17.
            // Future levels can use a dictionary if needed.
            Moby rc1RatchetInstance = portedLevel.mobs?.FirstOrDefault(m => m.mobyID == 415); // Find RC1 Ratchet by oClass
            Moby gcRatchetTemplate = referenceRc2PlanetLevel?.mobs?.FirstOrDefault(m => m.mobyID == 17); // Find GC Ratchet template by oClass

            if (rc1RatchetInstance != null && gcRatchetTemplate != null)
            {
                Console.WriteLine("Replacing RC1 Ratchet moby data with R&C2 template...");

                // Preserve RC1 Ratchet's placement
                Vector3 originalPosition = rc1RatchetInstance.position;
                Quaternion originalRotation = rc1RatchetInstance.rotation;
                Vector3 originalScale = rc1RatchetInstance.scale; // Usually (1,1,1) for Ratchet

                // Overwrite most fields of rc1RatchetInstance with gcRatchetTemplate's values
                // This ensures all unknown fields, flags, pvar setup etc. are from R&C2.
                rc1RatchetInstance.missionID = gcRatchetTemplate.missionID;
                rc1RatchetInstance.spawnType = gcRatchetTemplate.spawnType;
                rc1RatchetInstance.bolts = gcRatchetTemplate.bolts; // Or keep original bolts
                rc1RatchetInstance.dataval = gcRatchetTemplate.dataval;
                rc1RatchetInstance.drawDistance = gcRatchetTemplate.drawDistance;
                rc1RatchetInstance.updateDistance = gcRatchetTemplate.updateDistance;
                rc1RatchetInstance.unk3A = gcRatchetTemplate.unk3A;
                rc1RatchetInstance.unk3B = gcRatchetTemplate.unk3B;
                rc1RatchetInstance.exp = gcRatchetTemplate.exp;
                rc1RatchetInstance.unk9 = gcRatchetTemplate.unk9;
                rc1RatchetInstance.unk6 = gcRatchetTemplate.unk6;
                rc1RatchetInstance.groupIndex = gcRatchetTemplate.groupIndex;
                rc1RatchetInstance.isRooted = gcRatchetTemplate.isRooted;
                rc1RatchetInstance.rootedDistance = gcRatchetTemplate.rootedDistance;
                rc1RatchetInstance.unk7A = gcRatchetTemplate.unk7A;
                rc1RatchetInstance.unk7B = gcRatchetTemplate.unk7B;
                rc1RatchetInstance.unk8A = gcRatchetTemplate.unk8A;
                rc1RatchetInstance.unk8B = gcRatchetTemplate.unk8B;
                rc1RatchetInstance.unk12A = gcRatchetTemplate.unk12A;
                rc1RatchetInstance.unk12B = gcRatchetTemplate.unk12B;
                rc1RatchetInstance.occlusion = gcRatchetTemplate.occlusion;
                rc1RatchetInstance.mode = gcRatchetTemplate.mode;
                rc1RatchetInstance.color = gcRatchetTemplate.color;
                rc1RatchetInstance.light = gcRatchetTemplate.light;
                rc1RatchetInstance.cutscene = gcRatchetTemplate.cutscene;

                // PVar handling: Use the template's pvar index and data
                // This assumes that if gcRatchetTemplate.pvarIndex is valid,
                // its pVars data will be added to portedLevel.pVars during PVar Consolidation.
                rc1RatchetInstance.pvarIndex = gcRatchetTemplate.pvarIndex;
                rc1RatchetInstance.pVars = (byte[])gcRatchetTemplate.pVars?.Clone(); // Deep copy pvar data

                // Restore original placement
                rc1RatchetInstance.position = originalPosition;
                rc1RatchetInstance.rotation = originalRotation;
                rc1RatchetInstance.scale = originalScale;

                // Link to the correct emplaced Model (should be armor model at ID 0)
                Model actualRatchetModel = portedLevel.mobyModels?.FirstOrDefault(m => m.id == 0);
                if (actualRatchetModel != null)
                {
                    rc1RatchetInstance.model = actualRatchetModel;
                    rc1RatchetInstance.modelID = actualRatchetModel.id; // Should be 0
                }
                else
                {
                    Console.WriteLine("  Warning: MobyModel ID 0 (expected Ratchet/Armor base) not found after EmplaceCommonData during Ratchet refine.");
                }

                if (rc1RatchetInstance.model is MobyModel mobyModel)
                {
                    mobyModel.unk1 = 4019.569f;
                    mobyModel.unk2 = -679.321f;
                    mobyModel.unk3 = 2234.337f;
                    mobyModel.unk4 = 18117.051f;
                    mobyModel.unk6 = 1074200575;
                    mobyModel.vertexCount2 = 0;
                    mobyModel.textureConfig?.Clear();
                }

                rc1RatchetInstance.UpdateTransformMatrix();
                Console.WriteLine("  Ratchet moby data replaced with R&C2 template, placement preserved.");
            }
            else
            {
                Console.WriteLine("  Warning: Ratchet (Moby 0) not found in ported level or GC reference level. Cannot replace.");
            }

            // --- 5. Selective Moby Replacement (for other Mobys - TODO) ---
            Console.WriteLine("Skipping other Moby replacements for now.");

            // --- GC Template Moby Cloning by Model ID ---
            if (portedLevel.mobs != null && referenceRc2PlanetLevel?.mobs != null && referenceRc2PlanetLevel.mobyModels != null)
            {
                Console.WriteLine("Cloning GC mobys by modelID...");
                var finalMobList = new List<Moby>();
                int replacedCount = 0, removedCount = 0;

                foreach (var rc1MobyInstance in portedLevel.mobs)
                {
                    // Find a GC moby and model with the same modelID
                    var gcTemplateMoby = referenceRc2PlanetLevel.mobs.FirstOrDefault(m => m.modelID == rc1MobyInstance.modelID);
                    var gcReplacementModel = referenceRc2PlanetLevel.mobyModels.FirstOrDefault(m => m.id == rc1MobyInstance.modelID);

                    if (gcTemplateMoby != null && gcReplacementModel != null)
                    {
                        // Clone a safe GC moby into this RC1 moby's position/rotation/scale
                        var newRc2Moby = new Moby(portedLevel.game, gcReplacementModel, rc1MobyInstance.position, rc1MobyInstance.rotation, rc1MobyInstance.scale);

                        // Copy all safe GC properties
                        newRc2Moby.mobyID = gcTemplateMoby.mobyID;
                        newRc2Moby.modelID = gcReplacementModel.id;
                        newRc2Moby.missionID = gcTemplateMoby.missionID;
                        newRc2Moby.spawnType = gcTemplateMoby.spawnType;
                        newRc2Moby.bolts = gcTemplateMoby.bolts;
                        newRc2Moby.dataval = gcTemplateMoby.dataval;
                        newRc2Moby.drawDistance = gcTemplateMoby.drawDistance;
                        newRc2Moby.updateDistance = gcTemplateMoby.updateDistance;
                        newRc2Moby.exp = gcTemplateMoby.exp;
                        newRc2Moby.groupIndex = gcTemplateMoby.groupIndex;
                        newRc2Moby.pvarIndex = gcTemplateMoby.pvarIndex;
                        newRc2Moby.pVars = (byte[])gcTemplateMoby.pVars?.Clone();

                        newRc2Moby.UpdateTransformMatrix();
                        finalMobList.Add(newRc2Moby);
                        Console.WriteLine($"  -> Cloned GC moby oClass {gcTemplateMoby.mobyID} (modelID {gcTemplateMoby.modelID}) at RC1 moby position.");
                        replacedCount++;
                    }
                    else
                    {
                        // Couldn't find matching GC moby — remove it
                        Console.WriteLine($"  -> Removed RC1 moby oClass {rc1MobyInstance.mobyID}, modelID {rc1MobyInstance.modelID} — no GC match.");
                        removedCount++;
                    }
                }

                portedLevel.mobs = finalMobList;
                Console.WriteLine($"GC moby clone complete: {replacedCount} replaced, {removedCount} removed.");
            }

            // --- 6. PVar Consolidation ---
            if (portedLevel.mobs != null)
            {
                Console.WriteLine("Consolidating PVars...");
                var pvarDict = new Dictionary<string, int>(); // Key: base64 of pVar bytes, Value: index in finalPVars
                var finalPVars = new List<byte[]>();

                foreach (var moby in portedLevel.mobs)
                {
                    // Treat null and empty as the same
                    var pvar = moby.pVars ?? Array.Empty<byte>();
                    string key = Convert.ToBase64String(pvar);

                    if (!pvarDict.TryGetValue(key, out int idx))
                    {
                        idx = finalPVars.Count;
                        finalPVars.Add(pvar);
                        pvarDict[key] = idx;
                    }
                    moby.pvarIndex = (pvar.Length == 0) ? -1 : idx;
                }
                portedLevel.pVars = finalPVars;
                Console.WriteLine($"  Consolidated {finalPVars.Count} unique PVar blocks.");
            }

            // --- 7. Rebuild Moby ID List for Gameplay Header ---
            if (portedLevel.mobs != null)
            {
                portedLevel.mobyIds = portedLevel.mobs.Select(m => m.mobyID).ToList();
                Console.WriteLine("Rebuilt Moby ID list for gameplay header.");
            }

            // --- Validation: Check for invalid modelID and pvarIndex references ---
            if (portedLevel.mobs != null)
            {
                int mobyModelCount = portedLevel.mobyModels?.Count ?? 0;
                int textureCount = portedLevel.textures?.Count ?? 0;
                int pVarCount = portedLevel.pVars?.Count ?? 0;

                bool hasError = false;

                foreach (var moby in portedLevel.mobs)
                {
                    // ModelID validation
                    if (moby.modelID < 0 || moby.modelID >= mobyModelCount)
                    {
                        Console.WriteLine($"❌ Invalid modelID {moby.modelID} for moby oClass {moby.mobyID}");
                        hasError = true;
                    }

                    // pVarIndex validation
                    if (moby.pvarIndex != -1 && (moby.pvarIndex < 0 || moby.pvarIndex >= pVarCount))
                    {
                        Console.WriteLine($"❌ Invalid pVarIndex {moby.pvarIndex} for moby oClass {moby.mobyID}");
                        hasError = true;
                    }
                }

                // Optional: Validate model textureConfig indices
                if (portedLevel.mobyModels != null)
                {
                    foreach (var model in portedLevel.mobyModels)
                    {
                        if (model.textureConfig != null)
                        {
                            foreach (var conf in model.textureConfig)
                            {
                                if (conf.id < 0 || conf.id >= textureCount)
                                {
                                    Console.WriteLine($"❌ Invalid textureConfig id {conf.id} in model id {model.id}");
                                    hasError = true;
                                }
                            }
                        }
                    }
                }

                if (!hasError)
                    Console.WriteLine("✅ All moby modelIDs, pVarIndexes, and textureConfig IDs are valid.");
                else
                    Console.WriteLine("❌ Validation failed: See errors above.");
            }

            // --- Remove mobys with invalid modelID ---
            if (portedLevel.mobs != null && portedLevel.mobyModels != null)
            {
                int mobyModelCount = portedLevel.mobyModels.Count;
                int beforeCount = portedLevel.mobs.Count;
                portedLevel.mobs = portedLevel.mobs
                    .Where(moby =>
                    {
                        bool valid = moby.modelID >= 0 && moby.modelID < mobyModelCount;
                        if (!valid)
                            Console.WriteLine($"Removing moby oClass {moby.mobyID} with invalid modelID {moby.modelID}");
                        return valid;
                    })
                    .ToList();
                int afterCount = portedLevel.mobs.Count;
                Console.WriteLine($"Removed {beforeCount - afterCount} mobys with invalid modelIDs.");

                // Rebuild mobyIds table/header to match filtered mobys
                portedLevel.mobyIds = portedLevel.mobs.Select(m => m.mobyID).ToList();
                Console.WriteLine("Rebuilt Moby ID list for gameplay header after removals.");
            }

            // Remove mobys whose mobyID is not present in the GC template
            var validMobyIDs = new HashSet<int>(referenceRc2PlanetLevel.mobs.Select(m => m.mobyID));
            portedLevel.mobs = portedLevel.mobs.Where(m => validMobyIDs.Contains(m.mobyID)).ToList();

            Console.WriteLine("✅ Level finalization steps applied.");
        }


        public static void Main(string[] args)
        {
            Console.WriteLine(">>> R&C Level Finisher (Part 2) <<<");

            string partiallyConvertedLevelDir = @"C:\Users\Ryan_\Downloads\temp\Oltanis_RaC2Port\";
            string globalRc2Dir = @"D:\Projects\R&C1_to_R&C2_Planet_Format\Going_Commando_PSARC\rc2\ps3data\global\";
            string armor0EnginePath = Path.Combine(globalRc2Dir, "armor", "armor0.ps3"); // Path to armor0.ps3
            string gadgetsEnginePath = Path.Combine(globalRc2Dir, "gadgets", "gadgets.ps3"); // Path to gadgets.ps3

            // --- NEW: Path to a full R&C2 reference PLANET (e.g., Yeedil) for Moby templates ---
            string referenceGcPlanetDir = @"C:\Users\Ryan_\Downloads\temp\Damosel\"; // Your Yeedil dump
            string referenceGcPlanetEnginePath = Path.Combine(referenceGcPlanetDir, "engine.ps3");

            string finalOutputDir = @"C:\Users\Ryan_\Downloads\temp\Oltanis_RaC2Port_Final\";
            Directory.CreateDirectory(finalOutputDir);

            string portedEnginePath = Path.Combine(partiallyConvertedLevelDir, "engine.ps3");

            Console.WriteLine($"📂 Loading partially converted level: {portedEnginePath}");
            Level portedLevel = null;
            try { portedLevel = new Level(portedEnginePath); }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading partially converted level: {ex.Message}");
                return;
            }

            // --- Load Full R&C2 Reference Planet (for Moby templates like Ratchet) ---
            Console.WriteLine($"📂 Loading reference R&C2 planet: {referenceGcPlanetEnginePath}");
            Level referenceRc2PlanetLevel = null;
            if (File.Exists(referenceGcPlanetEnginePath))
            {
                try
                {
                    referenceRc2PlanetLevel = new Level(referenceGcPlanetEnginePath);
                    Console.WriteLine("  ✅ Reference R&C2 planet loaded successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Error loading reference R&C2 planet: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"  Warning: Reference R&C2 planet not found at {referenceGcPlanetEnginePath}. Some Moby refinements might be skipped.");
            }


            // === Load Global Armor Data using ArmorParser ===
            List<Model> globalArmorModels = new();
            List<List<Texture>> globalArmorTextures = new(); // List of texture lists (one per armor set/model)
            Console.WriteLine($"📂 Loading R&C2 global armor assets from: {armor0EnginePath}");
            if (File.Exists(armor0EnginePath))
            {
                try
                {
                    // Assuming armor0.ps3 contains one set of armor, for simplicity.
                    // If it contains multiple, you'd loop or use ArmorHeader.FindArmorFiles
                    using var armorParser = new ArmorParser(GameType.RaC2, armor0EnginePath);
                    var armorModel = armorParser.GetArmor();
                    if (armorModel != null)
                    {
                        globalArmorModels.Add(armorModel);
                        // ArmorParser.GetTextures() returns List<Texture> for the current file.
                        // ArmorTextures for Level object is List<List<Texture>>.
                        globalArmorTextures.Add(armorParser.GetTextures());
                    }
                    Console.WriteLine($"  ✅ Loaded {globalArmorModels.Count} global armor model(s) and {globalArmorTextures.FirstOrDefault()?.Count ?? 0} texture(s) for the first set.");
                }
                catch (Exception ex) { Console.WriteLine($"  ❌ Failed to load global armor data: {ex.Message}"); }
            }
            else { Console.WriteLine($"  Warning: Global armor file not found: {armor0EnginePath}"); }


            // === Load Global Gadget Data using GadgetParser ===
            List<Model> globalGadgetModels = new();
            List<Texture> globalGadgetTextures = new(); // Gadgets have a single list of textures
            Console.WriteLine($"📂 Loading R&C2 global gadget assets from: {gadgetsEnginePath}");
            if (File.Exists(gadgetsEnginePath))
            {
                try
                {
                    using var gadgetParser = new GadgetParser(GameType.RaC2, gadgetsEnginePath);
                    // GadgetParser.GetModels() returns List<MobyModel>, cast to List<Model>
                    globalGadgetModels = gadgetParser.GetModels().Cast<Model>().ToList();
                    globalGadgetTextures = gadgetParser.GetTextures();
                    Console.WriteLine($"  ✅ Loaded {globalGadgetModels.Count} global gadget model(s) and {globalGadgetTextures.Count} texture(s).");
                }
                catch (Exception ex) { Console.WriteLine($"  ❌ Failed to load global gadget data: {ex.Message}"); }
            }
            else { Console.WriteLine($"  Warning: Global gadget file not found: {gadgetsEnginePath}"); }


            if (portedLevel == null)
            {
                Console.WriteLine("Partially converted level could not be loaded. Exiting.");
                return;
            }
            Console.WriteLine("✅ Core level and global assets prepared for finalization.");

            FinalizeLevel(portedLevel, referenceRc2PlanetLevel, globalArmorModels, globalArmorTextures, globalGadgetModels, globalGadgetTextures);

            Console.WriteLine($"\n💾 Saving finalized RC2 level to: {finalOutputDir}");
            portedLevel.Save(finalOutputDir);

            string finalEngineFile = Path.Combine(finalOutputDir, "engine.ps3");
            Console.WriteLine("Patching final engine.ps3 header values...");
            try
            {
                using (var fs = File.Open(finalEngineFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Seek(0x08, SeekOrigin.Begin); WriteUintBigEndian(fs, 0x00020003);
                    fs.Seek(0x0C, SeekOrigin.Begin); WriteUintBigEndian(fs, 0x00000000);
                    fs.Seek(0xA0, SeekOrigin.Begin); WriteUintBigEndian(fs, 0xEAA90001);
                }
                Console.WriteLine("✅ Final engine.ps3 patched successfully.");
            }
            catch (Exception ex) { Console.WriteLine($"❌ Error while patching final engine.ps3: {ex.Message}"); }

            Console.WriteLine("\n=== Post-Finalization Verification (Reloading Converted Level) ===");
            // ... (your verification logging) ...
            try
            {
                var reloadedFinalLevel = new Level(finalEngineFile);
                Console.WriteLine($"GameType reported by reloaded level: {reloadedFinalLevel.game.num}");
                Console.WriteLine($"Moby Count: {(reloadedFinalLevel.mobs?.Count ?? 0)}");
                Console.WriteLine($"Armor Model Count in Level object: {(reloadedFinalLevel.armorModels?.Count ?? 0)}");
                Console.WriteLine($"Gadget Model Count in Level object: {(reloadedFinalLevel.gadgetModels?.Count ?? 0)}");
                Console.WriteLine($"Total MobyModels in engine (includes merged armor/gadgets): {(reloadedFinalLevel.mobyModels?.Count ?? 0)}");
                Console.WriteLine($"Total Textures in engine (includes merged armor/gadgets): {(reloadedFinalLevel.textures?.Count ?? 0)}");

            }
            catch (Exception ex) { Console.WriteLine($"❌ Error loading finalized level for verification: {ex.Message}\n{ex.StackTrace}"); }


            Console.WriteLine("\n✅ Finisher Done! Test the output in RPCS3.");
        }
    }
}