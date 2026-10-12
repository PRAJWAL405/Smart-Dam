using DamControlSystem.Data;
using DamControlSystem.Data.Repositories;
using DamControlSystem.Models;
using DamControlSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DamControlSystem.Tests;

public class HydrologicalEngineTests
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

    // Tests that normal water levels below 85% threshold produce zero discharge and no flood alert
    [Fact]
    public async Task Evaluate_SafeConditions_NoOutflowAndNoFloodAlert()
    {
        var (_, engine) = CreateService(nameof(Evaluate_SafeConditions_NoOutflowAndNoFloodAlert));

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", 100_000_000.0, 10.0);

        Assert.Equal("erai", result.DamId);
        Assert.False(result.FloodAlertTriggered);
        Assert.Equal(0.0, result.RecommendedOutflowM3s);
        Assert.Contains("safe operational parameters", result.StatusMessage);
    }

    // Tests that water levels projected above 85% threshold trigger calculated proactive outflow within safe discharge limits
    [Fact]
    public async Task Evaluate_ProactiveDischarge_ReleasesRequiredOutflowWithoutAlert()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ProactiveDischarge_ReleasesRequiredOutflowWithoutAlert));

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", 190_000_000.0, 20.0);

        Assert.False(result.FloodAlertTriggered);
        Assert.True(result.RecommendedOutflowM3s > 0.0);
        Assert.True(result.RecommendedOutflowM3s <= DamMetadata.Get("erai").MaxSafeDischargeM3s);
        Assert.Contains("Optimal proactive discharge", result.StatusMessage);
    }

    // Tests that extreme rainfall exceeding max safe discharge triggers an emergency flood alert and caps discharge
    [Fact]
    public async Task Evaluate_ExtremeRainfall_TriggersFloodAlertAndCapsDischarge()
    {
        var (_, engine) = CreateService(nameof(Evaluate_ExtremeRainfall_TriggersFloodAlertAndCapsDischarge));

        var result = await engine.EvaluateAndExecuteControlLogicAsync("erai", 192_000_000.0, 350.0);

        Assert.True(result.FloodAlertTriggered);
        Assert.Equal(DamMetadata.Get("erai").MaxSafeDischargeM3s, result.RecommendedOutflowM3s);
        Assert.Contains("EMERGENCY", result.StatusMessage);
    }

    // Tests that simulation mode calculates hydrological response without persisting state to the database
    [Fact]
    public void SimulateControlLogic_DoesNotPersistToDatabase()
    {
        var (db, engine) = CreateService(nameof(SimulateControlLogic_DoesNotPersistToDatabase));

        var simulation = engine.SimulateControlLogic("khadakwasla", 50_000_000.0, 50.0);

        Assert.NotNull(simulation);
        Assert.Contains("[SIMULATION]", simulation.StatusMessage);
        Assert.Empty(db.ControlLogs);
        Assert.Empty(db.ReservoirStates);
    }
}
