using System.Numerics;

namespace DuckovTogether.Core;

public class PlayerState
{
    public int PeerId { get; set; }
    public string EndPoint { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int Latency { get; set; }
    public bool IsInGame { get; set; }
    public string SceneId { get; set; } = "";
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.Now;
    public DateTime ConnectTime { get; set; } = DateTime.Now;
    
    public float MaxHealth { get; set; } = 100f;
    public float CurrentHealth { get; set; } = 100f;
}
