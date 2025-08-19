using ImGuiNET;
using ReLunacy.Engine.EntityManagement;
using ReLunacy.Export;
using Vec3 = System.Numerics.Vector3;

namespace ReLunacy.Frames;

public class LevelExportFrame : Frame
{
    public new string FrameName
    {
        get => base.FrameName;
        set => base.FrameName = value;
    }
    protected override ImGuiWindowFlags WindowFlags { get; set; } = ImGuiWindowFlags.None;

    private ExportSettings settings = new();
    private string exportPath = "";
    private bool isExporting = false;
    private string lastExportStatus = "";
    private System.Numerics.Vector4 statusColor = new(1, 1, 1, 1); // White

    // Add constructor
    public LevelExportFrame()
    {
        FrameName = "Level Export";
    }

    public override void RenderAsWindow(float deltaTime)
    {
        if (!ImGui.Begin(FrameName, ref isOpen, WindowFlags))
        {
            ImGui.End();
            return;
        }

        Render(deltaTime);
        ImGui.End();
    }

    protected override void Render(float deltaTime)
    {
        RenderExportOptions();
        RenderExportButton();
        RenderExportStatus();
    }

    private void RenderExportOptions()
    {
        ImGui.SeparatorText("Export Settings");

        // Export format
        string[] formats = { "Wavefront (.obj)", "Collada (.dae)", "glTF (.gltf)" };
        int formatIndex = (int) settings.Format;
        if (ImGui.Combo("Format", ref formatIndex, formats, formats.Length))
        {
            settings.Format = (ExportFormat) formatIndex;
        }

        // Export mode
        string[] modes = { "Combined", "Separate Objects", "By Type", "By Material" };
        int modeIndex = (int) settings.Mode;
        if (ImGui.Combo("Export Mode", ref modeIndex, modes, modes.Length))
        {
            settings.Mode = (ExportMode) modeIndex;
        }

        // Object types to include
        ImGui.SeparatorText("Objects to Include");
        bool includeMobys = settings.IncludeMobys;
        bool includeTies = settings.IncludeTies;
        bool includeUFrags = settings.IncludeUFrags;
        bool includeVolumes = settings.IncludeVolumes;

        if (ImGui.Checkbox("Mobys", ref includeMobys))
            settings.IncludeMobys = includeMobys;
        if (ImGui.Checkbox("Ties", ref includeTies))
            settings.IncludeTies = includeTies;
        if (ImGui.Checkbox("UFrags (Terrain)", ref includeUFrags))
            settings.IncludeUFrags = includeUFrags;
        if (ImGui.Checkbox("Volumes", ref includeVolumes))
            settings.IncludeVolumes = includeVolumes;

        // Additional options
        ImGui.SeparatorText("Additional Options");
        bool includeTextures = settings.IncludeTextures;
        bool includeMaterials = settings.IncludeMaterials;
        bool includeColors = settings.IncludeColors;

        if (ImGui.Checkbox("Include Textures", ref includeTextures))
            settings.IncludeTextures = includeTextures;
        if (ImGui.Checkbox("Include Materials", ref includeMaterials))
            settings.IncludeMaterials = includeMaterials;
        if (ImGui.Checkbox("Include Colors", ref includeColors))
            settings.IncludeColors = includeColors;

        // Coordinate system
        string[] coordinateSystems = { "Z-Up", "Y-Up", "X-Up" };
        int coordIndex = (int) settings.CoordinateSystem;
        if (ImGui.Combo("Coordinate System", ref coordIndex, coordinateSystems, coordinateSystems.Length))
        {
            settings.CoordinateSystem = (CoordinateSystem) coordIndex;
        }

        float scaleFactor = settings.ScaleFactor;
        if (ImGui.SliderFloat("Scale Factor", ref scaleFactor, 0.1f, 10.0f))
            settings.ScaleFactor = scaleFactor;

        // Add checkbox for flipping model 90 degrees to the left
        bool flipModel = settings.FlipModel90Left;
        if (ImGui.Checkbox("Flip Model 90° Left (for compatibility)", ref flipModel))
            settings.FlipModel90Left = flipModel;
    }

    private void RenderExportButton()
    {
        ImGui.Separator();

        // Manual path input
        ImGui.Text("Export Path:");
        ImGui.SameLine();
        
        if (ImGui.Button("📁 Browse..."))
        {
            // Since we can't use native file dialogs easily, provide a text input for manual path entry
            // You could integrate with a file dialog library here if needed
            SetExportStatus("Please enter the full export path below", new System.Numerics.Vector4(1, 1, 0, 1)); // Yellow
        }
        
        // Text input for export path
        string pathInput = exportPath;
        if (ImGui.InputText("##ExportPath", ref pathInput, 500))
        {
            exportPath = pathInput;
            if (!string.IsNullOrEmpty(exportPath))
            {
                SetExportStatus($"Path set: {Path.GetFileName(exportPath)}", new System.Numerics.Vector4(0, 1, 0, 1)); // Green
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Auto-Generate"))
        {
            // Generate a timestamped filename
            string filter = settings.Format switch
            {
                ExportFormat.Wavefront => ".obj",
                ExportFormat.Collada => ".dae",
                ExportFormat.glTF => ".gltf",
                _ => ".obj"
            };

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            exportPath = Path.Combine(Environment.CurrentDirectory, $"exported_level_{timestamp}{filter}");
            
            LunaLog.LogInfo($"Auto-generated export path: {exportPath}");
            SetExportStatus($"Auto-generated: {Path.GetFileName(exportPath)}", new System.Numerics.Vector4(0, 1, 0, 1)); // Green
        }

        // Show current path
        ImGui.Text($"Current: {(string.IsNullOrEmpty(exportPath) ? "No path selected" : exportPath)}");

        // Export button
        ImGui.Separator();
        
        if (!string.IsNullOrEmpty(exportPath) && !isExporting)
        {
            if (ImGui.Button("🚀 Export Level", new System.Numerics.Vector2(150, 30)))
            {
                StartExport();
            }
        }
        else if (isExporting)
        {
            ImGui.Text("Exporting...");
            
            // Add a progress indicator
            ImGui.SameLine();
            ImGui.ProgressBar(-1.0f * (float)ImGui.GetTime(), new System.Numerics.Vector2(0, 0), "");
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Please set an export path first");
        }
    }

    private void RenderExportStatus()
    {
        if (!string.IsNullOrEmpty(lastExportStatus))
        {
            ImGui.Separator();
            ImGui.TextColored(statusColor, lastExportStatus);
            
            // Show additional help when export completes successfully
            if (lastExportStatus.Contains("✅") && !isExporting)
            {
                ImGui.SameLine();
                if (ImGui.Button("📂 Open Folder"))
                {
                    OpenFileLocation(exportPath);
                }
                
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1.0f), $"Full path: {exportPath}");
                
                // Show additional export options
                ImGui.Spacing();
                if (ImGui.Button("📋 Copy Path to Clipboard"))
                {
                    ImGui.SetClipboardText(exportPath);
                    SetExportStatus("✅ Export path copied to clipboard!", new System.Numerics.Vector4(0, 1, 0, 1)); // Green
                }
            }
        }

        // Show export progress details when exporting
        if (isExporting)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "⏳ Export in progress...");
            ImGui.Text("Check the console output for detailed progress information.");
        }
    }

    private void OpenFileLocation(string filePath)
    {
        try
        {
            // Get the directory path
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Environment.CurrentDirectory;
            }

            // Try to open the directory
            if (Directory.Exists(directory))
            {
                // Use cross-platform approach
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true,
                    Verb = "open"
                };
                
                System.Diagnostics.Process.Start(processInfo);
            }
            else
            {
                LunaLog.LogError($"Directory does not exist: {directory}");
                SetExportStatus("❌ Could not open folder - directory does not exist", new System.Numerics.Vector4(1, 0, 0, 1)); // Red
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to open file location: {ex.Message}");
            SetExportStatus($"❌ Failed to open folder: {ex.Message}", new System.Numerics.Vector4(1, 0, 0, 1)); // Red
        }
    }

    private void SetExportStatus(string message, System.Numerics.Vector4 color)
    {
        lastExportStatus = message;
        statusColor = color;
    }

    private async void StartExport()
    {
        isExporting = true;
        SetExportStatus("🚀 Starting export...", new System.Numerics.Vector4(0, 0.8f, 1, 1)); // Light blue

        try
        {
            var entities = EntityManager.Singleton.GetAllEntities();
            if (entities == null || entities.Count == 0)
            {
                LunaLog.LogError("No entities found to export!");
                SetExportStatus("❌ No entities found to export!", new System.Numerics.Vector4(1, 0, 0, 1)); // Red
                return;
            }

            LunaLog.LogInfo($"Starting export of {entities.Count} entities to {exportPath}");
            
            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                LunaLog.LogInfo($"Created directory: {directory}");
            }
            
            // Pass the AssetLoader for texture extraction
            var exporter = new ReLunacyLevelExporter(settings, Window.Singleton?.AssetLoader);
            await Task.Run(() => exporter.ExportLevel(exportPath, entities));

            // Check if file was actually created and get its size
            if (File.Exists(exportPath))
            {
                var fileInfo = new FileInfo(exportPath);
                LunaLog.LogInfo($"✅ Level exported successfully to: {exportPath}");
                LunaLog.LogInfo($"📁 Output file size: {fileInfo.Length / 1024} KB");
                
                SetExportStatus($"✅ Export completed! File size: {fileInfo.Length / 1024} KB", new System.Numerics.Vector4(0, 1, 0, 1)); // Green
            }
            else
            {
                LunaLog.LogError("❌ Export completed but output file was not found!");
                SetExportStatus("❌ Export failed - no output file created", new System.Numerics.Vector4(1, 0, 0, 1)); // Red
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"❌ Export failed: {ex.Message}");
            LunaLog.LogError($"Stack trace: {ex.StackTrace}");
            SetExportStatus($"❌ Export failed: {ex.Message}", new System.Numerics.Vector4(1, 0, 0, 1)); // Red
        }
        finally
        {
            isExporting = false;
        }
    }
}

public class ExportSettings
{
    public ExportFormat Format { get; set; } = ExportFormat.Wavefront;
    public ExportMode Mode { get; set; } = ExportMode.Combined;
    public bool IncludeMobys { get; set; } = true;
    public bool IncludeTies { get; set; } = true;
    public bool IncludeUFrags { get; set; } = true;
    public bool IncludeVolumes { get; set; } = false;
    public bool IncludeTextures { get; set; } = true;
    public bool IncludeMaterials { get; set; } = true;
    public bool IncludeColors { get; set; } = true;
    public CoordinateSystem CoordinateSystem { get; set; } = CoordinateSystem.ZUp;
    public float ScaleFactor { get; set; } = 1.0f;
    public bool FlipModel90Left { get; set; } = false; // New option for flipping model
}

public enum ExportFormat
{
    Wavefront = 0,
    Collada = 1,
    glTF = 2
}

public enum ExportMode
{
    Combined = 0,
    Separate = 1,
    ByType = 2,
    ByMaterial = 3
}

public enum CoordinateSystem
{
    ZUp = 0,
    YUp = 1,
    XUp = 2
}
