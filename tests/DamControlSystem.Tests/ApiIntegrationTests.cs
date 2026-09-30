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

// ============================================================
// API INTEGRATION TESTS — DamController
// Uses WebApplicationFactory<Program> to host the real app in-memory
// TC-M2-01 through TC-M9-05
// ============================================================
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace DB with InMemory for API integration tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<SmartDamDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                services.AddDbContext<SmartDamDbContext>(options =>
                    options.UseInMemoryDatabase("ApiTestDb_" + Guid.NewGuid()));
            });
        }).CreateClient();
    }

    // ────────────────────────────────────────────────────────────
    // MODULE M2: Reservoir State — API tests
    // ────────────────────────────────────────────────────────────

    // TC-M2-01 — GET /api/v1/dam/status returns 200
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

    // TC-M2-02 — GET /api/v1/dam/{damId}/status for all 4 dams
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

    // TC-M2-03 — Unknown damId falls back to Erai (PD-01 documented)
    [Fact]
    public async Task GetDamStatus_UnknownDamId_FallsBackToEraiInsteadOf404()
    {
        var response = await _client.GetAsync("/api/v1/dam/nonexistent_dam_xyz/status");

        // ACTUAL BEHAVIOR: returns 200 with Erai data (silent fallback — documented as PD-01)
        // EXPECTED (ideal): 404 Not Found
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var state = doc.RootElement.GetProperty("state");
        var returnedDamId = state.GetProperty("damId").GetString();

        // Document the actual behavior: fallback to "erai"
        Assert.Equal("erai", returnedDamId);
        // Note: This is a defect — PD-01. The API should return 404 for unknown damId.
    }

    // TC-M2-04 — POST update-state persists correctly and returns control log
    [Fact]
    public async Task UpdateReservoirState_ValidPayload_Returns200AndCreatesControlLog()
    {
        var payload = new { currentVolumeM3 = 150_000_000.0, waterLevelMeters = 18.5 };
        var response = await _client.PostAsJsonAsync("/api/v1/dam/erai/update-state", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Response should be the control log
        Assert.True(root.TryGetProperty("damId", out var damIdProp));
        Assert.Equal("erai", damIdProp.GetString());
        Assert.True(root.TryGetProperty("recommendedOutflowM3s", out _));
        Assert.True(root.TryGetProperty("floodAlertTriggered", out _));
    }

    // TC-M2-05 — POST update-state with null body → 400
    [Fact]
    public async Task UpdateReservoirState_NullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(
            "/api/v1/dam/erai/update-state",
            new StringContent("", Encoding.UTF8, "application/json"));

        // ASP.NET Core model binding returns 400 for empty body with required fields
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // TC-M2-06 — BVA: volume = 0 (minimum boundary)
    [Fact]
    public async Task UpdateReservoirState_VolumeZero_HandledGracefully()
    {
        var payload = new { currentVolumeM3 = 0.0, waterLevelMeters = 0.0 };
        var response = await _client.PostAsJsonAsync("/api/v1/dam/erai/update-state", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        // With 0 volume, projected volume = only inflow → should be in safe zone
        Assert.Equal(false, doc.RootElement.GetProperty("floodAlertTriggered").GetBoolean());
    }

    // ────────────────────────────────────────────────────────────
    // MODULE M13: Forecast Simulation — API tests
    // ────────────────────────────────────────────────────────────

    // TC-M13-01 — GET forecast-eval returns simulation result with [SIMULATION] prefix
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

    // TC-M13-02 — forecast-eval with 0 params (defaults)
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

    // ────────────────────────────────────────────────────────────
    // MODULE M5: AI Recommendation — API tests
    // ────────────────────────────────────────────────────────────

    // TC-API-AI-01 — GET ai-recommendation returns valid structure
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

        // Risk level must be one of the 3 valid values
        var level = riskLevel.GetString();
        Assert.Contains(level, new[] { "NOMINAL", "WARNING", "CRITICAL" });
    }

    // ────────────────────────────────────────────────────────────
    // MODULE M8: Emergency Alert — API tests
    // ────────────────────────────────────────────────────────────

    // TC-M8-01 — Create emergency alert with valid inputs
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
        Assert.Equal(false, root.GetProperty("resolved").GetBoolean());
        Assert.True(root.GetProperty("id").GetInt64() > 0);
    }

    // TC-M8-02 — GET /alerts returns only unresolved
    [Fact]
    public async Task GetActiveAlerts_AfterCreatingAlerts_ReturnsOnlyUnresolved()
    {
        // Create an alert
        await _client.PostAsync(
            "/api/v1/dam/erai/emergency-alert?priority=HIGH&message=Test+alert&shiftOfficerName=Officer1",
            null);

        // Get all active alerts
        var response = await _client.GetAsync("/api/v1/dam/alerts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var alerts = doc.RootElement.EnumerateArray().ToList();

        // All returned alerts must be unresolved
        foreach (var alert in alerts)
        {
            Assert.Equal(false, alert.GetProperty("resolved").GetBoolean());
        }
    }

    // TC-M8-03 — Resolve an existing alert
    [Fact]
    public async Task ResolveAlert_ValidId_Returns200AndRemovesFromActiveList()
    {
        // Create alert
        var createResponse = await _client.PostAsync(
            "/api/v1/dam/erai/emergency-alert?priority=HIGH&message=TestResolve&shiftOfficerName=Officer",
            null);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createContent);
        long alertId = createDoc.RootElement.GetProperty("id").GetInt64();

        // Resolve it
        var resolveResponse = await _client.PostAsync($"/api/v1/dam/alert/{alertId}/resolve", null);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        // Get alerts — resolved one should not appear
        var listResponse = await _client.GetAsync("/api/v1/dam/alerts");
        var listContent = await listResponse.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listContent);

        var resolvedInList = listDoc.RootElement.EnumerateArray()
            .Any(a => a.GetProperty("id").GetInt64() == alertId);
        Assert.False(resolvedInList, "Resolved alert should not appear in active alerts list");
    }

    // TC-M8-04 — Resolve non-existent alert ID → 404
    [Fact]
    public async Task ResolveAlert_NonExistentId_Returns404()
    {
        var response = await _client.PostAsync("/api/v1/dam/alert/999999/resolve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Alert not found", content);
    }

    // ────────────────────────────────────────────────────────────
    // MODULE M9: Authority Workflow — API tests
    // ────────────────────────────────────────────────────────────

    // TC-M9-01 — Submit decision → PENDING_AUTHORITY
    [Fact]
    public async Task SubmitDecision_WhenLogExists_SetsStatusToPendingAuthority()
    {
        // First ensure a control log exists
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

    // TC-M9-02 — Authority APPROVE → status APPROVED
    [Fact]
    public async Task AuthorityAction_Approve_SetsStatusApproved()
    {
        // Set up state and log
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

    // TC-M9-03 — Authority REJECT with manual outflow → REJECTED
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

    // TC-M9-05 — Invalid action string falls silently to REJECT (PD-04)
    [Fact]
    public async Task AuthorityAction_InvalidActionString_FallsToReject()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 150_000_000.0, waterLevelMeters = 18.0 });
        await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        // INVALID_ACTION is not APPROVE → falls to REJECT path (PD-04)
        var response = await _client.PostAsync(
            "/api/v1/dam/erai/authority-action?action=INVALID_ACTION", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        // Documents actual behavior: silently rejected
        Assert.Equal("REJECTED", doc.RootElement.GetProperty("approvalStatus").GetString());
    }

    // TC-M9-04 — BVA: manualOutflow > maxSafeDischarge → gate clamped at 100%
    [Fact]
    public async Task AuthorityAction_ManualOutflowOverMax_GateClampsAt100Percent()
    {
        await _client.PostAsJsonAsync(
            "/api/v1/dam/erai/update-state",
            new { currentVolumeM3 = 190_000_000.0, waterLevelMeters = 21.0 });
        await _client.PostAsync("/api/v1/dam/erai/submit-decision", null);

        // manualOutflow = 900 > 800 max → gate should clamp to 100%
        await _client.PostAsync(
            "/api/v1/dam/erai/authority-action?action=REJECT&manualOutflow=900", null);

        // Check state
        var statusResponse = await _client.GetAsync("/api/v1/dam/erai/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(statusContent);
        var gatePercent = doc.RootElement.GetProperty("state").GetProperty("gateOpenPercentage").GetDouble();

        Assert.Equal(100.0, gatePercent);
    }
}
