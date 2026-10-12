# HYDRO-OS — Automated Smart Reservoir & Dam Management System

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4)](https://learn.microsoft.com/ef/core/)
[![MySQL / SQLite](https://img.shields.io/badge/DB-MySQL%208%20%7C%20SQLite-4479A1?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![STQA Test Suite](https://img.shields.io/badge/xUnit-79%20Passed%20(100%25)-brightgreen)](tests/DamControlSystem.Tests)
[![STQA HTML Report](https://img.shields.io/badge/STQA%20Report-Interactive%20HTML-blueviolet)](TestReport.html)
[![Playwright E2E](https://img.shields.io/badge/Playwright_E2E-75%20Tests%20%7C%2070%20Pass-blue)](tests/playwright)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](docker-compose.yml)

> **STQA Mini-Project — Semester 7 (Software Testing & Quality Assurance)**  
> An intelligent, real-time hydrological decision support engine and operational dashboard for reservoir management, proactive flood mitigation, automated sluice gate scheduling, and emergency response dispatch.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Quick Start — Local Run](#quick-start--local-run)
  - [1. Clone Repository](#1-clone-the-repository)
  - [2. Build Solution](#2-build-the-solution)
  - [3. Run Tests & Generate HTML Report](#3-run-tests--generate-html-report)
  - [4. Launch the Application](#4-launch-the-application)
  - [5. Login Credentials](#5-login-credentials)
- [Interactive STQA HTML Test Report](#interactive-stqa-html-test-report)
- [STQA Test Suite & Requirement Traceability Matrix](#stqa-test-suite--requirement-traceability-matrix)
  - [Module Coverage (79 Unit & Integration Tests)](#module-coverage-79-unit--integration-tests)
  - [Playwright E2E UI Automation (75 Tests)](#playwright-e2e-ui-automation-75-tests)
- [REST API Reference](#rest-api-reference)
- [Docker Deployment](#docker-deployment)
- [Supported Dams](#supported-dams)
- [Environment Configuration](#environment-configuration)
- [License & Academic Declaration](#license--academic-declaration)

---

## Overview

**HYDRO-OS v4.2** is a mission-critical Smart Dam Management System developed with **C# .NET 10 and ASP.NET Core Web API**. It is designed to evaluate, forecast, and automate reservoir sluice gate operations to prevent downstream flash floods while maintaining optimal water storage:

- **Rational Hydrological Runoff Modeling**: Dynamic inflow projection from 3-day precipitation forecasts.
- **Proactive Sluice Gate Control**: Automated release scheduling before inflow peaks breach storage thresholds.
- **AI Hydro-Advisor**: Multi-factor telemetry synthesis providing `CRITICAL`, `WARNING`, and `NOMINAL` operational advisories.
- **Role-Based Operational Workflows**: Operator shift handovers, manual discharge overrides, and two-tier authority review.
- **Emergency Sirens & Downstream Dispatch**: Automated dispatch pipeline alerting downstream villages, NDRF, and emergency services.
- **Rigorous STQA Verification**: 100% requirement traceability matrix verified across 79 automated .NET test cases and 75 Playwright E2E browser specifications.

---

## Architecture

```
Smart-Dam/
├── DamControlSystem.sln              # Visual Studio / .NET Solution
├── DamControlSystem.slnx             # Modern .NET 10 Solution definition
├── Dockerfile                        # Multi-stage production container
├── docker-compose.yml                # Full stack deployment: API + MySQL 8
├── run-tests.bat                     # Windows batch script for automated test & report generation
├── run-tests.ps1                     # PowerShell test runner script
├── TEST_COMMANDS.txt                 # Quick CLI reference for all test execution modes
├── TestReport.html                   # Standalone Interactive STQA HTML Test Report
├── src/
│   └── DamControlSystem/             # ASP.NET Core Web API (Port 8080)
│       ├── BackgroundServices/       # WaterFlowBackgroundService (hourly telemetry poll)
│       ├── Controllers/              # DamController — RESTful API endpoints
│       ├── Data/                     # SmartDamDbContext, Repositories, Migrations
│       ├── DTOs/                     # Strongly-typed request/response models
│       ├── Models/                   # DamMetadata, ReservoirState, ControlLog, EmergencyAlert
│       ├── Services/                 # HydrologicalEngine, AiSuggestionService, WeatherForecastService
│       ├── wwwroot/                  # HYDRO-OS v4.2 Vanilla JS Single Page Application
│       ├── appsettings.json          # DB connection strings & server configuration
│       └── Program.cs                # Dependency injection, middleware & DB fallback
└── tests/
    ├── DamControlSystem.Tests/       # xUnit automated test suite (79 Tests — 100% Pass)
    │   ├── HydrologicalEngineTests.cs# Core water balance, discharge & runoff logic
    │   ├── AiSuggestionServiceTests.cs# AI classification, confidence scores & schedules
    │   ├── DamMetadataTests.cs       # Multi-dam registry, cardinality & lookup fault tolerance
    │   ├── ExtendedTests.cs          # Boundary Value Analysis (BVA) & edge conditions
    │   ├── ApiIntegrationTests.cs    # WebApplicationFactory end-to-end API integration
    │   ├── StqaHtmlReportGenerator.cs# RTM mapping & HTML report generator engine
    │   └── ReportGenerationTest.cs   # Automated report trigger test fixture
    └── playwright/                   # Playwright E2E browser automation (75 Tests)
        ├── hydro-os.spec.js          # Full browser UI automation spec
        └── package.json
```

---

## Key Features

| Feature | Description |
|---|---|
| **Hydrological Inflow Prediction** | Computes projected runoff volume using the Rational Method ($Q = C \times I \times A$) integrated with Open-Meteo real-time rainfall data. |
| **Proactive Discharge Engine** | Throttles and optimizes discharge releases below downstream safe carrying capacity while maintaining reservoir storage under safe buffer levels (85%). |
| **AI Hydro-Advisor** | Evaluates precipitation, reservoir delta, and storage rate-of-rise to output operational advisories (`NOMINAL`, `WARNING`, `CRITICAL`). |
| **Two-Tier Authority Workflow** | Dual approval system requiring authority admin verification for critical sluice gate adjustments. |
| **Automated Emergency Sirens** | Dispatches tiered alert payloads to local villages, NDRF, District Commissioner, and the Irrigation Board. |
| **Multi-Reservoir Topology** | Pre-configured for four major Maharashtra dams: Erai, Khadakwasla, Panshet, and Mulshi. |
| **Live Doppler Radar & Telemetry** | High-contrast visual radar animation, storage dial gauges, rate-of-rise meters, and live status streaming. |
| **Automatic Database Fallback** | Seamlessly connects to MySQL 8.0 in production or transparently defaults to embedded SQLite (`smart_dam.db`) in local environments. |

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Backend Framework** | .NET 10.0 / C# 13, ASP.NET Core Web API |
| **ORM & Persistence** | Entity Framework Core 9.0 (MySQL Pomelo with auto-fallback to SQLite) |
| **Frontend UI** | HTML5, Modern CSS (Glassmorphism, Cyber-Industrial Dark Theme), Vanilla JavaScript (ES2023) |
| **Unit & Integration Testing** | xUnit 2.9, Microsoft.AspNetCore.Mvc.Testing, InMemory EF Core, Coverlet |
| **E2E UI Testing** | Playwright 1.63 (Chromium Headless & Interactive) |
| **Reporting Engine** | Custom STQA TRX-to-HTML Report Generator with Requirement Traceability Matrix |
| **DevOps & Containers** | Docker, Docker Compose, PowerShell & Windows Batch Automation |

---

## Prerequisites

### For Local Execution (.NET)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (or .NET 8.0 / 9.0)
- [Git](https://git-scm.com/)

### For E2E Browser Testing (Playwright)
- [Node.js 18+](https://nodejs.org/) & `npm`

---

## Quick Start — Local Run

### 1. Clone the Repository
```bash
git clone https://github.com/PRAJWAL405/Smart-Dam.git
cd Smart-Dam
```

### 2. Build the Solution
```bash
dotnet build DamControlSystem.sln
```

### 3. Run Tests & Generate HTML Report
Run the automated STQA test runner with a single command:

**On Windows (Command Prompt / Double Click):**
```cmd
run-tests.bat
```

**On PowerShell:**
```powershell
.\run-tests.ps1
```

**Standard .NET CLI:**
```bash
dotnet test DamControlSystem.sln
```

**Expected Test Output:**
```
==============================================================================
Smart Dam Control System - STQA Automated Test Runner
==============================================================================
[1/3] Executing STQA Test Suite via .NET Test Runner...
Passed!  - Failed: 0, Passed: 79, Skipped: 0, Total: 79, Duration: ~8 s

[2/3] Generating Structured STQA HTML Requirement Report...
------------------------------------------------------------
 STQA TEST EXECUTION SUMMARY
------------------------------------------------------------
 Total Test Cases : 79
 Passed           : 79
 Failed           : 0
 Pass Rate        : 100%
------------------------------------------------------------
 HTML Report      : TestReport.html
------------------------------------------------------------
[3/3] Done. To view the HTML report in your browser:
      Start-Process TestReport.html
```

### 4. Launch the Application
```bash
dotnet run --project src/DamControlSystem/DamControlSystem.csproj
```
The application will launch on **http://localhost:8080**.

Open your browser and navigate to:
👉 **[http://localhost:8080](http://localhost:8080)**

### 5. Login Credentials
HYDRO-OS utilizes session-based role-based access control (RBAC):
- **Shift Operator**: Click **OPERATOR LOGIN** → Enter name (e.g. `Operator John`) → Select Shift (`Morning / Afternoon / Night`) → Submit.
- **Authority Admin**: Click **ADMIN LOGIN** → Enter ID (e.g. `ADM-9041`) → Submit.

---

## Interactive STQA HTML Test Report

The project features a standalone, interactive HTML test report located at:
📄 **[`TestReport.html`](TestReport.html)**

To view the report directly in your browser:
```powershell
Start-Process TestReport.html
```

### Report Highlights & Features:
- **Executive Summary Dashboard**: Total tests, passed, failed, pass percentage, execution duration, and environment timestamp.
- **Live Search & Filter**: Instant filtering by Test ID, Requirement ID, Module Name, or Status (`ALL`, `PASSED`, `FAILED`).
- **Interactive Module Tabs**: Filter test results specifically by Module (M1 through M10).
- **Full Traceability Matrix**: Each test case maps directly to:
  - **Requirement ID** (e.g., `REQ-M1-01`, `REQ-M4-03`)
  - **Requirement Title & Objective**
  - **Formal Invariant / Specification Rule**
  - **Execution Time & Outcome**

---

## STQA Test Suite & Requirement Traceability Matrix

### Module Coverage (79 Unit & Integration Tests)

| Module | Code | Title | Tests | Verification Focus |
|---|---|---|:---:|---|
| **M1** | `REQ-M1` | Dam Metadata & Topologies | 5 | Multi-reservoir registry (Erai, Khadakwasla, Panshet, Mulshi), cardinality invariants, fallback lookup tolerance. |
| **M2** | `REQ-M2` | Reservoir Geometry & Inundation | 8 | Storage volume percentage calculations, freeboard clearances, high-water level invariants, volume deltas. |
| **M3** | `REQ-M3` | Sensor Telemetry & Fail-Safe Fallbacks | 10 | Boundary Value Analysis (BVA), negative value sanitization, spike noise rejection, cached telemetry fallbacks. |
| **M4** | `REQ-M4` | Gate Actuation & Safety Invariants | 8 | Sluice gate physical travel clamping (0–100%), slew rate step limiters, safe discharge limits, override locks. |
| **M5** | `REQ-M5` | AI Decision Advisory Service | 10 | Telemetry synthesis, rule engine classification (`CRITICAL`, `WARNING`, `NOMINAL`), confidence score calibration. |
| **M6** | `REQ-M6` | Live Weather & Forecast Synchronization | 7 | Open-Meteo rainfall parsing, 3-day projection arrays, Rational Method runoff conversion, API timeout fallbacks. |
| **M7** | `REQ-M7` | Downstream Evacuation & Alert Pipelines | 8 | Evacuation zone multi-tier dispatch, village broadcast payloads, priority escalation, alert acknowledgment. |
| **M8** | `REQ-M8` | Operator Telemetry Overrides & Audit | 7 | Shift handoff logging, manual gate override audit trails, immutability of historical logs, user identity tagging. |
| **M9** | `REQ-M9` | Authority Approval Workflow | 8 | Two-tier gate release approval, draft submission states, authority `APPROVE`/`REJECT` transitions, role privilege enforcement. |
| **M10** | `REQ-M10` | Failure Modes & System Resilience | 8 | Database disconnection resilience, HTTP 500 error sanitization, malformed payload handling, concurrency guards. |
| **Total** | | **Complete STQA Test Suite** | **79** | **100% Pass Rate** |

---

### Playwright E2E UI Automation (75 Tests)

Located in `tests/playwright/`:

```bash
cd tests/playwright
npm install
npx playwright test
```

To run with Playwright's built-in web reporter:
```bash
npx playwright test --reporter=html
npx playwright show-report
```

---

## REST API Reference

All endpoints are hosted under `/api/v1/dam`:

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/dam/status` | Current reservoir state and decision log (Default: `erai`) |
| `GET` | `/api/v1/dam/{damId}/status` | Status and telemetry for specified dam (`erai`, `khadakwasla`, etc.) |
| `POST` | `/api/v1/dam/{damId}/update-state` | Update level/volume, fetch weather, and execute control engine |
| `GET` | `/api/v1/dam/{damId}/forecast-eval` | On-demand hypothetical simulation without mutating DB state |
| `GET` | `/api/v1/dam/{damId}/ai-recommendation` | AI risk advisory synthesis, confidence score, and gate schedule |
| `POST` | `/api/v1/dam/{damId}/submit-decision` | Submit operator gate recommendation for authority review |
| `POST` | `/api/v1/dam/{damId}/authority-action` | Authority approval (`APPROVE`) or rejection/override (`REJECT`) |
| `POST` | `/api/v1/dam/{damId}/emergency-alert` | Dispatch emergency siren alert to downstream zones |
| `GET` | `/api/v1/dam/alerts` | List all active downstream emergency alerts |
| `POST` | `/api/v1/dam/alert/{id}/resolve` | Mark an emergency alert as resolved |

---

## Docker Deployment

To launch the full containerized environment:

```bash
docker compose up --build -d
```

| Service | Accessible URL |
|---|---|
| **HYDRO-OS Dashboard & API** | [http://localhost:8080](http://localhost:8080) |
| **MySQL Database Container** | `localhost:3307` (mapped from 3306) |

To stop the containers:
```bash
docker compose down
```

---

## Supported Dams

| Dam Name | ID | Location | Max Capacity | Full Reservoir Level | Catchment Area | Safe Downstream Discharge |
|---|---|---|:---:|:---:|:---:|:---:|
| **Erai Dam** | `erai` | Chandrapur, Maharashtra | 453 M m³ | 23.60 m | 460 km² | 1,200 m³/s |
| **Khadakwasla Dam** | `khadakwasla` | Pune, Maharashtra | 97.3 M m³ | 18.90 m | 501 km² | 850 m³/s |
| **Panshet Dam** | `panshet` | Pune, Maharashtra | 307 M m³ | 57.60 m | 118 km² | 1,100 m³/s |
| **Mulshi Dam** | `mulshi` | Pune, Maharashtra | 522 M m³ | 68.70 m | 235 km² | 1,450 m³/s |

---

## Environment Configuration

Configuration is managed via `src/DamControlSystem/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=smart_dam;User=root;Password=yourpassword;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

*Note: If the MySQL server is unreachable, the system automatically initializes an embedded SQLite database (`smart_dam.db`) in the root folder with zero configuration required.*

---

## License & Academic Declaration

This project was engineered as part of the **Semester 7 Software Testing & Quality Assurance (STQA) Mini-Project curriculum**.  
All requirements, test fixtures, and artifacts are intended for academic evaluation and demonstration of professional software testing standards.
