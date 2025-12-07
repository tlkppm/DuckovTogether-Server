using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Text;

namespace DuckovTogether.Core.Assets;

public class UnityAssetReader
{
    private static UnityAssetReader? _instance;
    public static UnityAssetReader Instance => _instance ??= new UnityAssetReader();
    
    private string? _gamePath;
    private AssetsManager? _assetsManager;
    
    public Dictionary<int, ItemData> Items { get; } = new();
    public Dictionary<string, SceneData> Scenes { get; } = new();
    public Dictionary<string, AITypeData> AITypes { get; } = new();
    public Dictionary<string, LootTableData> LootTables { get; } = new();
    public Dictionary<string, string> Localization { get; } = new();
    
    public bool Initialize(string gamePath)
    {
        _gamePath = gamePath;
        
        if (!Directory.Exists(gamePath))
        {
            Console.WriteLine($"[AssetReader] Game path not found: {gamePath}");
            return false;
        }
        
        var dataPath = Path.Combine(gamePath, "Duckov_Data");
        if (!Directory.Exists(dataPath))
        {
            Console.WriteLine($"[AssetReader] Data path not found: {dataPath}");
            return false;
        }
        
        _assetsManager = new AssetsManager();
        
        LoadLocalization();
        LoadGameAssets();
        
        Console.WriteLine($"[AssetReader] Loaded: {Items.Count} items, {Scenes.Count} scenes, {AITypes.Count} AI types");
        return true;
    }
    
    private void LoadLocalization()
    {
        var locPath = Path.Combine(_gamePath!, "Duckov_Data", "StreamingAssets", "Localization", "English.csv");
        if (!File.Exists(locPath))
        {
            Console.WriteLine("[AssetReader] Localization file not found");
            return;
        }
        
        try
        {
            var lines = File.ReadAllLines(locPath, Encoding.UTF8);
            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim().Trim('"').Replace("\\", "");
                    if (!string.IsNullOrEmpty(key))
                    {
                        Localization[key] = value;
                    }
                }
            }
            Console.WriteLine($"[AssetReader] Loaded {Localization.Count} localization entries");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetReader] Failed to load localization: {ex.Message}");
        }
    }
    
    private void LoadGameAssets()
    {
        try
        {
            var ggmPath = Path.Combine(_gamePath!, "Duckov_Data", "globalgamemanagers");
            if (!File.Exists(ggmPath))
            {
                Console.WriteLine("[AssetReader] globalgamemanagers not found");
                return;
            }
            
            var ggmAssetsPath = Path.Combine(_gamePath!, "Duckov_Data", "globalgamemanagers.assets");
            if (File.Exists(ggmAssetsPath))
            {
                LoadAssetsFile(ggmAssetsPath);
            }
            
            var resourcesPath = Path.Combine(_gamePath!, "Duckov_Data", "resources.assets");
            if (File.Exists(resourcesPath))
            {
                LoadAssetsFile(resourcesPath);
            }
            
            for (int i = 0; i <= 49; i++)
            {
                var levelPath = Path.Combine(_gamePath!, "Duckov_Data", $"level{i}");
                if (File.Exists(levelPath))
                {
                    LoadLevelFile(levelPath, i);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetReader] Failed to load assets: {ex.Message}");
        }
    }
    
    private void LoadAssetsFile(string path)
    {
        try
        {
            var assetsFileInst = _assetsManager!.LoadAssetsFile(path, true);
            var assetsFile = assetsFileInst.file;
            
            foreach (var info in assetsFile.Metadata.AssetInfos)
            {
                try
                {
                    var typeName = info.TypeId.ToString();
                    
                    if (info.TypeId == (int)AssetClassID.MonoBehaviour)
                    {
                        ProcessMonoBehaviour(assetsFileInst, info);
                    }
                    else if (info.TypeId == (int)AssetClassID.GameObject)
                    {
                        ProcessGameObject(assetsFileInst, info);
                    }
                }
                catch { }
            }
            
            _assetsManager.UnloadAssetsFile(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetReader] Failed to load {path}: {ex.Message}");
        }
    }
    
    private void LoadLevelFile(string path, int levelIndex)
    {
        try
        {
            var assetsFileInst = _assetsManager!.LoadAssetsFile(path, true);
            var assetsFile = assetsFileInst.file;
            
            string sceneName = $"Level_{levelIndex}";
            var sceneData = new SceneData
            {
                SceneId = sceneName,
                BuildIndex = levelIndex,
                AISpawns = new List<AISpawnData>(),
                LootSpawns = new List<LootSpawnData>(),
                PlayerSpawns = new List<Vector3Data>()
            };
            
            foreach (var info in assetsFile.Metadata.AssetInfos)
            {
                try
                {
                    if (info.TypeId == (int)AssetClassID.GameObject)
                    {
                        var baseField = _assetsManager.GetBaseField(assetsFileInst, info);
                        var name = baseField["m_Name"].AsString;
                        
                        if (name.Contains("Spawner") || name.Contains("spawn", StringComparison.OrdinalIgnoreCase))
                        {
                            var transform = FindTransformData(assetsFileInst, baseField);
                            if (transform != null)
                            {
                                if (name.Contains("AI") || name.Contains("Character") || name.Contains("Enemy"))
                                {
                                    sceneData.AISpawns.Add(new AISpawnData
                                    {
                                        SpawnerId = info.PathId.GetHashCode(),
                                        Position = transform,
                                        AIType = ExtractAIType(name)
                                    });
                                }
                                else if (name.Contains("Player"))
                                {
                                    sceneData.PlayerSpawns.Add(transform);
                                }
                            }
                        }
                        else if (name.Contains("Loot") || name.Contains("Container") || name.Contains("Box"))
                        {
                            var transform = FindTransformData(assetsFileInst, baseField);
                            if (transform != null)
                            {
                                sceneData.LootSpawns.Add(new LootSpawnData
                                {
                                    ContainerId = info.PathId.GetHashCode(),
                                    Position = transform,
                                    ContainerType = ExtractContainerType(name)
                                });
                            }
                        }
                    }
                }
                catch { }
            }
            
            if (sceneData.AISpawns.Count > 0 || sceneData.LootSpawns.Count > 0 || sceneData.PlayerSpawns.Count > 0)
            {
                Scenes[sceneName] = sceneData;
            }
            
            _assetsManager.UnloadAssetsFile(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetReader] Failed to load level {levelIndex}: {ex.Message}");
        }
    }
    
    private void ProcessMonoBehaviour(AssetsFileInstance inst, AssetFileInfo info)
    {
        try
        {
            var baseField = _assetsManager!.GetBaseField(inst, info);
            var scriptRef = baseField["m_Script"];
            
            if (scriptRef != null)
            {
                var scriptName = GetScriptName(inst, scriptRef);
                
                if (scriptName == "Item" || scriptName == "ItemStats" || scriptName.Contains("ItemData"))
                {
                    ExtractItemData(baseField, info.PathId);
                }
                else if (scriptName.Contains("AIData") || scriptName.Contains("CharacterData"))
                {
                    ExtractAITypeData(baseField, scriptName);
                }
                else if (scriptName.Contains("LootTable"))
                {
                    ExtractLootTableData(baseField);
                }
            }
        }
        catch { }
    }
    
    private void ProcessGameObject(AssetsFileInstance inst, AssetFileInfo info)
    {
    }
    
    private void ExtractItemData(AssetTypeValueField baseField, long pathId)
    {
        try
        {
            var typeId = 0;
            var name = "";
            var displayName = "";
            var maxStack = 1;
            var weight = 0.1f;
            
            foreach (var child in baseField.Children)
            {
                var fieldName = child.FieldName.ToLower();
                if (fieldName.Contains("typeid") || fieldName == "id")
                    typeId = child.AsInt;
                else if (fieldName == "name" || fieldName == "m_name")
                    name = child.AsString ?? "";
                else if (fieldName.Contains("display") || fieldName.Contains("localiz"))
                    displayName = child.AsString ?? "";
                else if (fieldName.Contains("stack") || fieldName.Contains("max"))
                    maxStack = child.AsInt;
                else if (fieldName.Contains("weight"))
                    weight = child.AsFloat;
            }
            
            if (typeId > 0 || !string.IsNullOrEmpty(name))
            {
                var itemId = typeId > 0 ? typeId : (int)pathId;
                Items[itemId] = new ItemData
                {
                    TypeId = itemId,
                    Name = name,
                    DisplayName = string.IsNullOrEmpty(displayName) ? GetLocalizedName(name) : displayName,
                    MaxStack = maxStack,
                    Weight = weight
                };
            }
        }
        catch { }
    }
    
    private void ExtractAITypeData(AssetTypeValueField baseField, string typeName)
    {
        try
        {
            var aiData = new AITypeData
            {
                TypeName = typeName,
                MaxHealth = 100f,
                MoveSpeed = 3.5f,
                AttackDamage = 10f
            };
            
            foreach (var child in baseField.Children)
            {
                var fieldName = child.FieldName.ToLower();
                if (fieldName.Contains("health") || fieldName.Contains("hp"))
                    aiData.MaxHealth = child.AsFloat;
                else if (fieldName.Contains("speed"))
                    aiData.MoveSpeed = child.AsFloat;
                else if (fieldName.Contains("damage") || fieldName.Contains("attack"))
                    aiData.AttackDamage = child.AsFloat;
            }
            
            AITypes[typeName] = aiData;
        }
        catch { }
    }
    
    private void ExtractLootTableData(AssetTypeValueField baseField)
    {
        try
        {
            var name = baseField["m_Name"]?.AsString ?? "";
            if (string.IsNullOrEmpty(name)) return;
            
            LootTables[name] = new LootTableData
            {
                TableName = name,
                Items = new List<LootTableEntry>()
            };
        }
        catch { }
    }
    
    private string GetScriptName(AssetsFileInstance inst, AssetTypeValueField scriptRef)
    {
        try
        {
            var fileId = scriptRef["m_FileID"].AsInt;
            var pathId = scriptRef["m_PathID"].AsLong;
            
            if (pathId == 0) return "";
            
            var targetInst = fileId == 0 ? inst : _assetsManager!.LoadAssetsFileFromBundle(inst.parentBundle!, fileId);
            var scriptInfo = targetInst.file.Metadata.GetAssetInfo(pathId);
            if (scriptInfo == null) return "";
            
            var scriptBase = _assetsManager!.GetBaseField(targetInst, scriptInfo);
            return scriptBase["m_Name"]?.AsString ?? "";
        }
        catch
        {
            return "";
        }
    }
    
    private Vector3Data? FindTransformData(AssetsFileInstance inst, AssetTypeValueField goField)
    {
        try
        {
            var components = goField["m_Component"];
            if (components == null) return null;
            
            foreach (var comp in components.Children)
            {
                var compRef = comp["component"];
                var pathId = compRef["m_PathID"].AsLong;
                if (pathId == 0) continue;
                
                var compInfo = inst.file.Metadata.GetAssetInfo(pathId);
                if (compInfo == null) continue;
                
                if (compInfo.TypeId == (int)AssetClassID.Transform)
                {
                    var transformField = _assetsManager!.GetBaseField(inst, compInfo);
                    var localPos = transformField["m_LocalPosition"];
                    
                    return new Vector3Data
                    {
                        X = localPos["x"].AsFloat,
                        Y = localPos["y"].AsFloat,
                        Z = localPos["z"].AsFloat
                    };
                }
            }
        }
        catch { }
        
        return null;
    }
    
    private string ExtractAIType(string name)
    {
        if (name.Contains("Scav")) return "Scav";
        if (name.Contains("PMC")) return "PMC";
        if (name.Contains("Boss")) return "Boss";
        if (name.Contains("Guard")) return "Guard";
        return "Default";
    }
    
    private string ExtractContainerType(string name)
    {
        if (name.Contains("Weapon")) return "Weapon";
        if (name.Contains("Medical")) return "Medical";
        if (name.Contains("Ammo")) return "Ammo";
        if (name.Contains("Rare")) return "Rare";
        return "Generic";
    }
    
    private string GetLocalizedName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (Localization.TryGetValue(key, out var value)) return value;
        if (Localization.TryGetValue($"Item_{key}", out value)) return value;
        return key;
    }
    
    public void SaveExtractedData(string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        
        var itemsJson = Newtonsoft.Json.JsonConvert.SerializeObject(Items.Values.ToList(), Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(Path.Combine(outputPath, "items.json"), itemsJson);
        
        var scenesDir = Path.Combine(outputPath, "scenes");
        Directory.CreateDirectory(scenesDir);
        foreach (var scene in Scenes.Values)
        {
            var sceneJson = Newtonsoft.Json.JsonConvert.SerializeObject(scene, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(scenesDir, $"{scene.SceneId}.json"), sceneJson);
        }
        
        var aiTypesJson = Newtonsoft.Json.JsonConvert.SerializeObject(AITypes.Values.ToList(), Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(Path.Combine(outputPath, "ai_types.json"), aiTypesJson);
        
        Console.WriteLine($"[AssetReader] Saved extracted data to: {outputPath}");
    }
}

public class ItemData
{
    public int TypeId { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "Unknown";
    public int MaxStack { get; set; } = 1;
    public float Weight { get; set; } = 0.1f;
    public int Value { get; set; } = 100;
}

public class SceneData
{
    public string SceneId { get; set; } = "";
    public int BuildIndex { get; set; }
    public List<AISpawnData> AISpawns { get; set; } = new();
    public List<LootSpawnData> LootSpawns { get; set; } = new();
    public List<Vector3Data> PlayerSpawns { get; set; } = new();
}

public class AISpawnData
{
    public int SpawnerId { get; set; }
    public Vector3Data Position { get; set; } = new();
    public string AIType { get; set; } = "";
}

public class LootSpawnData
{
    public int ContainerId { get; set; }
    public Vector3Data Position { get; set; } = new();
    public string ContainerType { get; set; } = "";
}

public class Vector3Data
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class AITypeData
{
    public string TypeName { get; set; } = "";
    public float MaxHealth { get; set; }
    public float MoveSpeed { get; set; }
    public float AttackDamage { get; set; }
}

public class LootTableData
{
    public string TableName { get; set; } = "";
    public List<LootTableEntry> Items { get; set; } = new();
}

public class LootTableEntry
{
    public int ItemId { get; set; }
    public float Weight { get; set; }
    public int MinCount { get; set; }
    public int MaxCount { get; set; }
}
