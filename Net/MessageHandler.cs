using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json;
using DuckovTogether.Core.Sync;
using DuckovTogether.Core.GameLogic;

namespace DuckovTogether.Net;

public enum MessageType : byte
{
    ClientStatus = 1,
    PlayerPosition = 2,
    PlayerAnimation = 3,
    ChatMessage = 4,
    SceneVote = 5,
    JsonMessage = 9,
    AISync = 10,
    AIHealth = 11,
    AIAnimation = 12,
    LootSync = 20,
    ItemPickup = 21,
    WeaponFire = 30,
    Damage = 31,
    PlayerHealth = 32,
    SetId = 100,
    Kick = 101
}

public class SetIdData
{
    public string type { get; set; } = "setId";
    public string networkId { get; set; } = "";
    public string timestamp { get; set; } = "";
}

public class BaseJsonMessage
{
    public string type { get; set; } = "";
}

public class PlayerListData
{
    public string type { get; set; } = "playerList";
    public List<PlayerListItem> players { get; set; } = new();
}

public class PlayerListItem
{
    public int peerId { get; set; }
    public string endPoint { get; set; } = "";
    public string playerName { get; set; } = "";
    public bool isInGame { get; set; }
    public string sceneId { get; set; } = "";
    public int latency { get; set; }
}

public class MessageHandler
{
    private readonly HeadlessNetService _netService;
    private readonly NetDataWriter _writer = new();
    private DateTime _lastPlayerListBroadcast = DateTime.MinValue;
    private const double PLAYER_LIST_INTERVAL = 2.0;
    
    public MessageHandler(HeadlessNetService netService)
    {
        _netService = netService;
        _netService.OnDataReceived += HandleMessage;
        _netService.OnPlayerConnected += OnPlayerConnected;
        _netService.OnPlayerDisconnected += OnPlayerDisconnected;
        
        SyncManager.Instance.Initialize(netService);
    }
    
    public void Update()
    {
        SyncManager.Instance.Update();
    }
    
    private void BroadcastPlayerList()
    {
        if (_netService.PlayerCount == 0) return;
        
        var playerList = new PlayerListData();
        foreach (var player in _netService.GetAllPlayers())
        {
            playerList.players.Add(new PlayerListItem
            {
                peerId = player.PeerId,
                endPoint = player.EndPoint,
                playerName = player.PlayerName,
                isInGame = player.IsInGame,
                sceneId = player.SceneId,
                latency = player.Latency
            });
        }
        
        var json = JsonConvert.SerializeObject(playerList);
        _writer.Reset();
        _writer.Put((byte)MessageType.JsonMessage);
        _writer.Put(json);
        _netService.SendToAll(_writer);
    }
    
    private void OnPlayerDisconnected(int peerId, DisconnectReason reason)
    {
        BroadcastPlayerList();
    }
    
    private void OnPlayerConnected(int peerId, Core.PlayerState state)
    {
        SendSetId(peerId, state.EndPoint);
    }
    
    private void SendSetId(int peerId, string endPoint)
    {
        var setIdData = new SetIdData
        {
            networkId = endPoint,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        };
        
        var json = JsonConvert.SerializeObject(setIdData);
        
        _writer.Reset();
        _writer.Put((byte)MessageType.JsonMessage);
        _writer.Put(json);
        
        var peer = GetPeerById(peerId);
        if (peer != null)
        {
            _netService.SendToPeer(peer, _writer);
            Console.WriteLine($"[MessageHandler] Sent SetId to peer {peerId}: {endPoint}");
        }
    }
    
    private NetPeer? GetPeerById(int peerId)
    {
        if (_netService.NetManager == null) return null;
        foreach (var peer in _netService.NetManager.ConnectedPeerList)
        {
            if (peer.Id == peerId) return peer;
        }
        return null;
    }
    
    private void HandleMessage(int peerId, NetPacketReader reader, byte channel)
    {
        if (reader.AvailableBytes < 1) return;
        
        var msgType = (MessageType)reader.GetByte();
        
        switch (msgType)
        {
            case MessageType.ClientStatus:
                HandleClientStatus(peerId, reader);
                break;
            case MessageType.PlayerPosition:
                HandlePlayerPosition(peerId, reader);
                break;
            case MessageType.ChatMessage:
                HandleChatMessage(peerId, reader);
                break;
            case MessageType.JsonMessage:
                HandleJsonMessage(peerId, reader, channel);
                break;
            default:
                BroadcastRawMessage(peerId, reader, channel);
                break;
        }
    }
    
    private void HandleJsonMessage(int peerId, NetPacketReader reader, byte channel)
    {
        try
        {
            var json = reader.GetString();
            var baseMsg = JsonConvert.DeserializeObject<BaseJsonMessage>(json);
            if (baseMsg == null) return;
            
            var peer = GetPeerById(peerId);
            
            var requestTypes = new[] { 
                "sceneVoteRequest", "sceneVoteReady", "updateClientStatus",
                "lootRequest", "itemDropRequest", "itemPickupRequest",
                "damageReport", "ai_health_report", "clientStatus"
            };
            
            if (requestTypes.Contains(baseMsg.type))
            {
                ClientRequestHandler.Instance.HandleJsonRequest(peerId, json, peer!);
                
                if (baseMsg.type == "clientStatus" || baseMsg.type == "updateClientStatus")
                {
                    HandleJsonClientStatus(peerId, json);
                }
            }
            else
            {
                BroadcastJsonMessage(peerId, json, channel);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] HandleJsonMessage: {ex.Message}");
        }
    }
    
    private void HandleJsonClientStatus(int peerId, string json)
    {
        try
        {
            dynamic data = JsonConvert.DeserializeObject(json)!;
            var player = _netService.GetPlayer(peerId);
            if (player != null)
            {
                player.PlayerName = (string?)data.playerName ?? player.PlayerName;
                player.IsInGame = (bool?)data.isInGame ?? false;
                player.SceneId = (string?)data.sceneId ?? "";
                player.LastUpdate = DateTime.Now;
                Console.WriteLine($"[JsonStatus] {player.PlayerName} - InGame: {player.IsInGame}, Scene: {player.SceneId}");
            }
        }
        catch { }
    }
    
    private void BroadcastJsonMessage(int senderPeerId, string json, byte channel)
    {
        if (_netService.NetManager == null) return;
        
        _writer.Reset();
        _writer.Put((byte)MessageType.JsonMessage);
        _writer.Put(json);
        
        foreach (var peer in _netService.NetManager.ConnectedPeerList)
        {
            if (peer.Id != senderPeerId)
            {
                peer.Send(_writer, channel, DeliveryMethod.ReliableOrdered);
            }
        }
    }
    
    private void HandleClientStatus(int peerId, NetPacketReader reader)
    {
        try
        {
            var playerName = reader.GetString();
            var isInGame = reader.GetBool();
            var sceneId = reader.GetString();
            
            var player = _netService.GetPlayer(peerId);
            if (player != null)
            {
                player.PlayerName = playerName;
                player.IsInGame = isInGame;
                player.SceneId = sceneId;
                player.LastUpdate = DateTime.Now;
                
                Console.WriteLine($"[Status] {playerName} - InGame: {isInGame}, Scene: {sceneId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] HandleClientStatus: {ex.Message}");
        }
    }
    
    private void HandlePlayerPosition(int peerId, NetPacketReader reader)
    {
        try
        {
            var x = reader.GetFloat();
            var y = reader.GetFloat();
            var z = reader.GetFloat();
            
            var player = _netService.GetPlayer(peerId);
            if (player != null)
            {
                player.Position = new System.Numerics.Vector3(x, y, z);
                player.LastUpdate = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] HandlePlayerPosition: {ex.Message}");
        }
    }
    
    private void HandleChatMessage(int peerId, NetPacketReader reader)
    {
        try
        {
            var message = reader.GetString();
            var player = _netService.GetPlayer(peerId);
            var playerName = player?.PlayerName ?? $"Player_{peerId}";
            
            Console.WriteLine($"[Chat] {playerName}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] HandleChatMessage: {ex.Message}");
        }
    }
    
    private void BroadcastRawMessage(int senderPeerId, NetPacketReader reader, byte channel)
    {
        if (_netService.NetManager == null) return;
        
        reader.SetPosition(0);
        var data = reader.GetRemainingBytes();
        
        _writer.Reset();
        _writer.Put(data);
        
        var deliveryMethod = channel switch
        {
            0 => DeliveryMethod.ReliableOrdered,
            1 => DeliveryMethod.ReliableOrdered,
            2 => DeliveryMethod.ReliableUnordered,
            3 => DeliveryMethod.Unreliable,
            _ => DeliveryMethod.ReliableOrdered
        };
        
        foreach (var peer in _netService.NetManager.ConnectedPeerList)
        {
            if (peer.Id != senderPeerId)
            {
                peer.Send(_writer, channel, deliveryMethod);
            }
        }
    }
}
