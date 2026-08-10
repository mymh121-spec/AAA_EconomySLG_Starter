using System;
using Game.Domain.Common;

namespace Game.Domain.Missions
{
    public enum MissionType
    {
        CaptureMine,
        EscortShipment,
        Delivery,
        Smuggling,
        SabotageFactory,
        Reconnaissance
    }

    public enum MissionStatus
    {
        Planned,
        InProgress,
        Success,
        Failed,
        Cancelled
    }

    public sealed class MissionDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public MissionType Type { get; }
        public int DurationDays { get; }
        public decimal BaseRisk { get; }
        public decimal RequiredPower { get; }
        public decimal Reward { get; }

        public MissionDefinition(
            string id,
            MissionType type,
            int durationDays,
            decimal baseRisk,
            decimal requiredPower,
            decimal reward,
            string displayName = null)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Type = type;
            DurationDays = Math.Max(1, durationDays);
            BaseRisk = Math.Clamp(baseRisk, 0, 1);
            RequiredPower = Math.Max(0, requiredPower);
            Reward = Math.Max(0, reward);
        }
    }

    public sealed class MissionInstance
    {
        public string Id { get; }
        public MissionDefinition Definition { get; }
        public CompanyId OwnerId { get; }
        public RegionId RegionId { get; }
        public GameDay StartDay { get; }
        public GameDay ResolveDay { get; }
        public MissionStatus Status { get; private set; }

        public MissionInstance(
            string id,
            MissionDefinition definition,
            CompanyId ownerId,
            RegionId regionId,
            GameDay startDay)
        {
            Id = id;
            Definition = definition;
            OwnerId = ownerId;
            RegionId = regionId;
            StartDay = startDay;
            ResolveDay = startDay.Add(definition.DurationDays);
            Status = MissionStatus.Planned;
        }

        public void Start()
        {
            if (Status != MissionStatus.Planned)
                throw new InvalidOperationException("Mission cannot be started.");

            Status = MissionStatus.InProgress;
        }

        public void Resolve(bool success)
        {
            if (Status != MissionStatus.InProgress)
                throw new InvalidOperationException("Mission is not active.");

            Status = success
                ? MissionStatus.Success
                : MissionStatus.Failed;
        }
    }

    public interface IWorldEffect
    {
        void Apply(WorldState world);
    }

    public sealed class WorldState
    {
        private readonly System.Collections.Generic.Dictionary<string, ResourceNodeState> _nodes =
            new System.Collections.Generic.Dictionary<string, ResourceNodeState>();

        public void RegisterNode(ResourceNodeState node) => _nodes[node.Id] = node;

        public ResourceNodeState GetNode(string id) => _nodes[id];
    }

    public sealed class ResourceNodeState
    {
        public string Id { get; }
        public ResourceId ResourceId { get; }
        public bool Operational { get; private set; }
        public decimal DailyCapacity { get; }

        public ResourceNodeState(
            string id,
            ResourceId resourceId,
            decimal dailyCapacity,
            bool operational)
        {
            Id = id;
            ResourceId = resourceId;
            DailyCapacity = Math.Max(0, dailyCapacity);
            Operational = operational;
        }

        public void SetOperational(bool value) => Operational = value;

        public decimal GetDailyOutput() => Operational ? DailyCapacity : 0;
    }

    public sealed class EnableResourceNodeEffect : IWorldEffect
    {
        private readonly string _nodeId;

        public EnableResourceNodeEffect(string nodeId)
        {
            _nodeId = nodeId;
        }

        public void Apply(WorldState world)
        {
            world.GetNode(_nodeId).SetOperational(true);
        }
    }
}
