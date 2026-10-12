using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DamControlSystem.DTOs;
using DamControlSystem.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DamControlSystem.Data;

namespace DamControlSystem.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DatabaseProvider", "InMemory");
        }).CreateClient();
    }

    // Tests that GET /api/v1/dam/status returns HTTP 200 with default reservoir state and latest control log
    [Fact]
    public async Task GetDamStatus_DefaultErai_Returns200WithStateAndLog()
    {
        var response = await _client.GetAsync("/api/v1/dam/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("state", out _), "Response must contain 'state'");
        Assert.True(root.TryGetProperty("latestLog", out _), "Response must contain 'latestLog'");
    }

    // Tests that GET /api/v1/dam/{damId}/status returns HTTP 200 and the matching dam identifier for all four reservoirs
    [Theory]
    [InlineData("erai")]
    [InlineData("khadakwasla")]
    [InlineData("panshet")]
    [InlineData("mulshi")]
    public async Task GetDamStatus_AllValidDams_Return200WithCorrectDamId(string damId)
    {
        var response = await _client.GetAsync($"/api/v1/dam/{damId}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var state = doc.RootElement.GetProperty("state");
        var returnedDamId = state.GetProperty("damId").GetString();

        Assert.Equal(damId, returnedDamId);
    }

    // Tests that GET /api/v1/dam/{damId}/status with an unknown dam ID gracefully falls back to default Erai dam
    [Fact]
    public async Task GetDamStatus_UnknownDamId_FallsBackToEraiInsteadOf404()
    {
        var response = await _client.GetAsync("/api/v1/dam/nonexistent_dam_xyz/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var state = doc.RootElement.GetProperty("state");
        var returnedDamId = state.GetProperty("damId").GetString();

        Assert.Equal("erai", returnedDamId);
    }

    // Tests that POST /api/v1/dam/{damId}/update-state persists reservoir telemetry and returns an updated control log
    [Fact]
    public async Task UpdateReservoirState_ValidPayload_Returns200AndCreatesControlLog()
    {
        var payload = new { currentVolumeM3 = 150_000_000.0, waterLevelMeters = 18.5 };
        var response = await _client.PostAsJsonAsync("/api/v1/dam/erai/update-state", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("damId", out var damIdProp));
        Assert.Equal("erai", damIdProp.GetString());
        Assert.True(root.TryGetProperty("recommendedOutflowM3s", out _));
        Assert.True(root.TryGetProperty("floodAlertTriggered", out _));
    }

    // Tests that POST /api/v1/dam/{damId}/update-state returns HTTP 400 Bad Request when request body is empty
    [Fact]
    public async Task UpdateReservoirState_NullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(
            "/api/v1/dam/erai/update-state",
            new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Tests that POST /api/v1/dam/{damId}/update-state handles minimum volume boundary condition (0 m3) gracefully
    [Fact]
    public async Task UpdateReservoirState_VolumeZero_HandledGracefully()
    {
        var payload = new { currentVolumeM3 = 0.0, waterLevelMeters = 0.0 };
        var response = await _client.PostAsJsonAsync("/api/v1/dam/erai/update-state", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        Assert.False(doc.RootElement.GetProperty("floodAlertTriggered").GetBoolean());
    }

    // Tests that GET /api/v1/dam/{damId}/forecast-eval returns simulation results with simulation status prefix
    [Fact]
    public async Task ForecastEval_ValidParams_ReturnsSimulationResult()
    {
        var response = await _client.GetAsync(
            "/api/v1/dam/erai/forecast-eval?currentVolumeM3=150000000&forecastRainfallMm=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var statusMessage = doc.RootElement.GetProperty("statusMessage").GetString();

        Assert.Contains("[SIMULATION]", statusMessage);
    }

    // Tests that GET /api/v1/dam/{damId}/forecast-eval with zero parameter values evaluates to safe operational status
    [Fact]
    public async Task ForecastEval_ZeroParams_Returns200WithSafeStatus()
    {
        var response = await _client.GetAsync(
            "/api/v1/dam/erai/forecast-eval?currentVolumeM3=0&forecastRainfallMm=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.False(doc.RootElement.GetProperty("floodAlertTriggered").GetBoolean());
    }

    // Tests that GET /api/v1/dam/ai-recommendation returns HTTP 200 with all required AI advisory fields and risk level
    [Fact]
    public async Task GetAiRecommendation_DefaultErai_Returns200WithRequiredFields()
    {
        var response = await _client.GetAsync("/api/v1/dam/ai-recommendation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("riskLevel", out var riskLevel));
        Assert.True(root.TryGetProperty("advisoryMessage", out _));
        Assert.True(root.TryGetProperty("confidenceScore", out _));
        Assert.True(root.TryGetProperty("gateScheduleRecommendation", out _));
        Assert.True(root.TryGetProperty("suggestedActions", out _));

        var level = riskLevel.GetString();
        Assert.Contains(level, new[] { "NOMINAL", "WARNING", "CRITICAL" });
    }

    // Tests that POST /api/v1/dam/{damId}/emergency-alert creates an unresolved emergency alert record
    [Fact]
    public async Task CreateEmergencyAlert_ValidInputs_Returns200AndCreatesAlert()
    {
        var response = await _client.PostAsync(
            "/api/v1/dam/erai/emergency-alert?priority=CRITICAL&message=Flood%20imminent%20downstream&shiftOfficerName=Ravi%20Kumar",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.Equal("erai", root.GetProperty("damId").GetString());
        Assert.Equal("CRITICAL", root.GetProperty("priority").GetString());
        Assert.False(root.GetProperty("resolved").GetBoolean());
        Assert.True(root.GetProperty("id").GetInt64() > 0);
    }

    // Tests that GET /api/v1/dam/alerts filters and returns only active unresolved emergency alerts
    [Fact]
    public async Task GetActiveAlerts_AfterCreatingAlerts_ReturnsOnlyUnresolved()
    {
        await _client.PostAsync(
            "/api/v1/dam/erai/emergency-alert?priority=HIGH&message=Test+alert&shiftOfficerName=Officer1",
            null);

        var response = await _client.GetAsync("/api/v1/dam/alerts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var alerts = doc.RootElement.EnumerateArray().ToList();

        foreach (var alert in alerts)
        {
            Assert.False(alert.GetProperty("resolved").GetBoolean());
        }
    }

    // Tests that POST /api/v1/dam/alert/{id}/resolve marks an alert resolved and removes it from the active alerts list
    [Fact]
    public async Task ResolveAlert_ValidId_Returns200AndRemovesFromActiveList()
    {
        var createResponse = await _client.PostAsync(
            "/api/v1/dam/erai/emergency-alert?priority=HIGH&message=TestResolve&shiftOfficerName=Officer",
            null);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        long alertId = createDoc.RootElement.GetProperty("id").GetInt64();

        var resolveResponse = await _client.PostAsync($"/api/v1/dam/alert/{alertId}/resolve", null);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/v1/dam/alerts");
        var listContent = await listResponse.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listContent);

        var resolvedInList = listDoc.RootElement.EnumerateArray()
            .Any(a => a.GetProperty("id").GetInt64() == alertId);
        Assert.False(resolvedInList, "Resolved alert should not appear in active alerts list");
    }

    // Tests that POST /api/v1/dam/alert/{id}/resolve returns HTTP 404 Not Found for non-existent alert IDs
    [Fact]
    public async Task ResolveAlert_NonExistentId_Returns404()
    {
        var response = await _client.PostAsync("/api/v1/dam/alert/999999/resolve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Alert not found", content);
    }

    // Tests that POST /api/v1/dam/{damId}/submit-decision transitions control log status to PENDING_AUTHORITY
    [Fact]
    public async Task SubmitDecision_WhenLogExists_SetsStatusToPendingAuthority()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 100_000_000.0, waterLevelMeters = 15.0 });

        var response = await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("approvalStatus").GetString();
        Assert.Equal("PENDING_AUTHORITY", status);
    }

    // Tests that POST /api/v1/dam/{damId}/authority-action with APPROVE sets control log approval status to APPROVED
    [Fact]
    public async Task AuthorityAction_Approve_SetsStatusApproved()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 190_000_000.0, waterLevelMeters = 21.0 });
        await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        var response = await _client.PostAsync(
            "/api/v1/dam/erai/authority-action?action=APPROVE", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("APPROVED", doc.RootElement.GetProperty("approvalStatus").GetString());
        Assert.Contains("[APPROVED BY AUTHORITY]", doc.RootElement.GetProperty("statusMessage").GetString());
    }

    // Tests that POST /api/v1/dam/{damId}/authority-action with REJECT sets status to REJECTED and overrides recommended outflow
    [Fact]
    public async Task AuthorityAction_Reject_WithManualOutflow_SetsStatusRejected()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 190_000_000.0, waterLevelMeters = 21.0 });
        await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        var response = await _client.PostAsync(
            "/api/v1/dam/erai/authority-action?action=REJECT&manualOutflow=250", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("REJECTED", doc.RootElement.GetProperty("approvalStatus").GetString());
        Assert.Equal(250.0, doc.RootElement.GetProperty("recommendedOutflowM3s").GetDouble());
    }

    // Tests that POST /api/v1/dam/{damId}/authority-action handles invalid action strings by defaulting to rejection
    [Fact]
    public async Task AuthorityAction_InvalidActionString_FallsToReject()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 150_000_000.0, waterLevelMeters = 18.0 });
        await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        var response = await _client.PostAsync(
            "/api/v1/dam/erai/authority-action?action=INVALID_ACTION", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("REJECTED", doc.RootElement.GetProperty("approvalStatus").GetString());
    }

    // Tests that manual outflow exceeding maximum safe discharge limits clamps gate opening percentage at 100%
    [Fact]
    public async Task AuthorityAction_ManualOutflowOverMax_GateClampsAt100Percent()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 190_000_000.0, waterLevelMeters = 21.0 });
        await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        await _client.PostAsync(
            "/api/v1/dam/erai/authority-action?action=REJECT&manualOutflow=900", null);

        var statusResponse = await _client.GetAsync("/api/v1/dam/erai/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(statusContent);
        var gatePercent = doc.RootElement.GetProperty("state").GetProperty("gateOpenPercentage").GetDouble();

        Assert.Equal(100.0, gatePercent);
    }
}
