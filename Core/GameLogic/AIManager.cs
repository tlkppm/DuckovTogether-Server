using System.Numerics;
using DuckovTogether.Net;

namespace DuckovTogether.Core.GameLogic;

public class AIManager
{
    private static AIManager? _instance;
    public static AIManager Instance => _instance ??= new AIManager();
    
    private readonly Dictionary<int, AIEntity> _entities = new();
    private readonly Dictionary<string, List<AISpawnPoint>> _spawnPoints = new();
    private readonly object _lock = new();
    
    private DateTime _lastUpdate = DateTime.Now;
    private DateTime _lastBroadcast = DateTime.Now;
    private const double UPDATE_INTERVAL = 0.05;
    private const double BROADCAST_INTERVAL = 0.1;
    
    public int EntityCount => _entities.Count;
    
    public void LoadSceneData(string sceneId)
    {
        lock (_lock)
        {
            foreach (var entity in _entities.Values.Where(e => e.SceneId == sceneId).ToList())
            {
                _entities.Remove(entity.EntityId);
            }
            
            if (_spawnPoints.TryGetValue(sceneId, out var points))
            {
                foreach (var point in points)
                {
                    SpawnAI(point);
                }
            }
            
            Console.WriteLine($"[AIManager] Loaded scene: {sceneId}, spawned {_entities.Count(e => e.Value.SceneId == sceneId)} AI");
        }
    }
    
    public void RegisterSpawnPoint(string sceneId, AISpawnPoint point)
    {
        lock (_lock)
        {
            if (!_spawnPoints.ContainsKey(sceneId))
                _spawnPoints[sceneId] = new List<AISpawnPoint>();
            
            _spawnPoints[sceneId].Add(point);
        }
    }
    
    public AIEntity? SpawnAI(AISpawnPoint point)
    {
        lock (_lock)
        {
            var entity = new AIEntity
            {
                EntityId = GenerateEntityId(point),
                TypeName = point.AIType,
                Type = point.AICategory,
                Position = point.Position,
                Forward = point.Forward,
                SceneId = point.SceneId,
                SpawnerId = point.SpawnerId,
                MaxHealth = point.MaxHealth,
                CurrentHealth = point.MaxHealth,
                DetectRange = point.DetectRange,
                AttackRange = point.AttackRange,
                AttackDamage = point.AttackDamage,
                MoveSpeed = point.MoveSpeed,
                PatrolPath = point.PatrolPath,
                SpawnTime = DateTime.Now,
                LastUpdateTime = DateTime.Now,
                State = point.PatrolPath.Count > 0 ? AIState.Patrol : AIState.Idle
            };
            
            _entities[entity.EntityId] = entity;
            Console.WriteLine($"[AIManager] Spawned AI: {entity.TypeName} (ID: {entity.EntityId}) at {entity.Position}");
            return entity;
        }
    }
    
    public void Update(Dictionary<int, PlayerState> players)
    {
        var now = DateTime.Now;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        
        if (deltaTime < UPDATE_INTERVAL) return;
        _lastUpdate = now;
        
        lock (_lock)
        {
            foreach (var entity in _entities.Values)
            {
                if (entity.IsDead) continue;
                UpdateAIBehavior(entity, players, deltaTime);
            }
        }
    }
    
    private void UpdateAIBehavior(AIEntity entity, Dictionary<int, PlayerState> players, float deltaTime)
    {
        var nearestPlayer = FindNearestPlayer(entity, players);
        
        switch (entity.State)
        {
            case AIState.Idle:
                if (nearestPlayer != null && DistanceTo(entity.Position, nearestPlayer.Position) < entity.DetectRange)
                {
                    entity.TargetPlayerId = nearestPlayer.PeerId;
                    entity.State = AIState.Chase;
                    entity.LastStateChange = DateTime.Now;
                }
                else if (entity.PatrolPath.Count > 0)
                {
                    entity.State = AIState.Patrol;
                }
                break;
                
            case AIState.Patrol:
                if (nearestPlayer != null && DistanceTo(entity.Position, nearestPlayer.Position) < entity.DetectRange)
                {
                    entity.TargetPlayerId = nearestPlayer.PeerId;
                    entity.State = AIState.Chase;
                    entity.LastStateChange = DateTime.Now;
                }
                else if (entity.PatrolPath.Count > 0)
                {
                    var target = entity.PatrolPath[entity.PatrolIndex];
                    entity.MoveTo(target, deltaTime);
                    
                    if (DistanceTo(entity.Position, target) < 0.5f)
                    {
                        entity.PatrolIndex = (entity.PatrolIndex + 1) % entity.PatrolPath.Count;
                    }
                }
                break;
                
            case AIState.Chase:
                if (entity.TargetPlayerId == null || !players.ContainsKey(entity.TargetPlayerId.Value))
                {
                    entity.TargetPlayerId = null;
                    entity.State = AIState.Idle;
                    break;
                }
                
                var target2 = players[entity.TargetPlayerId.Value];
                var dist = DistanceTo(entity.Position, target2.Position);
                
                if (dist > entity.DetectRange * 1.5f)
                {
                    entity.TargetPlayerId = null;
                    entity.State = AIState.Idle;
                }
                else if (dist <= entity.AttackRange)
                {
                    entity.State = AIState.Attack;
                    entity.LastStateChange = DateTime.Now;
                }
                else
                {
                    entity.MoveTo(target2.Position, deltaTime);
                }
                break;
                
            case AIState.Attack:
                if (entity.TargetPlayerId == null || !players.ContainsKey(entity.TargetPlayerId.Value))
                {
                    entity.State = AIState.Idle;
                    break;
                }
                
                var attackTarget = players[entity.TargetPlayerId.Value];
                var attackDist = DistanceTo(entity.Position, attackTarget.Position);
                
                if (attackDist > entity.AttackRange * 1.2f)
                {
                    entity.State = AIState.Chase;
                }
                else if ((DateTime.Now - entity.LastAttackTime).TotalSeconds >= entity.AttackCooldown)
                {
                    entity.LastAttackTime = DateTime.Now;
                }
                break;
                
            case AIState.Dead:
                break;
        }
        
        entity.LastUpdateTime = DateTime.Now;
    }
    
    private PlayerState? FindNearestPlayer(AIEntity entity, Dictionary<int, PlayerState> players)
    {
        PlayerState? nearest = null;
        var minDist = float.MaxValue;
        
        foreach (var player in players.Values)
        {
            if (!player.IsInGame || player.SceneId != entity.SceneId) continue;
            
            var dist = DistanceTo(entity.Position, player.Position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = player;
            }
        }
        
        return nearest;
    }
    
    private float DistanceTo(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }
    
    private int GenerateEntityId(AISpawnPoint point)
    {
        unchecked
        {
            var h = 2166136261u;
            h ^= (uint)point.SpawnerId;
            h *= 16777619;
            h ^= (uint)point.Position.GetHashCode();
            h *= 16777619;
            return (int)h;
        }
    }
    
    public AIEntity? GetEntity(int entityId)
    {
        lock (_lock)
        {
            return _entities.TryGetValue(entityId, out var entity) ? entity : null;
        }
    }
    
    public IEnumerable<AIEntity> GetEntitiesInScene(string sceneId)
    {
        lock (_lock)
        {
            return _entities.Values.Where(e => e.SceneId == sceneId).ToList();
        }
    }
    
    public void DamageEntity(int entityId, float damage, int fromPlayerId)
    {
        lock (_lock)
        {
            if (_entities.TryGetValue(entityId, out var entity))
            {
                entity.TakeDamage(damage, fromPlayerId);
                Console.WriteLine($"[AIManager] AI {entityId} took {damage} damage from player {fromPlayerId}, HP: {entity.CurrentHealth}/{entity.MaxHealth}");
            }
        }
    }
}

public class AISpawnPoint
{
    public int SpawnerId { get; set; }
    public string SceneId { get; set; } = "";
    public string AIType { get; set; } = "";
    public AIType AICategory { get; set; } = GameLogic.AIType.Normal;
    public Vector3 Position { get; set; }
    public Vector3 Forward { get; set; } = Vector3.UnitZ;
    public float MaxHealth { get; set; } = 100f;
    public float DetectRange { get; set; } = 15f;
    public float AttackRange { get; set; } = 2f;
    public float AttackDamage { get; set; } = 10f;
    public float MoveSpeed { get; set; } = 3.5f;
    public float RespawnTime { get; set; } = 300f;
    public List<Vector3> PatrolPath { get; set; } = new();
}
