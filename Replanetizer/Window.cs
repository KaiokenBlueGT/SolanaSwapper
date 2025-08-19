﻿// Copyright (C) 2018-2022, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp;
using Replanetizer.Frames;
using Replanetizer.Utils;
using Replanetizer.Renderer;
using SixLabors.ImageSharp.PixelFormats;
using LibReplanetizer;
using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using GeometrySwapper;
using static LibReplanetizer.DataFunctions;

namespace Replanetizer
{
    public class Window : GameWindow
    {
        private static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();
        public string openGLString = "Unknown OpenGL Version";

        private ImGuiController? controller;
        private List<Frame> openFrames;

        public string[] args;

        public Window(string[] args) : base(GameWindowSettings.Default,
            new NativeWindowSettings() { ClientSize = new Vector2i(1600, 900), APIVersion = new Version(3, 3), Flags = ContextFlags.ForwardCompatible, Profile = ContextProfile.Core, Vsync = VSyncMode.On })
        {
            this.args = args;
            openFrames = new List<Frame>();

            string? applicationFolder = System.AppContext.BaseDirectory;
            string iconsFolder = Path.Join(applicationFolder, "Icons");

            Image<Rgba32> image = Image.Load<Rgba32>(Path.Join(iconsFolder, "Replanetizer.png"));
            byte[] imageBytes = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(imageBytes);

            OpenTK.Windowing.Common.Input.Image img = new OpenTK.Windowing.Common.Input.Image(image.Width, image.Height, imageBytes);
            OpenTK.Windowing.Common.Input.Image[] imgs = new OpenTK.Windowing.Common.Input.Image[] { img };
            this.Icon = new OpenTK.Windowing.Common.Input.WindowIcon(imgs);
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            openGLString = "OpenGL " + GL.GetString(StringName.Version);
            Title = String.Format("Replanetizer ({0})", openGLString);

            controller = new ImGuiController(ClientSize.X, ClientSize.Y);

            UpdateInfoFrame.CheckForNewVersion(this);

            if (args.Length > 0)
            {
                LevelFrame lf = new LevelFrame(this, args[0]);
                AddFrame(lf);
            }
        }

        public void AddFrame(Frame frame)
        {
            openFrames.Add(frame);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            if (controller != null)
            {
                // Tell ImGui of the new size
                controller.WindowResized(ClientSize.X, ClientSize.Y);
            }
        }

        public static bool FrameMustClose(Frame frame)
        {
            return !frame.isOpen;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            openFrames.RemoveAll(FrameMustClose);

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            if (controller != null)
                controller.Update(this, (float) e.Time);

            RenderUI((float) e.Time);

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            GL.ClearColor(new Color4(0, 32, 48, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            if (controller != null)
                controller.Render();

            GLUtil.CheckGlError("End of frame");
            SwapBuffers();
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);

            if (controller != null)
                controller.PressChar((char) e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            if (controller != null)
                controller.MouseScroll(e.Offset);
        }

        private static bool FrameIsLevel(Frame frame)
        {
            return frame.GetType() == typeof(LevelFrame);
        }

        private void RenderMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open engine.ps3"))
                    {
                        var res = CrossFileDialog.OpenFile(filter: ".ps3");
                        if (res.Length > 0)
                        {
                            openFrames.RemoveAll(FrameIsLevel);
                            LevelFrame lf = new LevelFrame(this, res);
                            AddFrame(lf);
                        }
                    }

                    ImGui.Separator();

                    // 🆕 ADD IMPORT OPTIONS
                    if (ImGui.BeginMenu("Import"))
                    {
                        if (ImGui.MenuItem("Import OBJ as Terrain"))
                        {
                            ImportObjAsTerrain();

                            // Directly call collision generation after import
                            var levelFrame = openFrames.OfType<LevelFrame>().FirstOrDefault();
                            if (levelFrame?.level != null && levelFrame.level.terrainEngine != null)
                            {
                                var fragments = levelFrame.level.terrainEngine.fragments;
                                if (fragments != null && fragments.Count > 0)
                                {
                                    Console.WriteLine("[Menu] Directly calling GenerateAndAssignCollision from menu handler...");
                                    ObjTerrainImporter.GenerateAndAssignCollision(levelFrame.level, fragments);
                                    Console.WriteLine("[Menu] Finished direct collision generation call.");
                                }
                                else
                                {
                                    Console.WriteLine("[Menu] No terrain fragments found for collision generation.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("[Menu] Level or terrainEngine is null after import.");
                            }
                        }

                        ImGui.EndMenu();
                    }

                    // 🆕 ADD MINIMAL SAVE OPTIONS
                    if (ImGui.BeginMenu("Save Options"))
                    {
                        if (ImGui.MenuItem("Save As (Normal)"))
                        {
                            SaveLevelNormal();
                        }

                        if (ImGui.MenuItem("Minimal Save (No Processing)"))
                        {
                            SaveLevelMinimal();
                        }

                        ImGui.EndMenu();
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Quit"))
                    {
                        Environment.Exit(0);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Tools"))
                {
                    if (ImGui.MenuItem("R&C1 to R&C3 Geometry Swapper"))
                    {
                        AddFrame(new GeometrySwapperFrame(this));
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("About"))
                {
                    if (ImGui.MenuItem("About Replanetizer"))
                    {
                        AddFrame(new AboutFrame(this));
                    }
                    if (ImGui.MenuItem("Open ImGui demo window"))
                    {
                        AddFrame(new DemoWindowFrame(this));
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }
        }

        private void RenderUI(float deltaTime)
        {
            RenderMenuBar();

            foreach (Frame frame in openFrames)
            {
                frame.RenderAsWindow(deltaTime);
            }
        }

        /// <summary>
        /// Normal save with all existing processing and validation
        /// </summary>
        private void SaveLevelNormal()
        {
            var levelFrame = openFrames.OfType<LevelFrame>().FirstOrDefault();
            if (levelFrame?.level == null)
            {
                Console.WriteLine("❌ No level loaded for saving");
                return;
            }

            var savePath = CrossFileDialog.SaveFile("Save Level", ".ps3");
            if (!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    Console.WriteLine("💾 Performing normal save with enhanced geometry protection...");

                    var level = levelFrame.level;

                    // 🆕 PROTECT ALL GEOMETRY DATA
                    var cuboidProtection = PreserveAllCuboidRotations(level);
                    var tieCullingProtection = TieCullingDataFixer.PreserveTieCullingData(level);

                    level.Save(Path.GetDirectoryName(savePath));

                    // 🆕 RESTORE ALL GEOMETRY DATA
                    RestoreAllCuboidRotations(level, cuboidProtection);
                    TieCullingDataFixer.RestoreTieCullingData(level, tieCullingProtection);

                    Console.WriteLine($"✅ Level saved normally to: {Path.GetDirectoryName(savePath)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error during normal save: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Minimal save with absolutely no processing - just raw LibReplanetizer save
        /// </summary>
        private void SaveLevelMinimal()
        {
            var levelFrame = openFrames.OfType<LevelFrame>().FirstOrDefault();
            if (levelFrame?.level == null)
            {
                Console.WriteLine("❌ No level loaded for saving");
                return;
            }

            var savePath = CrossFileDialog.SaveFile("Minimal Save Level", ".ps3");
            if (!string.IsNullOrEmpty(savePath))
            {
                try
                {
                    Console.WriteLine("💾 Performing MINIMAL save - bypassing chunk serialization...");

                    var level = levelFrame.level;

                    // 🆕 PROTECT SHIP CAMERA ROTATIONS BEFORE SAVE
                    var shipCameraProtection = PreserveShipCameraRotations(level);

                    // 🆕 FORCE NO CHUNKS TO PREVENT CORRUPTION
                    var originalTerrainChunks = level.terrainChunks;
                    var originalCollisionChunks = level.collisionChunks;
                    var originalCollBytesChunks = level.collBytesChunks;
                    var originalChunkCount = level.levelVariables?.chunkCount ?? 0;

                    // Temporarily clear chunk data
                    level.terrainChunks = new List<Terrain>();
                    level.collisionChunks = new List<Collision>();
                    level.collBytesChunks = new List<byte[]>();
                    if (level.levelVariables != null)
                        level.levelVariables.chunkCount = 0;

                    // Save without chunks
                    level.Save(Path.GetDirectoryName(savePath));

                    // Restore original data
                    level.terrainChunks = originalTerrainChunks;
                    level.collisionChunks = originalCollisionChunks;
                    level.collBytesChunks = originalCollBytesChunks;
                    if (level.levelVariables != null)
                        level.levelVariables.chunkCount = originalChunkCount;

                    // 🆕 RESTORE SHIP CAMERA ROTATIONS AFTER SAVE
                    RestoreShipCameraRotations(level, shipCameraProtection);

                    Console.WriteLine($"✅ Level saved minimally to: {Path.GetDirectoryName(savePath)}");
                    Console.WriteLine("🔍 This should eliminate the 0xFF → 0x00 corruption");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error during minimal save: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 🆕 NEW: Import OBJ file as terrain
        /// </summary>
        private void ImportObjAsTerrain()
        {
            var levelFrame = openFrames.OfType<LevelFrame>().FirstOrDefault();
            if (levelFrame?.level == null)
            {
                Console.WriteLine("❌ No level loaded for import");
                return;
            }

            var objPath = CrossFileDialog.OpenFile("Import OBJ as Terrain", ".obj");
            if (!string.IsNullOrEmpty(objPath))
            {
                try
                {
                    Console.WriteLine($"📥 Importing OBJ file as terrain: {Path.GetFileName(objPath)}");

                    var importer = new ObjTerrainImporter();
                    var terrainFragments = importer.ImportObjAsTerrain(objPath, levelFrame.level.textures);

                    Console.WriteLine($"[DEBUG] terrainFragments.Count after import: {terrainFragments.Count}");
                    foreach (var fragment in terrainFragments.Take(3))
                    {
                        if (fragment?.model != null)
                        {
                            Console.WriteLine($"[DEBUG] Fragment: modelID={fragment.modelID}, vertexCount={fragment.model.vertexCount}, faceCount={fragment.model.faceCount}");
                        }
                    }

                    if (terrainFragments.Count > 0)
                    {
                        Console.WriteLine($"[Importer] {terrainFragments.Count} terrain fragments generated. Proceeding with assignment.");

                        var level = levelFrame.level;

                        // Generate collision ONLY when terrain fragments exist.
                        Console.WriteLine("[DEBUG] Calling GenerateAndAssignCollision...");
                        ObjTerrainImporter.GenerateAndAssignCollision(level, terrainFragments);
                        Console.WriteLine("[DEBUG] Finished GenerateAndAssignCollision call.");

                        ushort levelNumber = level.terrainEngine?.levelNumber ?? 0;
                        var terrainChunk = new Terrain(terrainFragments, levelNumber);

                        level.terrainEngine = terrainChunk;
                        Console.WriteLine($"[DEBUG] Assigned terrainEngine: fragments count = {level.terrainEngine?.fragments?.Count ?? -1}");
                        foreach (var fragment in level.terrainEngine.fragments.Take(3))
                        {
                            if (fragment?.model != null)
                            {
                                Console.WriteLine($"[DEBUG] terrainEngine Fragment: modelID={fragment.modelID}, vertexCount={fragment.model.vertexCount}, faceCount={fragment.model.faceCount}");
                            }
                        }
                        level.terrainChunks = new List<Terrain>(); // No chunks for imported terrain
                        level.collBytesChunks = new List<byte[]>(); // No collision bytes chunks for imported terrain
                        if (level.levelVariables != null)
                            level.levelVariables.chunkCount = 0;

                        Console.WriteLine($"✅ Replaced main terrain with {terrainFragments.Count} fragments");
                        Console.WriteLine($"✅ Replaced main collision with imported terrain collision");
                        
                        if (levelFrame.levelRenderer != null)
                        {
                            Console.WriteLine("🔄 Refreshing level renderer...");
                            try
                            {
                                var rendererType = levelFrame.levelRenderer.GetType();
                                var updateMethod = rendererType.GetMethod("UpdateLevel") ?? 
                                                 rendererType.GetMethod("Refresh") ?? 
                                                 rendererType.GetMethod("ReloadTerrain");
                                updateMethod?.Invoke(levelFrame.levelRenderer, null);
                            }
                            catch (Exception renderEx)
                            {
                                Console.WriteLine($"⚠️ Could not force renderer refresh: {renderEx.Message}");
                                Console.WriteLine("💡 Try moving the camera or switching view modes to see the imported terrain");
                            }
                        }

                        Console.WriteLine($"🎯 Successfully imported terrain from: {Path.GetFileName(objPath)}");
                        Console.WriteLine($"📊 Terrain stats:");
                        Console.WriteLine($"   - Fragments: {terrainFragments.Count}");
                        
                        foreach (var fragment in terrainFragments.Take(5))
                        {
                            if (fragment?.model != null)
                            {
                                Console.WriteLine($"   - Fragment: {fragment.model.vertexCount} vertices, {fragment.model.faceCount} faces");
                            }
                        }
                        
                        if (terrainFragments.Count > 5)
                        {
                            Console.WriteLine($"   ... and {terrainFragments.Count - 5} more fragments");
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ No terrain data was imported. The OBJ file might be empty or invalid.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error during OBJ import: {ex.Message}");
                    Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        /// 🆕 ENHANCED: Protect ALL geometry data that might have coordinate conversion issues
        /// </summary>
        private Dictionary<int, (Vector3 position, Quaternion rotation, Vector3 scale, Matrix4 inverseMatrix)> PreserveAllCuboidRotations(Level level)
        {
            var protection = new Dictionary<int, (Vector3, Quaternion, Vector3, Matrix4)>();
            
            if (level?.cuboids == null)
                return protection;
            
            var shipCameraIds = new HashSet<int>();
            if (level.levelVariables != null)
            {
                if (level.levelVariables.shipCameraStartID >= 0)
                    shipCameraIds.Add(level.levelVariables.shipCameraStartID);
                if (level.levelVariables.shipCameraEndID >= 0)
                    shipCameraIds.Add(level.levelVariables.shipCameraEndID);
            }
            
            foreach (var cuboid in level.cuboids)
            {
                bool isShipCamera = shipCameraIds.Contains(cuboid.id);
                
                // Ensure all cuboids have properly synced matrices
                cuboid.UpdateTransformMatrix();
                
                // Capture the data after ensuring proper matrices
                var rawBytes = cuboid.ToByteArray();
                var inverseMatrix = ReadMatrix4(rawBytes, 0x40);
                
                protection[cuboid.id] = (cuboid.position, cuboid.rotation, cuboid.scale, inverseMatrix);
                
                if (isShipCamera)
                {
                    Console.WriteLine($"🔒 Protected ship camera cuboid {cuboid.id}: rotation={cuboid.rotation}");
                }
            }
            
            Console.WriteLine($"🔒 Protected {protection.Count} total cuboids (including {shipCameraIds.Count} ship cameras)");
            return protection;
        }

        /// <summary>
        /// Captures ship camera rotation data before save to prevent corruption
        /// </summary>
        private Dictionary<int, (Vector3 position, Quaternion rotation, Vector3 scale, Matrix4 inverseMatrix)> PreserveShipCameraRotations(Level level)
        {
            var protection = new Dictionary<int, (Vector3, Quaternion, Vector3, Matrix4)>();

            if (level?.levelVariables == null || level.cuboids == null)
                return protection;

            var shipCameraIds = new[] {
                level.levelVariables.shipCameraStartID,
                level.levelVariables.shipCameraEndID
            }.Where(id => id >= 0);

            foreach (int camId in shipCameraIds)
            {
                var cuboid = level.cuboids.FirstOrDefault(c => c.id == camId);
                if (cuboid != null)
                {
                    // Capture the exact rotation data
                    var rawBytes = cuboid.ToByteArray();
                    var inverseMatrix = ReadMatrix4(rawBytes, 0x40);

                    protection[camId] = (cuboid.position, cuboid.rotation, cuboid.scale, inverseMatrix);

                    Console.WriteLine($"🔒 Protected ship camera cuboid {camId}: rotation={cuboid.rotation}");
                }
            }

            return protection;
        }

        /// <summary>
        /// Restores ship camera rotation data after save to prevent corruption
        /// </summary>
        private void RestoreShipCameraRotations(Level level, Dictionary<int, (Vector3 position, Quaternion rotation, Vector3 scale, Matrix4 inverseMatrix)> protection)
        {
            if (level?.cuboids == null || protection.Count == 0)
                return;

            foreach (var (cuboidId, (position, rotation, scale, inverseMatrix)) in protection)
            {
                var cuboid = level.cuboids.FirstOrDefault(c => c.id == cuboidId);
                if (cuboid != null)
                {
                    // Restore the exact rotation data
                    cuboid.position = position;
                    cuboid.rotation = rotation;
                    cuboid.scale = scale;
                    cuboid.UpdateTransformMatrix();

                    Console.WriteLine($"🔓 Restored ship camera cuboid {cuboidId}: rotation={cuboid.rotation}");
                }
            }

            Console.WriteLine("✅ Ship camera rotations protected during save operation");
        }

        /// <summary>
        /// Restores all cuboid rotation data after save to prevent corruption
        /// </summary>
        private void RestoreAllCuboidRotations(Level level, Dictionary<int, (Vector3 position, Quaternion rotation, Vector3 scale, Matrix4 inverseMatrix)> protection)
        {
            if (level?.cuboids == null || protection.Count == 0)
                return;

            foreach (var cuboid in level.cuboids)
            {
                if (protection.TryGetValue(cuboid.id, out var data))
                {
                    var (position, rotation, scale, inverseMatrix) = data;

                    // Restore the exact rotation data
                    cuboid.position = position;
                    cuboid.rotation = rotation;
                    cuboid.scale = scale;
                    cuboid.UpdateTransformMatrix();
                }
            }

            Console.WriteLine("✅ All cuboid rotations restored after save operation");
        }
    }
}
