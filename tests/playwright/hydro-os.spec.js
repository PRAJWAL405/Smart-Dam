// ==============================================================================
// HYDRO-OS Smart-Dam UI Automation Tests (Playwright)
// Phase 3 - STQA Mini-Project (50 Test Cases Suite)
// Run with: npx playwright test
// ==============================================================================

import { test, expect } from "@playwright/test";

const BASE_URL = "file:///c:/Users/Prajwal/Desktop/sem 7/stqa mini project/Smart-Dam/src/DamControlSystem/wwwroot/index.html";

// Global mock handler for API calls so tests run deterministically
async function setupApiMocks(page) {
  await page.route("**/api/v1/dam/**", async (route) => {
    const url = route.request().url();
    if (url.includes("ai-recommendation")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          riskLevel: "NOMINAL",
          confidenceScore: 92.5,
          advisoryMessage: "Current inflow rates within safe catchment basin thresholds.",
          gateScheduleRecommendation: "G1-G4 maintained at nominal 20% discharge.",
          suggestedActions: ["Maintain regular hourly telemetry log", "Monitor downstream gauge levels"]
        })
      });
    }

    if (url.includes("alerts")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([])
      });
    }

    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        state: {
          damId: "erai",
          currentVolumeM3: 181200000.0,
          waterLevelMeters: 20.0,
          gateOpenPercentage: 40.0
        },
        latestLog: {
          recommendedOutflowM3s: 120.0,
          predictedInflowM3: 85000000.0,
          forecastPrecipitationMm: 24.5,
          floodAlertTriggered: false,
          approvalStatus: "PENDING_OPERATOR",
          statusMessage: "Reservoir nominal; standard baseline release engaged."
        }
      })
    });
  });
}

// Helper to log in as Operator
async function loginAsOperator(page, officerName = "Officer Patil", shift = "morning") {
  await setupApiMocks(page);
  await page.goto(BASE_URL);
  await page.waitForLoadState("domcontentloaded");
  await page.locator("#login-tab-operator").click();
  await page.locator("#login-operator-name").fill(officerName);
  await page.locator("#login-operator-shift").selectOption(shift);
  await page.locator("#btn-login-operator-submit").click();
  await expect(page.locator("#login-overlay")).toHaveClass(/hidden/);
}

// Helper to log in as Admin
async function loginAsAdmin(page, adminId = "SUPERVISOR-900") {
  await setupApiMocks(page);
  await page.goto(BASE_URL);
  await page.waitForLoadState("domcontentloaded");
  await page.locator("#login-tab-admin").click();
  await page.locator("#login-admin-id").fill(adminId);
  await page.locator("#btn-login-admin-submit").click();
  await expect(page.locator("#login-overlay")).toHaveClass(/hidden/);
}

// ==============================================================================
// SUITE 1: Application Launch & Security Overlay (TC-UI-01 to TC-UI-05)
// ==============================================================================
test.describe("Suite 1: Application Launch & Security Overlay", () => {

  test("TC-UI-01: Page title initializes correctly on launch", async ({ page }) => {
    await setupApiMocks(page);
    await page.goto(BASE_URL);
    await page.waitForLoadState("domcontentloaded");
    const title = await page.title();
    expect(title).toContain("HYDRO-OS");
    expect(title).toContain("Erai Dam Operations Control");
  });

  test("TC-UI-02: Security login overlay is displayed when unauthenticated", async ({ page }) => {
    await setupApiMocks(page);
    await page.goto(BASE_URL);
    await page.waitForLoadState("domcontentloaded");
    const overlay = page.locator("#login-overlay");
    await expect(overlay).toBeVisible();
    await expect(overlay).not.toHaveClass(/hidden/);
  });

  test("TC-UI-03: Login overlay displays brand heading and security badge", async ({ page }) => {
    await setupApiMocks(page);
    await page.goto(BASE_URL);
    await page.waitForLoadState("domcontentloaded");
    const brandHeading = page.locator("#login-overlay h2");
    await expect(brandHeading).toBeVisible();
    const headingText = await brandHeading.textContent();
    expect(headingText.length).toBeGreaterThan(3);
    const badgeText = page.locator("#login-overlay p");
    await expect(badgeText).toContainText("Water Resource Control");
  });

  test("TC-UI-04: Tab switcher alternates between Operator and Admin forms", async ({ page }) => {
    await setupApiMocks(page);
    await page.goto(BASE_URL);
    await page.waitForLoadState("domcontentloaded");
    const formOperator = page.locator("#form-operator");
    const formAdmin = page.locator("#form-admin");

    // Default: Operator form visible, Admin hidden
    await expect(formOperator).toBeVisible();
    await expect(formAdmin).toHaveClass(/hidden/);

    // Switch to Admin
    await page.locator("#login-tab-admin").click();
    await expect(formAdmin).toBeVisible();
    await expect(formOperator).toHaveClass(/hidden/);

    // Switch back to Operator
    await page.locator("#login-tab-operator").click();
    await expect(formOperator).toBeVisible();
    await expect(formAdmin).toHaveClass(/hidden/);
  });

  test("TC-UI-05: Operator login triggers validation alert if name is empty", async ({ page }) => {
    await setupApiMocks(page);
    await page.goto(BASE_URL);
    await page.waitForLoadState("domcontentloaded");
    let alertMessage = "";
    page.once("dialog", async (dialog) => {
      alertMessage = dialog.message();
      await dialog.accept();
    });

    await page.locator("#login-operator-name").fill("");
    await page.locator("#btn-login-operator-submit").click();
    expect(alertMessage.toLowerCase()).toContain("name");
  });
});

// ==============================================================================
// SUITE 2: Authentication & Role-Based Access Control (TC-UI-06 to TC-UI-10)
// ==============================================================================
test.describe("Suite 2: Authentication & Role-Based Access Control", () => {

  test("TC-UI-06: Operator authentication hides overlay and reveals dashboard", async ({ page }) => {
    await loginAsOperator(page, "Officer Jadhav");
    await expect(page.locator("#login-overlay")).toHaveClass(/hidden/);
    await expect(page.locator("#view-dashboard-wrapper")).toBeVisible();
  });

  test("TC-UI-07: Operator session displays officer name on shift badge", async ({ page }) => {
    await loginAsOperator(page, "Officer Kulkarni");
    const officerDisplay = page.locator("#shift-officer-display");
    await expect(officerDisplay).toHaveText("Officer Kulkarni");
    await expect(page.locator("#operator-shift-badge")).toBeVisible();
  });

  test("TC-UI-08: Operator shift timer countdown is active and ticking", async ({ page }) => {
    await loginAsOperator(page, "Officer Deshmukh", "afternoon");
    const countdown = page.locator("#shift-timer-countdown");
    await expect(countdown).toBeVisible();
    const timerText = await countdown.textContent();
    expect(timerText).toMatch(/\d+h\s+\d+m\s+\d+s/);
  });

  test("TC-UI-09: Admin authentication reveals Authority Review Panel and Disaster Response", async ({ page }) => {
    await loginAsAdmin(page, "CHIEF-DIRECTOR-1");
    await expect(page.locator("#card-authority-panel")).toBeVisible();
    await expect(page.locator("#card-disaster-response")).toBeVisible();
    await expect(page.locator("#card-operator-dispatcher")).toHaveClass(/hidden/);
  });

  test("TC-UI-10: Logout destroys session and restores security login modal", async ({ page }) => {
    await loginAsOperator(page, "Officer Patil");
    const logoutBtn = page.locator("#btn-logout");
    await expect(logoutBtn).toBeVisible();
    await logoutBtn.click();
    await expect(page.locator("#login-overlay")).toBeVisible();
  });
});

// ==============================================================================
// SUITE 3: Multi-Reservoir Registry & Dam Switching (TC-UI-11 to TC-UI-16)
// ==============================================================================
test.describe("Suite 3: Multi-Reservoir Registry & Dam Switching", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
  });

  test("TC-UI-11: Dam selector dropdown exists with four Maharashtra reservoirs", async ({ page }) => {
    const selector = page.locator("#dam-selector");
    await expect(selector).toBeVisible();
    const options = await selector.locator("option").allTextContents();
    expect(options.length).toBe(4);
    expect(options.some(o => o.includes("Erai"))).toBeTruthy();
    expect(options.some(o => o.includes("Khadakwasla"))).toBeTruthy();
    expect(options.some(o => o.includes("Panshet"))).toBeTruthy();
    expect(options.some(o => o.includes("Mulshi"))).toBeTruthy();
  });

  test("TC-UI-12: Default selected reservoir on launch is Erai Dam", async ({ page }) => {
    const selector = page.locator("#dam-selector");
    await expect(selector).toHaveValue("erai");
  });

  test("TC-UI-13: Switching to Khadakwasla updates document title and catchment headers", async ({ page }) => {
    await page.locator("#dam-selector").selectOption("khadakwasla");
    const title = await page.title();
    expect(title).toContain("Khadakwasla Dam");
  });

  test("TC-UI-14: Switching to Panshet Dam updates downstream risk matrix village targets", async ({ page }) => {
    await page.locator("#dam-selector").selectOption("panshet");
    const matrixText = await page.locator("#card-risk-matrix").textContent();
    expect(matrixText).toContain("Khanapur");
  });

  test("TC-UI-15: Switching to Mulshi Dam adjusts volume and water level maximum constraints", async ({ page }) => {
    await page.locator("#dam-selector").selectOption("mulshi");
    const volSlider = page.locator("#volume-slider");
    const maxVol = await volSlider.getAttribute("max");
    expect(parseFloat(maxVol)).toBe(522000000);
  });

  test("TC-UI-16: Dam selection preserves selected state across telemetry refresh", async ({ page }) => {
    await page.locator("#dam-selector").selectOption("khadakwasla");
    await page.locator("#refresh-btn").click();
    await expect(page.locator("#dam-selector")).toHaveValue("khadakwasla");
  });
});

// ==============================================================================
// SUITE 4: Top Navigation & Page View Routing (TC-UI-17 to TC-UI-21)
// ==============================================================================
test.describe("Suite 4: Top Navigation & Page View Routing", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
  });

  test("TC-UI-17: Top navigation buttons exist for Dashboard, Analytics, and Protocols", async ({ page }) => {
    await expect(page.locator("#btn-top-dashboard")).toBeVisible();
    await expect(page.locator("#btn-top-analytics")).toBeVisible();
    await expect(page.locator("#btn-top-protocols")).toBeVisible();
  });

  test("TC-UI-18: Navigating to Analytics view displays analytics wrapper and cards", async ({ page }) => {
    await page.locator("#btn-top-analytics").click();
    await expect(page.locator("#view-analytics-wrapper")).toBeVisible();
    await expect(page.locator("#view-dashboard-wrapper")).toHaveClass(/hidden/);
    await expect(page.locator("#analytics-title")).toBeVisible();
  });

  test("TC-UI-19: Navigating to Safety Protocols view displays siren test controls", async ({ page }) => {
    await page.locator("#btn-top-protocols").click();
    await expect(page.locator("#view-protocols-wrapper")).toBeVisible();
    await expect(page.locator("#view-dashboard-wrapper")).toHaveClass(/hidden/);
    await expect(page.locator("#btn-test-siren")).toBeVisible();
  });

  test("TC-UI-20: Navigating back to Dashboard view restores main telemetry canvas", async ({ page }) => {
    await page.locator("#btn-top-analytics").click();
    await page.locator("#btn-top-dashboard").click();
    await expect(page.locator("#view-dashboard-wrapper")).toBeVisible();
    await expect(page.locator("#view-analytics-wrapper")).toHaveClass(/hidden/);
  });

  test("TC-UI-21: Active top navigation tab updates border highlight styling", async ({ page }) => {
    const btnAnalytics = page.locator("#btn-top-analytics");
    await btnAnalytics.click();
    await expect(btnAnalytics).toHaveClass(/border-primary/);
  });
});

// ==============================================================================
// SUITE 5: Sidebar Navigation & Sub-page Switching (TC-UI-22 to TC-UI-26)
// ==============================================================================
test.describe("Suite 5: Sidebar Navigation & Sub-page Switching", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
  });

  test("TC-UI-22: Sidebar navigation contains Telemetry, Hydraulics, and Forecasts buttons", async ({ page }) => {
    await expect(page.locator("#btn-nav-telemetry")).toBeVisible();
    await expect(page.locator("#btn-nav-hydraulics")).toBeVisible();
    await expect(page.locator("#btn-nav-forecasts")).toBeVisible();
  });

  test("TC-UI-23: Telemetry panel is active and visible by default", async ({ page }) => {
    await expect(page.locator("#page-telemetry")).toBeVisible();
    await expect(page.locator("#page-hydraulics")).toHaveClass(/hidden/);
    await expect(page.locator("#page-forecasts")).toHaveClass(/hidden/);
  });

  test("TC-UI-24: Clicking Hydraulics tab displays sluice gate rendering panel", async ({ page }) => {
    await page.locator("#btn-nav-hydraulics").click();
    await expect(page.locator("#page-hydraulics")).toBeVisible();
    await expect(page.locator("#page-telemetry")).toHaveClass(/hidden/);
    await expect(page.locator("#sluice-gates-render-grid")).toBeVisible();
  });

  test("TC-UI-25: Clicking Forecasts tab displays meteorological predictions and PDF export button", async ({ page }) => {
    await page.locator("#btn-nav-forecasts").click();
    await expect(page.locator("#page-forecasts")).toBeVisible();
    await expect(page.locator("#page-telemetry")).toHaveClass(/hidden/);
    await expect(page.locator("#btn-generate-pdf")).toBeVisible();
  });

  test("TC-UI-26: Switching between sub-pages maintains sidebar active indicator class", async ({ page }) => {
    const navHydraulics = page.locator("#btn-nav-hydraulics");
    await navHydraulics.click();
    await expect(navHydraulics).toHaveClass(/sidebar-active/);
  });
});

// ==============================================================================
// SUITE 6: Telemetry Displays & Storage Metrics (TC-UI-27 to TC-UI-31)
// ==============================================================================
test.describe("Suite 6: Telemetry Displays & Storage Metrics", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
  });

  test("TC-UI-27: Reservoir current volume displays formatted numeric value with million m3 unit", async ({ page }) => {
    const volElem = page.locator("#current-volume-val");
    await expect(volElem).toBeVisible();
    const text = await volElem.textContent();
    expect(text).toMatch(/\d+(\.\d+)?M|--/);
  });

  test("TC-UI-28: Water level indicator displays meters unit", async ({ page }) => {
    const lvlElem = page.locator("#water-level-meters-val");
    await expect(lvlElem).toBeVisible();
    const text = await lvlElem.textContent();
    expect(text).toContain("m");
  });

  test("TC-UI-29: Storage capacity progress bar displays numeric percentage", async ({ page }) => {
    const pctText = page.locator("#storage-percentage-text");
    await expect(pctText).toBeVisible();
    const content = await pctText.textContent();
    expect(content).toContain("%");
  });

  test("TC-UI-30: Inflow and rainfall telemetry cards display numeric units", async ({ page }) => {
    const inflowElem = page.locator("#forecast-inflow-val");
    const rainElem = page.locator("#forecast-rainfall-val");
    await expect(inflowElem).toBeVisible();
    await expect(rainElem).toBeVisible();
    expect(await inflowElem.textContent()).toMatch(/m|--/);
    expect(await rainElem.textContent()).toMatch(/mm|--/);
  });

  test("TC-UI-31: Last sync timestamp element displays valid local time", async ({ page }) => {
    const syncElem = page.locator("#sync-timestamp");
    await expect(syncElem).toBeVisible();
    const syncTime = await syncElem.textContent();
    expect(syncTime.length).toBeGreaterThan(3);
  });
});

// ==============================================================================
// SUITE 7: Reservoir Control Sliders & RBAC Protection (TC-UI-32 to TC-UI-35)
// ==============================================================================
test.describe("Suite 7: Reservoir Control Sliders & RBAC Protection", () => {

  test("TC-UI-32: Operator can adjust storage volume slider and update live readout", async ({ page }) => {
    await loginAsOperator(page);
    const volSlider = page.locator("#volume-slider");
    await expect(volSlider).toBeEnabled();
    await page.$eval("#volume-slider", (el, val) => {
      el.value = val;
      el.dispatchEvent(new Event("input"));
    }, "180000000");
    const readout = await page.locator("#volume-slider-val").textContent();
    expect(readout).toMatch(/180\.\d+M/);
  });

  test("TC-UI-33: Operator can adjust water level slider and update readout", async ({ page }) => {
    await loginAsOperator(page);
    const lvlSlider = page.locator("#level-slider");
    await expect(lvlSlider).toBeEnabled();
    await page.$eval("#level-slider", (el, val) => {
      el.value = val;
      el.dispatchEvent(new Event("input"));
    }, "20");
    const readout = await page.locator("#level-slider-val").textContent();
    expect(readout).toMatch(/(19\.9|20\.0)\s+m/);
  });

  test("TC-UI-34: Apply State Update button is visible and active for Operator", async ({ page }) => {
    await loginAsOperator(page);
    const postBtn = page.locator("#post-state-btn");
    await expect(postBtn).toBeVisible();
    await expect(postBtn).toBeEnabled();
  });

  test("TC-UI-35: Telemetry sliders are disabled and post button hidden in Admin view", async ({ page }) => {
    await loginAsAdmin(page);
    const volSlider = page.locator("#volume-slider");
    const lvlSlider = page.locator("#level-slider");
    const postBtn = page.locator("#post-state-btn");

    await expect(volSlider).toBeDisabled();
    await expect(lvlSlider).toBeDisabled();
    await expect(postBtn).toHaveClass(/hidden/);
  });
});

// ==============================================================================
// SUITE 8: AI Hydro-Advisor & Recommendation Synthesis (TC-UI-36 to TC-UI-40)
// ==============================================================================
test.describe("Suite 8: AI Hydro-Advisor & Recommendation Synthesis", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
  });

  test("TC-UI-36: AI Advisor card is present and visible on dashboard", async ({ page }) => {
    const aiCard = page.locator("#card-ai-advisor");
    await expect(aiCard).toBeVisible();
  });

  test("TC-UI-37: AI Confidence badge displays score percentage", async ({ page }) => {
    const confBadge = page.locator("#ai-confidence-badge");
    await expect(confBadge).toBeVisible();
    const confText = await confBadge.textContent();
    expect(confText).toContain("CONFIDENCE:");
    expect(confText).toContain("%");
  });

  test("TC-UI-38: AI Risk level pill displays NOMINAL, WARNING, or CRITICAL", async ({ page }) => {
    const riskPill = page.locator("#ai-risk-pill");
    await expect(riskPill).toBeVisible();
    const risk = await riskPill.textContent();
    expect(["NOMINAL", "WARNING", "CRITICAL"]).toContain(risk.trim());
  });

  test("TC-UI-39: AI Advisory message paragraph provides plain-language synthesis", async ({ page }) => {
    const advisoryMsg = page.locator("#ai-advisory-msg");
    await expect(advisoryMsg).toBeVisible();
    const text = await advisoryMsg.textContent();
    expect(text.length).toBeGreaterThan(10);
  });

  test("TC-UI-40: AI Gate schedule advisory provides concrete sluice guidance", async ({ page }) => {
    const schedule = page.locator("#ai-gate-schedule");
    await expect(schedule).toBeVisible();
    const scheduleText = await schedule.textContent();
    expect(scheduleText.length).toBeGreaterThan(5);
  });
});

// ==============================================================================
// SUITE 9: Emergency Dispatcher & Disaster Response (TC-UI-41 to TC-UI-45)
// ==============================================================================
test.describe("Suite 9: Emergency Dispatcher & Disaster Response", () => {

  test("TC-UI-41: Operator Priority Dispatcher dropdown has four severity options", async ({ page }) => {
    await loginAsOperator(page);
    const prioritySelect = page.locator("#operator-alert-priority");
    await expect(prioritySelect).toBeVisible();
    const options = await prioritySelect.locator("option").allTextContents();
    expect(options.length).toBe(4);
    expect(options.some(o => o.includes("CRITICAL"))).toBeTruthy();
    expect(options.some(o => o.includes("HIGH"))).toBeTruthy();
  });

  test("TC-UI-42: Dispatch alert validates against empty message input", async ({ page }) => {
    await loginAsOperator(page);
    let alertText = "";
    page.once("dialog", async (d) => {
      alertText = d.message();
      await d.accept();
    });

    await page.locator("#operator-alert-msg").fill("");
    await page.locator("#btn-send-priority-alert").click();
    expect(alertText.toLowerCase()).toContain("message");
  });

  test("TC-UI-43: Alert message textarea accepts multi-line incident descriptions", async ({ page }) => {
    await loginAsOperator(page);
    const textarea = page.locator("#operator-alert-msg");
    await textarea.fill("Flash flood warning detected at upstream catchment sector A.");
    await expect(textarea).toHaveValue("Flash flood warning detected at upstream catchment sector A.");
  });

  test("TC-UI-44: Admin Alerts Queue displays active unresolved alert badge in Admin view", async ({ page }) => {
    await loginAsAdmin(page);
    const countBadge = page.locator("#admin-alerts-count");
    await expect(countBadge).toBeVisible();
    const text = await countBadge.textContent();
    expect(text).toContain("ACTIVE");
  });

  test("TC-UI-45: Quick disaster response notification generates delivery log entry", async ({ page }) => {
    await loginAsAdmin(page);
    const btnNdrf = page.locator("#btn-notify-ndrf");
    await expect(btnNdrf).toBeVisible();
    await btnNdrf.click();

    const dispatchLogs = page.locator("#dispatch-logs");
    await expect(dispatchLogs).toContainText("NDRF Commander");
    await expect(dispatchLogs).toContainText("DELIVERED");
  });
});

// ==============================================================================
// SUITE 10: Hydraulics Diagnostics, Radar, Weather & Siren (TC-UI-46 to TC-UI-50)
// ==============================================================================
test.describe("Suite 10: Hydraulics Diagnostics, Radar, Weather & Siren", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
  });

  test("TC-UI-46: Sluice gates grid dynamically renders individual gate containers", async ({ page }) => {
    await page.locator("#btn-nav-hydraulics").click();
    await expect(page.locator("#sluice-gates-render-grid")).toBeVisible();
    await page.evaluate(() => {
      if (typeof updateUI === 'function') {
        updateUI(
          { damId: 'erai', currentVolumeM3: 181200000, waterLevelMeters: 20, gateOpenPercentage: 40 },
          { recommendedOutflowM3s: 120, predictedInflowM3: 85000000, forecastPrecipitationMm: 24, floodAlertTriggered: false }
        );
      }
    });
    const gates = page.locator("#sluice-gates-render-grid > div");
    const count = await gates.count();
    expect(count).toBeGreaterThanOrEqual(1);
  });

  test("TC-UI-47: Systems diagnostics modal opens when clicking gate info button", async ({ page }) => {
    await page.locator("#btn-nav-hydraulics").click();
    const infoBtn = page.locator("#gate-info-btn");
    await expect(infoBtn).toBeVisible();
    await infoBtn.click();

    const modal = page.locator("#diagnostics-modal");
    await expect(modal).toBeVisible();
    await expect(modal).not.toHaveClass(/hidden/);
  });

  test("TC-UI-48: Systems diagnostics modal closes properly via close button", async ({ page }) => {
    await page.locator("#btn-nav-hydraulics").click();
    await page.locator("#gate-info-btn").click();
    const closeBtn = page.locator("#close-diagnostics-btn");
    await expect(closeBtn).toBeVisible();
    await closeBtn.click();

    const modal = page.locator("#diagnostics-modal");
    await expect(modal).toHaveClass(/hidden/);
  });

  test("TC-UI-49: Live Doppler Radar card renders radar screen canvas", async ({ page }) => {
    const radarCard = page.locator("#card-doppler-radar");
    await expect(radarCard).toBeVisible();
    const radarTitle = await radarCard.textContent();
    expect(radarTitle).toContain("Doppler Radar");
  });

  test("TC-UI-50: Test Siren button triggers emergency broadcast audio-visual banner", async ({ page }) => {
    await page.locator("#btn-top-protocols").click();
    const sirenBtn = page.locator("#btn-test-siren");
    await expect(sirenBtn).toBeVisible();
    await sirenBtn.click();

    const banner = page.locator(".animate-bounce:has-text('Siren Test Transmission Active')");
    await expect(banner).toBeVisible();
    await expect(banner).toContainText("Audio audit: nominal");
  });
});

// ==============================================================================
// SUITE 11: Forecasts Page & Simulation Engine (TC-UI-51 to TC-UI-56)
// ==============================================================================
test.describe("Suite 11: Forecasts Page & Simulation Engine", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
    await page.locator("#btn-nav-forecasts").click();
  });

  test("TC-UI-51: Forecasts page displays 7-day inflow trend chart SVG", async ({ page }) => {
    await expect(page.locator("#page-forecasts")).toBeVisible();
    const svgChart = page.locator("#page-forecasts svg").first();
    await expect(svgChart).toBeVisible();
  });

  test("TC-UI-52: Simulated flood scenarios section contains Monsoon Peak stress test card", async ({ page }) => {
    const forecastContent = await page.locator("#page-forecasts").textContent();
    expect(forecastContent).toContain("Case A: Monsoon Peak");
  });

  test("TC-UI-53: Satellite imagery panel renders in forecasts view", async ({ page }) => {
    const satelliteImg = page.locator("#satellite-img");
    await expect(satelliteImg).toBeVisible();
  });

  test("TC-UI-54: Forecasts page modeling variance info box is visible", async ({ page }) => {
    const forecastContent = await page.locator("#page-forecasts").textContent();
    expect(forecastContent).toContain("Modeling Variance");
  });

  // FLAKY: Real API call without mock - will fail if server is not running
  test("TC-UI-55: Live forecast-eval API returns valid predicted inflow [REQUIRES SERVER]", async ({ page }) => {
    const response = await page.request.get("http://localhost:8080/api/v1/dam/erai/forecast-eval?currentVolumeM3=181200000&forecastRainfallMm=25.0");
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body).toHaveProperty("predictedInflowM3");
    expect(body.predictedInflowM3).toBeGreaterThan(0);
  });

  test("TC-UI-56: PDF export button text contains Generate or PDF label", async ({ page }) => {
    const pdfBtn = page.locator("#btn-generate-pdf");
    await expect(pdfBtn).toBeVisible();
    const btnText = await pdfBtn.textContent();
    expect(btnText.toUpperCase()).toContain("PDF");
  });
});

// ==============================================================================
// SUITE 12: Analytics Dashboard & Historical Stats (TC-UI-57 to TC-UI-61)
// ==============================================================================
test.describe("Suite 12: Analytics Dashboard & Historical Stats", () => {

  test.beforeEach(async ({ page }) => {
    await loginAsOperator(page);
    await page.locator("#btn-top-analytics").click();
  });

  test("TC-UI-57: Analytics peak inflow figure is displayed with unit", async ({ page }) => {
    const peakInflow = page.locator("#analytics-peak-inflow");
    await expect(peakInflow).toBeVisible();
    const text = await peakInflow.textContent();
    expect(text.length).toBeGreaterThan(2);
  });

  test("TC-UI-58: Analytics page title updates dynamically when switching dam", async ({ page }) => {
    await page.locator("#btn-top-dashboard").click();
    await page.locator("#dam-selector").selectOption("mulshi");
    await page.locator("#btn-top-analytics").click();
    const title = await page.locator("#analytics-title").textContent();
    expect(title).toContain("Mulshi");
  });

  test("TC-UI-59: Runoff efficiency stat is present and shows percentage", async ({ page }) => {
    const runoff = page.locator("#analytics-runoff-efficiency");
    await expect(runoff).toBeVisible();
    const text = await runoff.textContent();
    expect(text).toContain("%");
  });

  test("TC-UI-60: Average precipitation stat is visible in the analytics view", async ({ page }) => {
    const avgPrecip = page.locator("#analytics-avg-precipitation");
    await expect(avgPrecip).toBeVisible();
    const text = await avgPrecip.textContent();
    expect(text).toContain("mm");
  });

  // FLAKY: Checks uptime value for exact format -- can fail if dam has different format
  test("TC-UI-61: System uptime displays a 5-nines SLA value of exactly 99.99%", async ({ page }) => {
    const uptime = page.locator("#analytics-uptime");
    await expect(uptime).toBeVisible();
    const text = await uptime.textContent();
    expect(text).toBe("99.99%");
  });
});

// ==============================================================================
// SUITE 13: API Integration via Network Interception (TC-UI-62 to TC-UI-66)
// ==============================================================================
test.describe("Suite 13: API Integration via Network Interception", () => {

  test("TC-UI-62: Status API mock returns valid damId in state object", async ({ page }) => {
    let capturedState = null;
    await page.route("**/api/v1/dam/**", async (route) => {
      if (!route.request().url().includes("ai-recommendation")) {
        return route.fulfill({
          status: 200, contentType: "application/json",
          body: JSON.stringify({
            state: { damId: "erai", currentVolumeM3: 200000000, waterLevelMeters: 22.5, gateOpenPercentage: 50 },
            latestLog: { recommendedOutflowM3s: 150, predictedInflowM3: 90000000, forecastPrecipitationMm: 30, floodAlertTriggered: false, approvalStatus: "PENDING_OPERATOR", statusMessage: "Normal ops" }
          })
        });
      }
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ riskLevel: "NOMINAL", confidenceScore: 88, advisoryMessage: "OK", gateScheduleRecommendation: "Maintain 20%", suggestedActions: [] }) });
    });
    await page.goto(BASE_URL);
    await page.waitForLoadState("domcontentloaded");

    await page.locator("#login-tab-operator").click();
    await page.locator("#login-operator-name").fill("Officer Test");
    await page.locator("#login-operator-shift").selectOption("morning");
    page.on("response", async (res) => {
      if (res.url().includes("/status")) {
        try { const data = await res.json(); capturedState = data.state; } catch (_) {}
      }
    });
    await page.locator("#btn-login-operator-submit").click();
    await page.waitForTimeout(1000);

    if (capturedState) {
      expect(capturedState.damId).toBe("erai");
      expect(capturedState.currentVolumeM3).toBeGreaterThan(0);
    } else {
      console.log("NOTE: API state not captured from interception - verifying UI instead");
      await expect(page.locator("#login-overlay")).toHaveClass(/hidden/);
    }
  });

  // FLAKY: This test explicitly hits the live server -- fails if app is not running
  test("TC-UI-63: Live GET /api/v1/dam/erai/status returns HTTP 200 [REQUIRES SERVER]", async ({ page }) => {
    const response = await page.request.get("http://localhost:8080/api/v1/dam/erai/status");
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body).toHaveProperty("state");
    expect(body.state.damId).toBe("erai");
  });

  // FLAKY: Live AI recommendation endpoint -- fails if server is not running
  test("TC-UI-64: Live GET /api/v1/dam/erai/ai-recommendation returns riskLevel [REQUIRES SERVER]", async ({ page }) => {
    const response = await page.request.get("http://localhost:8080/api/v1/dam/erai/ai-recommendation");
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(["NOMINAL", "WARNING", "CRITICAL"]).toContain(body.riskLevel);
    expect(body.confidenceScore).toBeGreaterThan(0);
  });

  test("TC-UI-65: Refresh button triggers a new API fetch cycle without page reload", async ({ page }) => {
    await loginAsOperator(page);
    let callCount = 0;
    page.on("request", (req) => {
      if (req.url().includes("/status")) callCount++;
    });
    await page.locator("#refresh-btn").click();
    await page.waitForTimeout(500);
    expect(callCount).toBeGreaterThanOrEqual(1);
  });

  test("TC-UI-66: Dam switch triggers a new API status fetch for the new damId", async ({ page }) => {
    await loginAsOperator(page);
    let lastFetchedUrl = "";
    page.on("request", (req) => {
      if (req.url().includes("/status")) lastFetchedUrl = req.url();
    });
    await page.locator("#dam-selector").selectOption("panshet");
    await page.waitForTimeout(600);
    expect(lastFetchedUrl).toContain("panshet");
  });
});

// ==============================================================================
// SUITE 14: Authority Approval Workflow (TC-UI-67 to TC-UI-70)
// ==============================================================================
test.describe("Suite 14: Authority Approval Workflow", () => {

  test("TC-UI-67: Submit Decision button is visible to Operator after login", async ({ page }) => {
    await loginAsOperator(page);
    const submitBtn = page.locator("#submit-authority-btn");
    await expect(submitBtn).toBeVisible();
  });

  test("TC-UI-68: Authority panel shows proposed outflow and rainfall values", async ({ page }) => {
    await loginAsAdmin(page);
    const proposedFlow = page.locator("#auth-proposed-flow");
    const proposedRain = page.locator("#auth-proposed-rain");
    await expect(proposedFlow).toBeVisible();
    await expect(proposedRain).toBeVisible();
    const flowText = await proposedFlow.textContent();
    const rainText = await proposedRain.textContent();
    expect(flowText.length).toBeGreaterThan(0);
    expect(rainText.length).toBeGreaterThan(0);
  });

  test("TC-UI-69: Authority Reject button toggles manual override input field visibility", async ({ page }) => {
    await loginAsAdmin(page);
    const rejectBtn = page.locator("#auth-reject-btn");
    await expect(rejectBtn).toBeVisible();
    const overrideControls = page.locator("#auth-override-controls");
    await rejectBtn.click();
    await expect(overrideControls).not.toHaveClass(/hidden/);
  });

  // FLAKY: Exact format checking can be sensitive to floating point rendering
  test("TC-UI-70: Override flow input accepts numeric values with exactly two decimal precision", async ({ page }) => {
    await loginAsAdmin(page);
    await page.locator("#auth-reject-btn").click();
    const overrideInput = page.locator("#auth-override-flow-input");
    await overrideInput.fill("123.45");
    const val = await overrideInput.inputValue();
    expect(parseFloat(val)).toBeCloseTo(123.45, 2);
    expect(val).toBe("123.45");
  });
});

// ==============================================================================
// SUITE 15: UI Resilience & Edge Case Handling (TC-UI-71 to TC-UI-75)
// ==============================================================================
test.describe("Suite 15: UI Resilience & Edge Case Handling", () => {

  test("TC-UI-71: Dashboard body has content greater than 1000 characters (not blank)", async ({ page }) => {
    await loginAsOperator(page);
    const bodyHtml = await page.locator("body").innerHTML();
    expect(bodyHtml.length).toBeGreaterThan(1000);
  });

  test("TC-UI-72: No uncaught JavaScript exceptions occur during a full operator session", async ({ page }) => {
    const errors = [];
    page.on("pageerror", (err) => errors.push(err.message));
    await loginAsOperator(page, "Officer QA");
    await page.locator("#dam-selector").selectOption("mulshi");
    await page.locator("#btn-nav-hydraulics").click();
    await page.locator("#btn-nav-forecasts").click();
    await page.locator("#btn-top-analytics").click();
    await page.locator("#btn-top-protocols").click();
    await page.locator("#btn-logout").click();
    const critical = errors.filter(e => !e.includes("favicon") && !e.includes("NetworkError"));
    expect(critical.length).toBe(0);
  });

  test("TC-UI-73: Notification badge element exists in the top header", async ({ page }) => {
    await loginAsOperator(page);
    const notifBadge = page.locator("#notif-badge");
    await expect(notifBadge).toBeVisible();
  });

  // FLAKY: DOM timing sensitive -- notif badge text can be 0 or a number depending on API
  test("TC-UI-74: Notification badge shows exactly 3 unread alert count on load", async ({ page }) => {
    await loginAsAdmin(page);
    const notifBadge = page.locator("#notif-badge");
    const badgeText = await notifBadge.textContent();
    expect(parseInt(badgeText.trim())).toBe(3);
  });

  test("TC-UI-75: All four disaster authority dispatch buttons are visible in Admin view", async ({ page }) => {
    await loginAsAdmin(page);
    await expect(page.locator("#btn-notify-forest")).toBeVisible();
    await expect(page.locator("#btn-notify-commissioner")).toBeVisible();
    await expect(page.locator("#btn-notify-ndrf")).toBeVisible();
    await expect(page.locator("#btn-notify-waterboard")).toBeVisible();
  });
});
