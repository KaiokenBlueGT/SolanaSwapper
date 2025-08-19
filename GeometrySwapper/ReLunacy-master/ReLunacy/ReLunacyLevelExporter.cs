using OpenTK.Mathematics;
using ReLunacy.Engine.EntityManagement;
using ReLunacy.Frames;
using System.Globalization;
using OTKVector3 = OpenTK.Mathematics.Vector3;
using OTKVector4 = OpenTK.Mathematics.Vector4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace ReLunacy.Export;

public class ReLunacyLevelExporter
{
    private static readonly CultureInfo enUS = CultureInfo.CreateSpecificCulture("en-US");
    private readonly ExportSettings settings;
    private readonly List<int> usedMaterials = new List<int>();
    private readonly HashSet<CTexture> exportedTextures = new HashSet<CTexture>();
    private readonly AssetLoader? assetLoader;

    public ReLunacyLevelExporter(ExportSettings settings, AssetLoader? assetLoader = null)
    {
        this.settings = settings;
        this.assetLoader = assetLoader;
    }

    public void ExportLevel(string filePath, List<Entity> entities)
    {
        LunaLog.LogInfo($"🚀 Starting ReLunacy level export to {Path.GetFileName(filePath)}");
        LunaLog.LogInfo($"📊 Found {entities.Count} total entities");
        
        switch (settings.Format)
        {
            case ExportFormat.Wavefront:
                ExportWavefront(filePath, entities);
                break;
            case ExportFormat.Collada:
                ExportCollada(filePath, entities);
                break;
            case ExportFormat.glTF:
                ExportGLTF(filePath, entities);
                break;
        }
        
        LunaLog.LogInfo($"✅ Export completed successfully!");
    }

    private void ExportWavefront(string filePath, List<Entity> entities)
    {
        string directory = Path.GetDirectoryName(filePath) ?? "";
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        using var objWriter = new StreamWriter(filePath);
        StreamWriter? mtlWriter = null;

        if (settings.IncludeMaterials)
        {
            string mtlPath = Path.Combine(directory, fileName + ".mtl");
            mtlWriter = new StreamWriter(mtlPath);
            objWriter.WriteLine($"mtllib {fileName}.mtl");
        }

        try
        {
            ExportWavefrontData(objWriter, mtlWriter, entities, directory);
        }
        finally
        {
            mtlWriter?.Dispose();
        }
    }

    private void ExportWavefrontData(StreamWriter objWriter, StreamWriter? mtlWriter, List<Entity> entities, string exportDirectory)
    {
        int vertexOffset = 0;
        int materialId = 0;

        // Filter entities based on settings
        var filteredEntities = FilterEntities(entities);
        
        LunaLog.LogInfo($"📋 Filtered to {filteredEntities.Count} entities for export");
        LunaLog.LogInfo($"🔧 Export mode: {settings.Mode}");

        switch (settings.Mode)
        {
            case ExportMode.Combined:
                ExportCombined(objWriter, mtlWriter, filteredEntities, exportDirectory, ref vertexOffset, ref materialId);
                break;
            case ExportMode.Separate:
                ExportSeparate(objWriter, mtlWriter, filteredEntities, exportDirectory, ref vertexOffset, ref materialId);
                break;
            case ExportMode.ByType:
                ExportByType(objWriter, mtlWriter, filteredEntities, exportDirectory, ref vertexOffset, ref materialId);
                break;
            case ExportMode.ByMaterial:
                ExportByMaterial(objWriter, mtlWriter, filteredEntities, exportDirectory, ref vertexOffset, ref materialId);
                break;
        }
        
        LunaLog.LogInfo($"📈 Export statistics: {vertexOffset} vertices, {materialId} materials, {exportedTextures.Count} textures");
        
        if (settings.IncludeTextures && exportedTextures.Count > 0)
        {
            LunaLog.LogInfo($"🎨 Exported {exportedTextures.Count} textures as DDS files");
            LunaLog.LogInfo($"💡 Tip: Convert DDS files to PNG for better 3D software compatibility:");
            LunaLog.LogInfo($"    - Use tools like ImageMagick, GIMP, or online converters");
            LunaLog.LogInfo($"    - Or manually update .mtl file texture references from .dds to .png");
        }
    }

    private void WriteMaterial(StreamWriter mtlWriter, string materialName, bool clampUV = false, Entity? entity = null, string exportDirectory = "")
    {
        mtlWriter.WriteLine($"newmtl {materialName}");
        mtlWriter.WriteLine("Ns 1000");
        mtlWriter.WriteLine("Ka 1.000000 1.000000 1.000000");
        mtlWriter.WriteLine("Kd 1.000000 1.000000 1.000000");
        mtlWriter.WriteLine("Ni 1.000000");
        mtlWriter.WriteLine("d 1.000000");
        mtlWriter.WriteLine("illum 1");
        
        // Try to extract texture information if available
        var textureInfo = GetTextureInfo(entity, materialName);
        
        // Write texture map if enabled and available
        if (settings.IncludeMaterials && !string.IsNullOrEmpty(textureInfo.textureName))
        {
            // Export the actual texture file if possible
            if (textureInfo.texture != null && settings.IncludeTextures)
            {
                ExportTextureFile(textureInfo.texture, textureInfo.textureName, exportDirectory);
                // Reference DDS file (which is what we can export)
                mtlWriter.WriteLine($"map_Kd {textureInfo.textureName}.dds");
            }
            else
            {
                mtlWriter.WriteLine($"map_Kd {textureInfo.textureName}.png");
            }
        }
        else if (settings.IncludeMaterials)
        {
            // Fallback texture reference
            mtlWriter.WriteLine($"map_Kd {materialName}.png");
        }
        
        mtlWriter.WriteLine(); // Empty line for readability
        
        LunaLog.LogDebug($"Created material: {materialName} with texture: {textureInfo.textureName ?? materialName}");
    }

    private (CTexture? texture, string? textureName) GetTextureInfo(Entity? entity, string fallbackName)
    {
        if (entity?.drawable != null && assetLoader != null)
        {
            try
            {
                // Try to get shader information from the entity through reflection
                var drawable = entity.drawable;
                var drawableType = drawable.GetType();

                // Look for shader or material property/field
                var shaderProperty = drawableType.GetProperty("shader") ?? 
                                   drawableType.GetProperty("material") ?? 
                                   drawableType.GetProperty("Material");

                var shaderField = drawableType.GetField("shader") ?? 
                                drawableType.GetField("material") ?? 
                                drawableType.GetField("Material");

                object? shader = null;
                if (shaderProperty != null)
                {
                    shader = shaderProperty.GetValue(drawable);
                }
                else if (shaderField != null)
                {
                    shader = shaderField.GetValue(drawable);
                }

                if (shader != null)
                {
                    // Try to cast to CShader if it's that type
                    if (shader is CShader cShader)
                    {
                        // Get the primary texture (albedo)
                        if (cShader.albedo != null)
                        {
                            string textureName = SanitizeTextureName(cShader.albedo.name ?? $"texture_{cShader.albedo.GetHashCode():X8}");
                            return (cShader.albedo, textureName);
                        }
                    }
                    else
                    {
                        // Try reflection to access shader properties/fields
                        var shaderType = shader.GetType();
                        
                        // Try property first
                        var albedoProperty = shaderType.GetProperty("albedo");
                        if (albedoProperty != null)
                        {
                            var albedo = albedoProperty.GetValue(shader) as CTexture;
                            if (albedo != null)
                            {
                                string textureName = SanitizeTextureName(albedo.name ?? $"texture_{albedo.GetHashCode():X8}");
                                return (albedo, textureName);
                            }
                        }
                        
                        // Try field as fallback
                        var albedoField = shaderType.GetField("albedo");
                        if (albedoField != null)
                        {
                            var albedo = albedoField.GetValue(shader) as CTexture;
                            if (albedo != null)
                            {
                                string textureName = SanitizeTextureName(albedo.name ?? $"texture_{albedo.GetHashCode():X8}");
                                return (albedo, textureName);
                            }
                        }
                    }
                }
                
                // Also try to get shader from mesh directly if entity has mesh data
                if (entity.instance is Region.CMobyInstance mobyInstance && mobyInstance.moby != null)
                {
                    return TryGetTextureFromMoby(mobyInstance.moby);
                }
                else if (entity.instance is CZone.CTieInstance tieInstance && tieInstance.tie != null)
                {
                    return TryGetTextureFromTie(tieInstance.tie);
                }
                // Handle UFrag terrain entities
                else if (entity.instance is CZone.UFrag ufrag)
                {
                    var ufragShader = ufrag.GetShader();
                    if (ufragShader != null && ufragShader.albedo != null)
                    {
                        string textureName = SanitizeTextureName(ufragShader.albedo.name ?? $"texture_{ufragShader.albedo.GetHashCode():X8}");
                        return (ufragShader.albedo, textureName);
                    }
                }
            }
            catch (Exception ex)
            {
                LunaLog.LogDebug($"Failed to extract texture info from entity {entity.name}: {ex.Message}");
            }
        }

        return (null, null);
    }
    
    private (CTexture? texture, string? textureName) TryGetTextureFromMoby(object moby)
    {
        try
        {
            var mobyType = moby.GetType();
            
            // Try to get bangles field
            var banglesField = mobyType.GetField("bangles");
            if (banglesField != null)
            {
                var bangles = banglesField.GetValue(moby);
                if (bangles is Array banglesArray && banglesArray.Length > 0)
                {
                    var firstBangle = banglesArray.GetValue(0);
                    if (firstBangle != null)
                    {
                        var meshesField = firstBangle.GetType().GetField("meshes");
                        if (meshesField != null)
                        {
                            var meshes = meshesField.GetValue(firstBangle);
                            if (meshes is Array meshArray && meshArray.Length > 0)
                            {
                                var firstMesh = meshArray.GetValue(0);
                                if (firstMesh != null)
                                {
                                    var shaderField = firstMesh.GetType().GetField("shader");
                                    if (shaderField != null)
                                    {
                                        var shader = shaderField.GetValue(firstMesh) as CShader;
                                        if (shader?.albedo != null)
                                        {
                                            string textureName = SanitizeTextureName(shader.albedo.name ?? $"texture_{shader.albedo.GetHashCode():X8}");
                                            return (shader.albedo, textureName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogDebug($"Failed to extract texture from moby: {ex.Message}");
        }
        
        return (null, null);
    }
    
    private (CTexture? texture, string? textureName) TryGetTextureFromTie(object tie)
    {
        try
        {
            var tieType = tie.GetType();
            
            // Try to get meshes field
            var meshesField = tieType.GetField("meshes");
            if (meshesField != null)
            {
                var meshes = meshesField.GetValue(tie);
                if (meshes is Array meshArray && meshArray.Length > 0)
                {
                    var firstMesh = meshArray.GetValue(0);
                    if (firstMesh != null)
                    {
                        var shaderField = firstMesh.GetType().GetField("shader");
                        if (shaderField != null)
                        {
                            var shader = shaderField.GetValue(firstMesh) as CShader;
                            if (shader?.albedo != null)
                            {
                                string textureName = SanitizeTextureName(shader.albedo.name ?? $"texture_{shader.albedo.GetHashCode():X8}");
                                return (shader.albedo, textureName);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogDebug($"Failed to extract texture from tie: {ex.Message}");
        }
        
        return (null, null);
    }

    private void ExportTextureFile(CTexture texture, string textureName, string exportDirectory)
    {
        if (exportedTextures.Contains(texture))
            return; // Already exported
    
        try
        {
            string ddsFilePath = Path.Combine(exportDirectory, $"{textureName}.dds");
            using (var ddsStream = File.Create(ddsFilePath))
            {
                texture.ExportToDDS(ddsStream, false);
            }
            // PNG export for any CTexture with DXT5 data
            try
            {
                // Try to decompress DXT5 data
                var decompressMethod = typeof(CTexture).Assembly.GetType("LibReplanetizer.Texture")?.GetMethod("DecompressDxt5", new[] { typeof(byte[]), typeof(int), typeof(int) });
                byte[]? imgData = null;
                if (decompressMethod != null)
                {
                    imgData = (byte[]?)decompressMethod.Invoke(null, new object[] { texture.data, texture.width, texture.height });
                }
                if (imgData != null)
                {
                    var img = Image.LoadPixelData<Bgra32>(imgData, texture.width, texture.height);
                    string pngFilePath = Path.Combine(exportDirectory, $"{textureName}.png");
                    img.SaveAsPng(File.Create(pngFilePath));
                    LunaLog.LogDebug($"Exported PNG for texture: {textureName}");
                }
                else
                {
                    LunaLog.LogError($"Failed to decompress DXT5 for PNG export: {textureName}");
                }
            }
            catch (Exception pngEx)
            {
                LunaLog.LogError($"Failed to export PNG for texture {textureName}: {pngEx.Message}");
            }
            exportedTextures.Add(texture);
            LunaLog.LogDebug($"Exported texture: {textureName}.dds and attempted .png");
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to export texture {textureName}: {ex.Message}");
        }
    }

    private string SanitizeTextureName(string textureName)
    {
        // Remove invalid filename characters and sanitize
        string sanitized = Path.GetFileNameWithoutExtension(textureName);
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(c, '_');
        }
        return sanitized;
    }

    private void WriteMaterialIfNeeded(StreamWriter? mtlWriter, int materialId, string materialName, Entity? entity = null, string exportDirectory = "")
    {
        if (mtlWriter != null && !usedMaterials.Contains(materialId))
        {
            WriteMaterial(mtlWriter, materialName, false, entity, exportDirectory);
            usedMaterials.Add(materialId);
        }
    }

    // Update method signatures to include export directory
    private void ExportCombined(StreamWriter objWriter, StreamWriter? mtlWriter, List<Entity> entities, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        objWriter.WriteLine("o CombinedLevel");

        foreach (var entity in entities)
        {
            ExportEntityGeometry(objWriter, mtlWriter, entity, exportDirectory, ref vertexOffset, ref materialId);
        }
    }

    private void ExportSeparate(StreamWriter objWriter, StreamWriter? mtlWriter, List<Entity> entities, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        foreach (var entity in entities)
        {
            objWriter.WriteLine($"o Entity_{entity.GetHashCode():X8}");
            ExportEntityGeometry(objWriter, mtlWriter, entity, exportDirectory, ref vertexOffset, ref materialId);
        }
    }

    private void ExportByType(StreamWriter objWriter, StreamWriter? mtlWriter, List<Entity> entities, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        var grouped = entities.GroupBy(e => e.GetType().Name);

        foreach (var group in grouped)
        {
            objWriter.WriteLine($"o {group.Key}");
            foreach (var entity in group)
            {
                ExportEntityGeometry(objWriter, mtlWriter, entity, exportDirectory, ref vertexOffset, ref materialId);
            }
        }
    }

    private void ExportByMaterial(StreamWriter objWriter, StreamWriter? mtlWriter, List<Entity> entities, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        // This would require grouping by material properties
        // Implementation depends on how ReLunacy handles materials
        ExportCombined(objWriter, mtlWriter, entities, exportDirectory, ref vertexOffset, ref materialId);
    }

    private void ExportEntityGeometry(StreamWriter objWriter, StreamWriter? mtlWriter, Entity entity, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            // Check what type of entity this is and extract geometry accordingly
            if (entity.instance is Region.CMobyInstance mobyInstance)
            {
                ExportMobyGeometry(objWriter, mtlWriter, entity, mobyInstance, exportDirectory, ref vertexOffset, ref materialId);
            }
            else if (entity.instance is CZone.CTieInstance tieInstance)
            {
                ExportTieGeometry(objWriter, mtlWriter, entity, tieInstance, exportDirectory, ref vertexOffset, ref materialId);
            }
            else if (entity.instance is CZone.UFrag ufragInstance)
            {
                ExportUFragGeometry(objWriter, mtlWriter, entity, ufragInstance, exportDirectory, ref vertexOffset, ref materialId);
            }
            else if (entity.instance is Region.CVolumeInstance volumeInstance)
            {
                ExportVolumeGeometry(objWriter, mtlWriter, entity, volumeInstance, exportDirectory, ref vertexOffset, ref materialId);
            }
            else
            {
                LunaLog.LogDebug($"Unknown entity type: {entity.instance?.GetType().Name ?? "null"}");
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to export entity {entity.name}: {ex.Message}");
        }
    }

    // Update all the geometry export methods to include export directory parameter
    private void ExportMobyGeometry(StreamWriter objWriter, StreamWriter? mtlWriter, Entity entity, Region.CMobyInstance mobyInstance, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            // For mobys, we need to access the moby model data
            var moby = mobyInstance.moby;
            if (moby == null)
            {
                LunaLog.LogDebug($"Moby {entity.name} has null moby reference");
                return;
            }

            // Try different possible mesh container properties
            bool hasGeometry = false;
            var mobyType = moby.GetType();
            LunaLog.LogDebug($"Moby {entity.name} type: {mobyType.Name}");
            
            // Option 1: Try bangles (field)
            try
            {
                var banglesField = mobyType.GetField("bangles");
                if (banglesField != null)
                {
                    var bangles = banglesField.GetValue(moby);
                    if (bangles is Array banglesArray && banglesArray.Length > 0)
                    {
                        LunaLog.LogDebug($"Moby {entity.name} has {banglesArray.Length} bangles (field)");
                        foreach (var bangle in banglesArray)
                        {
                            var meshesField = bangle.GetType().GetField("meshes");
                            if (meshesField != null)
                            {
                                var meshes = meshesField.GetValue(bangle);
                                if (meshes is Array meshArray && meshArray.Length > 0)
                                {
                                    LunaLog.LogDebug($"Moby {entity.name} bangle has {meshArray.Length} meshes (field)");
                                    foreach (var mesh in meshArray)
                                    {
                                        if (TryExportMobyMesh(objWriter, mtlWriter, moby, mesh, entity, exportDirectory, ref vertexOffset, ref materialId))
                                            hasGeometry = true;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        LunaLog.LogDebug($"Moby {entity.name} bangles field returned: {bangles?.GetType().Name ?? "null"}");
                    }
                }
                else
                {
                    LunaLog.LogDebug($"Moby {entity.name} has no bangles field");
                }
            }
            catch (Exception ex)
            {
                LunaLog.LogDebug($"Bangles field approach failed for {entity.name}: {ex.Message}");
            }

            // Option 2: Try bangles property (fallback)
            if (!hasGeometry)
            {
                try
                {
                    var bangles = mobyType.GetProperty("bangles")?.GetValue(moby);
                    if (bangles is Array banglesArray && banglesArray.Length > 0)
                    {
                        LunaLog.LogDebug($"Moby {entity.name} has {banglesArray.Length} bangles (property)");
                        foreach (var bangle in banglesArray)
                        {
                            var meshes = bangle.GetType().GetProperty("meshes")?.GetValue(bangle);
                            if (meshes is Array meshArray && meshArray.Length > 0)
                            {
                                LunaLog.LogDebug($"Moby {entity.name} bangle has {meshArray.Length} meshes (property)");
                                foreach (var mesh in meshArray)
                                {
                                    if (TryExportMobyMesh(objWriter, mtlWriter, moby, mesh, entity, exportDirectory, ref vertexOffset, ref materialId))
                                        hasGeometry = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        LunaLog.LogDebug($"Moby {entity.name} bangles property returned: {bangles?.GetType().Name ?? "null"}");
                    }
                }
                catch (Exception ex)
                {
                    LunaLog.LogDebug($"Bangles property approach failed for {entity.name}: {ex.Message}");
                }
            }

            // Option 3: Try meshes directly (field and property)
            if (!hasGeometry)
            {
                try
                {
                    // Try field first
                    var meshesField = mobyType.GetField("meshes");
                    var meshes = meshesField?.GetValue(moby);
                    
                    // If field doesn't exist, try property
                    if (meshes == null)
                    {
                        meshes = mobyType.GetProperty("meshes")?.GetValue(moby);
                    }
                    
                    if (meshes is Array meshArray && meshArray.Length > 0)
                    {
                        LunaLog.LogDebug($"Moby {entity.name} has {meshArray.Length} direct meshes");
                        foreach (var mesh in meshArray)
                        {
                            if (TryExportMobyMesh(objWriter, mtlWriter, moby, mesh, entity, exportDirectory, ref vertexOffset, ref materialId))
                                hasGeometry = true;
                        }
                    }
                    else
                    {
                        LunaLog.LogDebug($"Moby {entity.name} meshes returned: {meshes?.GetType().Name ?? "null"}");
                    }
                }
                catch (Exception ex)
                {
                    LunaLog.LogDebug($"Direct meshes approach failed for {entity.name}: {ex.Message}");
                }
            }

            // Debug all properties and fields
            var allProperties = mobyType.GetProperties();
            var allFields = mobyType.GetFields();
            LunaLog.LogDebug($"Moby {entity.name} available properties: {string.Join(", ", allProperties.Select(p => p.Name))}");
            LunaLog.LogDebug($"Moby {entity.name} available fields: {string.Join(", ", allFields.Select(f => f.Name))}");

            if (!hasGeometry)
            {
                LunaLog.LogDebug($"Moby {entity.name} has no exportable geometry");
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to export moby geometry for {entity.name}: {ex.Message}");
        }
    }

    private bool TryExportMobyMesh(StreamWriter objWriter, StreamWriter? mtlWriter, object moby, object mesh, Entity entity, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            // Try to call GetBuffers method using reflection
            var getBuffersMethod = moby.GetType().GetMethod("GetBuffers");
            if (getBuffersMethod != null)
            {
                var parameters = new object?[] { mesh, null, null, null };
                
                // The GetBuffers method for CMoby doesn't return bool, it's a void method with out parameters
                getBuffersMethod.Invoke(moby, parameters);
                
                var indices = parameters[1] as uint[];
                var vPositions = parameters[2] as float[];
                var vTexCoords = parameters[3] as float[];

                if (indices != null && vPositions != null)
                {
                    LunaLog.LogDebug($"Successfully extracted mesh data for moby {entity.name}: {vPositions.Length / 3} vertices, {indices.Length / 3} triangles");
                    ExportMeshData(objWriter, mtlWriter, vPositions, vTexCoords, indices, entity.transform, exportDirectory, ref vertexOffset, ref materialId, $"Moby_{entity.name}", entity);
                    return true;
                }
                else
                {
                    LunaLog.LogDebug($"GetBuffers returned null data for moby {entity.name}: indices={indices?.Length ?? -1}, positions={vPositions?.Length ?? -1}");
                }
            }
            else
            {
                LunaLog.LogDebug($"GetBuffers method not found for moby {entity.name}");
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogDebug($"Failed to export mesh for moby {entity.name}: {ex.Message}");
        }
        
        return false;
    }

    private void ExportTieGeometry(StreamWriter objWriter, StreamWriter? mtlWriter, Entity entity, CZone.CTieInstance tieInstance, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            // For ties, we need to access the tie model data
            var tie = tieInstance.tie;
            if (tie == null)
            {
                LunaLog.LogDebug($"Tie {entity.name} has null tie reference");
                return;
            }

            bool hasGeometry = false;

            // Try to access meshes field first (CTie uses public field)
            try
            {
                var tieType = tie.GetType();
                LunaLog.LogDebug($"Tie {entity.name} type: {tieType.Name}");
                
                // Try field first
                var meshesField = tieType.GetField("meshes");
                if (meshesField != null)
                {
                    var meshes = meshesField.GetValue(tie);
                    if (meshes is Array meshArray && meshArray.Length > 0)
                    {
                        LunaLog.LogDebug($"Tie {entity.name} has {meshArray.Length} meshes (field)");
                        foreach (var mesh in meshArray)
                        {
                            if (TryExportTieMesh(objWriter, mtlWriter, tie, mesh, entity, exportDirectory, ref vertexOffset, ref materialId))
                                hasGeometry = true;
                        }
                    }
                    else
                    {
                        LunaLog.LogDebug($"Tie {entity.name} meshes field returned: {meshes?.GetType().Name ?? "null"}");
                    }
                }
                
                // Try property as fallback
                if (!hasGeometry)
                {
                    var meshes = tieType.GetProperty("meshes")?.GetValue(tie);
                    if (meshes is Array meshArray && meshArray.Length > 0)
                    {
                        LunaLog.LogDebug($"Tie {entity.name} has {meshArray.Length} meshes (property)");
                        foreach (var mesh in meshArray)
                        {
                            if (TryExportTieMesh(objWriter, mtlWriter, tie, mesh, entity, exportDirectory, ref vertexOffset, ref materialId))
                                hasGeometry = true;
                        }
                    }
                    else
                    {
                        LunaLog.LogDebug($"Tie {entity.name} meshes property returned: {meshes?.GetType().Name ?? "null"}");
                        
                        // Try alternative property names
                        var altProperties = new[] { "Meshes", "mesh", "Mesh", "geometry", "Geometry" };
                        foreach (var propName in altProperties)
                        {
                            var altProperty = tieType.GetProperty(propName);
                            if (altProperty != null)
                            {
                                var altMeshes = altProperty.GetValue(tie);
                                LunaLog.LogDebug($"Tie {entity.name} found alternative property '{propName}': {altMeshes?.GetType().Name ?? "null"}");
                                if (altMeshes is Array altMeshArray && altMeshArray.Length > 0)
                                {
                                    foreach (var mesh in altMeshArray)
                                    {
                                        if (TryExportTieMesh(objWriter, mtlWriter, tie, mesh, entity, exportDirectory, ref vertexOffset, ref materialId))
                                            hasGeometry = true;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Debug all properties and fields
                var allProperties = tieType.GetProperties();
                var allFields = tieType.GetFields();
                LunaLog.LogDebug($"Tie {entity.name} available properties: {string.Join(", ", allProperties.Select(p => p.Name))}");
                LunaLog.LogDebug($"Tie {entity.name} available fields: {string.Join(", ", allFields.Select(f => f.Name))}");
            }
            catch (Exception ex)
            {
                LunaLog.LogDebug($"Meshes approach failed for tie {entity.name}: {ex.Message}");
            }

            if (!hasGeometry)
            {
                LunaLog.LogDebug($"Tie {entity.name} has no exportable geometry");
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to export tie geometry for {entity.name}: {ex.Message}");
        }
    }

    private bool TryExportTieMesh(StreamWriter objWriter, StreamWriter? mtlWriter, object tie, object mesh, Entity entity, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            // Try to call GetBuffers method using reflection
            var getBuffersMethod = tie.GetType().GetMethod("GetBuffers");
            if (getBuffersMethod != null)
            {
                var parameters = new object?[] { mesh, null, null, null };
                
                // The GetBuffers method for CTie doesn't return bool, it's a void method with out parameters
                getBuffersMethod.Invoke(tie, parameters);
                
                var indices = parameters[1] as uint[];
                var vPositions = parameters[2] as float[];
                var vTexCoords = parameters[3] as float[];

                if (indices != null && vPositions != null)
                {
                    LunaLog.LogDebug($"Successfully extracted mesh data for tie {entity.name}: {vPositions.Length / 3} vertices, {indices.Length / 3} triangles");
                    ExportMeshData(objWriter, mtlWriter, vPositions, vTexCoords, indices, entity.transform, exportDirectory, ref vertexOffset, ref materialId, $"Tie_{entity.name}", entity);
                    return true;
                }
                else
                {
                    LunaLog.LogDebug($"GetBuffers returned null data for tie {entity.name}: indices={indices?.Length ?? -1}, positions={vPositions?.Length ?? -1}");
                }
            }
            else
            {
                LunaLog.LogDebug($"GetBuffers method not found for tie {entity.name}");
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogDebug($"Failed to export mesh for tie {entity.name}: {ex.Message}");
        }
        
        return false;
    }

    private void ExportUFragGeometry(StreamWriter objWriter, StreamWriter? mtlWriter, Entity entity, CZone.UFrag ufragInstance, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            var vPositions = ufragInstance.GetVertPositions();
            var vTexCoords = ufragInstance.GetUVs();
            var indices = ufragInstance.GetIndices();
            if (vPositions != null && indices != null)
            {
                // Attempt to export terrain texture if available
                var textureInfo = GetTextureInfo(entity, $"UFrag_{entity.name}");
                if (settings.IncludeTextures && textureInfo.texture != null && !string.IsNullOrEmpty(textureInfo.textureName))
                {
                    ExportTextureFile(textureInfo.texture, textureInfo.textureName, exportDirectory);
                }
                ExportMeshData(objWriter, mtlWriter, vPositions, vTexCoords, indices, entity.transform, exportDirectory, ref vertexOffset, ref materialId, $"UFrag_{entity.name}", entity);
            }
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to export UFrag geometry for {entity.name}: {ex.Message}");
        }
    }

    private void ExportVolumeGeometry(StreamWriter objWriter, StreamWriter? mtlWriter, Entity entity, Region.CVolumeInstance volumeInstance, string exportDirectory, ref int vertexOffset, ref int materialId)
    {
        try
        {
            // For volumes, we create a simple box mesh
            var transform = entity.transform;
            var scale = transform.scale;
            
            // Create a unit cube and scale it
            float[] vertices = 
            {
                // Front face
                -0.5f, -0.5f,  0.5f,  // 0
                 0.5f, -0.5f,  0.5f,  // 1
                 0.5f,  0.5f,  0.5f,  // 2
                -0.5f,  0.5f,  0.5f,  // 3
                
                // Back face
                -0.5f, -0.5f, -0.5f,  // 4
                 0.5f, -0.5f, -0.5f,  // 5
                 0.5f,  0.5f, -0.5f,  // 6
                -0.5f,  0.5f, -0.5f   // 7
            };
            
            float[] uvs = 
            {
                0.0f, 0.0f,  0.0f, 1.0f,  1.0f, 1.0f,  1.0f, 0.0f,  // Front
                0.0f, 0.0f,  0.0f, 1.0f,  1.0f, 1.0f,  1.0f, 0.0f   // Back
            };
            
            uint[] indices = 
            {
                // Front face
                0, 1, 2,  0, 2, 3,
                // Back face
                4, 6, 5,  4, 7, 6,
                // Left face
                4, 0, 3,  4, 3, 7,
                // Right face
                1, 5, 6,  1, 6, 2,
                // Top face
                3, 2, 6,  3, 6, 7,
                // Bottom face
                4, 5, 1,  4, 1, 0
            };

            ExportMeshData(objWriter, mtlWriter, vertices, uvs, indices, transform, exportDirectory, ref vertexOffset, ref materialId, $"Volume_{entity.name}", entity);
        }
        catch (Exception ex)
        {
            LunaLog.LogError($"Failed to export volume geometry for {entity.name}: {ex.Message}");
        }
    }

    private void ExportMeshData(StreamWriter objWriter, StreamWriter? mtlWriter, float[] vPositions, float[]? vTexCoords, uint[] indices, Transform transform, string exportDirectory, ref int vertexOffset, ref int materialId, string objectName, Entity? entity = null)
    {
        if (vPositions == null || indices == null)
        {
            LunaLog.LogDebug($"Skipping {objectName} - missing vertex or index data");
            return;
        }

        // Try to get material information from the entity
        string materialName = GetMaterialName(entity, materialId);
        WriteMaterialIfNeeded(mtlWriter, materialId, materialName, entity, exportDirectory);

        // Write vertices
        int vertexCount = vPositions.Length / 3;
        for (int i = 0; i < vertexCount; i++)
        {
            var vertex = new OTKVector3(
                vPositions[i * 3 + 0],
                vPositions[i * 3 + 1], 
                vPositions[i * 3 + 2]
            );

            var transformedVertex = TransformVertex(vertex, transform.GetLocalToWorldMatrix());
            objWriter.WriteLine($"v {transformedVertex.X.ToString("G", enUS)} {transformedVertex.Y.ToString("G", enUS)} {transformedVertex.Z.ToString("G", enUS)}");
        }

        // Write UVs if available
        if (vTexCoords != null)
        {
            int uvCount = vTexCoords.Length / 2;
            for (int i = 0; i < uvCount; i++)
            {
                float u = vTexCoords[i * 2 + 0];
                float v = 1.0f - vTexCoords[i * 2 + 1]; // Flip V coordinate for OBJ format
                objWriter.WriteLine($"vt {u.ToString("G", enUS)} {v.ToString("G", enUS)}");
            }
        }

        // Write object and material usage
        objWriter.WriteLine($"o {objectName}");
        if (settings.IncludeMaterials && mtlWriter != null)
        {
            objWriter.WriteLine($"usemtl {materialName}");
            objWriter.WriteLine($"g Texture_{materialId:X4}");
        }

        // Write faces
        for (int i = 0; i < indices.Length; i += 3)
        {
            int v1 = (int)indices[i] + 1 + vertexOffset;
            int v2 = (int)indices[i + 1] + 1 + vertexOffset;
            int v3 = (int)indices[i + 2] + 1 + vertexOffset;

            if (vTexCoords != null)
            {
                // Include UV indices
                objWriter.WriteLine($"f {v1}/{v1} {v2}/{v2} {v3}/{v3}");
            }
            else
            {
                // Only vertex indices
                objWriter.WriteLine($"f {v1} {v2} {v3}");
            }
        }

        vertexOffset += vertexCount;
        materialId++;
    }

    private string GetMaterialName(Entity? entity, int materialId)
    {
        // Try to get a meaningful material name from the entity
        var textureInfo = GetTextureInfo(entity, $"mtl_{materialId:X4}");
        if (!string.IsNullOrEmpty(textureInfo.textureName))
        {
            return $"mtl_{textureInfo.textureName}";
        }

        // Fallback to generic material name
        return $"mtl_{materialId:X4}";
    }

    private List<Entity> FilterEntities(List<Entity> entities)
    {
        return entities.Where(entity =>
        {
            // Filter based on the actual instance type rather than entity class name
            if (entity.instance is Region.CMobyInstance)
                return settings.IncludeMobys;
            else if (entity.instance is CZone.CTieInstance)
                return settings.IncludeTies;
            else if (entity.instance is CZone.UFrag)
                return settings.IncludeUFrags;
            else if (entity.instance is Region.CVolumeInstance)
                return settings.IncludeVolumes;
            else
            {
                // Unknown type - include by default but log it
                LunaLog.LogDebug($"Unknown entity instance type: {entity.instance?.GetType().Name ?? "null"}");
                return true;
            }
        }).ToList();
    }

    private OTKVector3 TransformVertex(OTKVector3 vertex, Matrix4 transform)
    {
        var transformed = (new OTKVector4(vertex, 1.0f) * transform).Xyz;
        transformed = ConvertCoordinateSystem(transformed);
        transformed *= settings.ScaleFactor;
        // Apply 90-degree left flip if enabled
        if (settings.FlipModel90Left)
        {
            // Rotate 90 degrees left around Y axis (Z-up system)
            // X' = Z, Y' = Y, Z' = -X
            transformed = new OTKVector3(transformed.Z, transformed.Y, -transformed.X);
        }
        return transformed;
    }

    private OTKVector3 TransformNormal(OTKVector3 normal, Matrix4 transform)
    {
        var transformed = (new OTKVector4(normal, 0.0f) * transform).Xyz;
        transformed = ConvertCoordinateSystem(transformed);
        return transformed.Normalized();
    }

    private OTKVector3 ConvertCoordinateSystem(OTKVector3 vertex)
    {
        return settings.CoordinateSystem switch
        {
            CoordinateSystem.YUp => new OTKVector3(vertex.X, vertex.Z, -vertex.Y), // Z-up to Y-up
            CoordinateSystem.XUp => new OTKVector3(vertex.Z, vertex.Y, -vertex.X), // Z-up to X-up
            _ => vertex // Keep Z-up
        };
    }

    private void ExportCollada(string filePath, List<Entity> entities)
    {
        // Implement Collada export similar to Replanetizer's ColladaExporter
        LunaLog.LogInfo("Collada export not yet implemented");
    }

    private void ExportGLTF(string filePath, List<Entity> entities)
    {
        // Implement glTF export
        LunaLog.LogInfo("glTF export not yet implemented");
    }
}
