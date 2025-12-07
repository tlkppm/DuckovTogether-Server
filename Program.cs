using DuckovTogether.Core;
using DuckovTogether.Core.Assets;
using DuckovTogether.Core.GameLogic;
using DuckovTogether.Core.Save;
using DuckovTogether.Core.Security;
using DuckovTogether.Core.World;
using DuckovTogether.Net;

namespace DuckovTogether;

class Program
{
    private static bool _running = true;
    private static HeadlessNetService? _netService;
    private static MessageHandler? _messageHandler;
    
    static void Main(string[] args)
    {
        Console.Title = "Duckov Headless Server";
        Console.WriteLine("===========================================");
        Console.WriteLine("  Duckov Coop Mod - Headless Server");
        Console.WriteLine("  Version: 1.0.0");
        Console.WriteLine("===========================================");
        Console.WriteLine();
        
        var configPath = "server_config.json";
        var config = ServerConfig.Load(configPath);
        
        if (!File.Exists(configPath))
        {
            config.Save(configPath);
            Console.WriteLine($"[Config] Created default config: {configPath}");
        }
        
        foreach (var arg in args)
        {
            if (arg.StartsWith("--port="))
            {
                if (int.TryParse(arg.Substring(7), out var port))
                {
                    config.Port = port;
                }
            }
            else if (arg.StartsWith("--max-players="))
            {
                if (int.TryParse(arg.Substring(14), out var maxPlayers))
                {
                    config.MaxPlayers = maxPlayers;
                }
            }
            else if (arg.StartsWith("--name="))
            {
                config.ServerName = arg.Substring(7);
            }
            else if (arg.StartsWith("--game-path="))
            {
                config.GamePath = arg.Substring(12);
            }
        }
        
        Console.WriteLine($"[Config] Port: {config.Port}");
        Console.WriteLine($"[Config] Max Players: {config.MaxPlayers}");
        Console.WriteLine($"[Config] Server Name: {config.ServerName}");
        Console.WriteLine($"[Config] Tick Rate: {config.TickRate} Hz");
        Console.WriteLine($"[Config] Game Path: {config.GamePath}");
        Console.WriteLine();
        
        var gamePath = config.GamePath;
        if (string.IsNullOrEmpty(gamePath))
        {
            gamePath = GamePathDetector.DetectGamePath();
        }
        
        if (!string.IsNullOrEmpty(gamePath))
        {
            Console.WriteLine($"[Assets] Game path: {gamePath}");
            Console.WriteLine("[Assets] Loading game resources...");
            if (UnityAssetReader.Instance.Initialize(gamePath))
            {
                UnityAssetReader.Instance.SaveExtractedData(config.ExtractedDataPath);
                Console.WriteLine("[Assets] Game resources loaded successfully");
            }
            else
            {
                Console.WriteLine("[Assets] Warning: Failed to load game resources, running in proxy mode");
            }
        }
        else
        {
            Console.WriteLine("[Assets] Could not find game installation");
            Console.WriteLine("[Assets] Set 'GamePath' in server_config.json or use --game-path=<path>");
        }
        Console.WriteLine();
        
        ValidationService.Instance.Initialize(config.GameKey);
        GameServer.Instance.Initialize();
        
        _netService = new HeadlessNetService(config);
        _messageHandler = new MessageHandler(_netService);
        
        if (!_netService.Start())
        {
            Console.WriteLine("[Error] Failed to start server. Press any key to exit...");
            Console.ReadKey();
            return;
        }
        
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _running = false;
            Console.WriteLine("\n[Server] Shutting down...");
        };
        
        Console.WriteLine("[Server] Press Ctrl+C to stop");
        Console.WriteLine("[Server] Type 'help' for commands");
        Console.WriteLine();
        
        var inputThread = new Thread(InputLoop);
        inputThread.IsBackground = true;
        inputThread.Start();
        
        var tickInterval = 1000 / config.TickRate;
        var lastTick = DateTime.Now;
        
        while (_running)
        {
            _netService.Update();
            _messageHandler.Update();
            GameServer.Instance.Update();
            
            var elapsed = (DateTime.Now - lastTick).TotalMilliseconds;
            if (elapsed < tickInterval)
            {
                Thread.Sleep((int)(tickInterval - elapsed));
            }
            lastTick = DateTime.Now;
        }
        
        GameServer.Instance.Shutdown();
        ValidationService.Instance.Shutdown();
        _netService.Stop();
        Console.WriteLine("[Server] Goodbye!");
    }
    
    static void InputLoop()
    {
        while (_running)
        {
            var input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input)) continue;
            
            switch (input)
            {
                case "help":
                    Console.WriteLine("Commands:");
                    Console.WriteLine("  status  - Show server status");
                    Console.WriteLine("  players - List connected players");
                    Console.WriteLine("  kick <id> - Kick a player");
                    Console.WriteLine("  save    - Save all data");
                    Console.WriteLine("  world   - Show world state");
                    Console.WriteLine("  scene <id> - Change scene");
                    Console.WriteLine("  quit    - Stop the server");
                    break;
                    
                case "status":
                    Console.WriteLine($"[Status] Running: {_netService?.IsRunning}");
                    Console.WriteLine($"[Status] Players: {_netService?.PlayerCount}");
                    break;
                    
                case "players":
                    var players = _netService?.GetAllPlayers();
                    if (players == null || !players.Any())
                    {
                        Console.WriteLine("[Players] No players connected");
                    }
                    else
                    {
                        Console.WriteLine("[Players] Connected players:");
                        foreach (var p in players)
                        {
                            Console.WriteLine($"  [{p.PeerId}] {p.PlayerName} - {p.EndPoint} (Ping: {p.Latency}ms)");
                        }
                    }
                    break;
                    
                case "save":
                    ServerSaveManager.Instance.SaveAll();
                    Console.WriteLine("[Save] All data saved");
                    break;
                    
                case "world":
                    var world = GameServer.Instance.Saves.CurrentWorld;
                    Console.WriteLine($"[World] ID: {world.WorldId}");
                    Console.WriteLine($"[World] Scene: {world.CurrentScene}");
                    Console.WriteLine($"[World] Day: {world.GameDay}, Time: {world.GameTime:F1}");
                    Console.WriteLine($"[World] AI Entities: {GameServer.Instance.AI.EntityCount}");
                    Console.WriteLine($"[World] Created: {world.CreatedAt}, LastSaved: {world.LastSaved}");
                    break;
                    
                case "ai":
                    Console.WriteLine($"[AI] Total entities: {GameServer.Instance.AI.EntityCount}");
                    break;
                    
                case "quit":
                case "exit":
                case "stop":
                    _running = false;
                    break;
                    
                default:
                    if (input.StartsWith("scene "))
                    {
                        var sceneId = input.Substring(6).Trim();
                        GameServer.Instance.ChangeScene(sceneId);
                        break;
                    }
                    if (input.StartsWith("kick "))
                    {
                        var idStr = input.Substring(5).Trim();
                        if (int.TryParse(idStr, out var kickId))
                        {
                            var peer = _netService?.NetManager?.ConnectedPeerList
                                .FirstOrDefault(p => p.Id == kickId);
                            if (peer != null)
                            {
                                peer.Disconnect();
                                Console.WriteLine($"[Server] Kicked player {kickId}");
                            }
                            else
                            {
                                Console.WriteLine($"[Server] Player {kickId} not found");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unknown command: {input}");
                    }
                    break;
            }
        }
    }
}
