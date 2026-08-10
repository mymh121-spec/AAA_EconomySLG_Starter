using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Application;
using Game.Application.PvP;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Inventory;
using Game.Domain.Market;
using Game.Domain.Resources;

namespace Game.Server;

public sealed class AuthoritativeSimulationRuntime
{
    private static readonly string[] ResourceIds =
    {
        "iron", "coal", "wood", "oil", "steel",
        "food", "medicine", "machine", "semiconductor"
    };

    private readonly SimulationEngine _simulation;
    private readonly WorldEconomyState _world;
    private readonly Dictionary<CompanyId, CampaignParticipantState>
        _participants = new();
    private readonly PvpMarketCommandTranslator _marketTranslator = new();
    private readonly CampaignRuleSet _campaignRules = new();
    private readonly EconomicPowerCalculator _powerCalculator = new();
    private readonly Dictionary<CompanyId, int> _dominanceTurns = new();

    public TurnReport? LastTurnReport { get; private set; }
    public bool IsFinished { get; private set; }
    public string WinnerCompanyId { get; private set; } = string.Empty;

    public bool IsCompanyEliminated(CompanyId companyId)
    {
        return _participants.TryGetValue(companyId, out var participant) &&
            participant.IsEliminated;
    }

    public AuthoritativeSimulationRuntime(
        IReadOnlyList<PvpPlayerSlot> playerSlots,
        TurnNumber? initialTurn = null)
    {
        if (playerSlots == null || playerSlots.Count < 2 || playerSlots.Count > 4)
            throw new ArgumentException("권위 서버 매치는 2~4명이 필요합니다.", nameof(playerSlots));

        _world = new WorldEconomyState();
        var region = new RegionId("capital");
        var marketTuning = new MarketTuning();
        var market = new MarketManager(
            new SupplyDemandLedger(marketTuning),
            new PriceCalculator());

        RegisterMarkets(region);
        RegisterCompanies(playerSlots, region);

        var worldService = new WorldEconomyTurnService(
            _world,
            new WorldEconomyTuning(new OperatingCostPolicy(
                factoryCost: 100m,
                warehouseCost: 30m,
                vehicleCost: 20m,
                employeeWage: 5m,
                dailyInterestRate: 0.001m,
                bankruptcyDebtLimit: 100000m)),
            _campaignRules,
            new ResourceSiteEventSettings());

        _simulation = new SimulationEngine(
            new TurnResolutionOrchestrator(market, worldService),
            market,
            _ => Array.Empty<PhysicalFlow>(),
            new TurnRuleSet(maxActionPoints: 5, daysPerTurn: 1),
            initialTurn ?? new TurnNumber(1),
            new GameDay((initialTurn?.Value ?? 1) - 1));
    }

    public TurnReport Resolve(PvpTurnPackage package)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (!_simulation.CurrentTurn.Equals(package.Turn))
            throw new InvalidOperationException("서버 경제 턴과 PvP 턴이 일치하지 않습니다.");

        var commands = new List<ITurnCommand>(package.Commands.Count);
        for (int i = 0; i < package.Commands.Count; i++)
        {
            PvpCommandEnvelope envelope = package.Commands[i];
            if (!_marketTranslator.TryCreateTurnCommand(
                    envelope,
                    out ITurnCommand command,
                    out PvpOperationCode code))
            {
                throw new InvalidOperationException(
                    $"지원되지 않는 권위 명령입니다: {envelope.Kind} ({code})");
            }

            commands.Add(command);
        }

        LastTurnReport = _simulation.EndAuthoritativeTurn(commands);
        EvaluateMatchEnd(LastTurnReport.Turn);
        return LastTurnReport;
    }

    public WorldStateResponse CreateWorldView(CompanyId viewerCompanyId)
    {
        var markets = _world.Markets
            .OrderBy(item => item.RegionId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Definition.Id.Value, StringComparer.Ordinal)
            .Select(item => new MarketStateResponse(
                item.RegionId.Value,
                item.Definition.Id.Value,
                item.Definition.DisplayName,
                item.MarketState.CurrentPrice,
                item.MarketState.DailySupply,
                item.MarketState.DailyDemand,
                item.MarketState.MarketStock))
            .ToArray();

        var companies = _world.Companies
            .OrderBy(item => item.Company.Id.Value, StringComparer.Ordinal)
            .Select(item => new PublicCompanyStateResponse(
                item.Company.Id.Value,
                item.Company.Name,
                item.CampaignState.IsEliminated,
                _powerCalculator.Calculate(item.CampaignState, _campaignRules)))
            .ToArray();

        CompanyEconomyRuntime? viewer = null;
        _world.TryGetCompany(viewerCompanyId, out viewer);
        OwnCompanyStateResponse? ownCompany = viewer == null
            ? null
            : new OwnCompanyStateResponse(
                viewer.Company.Id.Value,
                viewer.Company.Cash,
                viewer.Company.Debt,
                viewer.Company.IsBankrupt,
                viewer.PrimaryWarehouse.Stocks
                    .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
                    .Select(item => new InventoryStateResponse(
                        item.Key.Value,
                        item.Value.OnHand,
                        item.Value.Reserved))
                    .ToArray());

        TurnNumber outputTurn = _simulation.CurrentTurn;
        var sites = _world.ResourceSites
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new ResourceSiteStateResponse(
                item.Id,
                item.RegionId.Value,
                item.ResourceId.Value,
                item.DiscoveryTurn.Value,
                item.GetOutput(outputTurn),
                item.MinimumOutput,
                item.IsActive))
            .ToArray();

        int visibleTurn = IsFinished && LastTurnReport != null
            ? LastTurnReport.Turn.Value
            : _simulation.CurrentTurn.Value;

        return new WorldStateResponse(
            visibleTurn,
            _simulation.CurrentCalendarDay.Value,
            markets,
            companies,
            ownCompany,
            sites,
            IsFinished,
            WinnerCompanyId);
    }

    public string ComputeStateHash(int revision)
    {
        var canonical = new StringBuilder(4096);
        Append(canonical, revision);
        Append(canonical, _simulation.CurrentTurn.Value);
        Append(canonical, _simulation.CurrentCalendarDay.Value);
        Append(canonical, IsFinished ? 1 : 0);
        Append(canonical, WinnerCompanyId);

        foreach (MarketRuntimeState market in _world.Markets
                     .OrderBy(item => item.RegionId.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.Definition.Id.Value, StringComparer.Ordinal))
        {
            Append(canonical, market.RegionId.Value);
            Append(canonical, market.Definition.Id.Value);
            Append(canonical, market.MarketState.CurrentPrice);
            Append(canonical, market.MarketState.DailySupply);
            Append(canonical, market.MarketState.DailyDemand);
            Append(canonical, market.MarketState.MarketStock);
        }

        foreach (CompanyEconomyRuntime company in _world.Companies
                     .OrderBy(item => item.Company.Id.Value, StringComparer.Ordinal))
        {
            Append(canonical, company.Company.Id.Value);
            Append(canonical, company.Company.Cash);
            Append(canonical, company.Company.Debt);
            Append(canonical, company.Company.IsBankrupt ? 1 : 0);

            foreach (var stock in company.PrimaryWarehouse.Stocks
                         .OrderBy(item => item.Key.Value, StringComparer.Ordinal))
            {
                Append(canonical, stock.Key.Value);
                Append(canonical, stock.Value.OnHand);
                Append(canonical, stock.Value.Reserved);
            }
        }

        foreach (ResourceExtractionSite site in _world.ResourceSites
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            Append(canonical, site.Id);
            Append(canonical, site.RegionId.Value);
            Append(canonical, site.ResourceId.Value);
            Append(canonical, site.DiscoveryTurn.Value);
            Append(canonical, site.GetOutput(_simulation.CurrentTurn));
            Append(canonical, site.IsActive ? 1 : 0);
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private void RegisterMarkets(RegionId region)
    {
        for (int i = 0; i < ResourceIds.Length; i++)
        {
            string id = ResourceIds[i];
            decimal price = id switch
            {
                "iron" => 100m,
                "coal" => 80m,
                "wood" => 60m,
                "oil" => 180m,
                "steel" => 220m,
                "food" => 50m,
                "medicine" => 300m,
                "machine" => 500m,
                "semiconductor" => 1200m,
                _ => 100m
            };
            var definition = new ResourceDefinition(
                new ResourceId(id),
                ResourceDisplayName(id),
                price,
                ResourceRarity.Common,
                storageVolume: 1m,
                isPerishable: id == "food" || id == "medicine");
            var state = new ResourceMarketState(definition.Id, price, 1000m);
            _world.RegisterMarket(new MarketRuntimeState(
                region,
                definition,
                state,
                baseSupply: id == "iron" ? 80m : 70m,
                baseDemand: id == "food" ? 100m : 60m));
        }
    }

    private void RegisterCompanies(
        IReadOnlyList<PvpPlayerSlot> playerSlots,
        RegionId region)
    {
        for (int i = 0; i < playerSlots.Count; i++)
        {
            PvpPlayerSlot slot = playerSlots[i];
            var company = new Company(
                slot.CompanyId,
                slot.DisplayName,
                initialCash: 1_000_000m);
            var participant = new CampaignParticipantState(company, i == 0);
            var warehouse = new Warehouse(
                new WarehouseId($"warehouse_{slot.CompanyId.Value}"),
                slot.CompanyId,
                region,
                capacity: 20_000m);

            foreach (MarketRuntimeState market in _world.Markets)
            {
                decimal initialStock = market.Definition.Id.Value switch
                {
                    "iron" => 500m,
                    "coal" => 300m,
                    "wood" => 300m,
                    "food" => 200m,
                    _ => 50m
                };
                warehouse.TryAdd(
                    market.Definition.Id,
                    initialStock,
                    market.Definition.StorageVolume);
            }

            var runtime = new CompanyEconomyRuntime(
                participant,
                warehouse,
                employeeCount: 0,
                availableWorkers: 0m,
                availablePower: 0m);
            _world.RegisterCompany(runtime);
            _participants.Add(company.Id, participant);
            _dominanceTurns.Add(company.Id, 0);
        }
    }

    private void EvaluateMatchEnd(TurnNumber resolvedTurn)
    {
        var active = _participants.Values
            .Where(item => !item.IsEliminated)
            .ToArray();

        if (active.Length == 1)
        {
            Finish(active[0].Company.Id);
            return;
        }

        if (resolvedTurn.Value >= _campaignRules.DominanceCheckStartTurn)
        {
            for (int i = 0; i < active.Length; i++)
            {
                CampaignParticipantState candidate = active[i];
                decimal candidatePower = _powerCalculator.Calculate(
                    candidate,
                    _campaignRules);
                decimal opponentPower = 0m;

                for (int j = 0; j < active.Length; j++)
                {
                    if (i != j)
                    {
                        opponentPower += _powerCalculator.Calculate(
                            active[j],
                            _campaignRules);
                    }
                }

                bool dominates = opponentPower > 0m &&
                    candidatePower >= opponentPower * _campaignRules.DominanceMultiplier;
                _dominanceTurns[candidate.Company.Id] = dominates
                    ? _dominanceTurns[candidate.Company.Id] + 1
                    : 0;

                if (_dominanceTurns[candidate.Company.Id] >=
                    _campaignRules.DominanceRequiredConsecutiveTurns)
                {
                    Finish(candidate.Company.Id);
                    return;
                }
            }
        }

        if (resolvedTurn.Value < _campaignRules.MaxTurns)
            return;

        var ranking = active
            .Select(item => new
            {
                item.Company.Id,
                Power = _powerCalculator.Calculate(item, _campaignRules)
            })
            .OrderByDescending(item => item.Power)
            .ThenBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToArray();

        if (ranking.Length > 0 &&
            (ranking.Length == 1 || ranking[0].Power > ranking[1].Power))
        {
            Finish(ranking[0].Id);
        }
        else
        {
            IsFinished = true;
            WinnerCompanyId = string.Empty;
        }
    }

    private void Finish(CompanyId winner)
    {
        IsFinished = true;
        WinnerCompanyId = winner.Value;
    }

    private static string ResourceDisplayName(string id) => id switch
    {
        "iron" => "철",
        "coal" => "석탄",
        "wood" => "목재",
        "oil" => "석유",
        "steel" => "강철",
        "food" => "식량",
        "medicine" => "의약품",
        "machine" => "기계",
        "semiconductor" => "반도체",
        _ => id
    };

    private static void Append(StringBuilder builder, string? value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length).Append(':').Append(safe).Append('|');
    }

    private static void Append(StringBuilder builder, int value) =>
        Append(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, decimal value) =>
        Append(builder, value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture));
}
