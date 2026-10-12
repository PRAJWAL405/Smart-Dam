using System.Text;
using System.Xml.Linq;

namespace DamControlSystem.Tests;

/// <summary>
/// Generates a comprehensive STQA HTML Test Report mapping test execution results
/// directly to Software Requirements Specifications (SRS).
/// </summary>
public static class StqaHtmlReportGenerator
{
    public class TestRequirementMetadata
    {
        public string RequirementId { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string RequirementTitle { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public string InvariantRule { get; set; } = string.Empty;
    }

    private static readonly Dictionary<string, TestRequirementMetadata> RequirementRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        // MODULE M1: Multi-Dam Topologies & Metadata
        ["GetRegistry_ContainsExpectedDams"] = new()
        {
            RequirementId = "REQ-M1-01",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Multi-Reservoir System Topology",
            Objective = "Verify all 4 registered dams (Erai, Khadakwasla, Panshet, Mulshi) exist in the system registry.",
            InvariantRule = "Registry.Count == 4 && Contains(erai, khadakwasla, panshet, mulshi)"
        },
        ["GetRegistry_ContainsExactlyFourDams"] = new()
        {
            RequirementId = "REQ-M1-02",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Strict Registry Cardinality",
            Objective = "Verify registry contains exactly four valid dam configurations without extraneous entries.",
            InvariantRule = "Registry.Count == 4"
        },
        ["Get_UnknownDam_FallsBackToErai"] = new()
        {
            RequirementId = "REQ-M1-03",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Dam Lookup Fault Tolerance",
            Objective = "Ensure unrecognized dam IDs fall back safely to default Erai dam configuration without throwing exceptions.",
            InvariantRule = "Get(unknown_id).Id == 'erai'"
        },
        ["Get_NullDamId_FallsBackToErai"] = new()
        {
            RequirementId = "REQ-M1-04",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Null Identifier Handling",
            Objective = "Verify passing null dam ID string defaults safely to Erai dam.",
            InvariantRule = "Get(null).Id == 'erai'"
        },
        ["Get_EmptyDamId_FallsBackToErai"] = new()
        {
            RequirementId = "REQ-M1-05",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Empty Identifier Handling",
            Objective = "Verify passing empty dam ID string defaults safely to Erai dam.",
            InvariantRule = "Get('').Id == 'erai'"
        },
        ["Get_WhitespaceDamId_FallsBackToErai"] = new()
        {
            RequirementId = "REQ-M1-06",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Whitespace Identifier Handling",
            Objective = "Verify passing whitespace-only dam ID string defaults safely to Erai dam.",
            InvariantRule = "Get('   ').Id == 'erai'"
        },
        ["Get_CaseInsensitive_ResolvesCorrectDam"] = new()
        {
            RequirementId = "REQ-M1-07",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Case-Insensitive Identifier Resolution",
            Objective = "Verify dam identifier matching is case-insensitive ('ERAI', 'Panshet', 'MULSHI').",
            InvariantRule = "Get(Upper(id)).Id == Lower(id)"
        },
        ["DamMetadata_HasAccurateCapacitiesAndSafeDischarge"] = new()
        {
            RequirementId = "REQ-M1-08",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Physical Reservoir Characteristics",
            Objective = "Validate maximum storage capacity (m³) and safe discharge rating (m³/s) per dam specifications.",
            InvariantRule = "MaxCapacityM3 > 0 && MaxSafeDischargeM3s > 0"
        },
        ["DamMetadata_WarningThreshold_IsExactly85Percent"] = new()
        {
            RequirementId = "REQ-M1-09",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "85% Warning Invariant",
            Objective = "Verify warning threshold is computed strictly at 85% of total gross storage capacity.",
            InvariantRule = "WarningThresholdM3 == MaxCapacityM3 * 0.85"
        },
        ["DamMetadata_AllDams_HaveNonEmptyDownstreamVillages"] = new()
        {
            RequirementId = "REQ-M1-10",
            Module = "Dam Metadata & Topologies",
            RequirementTitle = "Downstream Village Notification Registry",
            Objective = "Verify every dam has populated downstream settlements for emergency flood warnings.",
            InvariantRule = "DownstreamVillages.Count > 0 && NonEmpty(villageName)"
        },

        // MODULE M2 & M3: Hydrological Runoff & Control Engine
        ["ProjectedInflow_Formula_IsCorrect"] = new()
        {
            RequirementId = "REQ-M3-01",
            Module = "Hydrological Engine",
            RequirementTitle = "Rational Inflow Runoff Formula",
            Objective = "Verify inflow computation: Q = CatchmentArea × RunoffCoefficient × (Rainfall / 1000).",
            InvariantRule = "Inflow == (Rainfall / 1000) * Area * Coeff"
        },
        ["Evaluate_SafeConditions_NoOutflowAndNoFloodAlert"] = new()
        {
            RequirementId = "REQ-M3-02",
            Module = "Hydrological Engine",
            RequirementTitle = "Nominal Water Level Operation",
            Objective = "Ensure no proactive outflow is released and no flood alert triggers when water level is below 85% capacity.",
            InvariantRule = "ProjectedVol <= 85% => Outflow = 0 && Alert = false"
        },
        ["Evaluate_ProactiveDischarge_ReleasesRequiredOutflowWithoutAlert"] = new()
        {
            RequirementId = "REQ-M3-03",
            Module = "Hydrological Engine",
            RequirementTitle = "Proactive Discharge Calculation",
            Objective = "Calculate required 24-hour proactive outflow when projected volume exceeds 85% threshold within safe discharge caps.",
            InvariantRule = "ProjectedVol > 85% => Outflow = (ExcessVol / 86400) <= MaxSafe"
        },
        ["Evaluate_ExtremeRainfall_TriggersFloodAlertAndCapsDischarge"] = new()
        {
            RequirementId = "REQ-M3-04",
            Module = "Hydrological Engine",
            RequirementTitle = "Emergency Flood Alert & Discharge Clamping",
            Objective = "Trigger emergency flood alert and clamp outflow strictly at MaxSafeDischarge when excess exceeds discharge capability.",
            InvariantRule = "RequiredOutflow > MaxSafe => Alert = true && Outflow = MaxSafe"
        },
        ["SimulateControlLogic_DoesNotPersistToDatabase"] = new()
        {
            RequirementId = "REQ-M3-05",
            Module = "Hydrological Engine",
            RequirementTitle = "Hydrological Simulation Isolation",
            Objective = "Verify simulation mode computes water balance projections without persisting logs or states to the database.",
            InvariantRule = "DB.Logs.Count == Unchanged && Result.Status.Contains('[SIMULATION]')"
        },
        ["Evaluate_ZeroRainfall_InflowIsZero_NoAlert"] = new()
        {
            RequirementId = "REQ-M3-06",
            Module = "Hydrological Engine",
            RequirementTitle = "Zero Precipitation Inflow Boundary",
            Objective = "Verify 0 mm rainfall produces exactly 0 m³ inflow with zero flood alert.",
            InvariantRule = "Rainfall == 0 => Inflow == 0"
        },
        ["Evaluate_ProjectedVolumeExactlyAtThreshold_NoDischarge"] = new()
        {
            RequirementId = "REQ-M3-07",
            Module = "Hydrological Engine",
            RequirementTitle = "BVA: Exact Threshold Boundary",
            Objective = "Verify projected volume exactly equal to warning threshold does not trigger proactive outflow (strictly > threshold rule).",
            InvariantRule = "ProjectedVol == 85% => Outflow == 0"
        },
        ["Evaluate_ProjectedVolumeOneAboveThreshold_MinimalDischargeTriggered"] = new()
        {
            RequirementId = "REQ-M3-08",
            Module = "Hydrological Engine",
            RequirementTitle = "BVA: 1 m³ Above Threshold",
            Objective = "Verify projected volume at threshold + 1 m³ initiates calculated minimal discharge.",
            InvariantRule = "ProjectedVol == 85% + 1 => Outflow > 0"
        },
        ["Evaluate_StateTransition_NominalToWarning"] = new()
        {
            RequirementId = "REQ-M3-09",
            Module = "Hydrological Engine",
            RequirementTitle = "State Machine: Nominal to Warning",
            Objective = "Verify state transition from NOMINAL to WARNING upon water level increase above threshold.",
            InvariantRule = "Transition(Nominal -> Warning)"
        },
        ["Evaluate_StateTransition_WarningToCritical"] = new()
        {
            RequirementId = "REQ-M3-10",
            Module = "Hydrological Engine",
            RequirementTitle = "State Machine: Warning to Critical",
            Objective = "Verify state transition from WARNING to CRITICAL upon extreme rainfall triggering flood alert.",
            InvariantRule = "Transition(Warning -> Critical)"
        },
        ["Evaluate_Khadakwasla_UsesOwnThreshold_NotErais"] = new()
        {
            RequirementId = "REQ-M3-11",
            Module = "Hydrological Engine",
            RequirementTitle = "Reservoir Parameter Isolation: Khadakwasla",
            Objective = "Ensure Khadakwasla uses its specific 47.6M m³ threshold and 500 m³/s safe discharge without leaking Erai parameters.",
            InvariantRule = "DamId == 'khadakwasla' => Threshold == 47.6M m3"
        },
        ["Evaluate_Panshet_SafeConditions_NoAlert"] = new()
        {
            RequirementId = "REQ-M3-12",
            Module = "Hydrological Engine",
            RequirementTitle = "Reservoir Parameter Isolation: Panshet",
            Objective = "Ensure Panshet dam correctly assesses safe operational volume below its 249.9M m³ capacity threshold.",
            InvariantRule = "DamId == 'panshet' => Threshold == 249.9M m3"
        },
        ["Evaluate_Mulshi_EmergencyDischarge_CappedAtOwnMaxSafeDischarge"] = new()
        {
            RequirementId = "REQ-M3-13",
            Module = "Hydrological Engine",
            RequirementTitle = "Reservoir Parameter Isolation: Mulshi",
            Objective = "Ensure Mulshi emergency release is clamped at its specific 1500 m³/s discharge rating.",
            InvariantRule = "DamId == 'mulshi' => MaxSafe == 1500 m3/s"
        },

        // MODULE M7: Gate Mechanics & Actuators
        ["GatePercentage_Formula_IsCorrectAndClamped"] = new()
        {
            RequirementId = "REQ-M7-01",
            Module = "Hydraulic Gate Actuator",
            RequirementTitle = "Gate Aperture Clamping Math",
            Objective = "Verify gate opening percentage formula: Gate = Clamp((Outflow / MaxDischarge) * 100, 0, 100).",
            InvariantRule = "0 <= GateOpenPercentage <= 100"
        },
        ["Evaluate_GatePercentage_IsZero_WhenOutflowIsZero"] = new()
        {
            RequirementId = "REQ-M7-02",
            Module = "Hydraulic Gate Actuator",
            RequirementTitle = "Gate Closed Invariant",
            Objective = "Verify gate aperture is 0% when target outflow is 0 m³/s.",
            InvariantRule = "Outflow == 0 => GatePercentage == 0%"
        },
        ["Evaluate_GatePercentage_IsHundred_WhenOutflowIsMaxSafeDischarge"] = new()
        {
            RequirementId = "REQ-M7-03",
            Module = "Hydraulic Gate Actuator",
            RequirementTitle = "Full Gate Opening Invariant",
            Objective = "Verify gate aperture is 100% when outflow equals maximum safe discharge capacity.",
            InvariantRule = "Outflow == MaxSafe => GatePercentage == 100%"
        },

        // MODULE M5: AI Decision Support & Advisory Engine
        ["GenerateRecommendation_WhenFloodAlertActive_ReturnsCritical"] = new()
        {
            RequirementId = "REQ-M5-01",
            Module = "AI Decision Advisory",
            RequirementTitle = "Critical Flood Alert Advisory",
            Objective = "Ensure active flood alert forces AI risk classification to CRITICAL with high confidence score and immediate gate advisory.",
            InvariantRule = "FloodAlert == true => Risk == 'CRITICAL' && Confidence >= 98%"
        },
        ["GenerateRecommendation_WhenVolumeHigh_ReturnsWarning"] = new()
        {
            RequirementId = "REQ-M5-02",
            Module = "AI Decision Advisory",
            RequirementTitle = "Elevated Volume Warning Advisory",
            Objective = "Ensure reservoir volume > 85% triggers WARNING risk level with proactive gate schedule recommendation.",
            InvariantRule = "VolumeRatio > 0.85 => Risk == 'WARNING'"
        },
        ["GenerateRecommendation_WhenNormal_ReturnsNominal"] = new()
        {
            RequirementId = "REQ-M5-03",
            Module = "AI Decision Advisory",
            RequirementTitle = "Nominal Operating Advisory",
            Objective = "Ensure reservoir volume < 85% with no alerts triggers NOMINAL risk level.",
            InvariantRule = "VolumeRatio < 0.85 => Risk == 'NOMINAL'"
        },
        ["GenerateRecommendation_VolumeAt95Percent_ReturnsCritical"] = new()
        {
            RequirementId = "REQ-M5-04",
            Module = "AI Decision Advisory",
            RequirementTitle = "BVA: 95% Severe Capacity Risk",
            Objective = "Verify volume >= 95% triggers CRITICAL emergency risk.",
            InvariantRule = "VolumeRatio >= 0.95 => Risk == 'CRITICAL'"
        },
        ["GenerateRecommendation_VolumeAt83Percent_ReturnsWarning"] = new()
        {
            RequirementId = "REQ-M5-05",
            Module = "AI Decision Advisory",
            RequirementTitle = "BVA: 83% Warning Threshold",
            Objective = "Verify volume >= 83% triggers WARNING risk advisory.",
            InvariantRule = "VolumeRatio >= 0.83 => Risk == 'WARNING'"
        },
        ["GenerateRecommendation_VolumeAt82Point9Percent_ReturnsNominal"] = new()
        {
            RequirementId = "REQ-M5-06",
            Module = "AI Decision Advisory",
            RequirementTitle = "BVA: 82.9% Nominal Boundary",
            Objective = "Verify volume at 82.9% (below 83% threshold) returns NOMINAL risk.",
            InvariantRule = "VolumeRatio == 0.829 => Risk == 'NOMINAL'"
        },
        ["GenerateRecommendation_VolumeAt94Point9Percent_ReturnsWarning"] = new()
        {
            RequirementId = "REQ-M5-07",
            Module = "AI Decision Advisory",
            RequirementTitle = "BVA: 94.9% Warning Boundary",
            Objective = "Verify volume at 94.9% (below 95% critical threshold) returns WARNING risk.",
            InvariantRule = "VolumeRatio == 0.949 => Risk == 'WARNING'"
        },
        ["GenerateRecommendation_AllRiskLevels_HaveNonEmptySuggestedActions"] = new()
        {
            RequirementId = "REQ-M5-08",
            Module = "AI Decision Advisory",
            RequirementTitle = "Actionable Checklist Completeness",
            Objective = "Verify every evaluated risk category provides non-empty operational action items for operators.",
            InvariantRule = "SuggestedActions.Count > 0 && ConfidenceScore > 0"
        },
        ["GenerateRecommendation_NoPriorData_DefaultsToNominal"] = new()
        {
            RequirementId = "REQ-M5-09",
            Module = "AI Decision Advisory",
            RequirementTitle = "Cold-Start Resilience",
            Objective = "Ensure system defaults safely to NOMINAL advisory when no prior historical telemetry exists.",
            InvariantRule = "ColdStart => Risk == 'NOMINAL' && AdvisoryMessage != null"
        },

        // MODULE M8 & M9 & M13: RESTful API Endpoints & Authority Workflow
        ["GetDamStatus_DefaultErai_Returns200WithStateAndLog"] = new()
        {
            RequirementId = "REQ-M8-01",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Default Status Endpoint Contract",
            Objective = "GET /api/v1/dam/status returns HTTP 200 with reservoir state and latest control log.",
            InvariantRule = "StatusCode == 200 && Contains(state, latestLog)"
        },
        ["GetDamStatus_AllValidDams_Return200WithCorrectDamId"] = new()
        {
            RequirementId = "REQ-M8-02",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Parametric Reservoir Telemetry",
            Objective = "GET /api/v1/dam/{damId}/status returns HTTP 200 with matching dam ID for all 4 reservoirs.",
            InvariantRule = "StatusCode == 200 && Response.DamId == Requested.DamId"
        },
        ["GetDamStatus_UnknownDamId_FallsBackToEraiInsteadOf404"] = new()
        {
            RequirementId = "REQ-M8-03",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Status Endpoint Fallback Tolerance",
            Objective = "GET /api/v1/dam/{damId}/status gracefully falls back to default dam data upon unknown ID.",
            InvariantRule = "StatusCode == 200 && Response.DamId == 'erai'"
        },
        ["UpdateReservoirState_ValidPayload_Returns200AndCreatesControlLog"] = new()
        {
            RequirementId = "REQ-M8-04",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Telemetry Update & Evaluation",
            Objective = "POST /api/v1/dam/{damId}/update-state updates reservoir level and returns newly computed control log.",
            InvariantRule = "StatusCode == 200 && Response.RecommendedOutflow >= 0"
        },
        ["UpdateReservoirState_NullBody_ReturnsBadRequest"] = new()
        {
            RequirementId = "REQ-M8-05",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Payload Validation: Empty Body",
            Objective = "POST /api/v1/dam/{damId}/update-state returns HTTP 400 Bad Request on empty or malformed body.",
            InvariantRule = "StatusCode == 400"
        },
        ["UpdateReservoirState_VolumeZero_HandledGracefully"] = new()
        {
            RequirementId = "REQ-M8-06",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Payload Validation: Zero Volume",
            Objective = "POST /api/v1/dam/{damId}/update-state processes minimum boundary volume (0 m³) safely.",
            InvariantRule = "StatusCode == 200 && Alert == false"
        },
        ["ForecastEval_ValidParams_ReturnsSimulationResult"] = new()
        {
            RequirementId = "REQ-M8-07",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Forecast Simulation Endpoint",
            Objective = "GET /api/v1/dam/{damId}/forecast-eval calculates water balance without persisting to database.",
            InvariantRule = "StatusCode == 200 && StatusMessage.Contains('[SIMULATION]')"
        },
        ["ForecastEval_ZeroParams_Returns200WithSafeStatus"] = new()
        {
            RequirementId = "REQ-M8-08",
            Module = "RESTful API Endpoints",
            RequirementTitle = "Forecast Simulation Zero Boundary",
            Objective = "GET /api/v1/dam/{damId}/forecast-eval handles zero rainfall/volume inputs gracefully.",
            InvariantRule = "StatusCode == 200 && Alert == false"
        },
        ["GetAiRecommendation_DefaultErai_Returns200WithRequiredFields"] = new()
        {
            RequirementId = "REQ-M8-09",
            Module = "RESTful API Endpoints",
            RequirementTitle = "AI Advisory Endpoint Contract",
            Objective = "GET /api/v1/dam/ai-recommendation returns structured riskLevel, advisoryMessage, and gate schedule.",
            InvariantRule = "StatusCode == 200 && ValidRiskLevel(response.RiskLevel)"
        },
        ["CreateEmergencyAlert_ValidInputs_Returns200AndCreatesAlert"] = new()
        {
            RequirementId = "REQ-M6-01",
            Module = "Emergency Alert Management",
            RequirementTitle = "Emergency Alert Broadcast",
            Objective = "POST /api/v1/dam/{damId}/emergency-alert records active alert with priority and shift officer name.",
            InvariantRule = "StatusCode == 200 && Alert.Resolved == false && Alert.Id > 0"
        },
        ["GetActiveAlerts_AfterCreatingAlerts_ReturnsOnlyUnresolved"] = new()
        {
            RequirementId = "REQ-M6-02",
            Module = "Emergency Alert Management",
            RequirementTitle = "Active Alerts Query",
            Objective = "GET /api/v1/dam/alerts returns only active unresolved emergency notifications.",
            InvariantRule = "All(alert => alert.Resolved == false)"
        },
        ["ResolveAlert_ValidId_Returns200AndRemovesFromActiveList"] = new()
        {
            RequirementId = "REQ-M6-03",
            Module = "Emergency Alert Management",
            RequirementTitle = "Alert Resolution Lifecycle",
            Objective = "POST /api/v1/dam/alert/{id}/resolve marks alert as resolved and removes from active broadcast list.",
            InvariantRule = "StatusCode == 200 && !ActiveAlerts.Contains(id)"
        },
        ["ResolveAlert_NonExistentId_Returns404"] = new()
        {
            RequirementId = "REQ-M6-04",
            Module = "Emergency Alert Management",
            RequirementTitle = "Alert Resolution Invalid ID",
            Objective = "POST /api/v1/dam/alert/{id}/resolve returns HTTP 404 when target alert ID is non-existent.",
            InvariantRule = "StatusCode == 404"
        },
        ["SubmitDecision_WhenLogExists_SetsStatusToPendingAuthority"] = new()
        {
            RequirementId = "REQ-M9-01",
            Module = "Authority Approval Workflow",
            RequirementTitle = "Decision Submission",
            Objective = "POST /api/v1/dam/{damId}/submit-decision transitions control log status to PENDING_AUTHORITY.",
            InvariantRule = "ApprovalStatus == 'PENDING_AUTHORITY'"
        },
        ["AuthorityAction_Approve_SetsStatusApproved"] = new()
        {
            RequirementId = "REQ-M9-02",
            Module = "Authority Approval Workflow",
            RequirementTitle = "Authority Approval Action",
            Objective = "POST /api/v1/dam/{damId}/authority-action with action=APPROVE transitions log to APPROVED status.",
            InvariantRule = "ApprovalStatus == 'APPROVED'"
        },
        ["AuthorityAction_Reject_WithManualOutflow_SetsStatusRejected"] = new()
        {
            RequirementId = "REQ-M9-03",
            Module = "Authority Approval Workflow",
            RequirementTitle = "Authority Rejection & Manual Override",
            Objective = "POST /api/v1/dam/{damId}/authority-action with action=REJECT applies manual outflow override.",
            InvariantRule = "ApprovalStatus == 'REJECTED' && RecommendedOutflow == ManualOutflow"
        },
        ["AuthorityAction_InvalidActionString_FallsToReject"] = new()
        {
            RequirementId = "REQ-M9-04",
            Module = "Authority Approval Workflow",
            RequirementTitle = "Authority Action Safety Fallback",
            Objective = "POST /api/v1/dam/{damId}/authority-action with unrecognized action string defaults safely to rejection.",
            InvariantRule = "ApprovalStatus == 'REJECTED'"
        },
        ["AuthorityAction_ManualOutflowOverMax_GateClampsAt100Percent"] = new()
        {
            RequirementId = "REQ-M9-05",
            Module = "Authority Approval Workflow",
            RequirementTitle = "Manual Override Gate Clamping",
            Objective = "POST /api/v1/dam/{damId}/authority-action with manual outflow > MaxSafe clamps gate actuator at 100%.",
            InvariantRule = "GateOpenPercentage == 100.0"
        }
    };

    public static TestRequirementMetadata GetMetadata(string testMethodName)
    {
        var cleanName = testMethodName.Split('(')[0].Split('.').LastOrDefault() ?? testMethodName;
        if (RequirementRegistry.TryGetValue(cleanName, out var meta))
        {
            return meta;
        }

        // Generic fallback
        return new TestRequirementMetadata
        {
            RequirementId = "REQ-GEN-01",
            Module = "General STQA Invariant",
            RequirementTitle = cleanName,
            Objective = "Verify operational invariant and assertion constraints for " + cleanName,
            InvariantRule = "Assertions == True"
        };
    }

    /// <summary>
    /// Parses the TRX test file and produces a self-contained HTML test report.
    /// </summary>
    public static string GenerateHtmlReportFromTrx(string trxFilePath, string outputHtmlPath)
    {
        if (!File.Exists(trxFilePath))
        {
            throw new FileNotFoundException("TRX test result file not found at " + trxFilePath);
        }

        var doc = XDocument.Load(trxFilePath);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        var results = doc.Descendants(ns + "UnitTestResult").ToList();
        var testEntries = new List<(string Name, string Method, string Outcome, double DurationMs, TestRequirementMetadata Meta, string? Error)>();

        foreach (var r in results)
        {
            var rawName = r.Attribute("testName")?.Value ?? "UnknownTest";
            var cleanName = rawName.Replace("DamControlSystem.Tests.", "");
            var method = cleanName.Split('(')[0].Split('.').LastOrDefault() ?? cleanName;
            var outcome = r.Attribute("outcome")?.Value ?? "Unknown";
            var durationStr = r.Attribute("duration")?.Value;
            double durationMs = 0;
            if (TimeSpan.TryParse(durationStr, out var ts))
            {
                durationMs = Math.Round(ts.TotalMilliseconds, 2);
            }

            var outputElem = r.Element(ns + "Output");
            var errorInfo = outputElem?.Element(ns + "ErrorInfo");
            var errorMsg = errorInfo?.Element(ns + "Message")?.Value;

            var meta = GetMetadata(method);
            testEntries.Add((cleanName, method, outcome, durationMs, meta, errorMsg));
        }

        var total = testEntries.Count;
        var passed = testEntries.Count(t => t.Outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase));
        var failed = testEntries.Count(t => t.Outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase));
        var totalDurationSec = Math.Round(testEntries.Sum(t => t.DurationMs) / 1000.0, 2);
        var passRate = total > 0 ? Math.Round((double)passed / total * 100.0, 1) : 100.0;
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var modules = testEntries.GroupBy(t => t.Meta.Module).OrderBy(g => g.Key).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Smart Dam Control System — STQA Test Execution Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    :root { --bg: #0f172a; --card: #1e293b; --border: #334155; --text: #e2e8f0; --muted: #94a3b8; --pass: #10b981; --fail: #ef4444; --accent: #0ea5e9; }");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }");
        sb.AppendLine("    body { background: var(--bg); color: var(--text); padding: 32px 16px; line-height: 1.5; }");
        sb.AppendLine("    .container { max-width: 1200px; margin: 0 auto; }");
        sb.AppendLine("    .header { background: linear-gradient(135deg, #1e293b 0%, #0c4a6e 100%); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 24px; }");
        sb.AppendLine("    .header h1 { font-size: 26px; font-weight: 700; color: #fff; margin-bottom: 8px; }");
        sb.AppendLine("    .header p { color: var(--muted); font-size: 14px; }");
        sb.AppendLine("    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 24px; }");
        sb.AppendLine("    .stat-card { background: var(--card); border: 1px solid var(--border); border-radius: 10px; padding: 18px; }");
        sb.AppendLine("    .stat-card .label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: var(--muted); margin-bottom: 6px; }");
        sb.AppendLine("    .stat-card .value { font-size: 28px; font-weight: 700; }");
        sb.AppendLine("    .stat-card.pass .value { color: var(--pass); }");
        sb.AppendLine("    .stat-card.fail .value { color: var(--fail); }");
        sb.AppendLine("    .stat-card.accent .value { color: var(--accent); }");
        sb.AppendLine("    .section-title { font-size: 18px; font-weight: 600; margin: 24px 0 12px; color: #fff; display: flex; align-items: center; justify-content: space-between; }");
        sb.AppendLine("    .table-card { background: var(--card); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; margin-bottom: 24px; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }");
        sb.AppendLine("    th { background: #111827; color: var(--muted); font-weight: 600; padding: 12px 14px; border-bottom: 1px solid var(--border); }");
        sb.AppendLine("    td { padding: 12px 14px; border-bottom: 1px solid var(--border); vertical-align: top; }");
        sb.AppendLine("    tr:last-child td { border-bottom: none; }");
        sb.AppendLine("    tr:hover { background: rgba(255,255,255,0.02); }");
        sb.AppendLine("    .badge { display: inline-block; padding: 3px 8px; border-radius: 6px; font-size: 11px; font-weight: 700; text-transform: uppercase; }");
        sb.AppendLine("    .badge-pass { background: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); }");
        sb.AppendLine("    .badge-fail { background: rgba(239, 68, 68, 0.15); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.3); }");
        sb.AppendLine("    .badge-req { background: rgba(14, 165, 233, 0.15); color: #38bdf8; border: 1px solid rgba(14, 165, 233, 0.3); font-family: monospace; }");
        sb.AppendLine("    .code { font-family: monospace; font-size: 12px; color: #cbd5e1; }");
        sb.AppendLine("    .rule { font-family: monospace; font-size: 11px; color: #a5f3fc; background: rgba(0,0,0,0.25); padding: 2px 6px; border-radius: 4px; display: inline-block; margin-top: 4px; }");
        sb.AppendLine("    .footer { text-align: center; color: var(--muted); font-size: 12px; margin-top: 32px; padding-top: 16px; border-top: 1px solid var(--border); }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Header
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine("      <h1>Smart Dam Control System — STQA Test Execution Report</h1>");
        sb.AppendLine($"      <p>Automated Software Testing & Quality Assurance Verification Matrix • Generated on {timestamp}</p>");
        sb.AppendLine("    </div>");

        // Stat Cards
        sb.AppendLine("    <div class=\"stats-grid\">");
        sb.AppendLine("      <div class=\"stat-card accent\">");
        sb.AppendLine("        <div class=\"label\">Total Test Cases</div>");
        sb.AppendLine($"        <div class=\"value\">{total}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"stat-card pass\">");
        sb.AppendLine("        <div class=\"label\">Tests Passed</div>");
        sb.AppendLine($"        <div class=\"value\">{passed}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"stat-card fail\">");
        sb.AppendLine("        <div class=\"label\">Tests Failed</div>");
        sb.AppendLine($"        <div class=\"value\">{failed}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"stat-card pass\">");
        sb.AppendLine("        <div class=\"label\">Pass Rate</div>");
        sb.AppendLine($"        <div class=\"value\">{passRate}%</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"stat-card accent\">");
        sb.AppendLine("        <div class=\"label\">Execution Duration</div>");
        sb.AppendLine($"        <div class=\"value\">{totalDurationSec}s</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");

        // Module-by-Module Requirements Table
        foreach (var mod in modules)
        {
            sb.AppendLine($"    <div class=\"section-title\">");
            sb.AppendLine($"      <span>Module: {mod.Key}</span>");
            sb.AppendLine($"      <span style=\"font-size: 13px; font-weight: normal; color: var(--muted);\">{mod.Count()} Test Cases</span>");
            sb.AppendLine($"    </div>");

            sb.AppendLine("    <div class=\"table-card\">");
            sb.AppendLine("      <table>");
            sb.AppendLine("        <thead>");
            sb.AppendLine("          <tr>");
            sb.AppendLine("            <th style=\"width: 120px;\">Req ID</th>");
            sb.AppendLine("            <th style=\"width: 260px;\">Test Case / Method</th>");
            sb.AppendLine("            <th>Requirement Objective & Verification Rule</th>");
            sb.AppendLine("            <th style=\"width: 90px; text-align: center;\">Status</th>");
            sb.AppendLine("            <th style=\"width: 90px; text-align: right;\">Time</th>");
            sb.AppendLine("          </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody>");

            foreach (var test in mod.OrderBy(t => t.Meta.RequirementId).ThenBy(t => t.Name))
            {
                var isPass = test.Outcome.Equals("Passed", StringComparison.OrdinalIgnoreCase);
                var statusBadge = isPass
                    ? "<span class=\"badge badge-pass\">PASS</span>"
                    : "<span class=\"badge badge-fail\">FAIL</span>";

                sb.AppendLine("          <tr>");
                sb.AppendLine($"            <td><span class=\"badge badge-req\">{test.Meta.RequirementId}</span></td>");
                sb.AppendLine($"            <td><strong style=\"color: #fff;\">{test.Meta.RequirementTitle}</strong><br><span class=\"code\">{test.Name}</span></td>");
                sb.AppendLine($"            <td>{test.Meta.Objective}<br><span class=\"rule\">Invariant: {test.Meta.InvariantRule}</span></td>");
                sb.AppendLine($"            <td style=\"text-align: center;\">{statusBadge}</td>");
                sb.AppendLine($"            <td style=\"text-align: right; font-family: monospace; color: var(--muted);\">{test.DurationMs}ms</td>");
                sb.AppendLine("          </tr>");
            }

            sb.AppendLine("        </tbody>");
            sb.AppendLine("      </table>");
            sb.AppendLine("    </div>");
        }

        // Footer
        sb.AppendLine("    <div class=\"footer\">");
        sb.AppendLine("      <p>Smart Dam Automated Hydrological Control System • STQA Quality Assurance Harness • Target Framework: .NET 10.0</p>");
        sb.AppendLine("    </div>");

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        var html = sb.ToString();
        var dir = Path.GetDirectoryName(outputHtmlPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputHtmlPath, html);
        return outputHtmlPath;
    }
}
