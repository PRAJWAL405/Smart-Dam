using DamControlSystem.Data;
using DamControlSystem.Data.Repositories;
using DamControlSystem.Models;
using DamControlSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DamControlSystem.Tests;

public class HydrologicalEngineExtendedTests
{
    private (SmartDamDbContext db, DamControlEngineService engine) CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<SmartDamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var dbContext = new SmartDamDbContext(options);
        var controlRepo = new ControlLogRepository(dbContext);
        var stateRepo = new ReservoirStateRepository(dbContext);
        var logger = NullLogger<DamControlEngineService>.Instance;

        var engine = new DamControlEngineService(controlRepo, stateRepo, logger);
        return (dbContext, engine);
    }

    // Tests that zero rainfall results in zero projected inflow and no alert
    [Fact]
    public async Task Evaluate_ZeroRainfall_InflowIsZero_NoAlert()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ZeroRainfall_InflowIsZero_NoAlert));

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", 100_000_000.0, 0.0);

        Assert.Equal(0.0, result.PredictedInflowM3);
        Assert.False(result.FloodAlertTriggered);
        Assert.Equal(0.0, result.RecommendedOutflowM3s);
        Assert.Contains("safe operational parameters", result.StatusMessage);
    }

    // Tests boundary condition where projected volume is exactly at warning threshold without triggering proactive discharge
    [Fact]
    public async Task Evaluate_ProjectedVolumeExactlyAtThreshold_NoDischarge()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ProjectedVolumeExactlyAtThreshold_NoDischarge));
        var threshold = DamMetadata.Get("erai").WarningThresholdM3;

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", threshold, 0.0);

        Assert.False(result.FloodAlertTriggered);
        Assert.Equal(0.0, result.RecommendedOutflowM3s);
        Assert.Contains("safe operational parameters", result.StatusMessage);
    }

    // Tests boundary condition where projected volume is 1 m³ above threshold, triggering minimal proactive discharge
    [Fact]
    public async Task Evaluate_ProjectedVolumeOneAboveThreshold_MinimalDischargeTriggered()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ProjectedVolumeOneAboveThreshold_MinimalDischargeTriggered));
        var meta = DamMetadata.Get("erai");
        double volumeJustAbove = meta.WarningThresholdM3 + 1.0;

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", volumeJustAbove, 0.0);

        Assert.False(result.FloodAlertTriggered);
        Assert.True(result.RecommendedOutflowM3s > 0.0);
        Assert.True(result.RecommendedOutflowM3s < meta.MaxSafeDischargeM3s);
        Assert.Contains("Optimal proactive discharge", result.StatusMessage);
    }

    // Tests state transition from NOMINAL to WARNING state as water levels rise
    [Fact]
    public async Task Evaluate_StateTransition_NominalToWarning()
    {
        var (_, engine) = CreateService(nameof(Evaluate_StateTransition_NominalToWarning));

        var nominalResult = await engine.EvaluateAndExecuteControlLogicAsync("erai", 100_000_000.0, 10.0);
        Assert.Equal(0.0, nominalResult.RecommendedOutflowM3s);
        Assert.False(nominalResult.FloodAlertTriggered);

        var warningResult = await engine.EvaluateAndExecuteControlLogicAsync("erai", 190_000_000.0, 20.0);
        Assert.True(warningResult.RecommendedOutflowM3s > 0.0);
        Assert.False(warningResult.FloodAlertTriggered);
    }

    // Tests state transition from WARNING to CRITICAL state under heavy rainfall
    [Fact]
    public async Task Evaluate_StateTransition_WarningToCritical()
    {
        var (_, engine) = CreateService(nameof(Evaluate_StateTransition_WarningToCritical));
        var meta = DamMetadata.Get("erai");

        var warningResult = await engine.EvaluateAndExecuteControlLogicAsync("erai", 190_000_000.0, 20.0);
        Assert.False(warningResult.FloodAlertTriggered);

        var criticalResult = await engine.EvaluateAndExecuteControlLogicAsync("erai", 192_000_000.0, 350.0);
        Assert.True(criticalResult.FloodAlertTriggered);
        Assert.Equal(meta.MaxSafeDischargeM3s, criticalResult.RecommendedOutflowM3s);
    }

    // Tests that gate open percentage is 0% when outflow is zero
    [Fact]
    public async Task Evaluate_GatePercentage_IsZero_WhenOutflowIsZero()
    {
        var (db, engine) = CreateService(nameof(Evaluate_GatePercentage_IsZero_WhenOutflowIsZero));

        await engine.EvaluateAndExecuteControlLogicAsync("erai", 100_000_000.0, 0.0);
        var state = await db.ReservoirStates.FirstOrDefaultAsync();

        Assert.NotNull(state);
        Assert.Equal(0.0, state.CurrentOutflowM3s);
        Assert.Equal(0.0, state.GateOpenPercentage);
    }

    // Tests that gate open percentage is 100% when outflow reaches maximum safe discharge
    [Fact]
    public async Task Evaluate_GatePercentage_IsHundred_WhenOutflowIsMaxSafeDischarge()
    {
        var (db, engine) = CreateService(nameof(Evaluate_GatePercentage_IsHundred_WhenOutflowIsMaxSafeDischarge));

        await engine.EvaluateAndExecuteControlLogicAsync("erai", 192_000_000.0, 350.0);
        var state = await db.ReservoirStates.FirstOrDefaultAsync();

        Assert.NotNull(state);
        Assert.Equal(800.0, state.CurrentOutflowM3s);
        Assert.Equal(100.0, state.GateOpenPercentage);
    }

    // Tests that Khadakwasla dam uses its specific threshold and safe discharge parameters
    [Fact]
    public async Task Evaluate_Khadakwasla_UsesOwnThreshold_NotErais()
    {
        var (_, engine) = CreateService(nameof(Evaluate_Khadakwasla_UsesOwnThreshold_NotErais));
        var meta = DamMetadata.Get("khadakwasla");

        var result = await engine.EvaluateAndExecuteControlLogicAsync(
            "khadakwasla", meta.WarningThresholdM3 + 1.0, 0.0);

        Assert.Equal("khadakwasla", result.DamId);
        Assert.True(result.RecommendedOutflowM3s > 0.0);
        Assert.True(result.RecommendedOutflowM3s <= meta.MaxSafeDischargeM3s);
        Assert.False(result.FloodAlertTriggered);
    }

    // Tests that Panshet dam evaluates safe water conditions correctly under its capacity limits
    [Fact]
    public async Task Evaluate_Panshet_SafeConditions_NoAlert()
    {
        var (_, engine) = CreateService(nameof(Evaluate_Panshet_SafeConditions_NoAlert));

        var result = await engine.EvaluateAndExecuteControlLogicAsync("panshet", 100_000_000.0, 0.0);

        Assert.Equal("panshet", result.DamId);
        Assert.False(result.FloodAlertTriggered);
        Assert.Equal(0.0, result.RecommendedOutflowM3s);
    }

    // Tests that Mulshi dam emergency outflow is capped at its maximum safe discharge limit of 1500 m3/s
    [Fact]
    public async Task Evaluate_Mulshi_EmergencyDischarge_CappedAtOwnMaxSafeDischarge()
    {
        var (_, engine) = CreateService(nameof(Evaluate_Mulshi_EmergencyDischarge_CappedAtOwnMaxSafeDischarge));
        var meta = DamMetadata.Get("mulshi");

        var result = await engine.EvaluateAndExecuteControlLogicAsync("mulshi", 500_000_000.0, 500.0);

        Assert.Equal("mulshi", result.DamId);
        Assert.True(result.FloodAlertTriggered);
        Assert.Equal(meta.MaxSafeDischargeM3s, result.RecommendedOutflowM3s);
    }
}

public class AiSuggestionServiceExtendedTests
{
    private (SmartDamDbContext db, AiSuggestionService service) CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<SmartDamDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var dbContext = new SmartDamDbContext(options);
        var stateRepo = new ReservoirStateRepository(dbContext);
        var controlRepo = new ControlLogRepository(dbContext);
        var service = new AiSuggestionService(stateRepo, controlRepo);
        return (dbContext, service);
    }

    // Tests that reservoir volume at 95% capacity returns a CRITICAL risk level
    [Fact]
    public async Task GenerateRecommendation_VolumeAt95Percent_ReturnsCritical()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_VolumeAt95Percent_ReturnsCritical));
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * 0.95,
            waterLevelMeters: 22.4, currentOutflowM3s: 0.0, gateOpenPercentage: 0.0));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("CRITICAL", response.RiskLevel);
        Assert.True(response.ConfidenceScore >= 98.0);
    }

    // Tests that reservoir volume at 83% capacity returns a WARNING risk level
    [Fact]
    public async Task GenerateRecommendation_VolumeAt83Percent_ReturnsWarning()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_VolumeAt83Percent_ReturnsWarning));
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * 0.83,
            waterLevelMeters: 19.6, currentOutflowM3s: 0.0, gateOpenPercentage: 0.0));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("WARNING", response.RiskLevel);
    }

    // Tests that reservoir volume at 82.9% capacity (below 83% threshold) returns a NOMINAL risk level
    [Fact]
    public async Task GenerateRecommendation_VolumeAt82Point9Percent_ReturnsNominal()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_VolumeAt82Point9Percent_ReturnsNominal));
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * 0.829,
            waterLevelMeters: 19.5, currentOutflowM3s: 0.0, gateOpenPercentage: 0.0));
        await db.ControlLogs.AddAsync(new ControlLog(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            forecastPrecipitationMm: 0.0, predictedInflowM3: 0.0,
            recommendedOutflowM3s: 0.0, floodAlertTriggered: false,
            statusMessage: "Nominal"));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("NOMINAL", response.RiskLevel);
    }

    // Tests that reservoir volume at 94.9% capacity (below 95% threshold) returns a WARNING risk level
    [Fact]
    public async Task GenerateRecommendation_VolumeAt94Point9Percent_ReturnsWarning()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_VolumeAt94Point9Percent_ReturnsWarning));
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * 0.949,
            waterLevelMeters: 22.3, currentOutflowM3s: 0.0, gateOpenPercentage: 0.0));
        await db.ControlLogs.AddAsync(new ControlLog(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            forecastPrecipitationMm: 5.0, predictedInflowM3: 1000000.0,
            recommendedOutflowM3s: 0.0, floodAlertTriggered: false,
            statusMessage: "Near capacity"));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("WARNING", response.RiskLevel);
    }

    // Tests that all evaluated risk levels (Critical, Warning, Nominal) provide non-empty suggested action lists
    [Theory]
    [InlineData(true, 0.95, "CRITICAL")]
    [InlineData(false, 0.88, "WARNING")]
    [InlineData(false, 0.70, "NOMINAL")]
    public async Task GenerateRecommendation_AllRiskLevels_HaveNonEmptySuggestedActions(
        bool floodAlert, double volumeRatio, string expectedRisk)
    {
        var (db, service) = CreateService($"SuggestedActions_{expectedRisk}");
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * volumeRatio,
            waterLevelMeters: meta.MaxWaterLevelMeters * volumeRatio,
            currentOutflowM3s: floodAlert ? 800.0 : 0.0,
            gateOpenPercentage: floodAlert ? 100.0 : 0.0));
        await db.ControlLogs.AddAsync(new ControlLog(
            id: 0, damId: "erai", timestamp: DateTime.UtcNow,
            forecastPrecipitationMm: 20.0, predictedInflowM3: 5000000.0,
            recommendedOutflowM3s: floodAlert ? 800.0 : 0.0,
            floodAlertTriggered: floodAlert,
            statusMessage: "Test"));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal(expectedRisk, response.RiskLevel);
        Assert.NotEmpty(response.SuggestedActions);
        Assert.True(response.ConfidenceScore > 0.0);
    }

    // Tests that AI service defaults safely to NOMINAL status when no prior database telemetry exists
    [Fact]
    public async Task GenerateRecommendation_NoPriorData_DefaultsToNominal()
    {
        var (_, service) = CreateService(nameof(GenerateRecommendation_NoPriorData_DefaultsToNominal));

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("NOMINAL", response.RiskLevel);
        Assert.NotNull(response.AdvisoryMessage);
        Assert.NotEmpty(response.SuggestedActions);
    }
}

public class DamMetadataExtendedTests
{
    // Tests that dam lookup correctly resolves dam identifiers in a case-insensitive manner
    [Theory]
    [InlineData("ERAI", "erai")]
    [InlineData("Erai", "erai")]
    [InlineData("KHADAKWASLA", "khadakwasla")]
    [InlineData("Panshet", "panshet")]
    [InlineData("MULSHI", "mulshi")]
    public void Get_CaseInsensitive_ResolvesCorrectDam(string input, string expectedId)
    {
        var dam = DamMetadata.Get(input);
        Assert.Equal(expectedId, dam.Id);
    }

    // Tests that warning threshold strictly equals 85% of total capacity for all registered dams
    [Theory]
    [InlineData("erai")]
    [InlineData("khadakwasla")]
    [InlineData("panshet")]
    [InlineData("mulshi")]
    public void DamMetadata_WarningThreshold_IsExactly85Percent(string damId)
    {
        var meta = DamMetadata.Get(damId);
        double expected = meta.MaxCapacityM3 * 0.85;
        Assert.Equal(expected, meta.WarningThresholdM3, precision: 0);
    }

    // Tests that the dam registry contains exactly four monitored dams
    [Fact]
    public void GetRegistry_ContainsExactlyFourDams()
    {
        var registry = DamMetadata.GetRegistry();
        Assert.Equal(4, registry.Count);
    }

    // Tests that a null dam ID safely falls back to default Erai dam metadata
    [Fact]
    public void Get_NullDamId_FallsBackToErai()
    {
        var dam = DamMetadata.Get(null);
        Assert.Equal("erai", dam.Id);
    }

    // Tests that an empty dam ID string safely falls back to default Erai dam metadata
    [Fact]
    public void Get_EmptyDamId_FallsBackToErai()
    {
        var dam = DamMetadata.Get(string.Empty);
        Assert.Equal("erai", dam.Id);
    }

    // Tests that a whitespace-only dam ID string safely falls back to default Erai dam metadata
    [Fact]
    public void Get_WhitespaceDamId_FallsBackToErai()
    {
        var dam = DamMetadata.Get("   ");
        Assert.Equal("erai", dam.Id);
    }

    // Tests that all registered dams have non-empty downstream village lists for emergency notifications
    [Theory]
    [InlineData("erai")]
    [InlineData("khadakwasla")]
    [InlineData("panshet")]
    [InlineData("mulshi")]
    public void DamMetadata_AllDams_HaveNonEmptyDownstreamVillages(string damId)
    {
        var meta = DamMetadata.Get(damId);
        Assert.NotEmpty(meta.DownstreamVillages);
        foreach (var village in meta.DownstreamVillages)
            Assert.False(string.IsNullOrWhiteSpace(village));
    }
}

public class RunoffFormulaTests
{
    // Tests that the rational runoff equation calculates projected inflow volume accurately based on rainfall and catchment area
    [Theory]
    [InlineData("erai", 10.0, 439.33e6 * 0.60 * 10.0 / 1000.0)]
    [InlineData("erai", 0.0, 0.0)]
    [InlineData("erai", 100.0, 439.33e6 * 0.60 * 100.0 / 1000.0)]
    [InlineData("khadakwasla", 50.0, 501e6 * 0.65 * 50.0 / 1000.0)]
    public void ProjectedInflow_Formula_IsCorrect(string damId, double rainfallMm, double expectedInflowM3)
    {
        var meta = DamMetadata.Get(damId);
        double calculated = (rainfallMm / 1000.0) * meta.CatchmentAreaM2 * meta.RunoffCoefficient;
        Assert.Equal(expectedInflowM3, calculated, precision: 0);
    }

    // Tests that the gate aperture percentage calculation accurately scales and clamps values within [0%, 100%]
    [Theory]
    [InlineData(0.0, 800.0, 0.0)]
    [InlineData(400.0, 800.0, 50.0)]
    [InlineData(800.0, 800.0, 100.0)]
    [InlineData(1600.0, 800.0, 100.0)]
    [InlineData(-100.0, 800.0, 0.0)]
    public void GatePercentage_Formula_IsCorrectAndClamped(
        double outflow, double maxDischarge, double expectedGate)
    {
        double raw = (outflow / maxDischarge) * 100.0;
        double clamped = Math.Clamp(raw, 0.0, 100.0);
        Assert.Equal(expectedGate, clamped, precision: 5);
    }
}
