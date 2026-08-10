using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Logistics
{
    public enum ShipmentStatus
    {
        Preparing,
        InTransit,
        Arrived,
        Lost,
        Cancelled
    }

    public sealed class TradeRoute
    {
        public string Id { get; }
        public RegionId Origin { get; }
        public RegionId Destination { get; }
        public int TravelDays { get; }
        public decimal DailyCapacity { get; }
        public decimal BaseLossRate { get; }
        public decimal TollPerUnit { get; }
        public bool IsOpen { get; private set; }
        public decimal ReservedToday { get; private set; }

        public decimal AvailableCapacity =>
            Math.Max(0, DailyCapacity - ReservedToday);

        public TradeRoute(
            string id,
            RegionId origin,
            RegionId destination,
            int travelDays,
            decimal dailyCapacity,
            decimal baseLossRate,
            decimal tollPerUnit)
        {
            Id = id;
            Origin = origin;
            Destination = destination;
            TravelDays = Math.Max(1, travelDays);
            DailyCapacity = Math.Max(0, dailyCapacity);
            BaseLossRate = Math.Clamp(baseLossRate, 0, 1);
            TollPerUnit = Math.Max(0, tollPerUnit);
            IsOpen = true;
        }

        public void BeginDay()
        {
            ReservedToday = 0;
        }

        public bool TryReserve(decimal quantity)
        {
            if (!IsOpen || quantity <= 0 || AvailableCapacity < quantity)
                return false;

            ReservedToday += quantity;
            return true;
        }

        public void SetOpen(bool value)
        {
            IsOpen = value;
        }
    }

    public sealed class Shipment
    {
        public string Id { get; }
        public CompanyId OwnerId { get; }
        public ResourceId ResourceId { get; }
        public RegionId Origin { get; }
        public RegionId Destination { get; }
        public decimal DispatchedQuantity { get; }
        public decimal DeliveredQuantity { get; private set; }
        public decimal BaseLossRate { get; }
        public int RemainingDays { get; private set; }
        public ShipmentStatus Status { get; private set; }

        public Shipment(
            string id,
            CompanyId ownerId,
            ResourceId resourceId,
            TradeRoute route,
            decimal quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            Id = id;
            OwnerId = ownerId;
            ResourceId = resourceId;
            Origin = route.Origin;
            Destination = route.Destination;
            DispatchedQuantity = quantity;
            BaseLossRate = route.BaseLossRate;
            RemainingDays = route.TravelDays;
            Status = ShipmentStatus.Preparing;
        }

        public void Dispatch()
        {
            if (Status != ShipmentStatus.Preparing)
                throw new InvalidOperationException("이미 출발 처리된 운송입니다.");

            Status = ShipmentStatus.InTransit;
        }

        public bool AdvanceDay(decimal lossRate)
        {
            if (Status != ShipmentStatus.InTransit)
                return false;

            RemainingDays = Math.Max(0, RemainingDays - 1);

            if (RemainingDays > 0)
                return false;

            decimal clampedLoss = Math.Clamp(lossRate, 0, 1);
            DeliveredQuantity =
                DispatchedQuantity * (1.0m - clampedLoss);

            Status = DeliveredQuantity > 0
                ? ShipmentStatus.Arrived
                : ShipmentStatus.Lost;

            return true;
        }
    }

    public readonly struct ShipmentArrival
    {
        public string ShipmentId { get; }
        public CompanyId OwnerId { get; }
        public ResourceId ResourceId { get; }
        public RegionId Destination { get; }
        public decimal Quantity { get; }

        public ShipmentArrival(Shipment shipment)
        {
            ShipmentId = shipment.Id;
            OwnerId = shipment.OwnerId;
            ResourceId = shipment.ResourceId;
            Destination = shipment.Destination;
            Quantity = shipment.DeliveredQuantity;
        }
    }

    public sealed class LogisticsService
    {
        private readonly List<Shipment> _activeShipments =
            new List<Shipment>(128);

        public int ActiveShipmentCount => _activeShipments.Count;

        public bool TryDispatch(
            TradeRoute route,
            Shipment shipment)
        {
            if (shipment == null || !route.TryReserve(shipment.DispatchedQuantity))
                return false;

            shipment.Dispatch();
            _activeShipments.Add(shipment);
            return true;
        }

        public void AdvanceDay(
            decimal riskModifier,
            List<ShipmentArrival> arrivals)
        {
            if (arrivals == null)
                throw new ArgumentNullException(nameof(arrivals));

            arrivals.Clear();

            for (int i = _activeShipments.Count - 1; i >= 0; i--)
            {
                var shipment = _activeShipments[i];
                decimal lossRate = Math.Clamp(
                    shipment.BaseLossRate + riskModifier,
                    0,
                    1);

                if (!shipment.AdvanceDay(lossRate))
                    continue;

                if (shipment.Status == ShipmentStatus.Arrived)
                    arrivals.Add(new ShipmentArrival(shipment));

                int last = _activeShipments.Count - 1;
                _activeShipments[i] = _activeShipments[last];
                _activeShipments.RemoveAt(last);
            }
        }
    }
}
