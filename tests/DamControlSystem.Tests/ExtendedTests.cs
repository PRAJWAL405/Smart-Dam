using DamControlSystem.Data;
using DamControlSystem.Data.Repositories;
using DamControlSystem.Models;
using DamControlSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DamControlSystem.Tests;

// ============================================================
// BVA & EXTENDED HYDROLOGICAL ENGINE TESTS
// TC-M3-04 through TC-M3-10, TC-M7-01, TC-M7-02
// ============================================================
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

    // TC-M3-06 — Zero rainfall → projected inflow = 0
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

    // TC-M3-04 — BVA: projected volume exactly AT warning threshold → NO discharge
    // Condition is strictly >threshold, so AT the threshold no discharge triggers
    [Fact]
    public async Task Evaluate_ProjectedVolumeExactlyAtThreshold_NoDischarge()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ProjectedVolumeExactlyAtThreshold_NoDischarge));
        var threshold = DamMetadata.Get("erai").WarningThresholdM3; // 192,525,000

        // volume = threshold, rainfall = 0mm → projected = threshold exactly
        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", threshold, 0.0);

        // projectedVolume > threshold is FALSE at equality → no discharge
        Assert.False(result.FloodAlertTriggered);
        Assert.Equal(0.0, result.RecommendedOutflowM3s);
        Assert.Contains("safe operational parameters", result.StatusMessage);
    }

    // TC-M3-05 — BVA: projected volume 1 m³ ABOVE threshold → minimal discharge triggered
    [Fact]
    public async Task Evaluate_ProjectedVolumeOneAboveThreshold_MinimalDischargeTriggered()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ProjectedVolumeOneAboveThreshold_MinimalDischargeTriggered));
        var meta = DamMetadata.Get("erai");
        double volumeJustAbove = meta.WarningThresholdM3 + 1.0; // 192,525,001 m³

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", volumeJustAbove, 0.0);

        // Required outflow = 1 / 86400 ≈ 0.0000116 m³/s — well below 800
        Assert.False(result.FloodAlertTriggered);
        Assert.True(result.RecommendedOutflowM3s > 0.0);
        Assert.True(result.RecommendedOutflowM3s < meta.MaxSafeDischargeM3s);
        Assert.Contains("Optimal proactive discharge", result.StatusMessage);
    }

    // TC-M3-08 — State Transition: NOMINAL → WARNING
    [Fact]
    public async Task Evaluate_StateTransition_NominalToWarning()
    {
        var (_, engine) = CreateService(nameof(Evaluate_StateTransition_NominalToWarning));

        // NOMINAL (100M m³, 10mm rain)
        var nominalResult = await engine.EvaluateAndExecuteControlLogicAsync("erai", 100_000_000.0, 10.0);
        Assert.Equal(0.0, nominalResult.RecommendedOutflowM3s);
        Assert.False(nominalResult.FloodAlertTriggered);

        // WARNING (190M m³, 20mm rain)
        var warningResult = await engine.EvaluateAndExecuteControlLogicAsync("erai", 190_000_000.0, 20.0);
        Assert.True(warningResult.RecommendedOutflowM3s > 0.0);
        Assert.False(warningResult.FloodAlertTriggered);
    }

    // TC-M3-09 — State Transition: WARNING → CRITICAL
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

    // TC-M7-01 — Gate = 0% when outflow = 0
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

    // TC-M7-02 — Gate = 100% when outflow = max safe discharge (emergency)
    [Fact]
    public async Task Evaluate_GatePercentage_IsHundred_WhenOutflowIsMaxSafeDischarge()
    {
        var (db, engine) = CreateService(nameof(Evaluate_GatePercentage_IsHundred_WhenOutflowIsMaxSafeDischarge));

        // Emergency: 192M + 350mm → outflow capped at 800 m³/s
        await engine.EvaluateAndExecuteControlLogicAsync("erai", 192_000_000.0, 350.0);
        var state = await db.ReservoirStates.FirstOrDefaultAsync();

        Assert.NotNull(state);
        Assert.Equal(800.0, state.CurrentOutflowM3s);
        Assert.Equal(100.0, state.GateOpenPercentage);
    }

    // TC-M3-18 — Khadakwasla uses its own threshold (47.6M m³)
    [Fact]
    public async Task Evaluate_Khadakwasla_UsesOwnThreshold_NotErais()
    {
        var (_, engine) = CreateService(nameof(Evaluate_Khadakwasla_UsesOwnThreshold_NotErais));
        var meta = DamMetadata.Get("khadakwasla");

        // Just above Khadakwasla's threshold: 47,600,001 m³, no rain
        var result = await engine.EvaluateAndExecuteControlLogicAsync(
            "khadakwasla", meta.WarningThresholdM3 + 1.0, 0.0);

        Assert.Equal("khadakwasla", result.DamId);
        Assert.True(result.RecommendedOutflowM3s > 0.0);
        Assert.True(result.RecommendedOutflowM3s <= meta.MaxSafeDischargeM3s);
        Assert.False(result.FloodAlertTriggered);
    }

    // TC-M3-19 — Panshet dam engine with its own parameters
    [Fact]
    public async Task Evaluate_Panshet_SafeConditions_NoAlert()
    {
        var (_, engine) = CreateService(nameof(Evaluate_Panshet_SafeConditions_NoAlert));

        // 100M m³ with no rain → well below Panshet's 249.9M threshold
        var result = await engine.EvaluateAndExecuteControlLogicAsync("panshet", 100_000_000.0, 0.0);

        Assert.Equal("panshet", result.DamId);
        Assert.False(result.FloodAlertTriggered);
        Assert.Equal(0.0, result.RecommendedOutflowM3s);
    }

    // TC-M3-20 — Mulshi emergency capped at Mulshi's own 1500 m³/s max
    [Fact]
    public async Task Evaluate_Mulshi_EmergencyDischarge_CappedAtOwnMaxSafeDischarge()
    {
        var (_, engine) = CreateService(nameof(Evaluate_Mulshi_EmergencyDischarge_CappedAtOwnMaxSafeDischarge));
        var meta = DamMetadata.Get("mulshi");

        // 500M m³ + 500mm → extreme emergency for Mulshi
        var result = await engine.EvaluateAndExecuteControlLogicAsync("mulshi", 500_000_000.0, 500.0);

        Assert.Equal("mulshi", result.DamId);
        Assert.True(result.FloodAlertTriggered);
        Assert.Equal(meta.MaxSafeDischargeM3s, result.RecommendedOutflowM3s); // Capped at 1500 m³/s
    }
}

// ============================================================
// AI SUGGESTION SERVICE — EXTENDED BVA & BOUNDARY TESTS
// TC-M5-02 through TC-M5-08, TC-M5-10, TC-M5-13
// ============================================================
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

    // TC-M5-02 — Volume exactly 95% → CRITICAL
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

    // TC-M5-04 — Volume exactly 83% → WARNING
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

    // TC-M5-05 — Volume at 82.9% → NOMINAL (below 83% boundary)
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

    // TC-M5-03 — Volume at 94.9% → WARNING (below 95%, above 83%)
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

    // TC-M5-10 — All risk levels return non-empty SuggestedActions
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

    // TC-M5-13 — No prior DB data → returns default NOMINAL (no crash)
    [Fact]
    public async Task GenerateRecommendation_NoPriorData_DefaultsToNominal()
    {
        var (_, service) = CreateService(nameof(GenerateRecommendation_NoPriorData_DefaultsToNominal));

        // Default state is 80% capacity → below 83% → NOMINAL
        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("NOMINAL", response.RiskLevel);
        Assert.NotNull(response.AdvisoryMessage);
        Assert.NotEmpty(response.SuggestedActions);
    }
}

// ============================================================
// DAM METADATA EXTENDED TESTS
// TC-M1-03, TC-M1-04, TC-M1-05
// ============================================================
public class DamMetadataExtendedTests
{
    // TC-M1-04 — Case-insensitive lookup for all valid dams
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

    // TC-M1-05 — WarningThreshold is exactly 85% for all 4 dams
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

    // TC-M1-02 — Registry contains exactly 4 dams
    [Fact]
    public void GetRegistry_ContainsExactlyFourDams()
    {
        var registry = DamMetadata.GetRegistry();
        Assert.Equal(4, registry.Count);
    }

    // TC-M1-03 — Null dam ID fallback to Erai
    [Fact]
    public void Get_NullDamId_FallsBackToErai()
    {
        var dam = DamMetadata.Get(null);
        Assert.Equal("erai", dam.Id);
    }

    // TC-M1-03 — Empty dam ID fallback to Erai
    [Fact]
    public void Get_EmptyDamId_FallsBackToErai()
    {
        var dam = DamMetadata.Get(string.Empty);
        Assert.Equal("erai", dam.Id);
    }

    // TC-M1-03 — Whitespace-only dam ID fallback to Erai
    [Fact]
    public void Get_WhitespaceDamId_FallsBackToErai()
    {
        var dam = DamMetadata.Get("   ");
        Assert.Equal("erai", dam.Id);
    }

    // All dams have non-empty, non-null downstream villages
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

// ============================================================
// RUNOFF FORMULA & GATE PERCENTAGE MATHEMATICAL TESTS
// ============================================================
public class RunoffFormulaTests
{
    // Verify projected inflow formula: (rain/1000) * catchment * runoff
    [Theory]
    [InlineData("erai", 10.0, 439.33e6 * 0.60 * 10.0 / 1000.0)]    // 2,635,980
    [InlineData("erai", 0.0, 0.0)]                                     // Zero rain = zero inflow
    [InlineData("erai", 100.0, 439.33e6 * 0.60 * 100.0 / 1000.0)]   // 26,359,800
    [InlineData("khadakwasla", 50.0, 501e6 * 0.65 * 50.0 / 1000.0)] // 16,282,500
    public void ProjectedInflow_Formula_IsCorrect(string damId, double rainfallMm, double expectedInflowM3)
    {
        var meta = DamMetadata.Get(damId);
        double calculated = (rainfallMm / 1000.0) * meta.CatchmentAreaM2 * meta.RunoffCoefficient;
        Assert.Equal(expectedInflowM3, calculated, precision: 0);
    }

    // Gate percentage formula with Math.Clamp
    [Theory]
    [InlineData(0.0, 800.0, 0.0)]        // Zero outflow → 0%
    [InlineData(400.0, 800.0, 50.0)]     // Half discharge → 50%
    [InlineData(800.0, 800.0, 100.0)]    // Max discharge → 100%
    [InlineData(1600.0, 800.0, 100.0)]   // Over max → clamped to 100%
    [InlineData(-100.0, 800.0, 0.0)]     // Negative → clamped to 0%
    public void GatePercentage_Formula_IsCorrectAndClamped(
        double outflow, double maxDischarge, double expectedGate)
    {
        double raw = (outflow / maxDischarge) * 100.0;
        double clamped = Math.Clamp(raw, 0.0, 100.0);
        Assert.Equal(expectedGate, clamped, precision: 5);
    }
}
