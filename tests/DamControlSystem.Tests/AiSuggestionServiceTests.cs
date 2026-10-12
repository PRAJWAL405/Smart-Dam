using DamControlSystem.Data;
using DamControlSystem.Data.Repositories;
using DamControlSystem.Models;
using DamControlSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DamControlSystem.Tests;

public class AiSuggestionServiceTests
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

    // Tests that an active flood alert causes the AI service to return a CRITICAL risk level with urgent recommendations
    [Fact]
    public async Task GenerateRecommendation_WhenFloodAlertActive_ReturnsCritical()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_WhenFloodAlertActive_ReturnsCritical));
        var meta = DamMetadata.Get("erai");

        await db.ControlLogs.AddAsync(new ControlLog(
            id: 0,
            damId: "erai",
            timestamp: DateTime.UtcNow,
            forecastPrecipitationMm: 120.0,
            predictedInflowM3: 31000000.0,
            recommendedOutflowM3s: 800.0,
            floodAlertTriggered: true,
            statusMessage: "Emergency alert"
        ));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("CRITICAL", response.RiskLevel);
        Assert.True(response.ConfidenceScore >= 98.0);
        Assert.Contains("URGENT", response.AdvisoryMessage);
        Assert.Contains("IMMEDIATE ACTION", response.GateScheduleRecommendation);
        Assert.NotEmpty(response.SuggestedActions);
    }

    // Tests that high reservoir volume exceeding 85% causes the AI service to return a WARNING risk level with proactive release schedule
    [Fact]
    public async Task GenerateRecommendation_WhenVolumeHigh_ReturnsWarning()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_WhenVolumeHigh_ReturnsWarning));
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0,
            damId: "erai",
            timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * 0.88,
            waterLevelMeters: 21.0,
            currentOutflowM3s: 150.0,
            gateOpenPercentage: 20.0
        ));
        await db.ControlLogs.AddAsync(new ControlLog(
            id: 0,
            damId: "erai",
            timestamp: DateTime.UtcNow,
            forecastPrecipitationMm: 30.0,
            predictedInflowM3: 5000000.0,
            recommendedOutflowM3s: 150.0,
            floodAlertTriggered: false,
            statusMessage: "Proactive release"
        ));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("WARNING", response.RiskLevel);
        Assert.Contains("PROACTIVE RELEASE", response.GateScheduleRecommendation);
    }

    // Tests that safe reservoir levels below 85% with no alerts cause the AI service to return a NOMINAL risk level
    [Fact]
    public async Task GenerateRecommendation_WhenNormal_ReturnsNominal()
    {
        var (db, service) = CreateService(nameof(GenerateRecommendation_WhenNormal_ReturnsNominal));
        var meta = DamMetadata.Get("erai");

        await db.ReservoirStates.AddAsync(new ReservoirState(
            id: 0,
            damId: "erai",
            timestamp: DateTime.UtcNow,
            currentVolumeM3: meta.MaxCapacityM3 * 0.70,
            waterLevelMeters: 16.0,
            currentOutflowM3s: 0.0,
            gateOpenPercentage: 0.0
        ));
        await db.SaveChangesAsync();

        var response = await service.GenerateRecommendationAsync("erai");

        Assert.Equal("NOMINAL", response.RiskLevel);
        Assert.Contains("NOMINAL OPERATIONAL", response.GateScheduleRecommendation);
    }
}
