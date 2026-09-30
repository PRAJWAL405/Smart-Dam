# HYDRO-OS — Automated Smart Reservoir & Dam Management System

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4)](https://learn.microsoft.com/ef/core/)
[![MySQL 8.0](https://img.shields.io/badge/MySQL-8.0-4479A1?logo=mysql&logoColor=white)](https://www.mysql.com/)
[![xUnit Tests](https://img.shields.io/badge/xUnit-13%20Passed-brightgreen)](tests/DamControlSystem.Tests)
[![Playwright E2E](https://img.shields.io/badge/Playwright_E2E-75%20Tests%20%7C%2070%20Pass-blue)](tests/playwright)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](docker-compose.yml)

> **STQA Mini-Project — Semester 7**
> An intelligent, real-time hydrological decision support engine and operational dashboard for reservoir management, proactive flood mitigation, automated sluice gate scheduling, and emergency response dispatch.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Quick Start — Local Run](#quick-start--local-run)
- [Docker Deployment](#docker-deployment)
- [REST API Reference](#rest-api-reference)
- [STQA Test Suite](#stqa-test-suite)
  - [Unit & Integration Tests (xUnit)](#unit--integration-tests-xunit)
  - [E2E UI Automation (Playwright)](#e2e-ui-automation-playwright)
- [Project Structure](#project-structure)
- [Supported Dams](#supported-dams)
- [Environment Configuration](#environment-configuration)

---

## Overview

HYDRO-OS v4.2 is a full-stack Smart Dam Management System ported from Java (Spring Boot) to modern **C# .NET 10 / ASP.NET Core Web API**. It provides:

- Real-time hydrological telemetry and automated gate scheduling
- Role-based access control (Shift Operator vs. Government Authority Admin)
- AI-driven risk synthesis with CRITICAL / WARNING / NOMINAL classification
- Emergency alert dispatch pipeline with authority review workflows
- A responsive, single-page HYDRO-OS v4.2 dashboard frontend (Vanilla JS, no frameworks)

---

## Architecture

```
Smart-Dam/
├── DamControlSystem.sln              # Visual Studio / .NET Solution
├── DamControlSystem.slnx             # Modern .NET 10 Solution format
├── Dockerfile                        # Multi-stage production container (SDK 8 -> ASP.NET 8)
├── docker-compose.yml                # Full stack: API + MySQL 8
├── src/
│   └── DamControlSystem/             # ASP.NET Core Web API (Port 8080)
│       ├── BackgroundServices/       # WaterFlowBackgroundService (hourly hydrological assessment)
│       ├── Controllers/              # DamController — all REST endpoints
│       ├── Data/                     # SmartDamDbContext, Repositories
│       ├── DTOs/                     # Request & Response data transfer models
│       ├── Models/                   # DamMetadata, ReservoirState, ControlLog, EmergencyAlert
│       ├── Services/                 # DamControlEngineService, AiSuggestionService, WeatherForecastService
│       ├── wwwroot/                  # HYDRO-OS v4.2 static frontend dashboard (index.html)
│       ├── appsettings.json          # App configuration & DB connection strings
│       └── Program.cs                # App bootstrap, DI container, EF Core config
└── tests/
    ├── DamControlSystem.Tests/       # xUnit automated unit + integration tests
    │   ├── HydrologicalEngineTests.cs
    │   ├── AiSuggestionServiceTests.cs
    │   ├── DamMetadataTests.cs
    │   ├── ExtendedTests.cs          # BVA & extended hydrological edge case tests
    │   └── ApiIntegrationTests.cs    # Full ASP.NET Core WebApplicationFactory integration tests
    └── playwright/                   # Playwright E2E UI automation (75 test cases)
        ├── hydro-os.spec.js          # Complete 75-test STQA spec
        └── package.json
```

---

## Key Features

| Feature | Description |
|---|---|
| **Hydrological Inflow Prediction** | Calculates projected inflows via Rational Method (Q = C × I × A) using live 3-day rainfall from Open-Meteo API |
| **Proactive Discharge Engine** | Maintains reservoir volume below 85% safety threshold; throttles release to downstream safe flow capacity |
| **AI Recommendation Engine** | Synthesizes telemetry into `CRITICAL` / `WARNING` / `NOMINAL` risk advisories with confidence score and gate schedule |
| **Automated Emergency Alerting** | Dispatches siren alerts to downstream villages when inflow projections exceed channel thresholds |
| **Multi-Dam Support** | Erai (Chandrapur), Khadakwasla (Pune), Panshet (Pune), Mulshi (Pune) |
| **Dual-Role Dashboard** | Operator shift timer, emergency dispatcher; Admin authority review, manual discharge override |
| **Background Scheduler** | `WaterFlowBackgroundService` evaluates and updates gate percentages hourly |
| **Disaster Response Panel** | Admin one-click dispatch to Forest Officer, District Commissioner, NDRF, Water Board |
| **Live Doppler Radar** | Animated radar sweep and regional hydro-alert news feed on dashboard |
| **PDF Report Generator** | On-demand hydrological summary PDF export from Forecasts page |

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Backend Runtime** | .NET 10 / C# 13, ASP.NET Core Web API |
| **ORM** | Entity Framework Core 9 (MySQL via Pomelo, SQLite fallback) |
| **Database** | MySQL 8.0 (Docker) / SQLite (local dev auto-fallback) |
| **Frontend** | Vanilla HTML5, CSS3 (Tailwind CDN), JavaScript (ES2023 modules) |
| **Unit Tests** | xUnit 2.9, EF Core InMemory, coverlet |
| **Integration Tests** | Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) |
| **E2E UI Tests** | Playwright Test 1.63 (Chromium headless) |
| **Containerization** | Docker, Docker Compose |
| **Weather API** | Open-Meteo (free, no key required) |

---

## Prerequisites

### For Local Development (.NET)

| Tool | Version | Download |
|---|---|---|
| **.NET SDK** | 10.0 (or 8.0 / 9.0) | https://dotnet.microsoft.com/download |
| **Git** | Any recent version | https://git-scm.com |
| **MySQL** (optional) | 8.0 | https://dev.mysql.com/downloads/ |

> **Note:** If MySQL is not detected, the app automatically falls back to a local SQLite file (`smart_dam.db`). No database setup is required for a quick local run.

### For Docker Deployment

| Tool | Version |
|---|---|
| **Docker Desktop** | 24.0+ |
| **Docker Compose** | v2 (bundled with Docker Desktop) |

### For Running Playwright E2E Tests

| Tool | Version |
|---|---|
| **Node.js** | 18.0+ (LTS recommended) |
| **npm** | 9.0+ (bundled with Node.js) |

---

## Quick Start — Local Run

### 1. Clone the Repository

```bash
git clone https://github.com/Tanmaychandekar45/Smart-Dam.git
cd Smart-Dam
```

### 2. Build the Solution

```bash
dotnet build DamControlSystem.sln
```

### 3. Run Unit & Integration Tests

```bash
dotnet test DamControlSystem.sln
```

Expected output:
```
Passed!  - Failed: 0, Passed: 13, Skipped: 0
```

### 4. Launch the Application

```bash
dotnet run --project src/DamControlSystem
```

The API and dashboard will start on **http://localhost:8080**

Open your browser and navigate to **[http://localhost:8080](http://localhost:8080)**

### 5. Login Credentials

The app uses a session-based role system (no password required for demo):

| Role | How to Login |
|---|---|
| **Shift Operator** | Click "OPERATOR LOGIN" tab → Enter any name → Select shift → Submit |
| **Authority Admin** | Click "ADMIN LOGIN" tab → Enter any admin ID → Submit |

---

## Docker Deployment

To launch the full stack (ASP.NET Core API + MySQL 8 database):

```bash
docker compose up --build -d
```

| Service | URL / Port |
|---|---|
| **Web Dashboard & API** | http://localhost:8080 |
| **MySQL Database** | localhost:3307 (maps to internal 3306) |

To stop:
```bash
docker compose down
```

---

## REST API Reference

All endpoints are rooted under `/api/v1/dam`:

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/v1/dam/status` | Latest reservoir state and decision log (default: Erai) |
| `GET` | `/api/v1/dam/{damId}/status` | Current status and telemetry for specified dam |
| `POST` | `/api/v1/dam/{damId}/update-state` | Update reservoir volume/level, fetch rain forecast, run control logic |
| `GET` | `/api/v1/dam/{damId}/forecast-eval` | On-demand hypothetical simulation (non-persisting) |
| `GET` | `/api/v1/dam/{damId}/ai-recommendation` | AI risk synthesis, confidence score, and gate schedules |
| `POST` | `/api/v1/dam/{damId}/submit-decision` | Submit operator draft decision for authority review |
| `POST` | `/api/v1/dam/{damId}/authority-action` | Authority approval (`APPROVE`) or manual discharge override (`REJECT`) |
| `POST` | `/api/v1/dam/{damId}/emergency-alert` | Dispatch emergency priority alert from shift officer |
| `GET` | `/api/v1/dam/alerts` | List all active/unresolved emergency alerts |
| `POST` | `/api/v1/dam/alert/{id}/resolve` | Resolve an emergency alert by ID |

### Supported `damId` values

| `damId` | Dam Name | Location |
|---|---|---|
| `erai` | Erai Dam | Chandrapur, Maharashtra |
| `khadakwasla` | Khadakwasla Dam | Pune, Maharashtra |
| `panshet` | Panshet Dam | Pune, Maharashtra |
| `mulshi` | Mulshi Dam | Pune, Maharashtra |

---

## STQA Test Suite

This project implements a three-tier STQA test strategy:

```
Tier 1: Unit Tests          (xUnit — hydrological engine, AI service, dam metadata)
Tier 2: Integration Tests   (WebApplicationFactory — REST API endpoint validation)
Tier 3: E2E UI Tests        (Playwright — full browser automation, 75 test cases)
```

---

### Unit & Integration Tests (xUnit)

**Location:** `tests/DamControlSystem.Tests/`

#### Run All Tests

```bash
dotnet test DamControlSystem.sln
```

#### Test Files & Coverage

| File | Test Class | Scope |
|---|---|---|
| `HydrologicalEngineTests.cs` | `HydrologicalEngineTests` | Runoff projection formulas, proactive discharge, flood alert triggers, non-mutating simulations |
| `AiSuggestionServiceTests.cs` | `AiSuggestionServiceTests` | Risk level classification (CRITICAL / WARNING / NOMINAL), confidence score, gate schedule generation |
| `DamMetadataTests.cs` | `DamMetadataTests` | Registry validation for all 4 Maharashtra dams |
| `ExtendedTests.cs` | `HydrologicalEngineExtendedTests` | BVA tests — zero rainfall, boundary volume, peak inflow, flood threshold edge cases (TC-M3-04 to TC-M3-10, TC-M7-01/02) |
| `ApiIntegrationTests.cs` | `ApiIntegrationTests` | Full ASP.NET Core in-memory API tests — status, update-state, forecast-eval, ai-recommendation, emergency-alert, authority-action, alert resolve (TC-M2-01 to TC-M9-05) |

---

### E2E UI Automation (Playwright)

**Location:** `tests/playwright/`

#### Prerequisites

```bash
cd tests/playwright
npm install
npx playwright install chromium
```

#### Run All 75 Test Cases

```bash
npx playwright test
```

#### Run with Interactive HTML Report

```bash
npx playwright test --reporter=html
npx playwright show-report
```

#### Run a Specific Suite

```bash
npx playwright test --grep "Suite 1"
```

#### Test Suite Overview (75 Total)

| Suite | Test IDs | Area | Tests |
|---|---|---|---|
| Suite 1 | TC-UI-01 to 05 | Application Launch & Security Overlay | 5 |
| Suite 2 | TC-UI-06 to 10 | Authentication & Role-Based Access Control | 5 |
| Suite 3 | TC-UI-11 to 16 | Multi-Reservoir Registry & Dam Switching | 6 |
| Suite 4 | TC-UI-17 to 21 | Top Navigation & Page View Routing | 5 |
| Suite 5 | TC-UI-22 to 26 | Sidebar Navigation & Sub-page Switching | 5 |
| Suite 6 | TC-UI-27 to 31 | Telemetry Displays & Storage Metrics | 5 |
| Suite 7 | TC-UI-32 to 35 | Reservoir Control Sliders & RBAC Protection | 4 |
| Suite 8 | TC-UI-36 to 40 | AI Hydro-Advisor & Recommendation Synthesis | 5 |
| Suite 9 | TC-UI-41 to 45 | Emergency Dispatcher & Disaster Response | 5 |
| Suite 10 | TC-UI-46 to 50 | Hydraulics Diagnostics, Radar & Siren | 5 |
| Suite 11 | TC-UI-51 to 56 | Forecasts Page & Simulation Engine | 6 |
| Suite 12 | TC-UI-57 to 61 | Analytics Dashboard & Historical Stats | 5 |
| Suite 13 | TC-UI-62 to 66 | API Integration via Network Interception | 5 |
| Suite 14 | TC-UI-67 to 70 | Authority Approval Workflow | 4 |
| Suite 15 | TC-UI-71 to 75 | UI Resilience & Edge Case Handling | 5 |

#### Latest Playwright Execution Results

```
75 tests run using 1 worker
70 passed  |  5 failed  |  0 skipped
Duration: 3.0 minutes
```

| Result | Count | Details |
|---|---|---|
| ✅ Passed | 70 | Suites 1–10 fully green; Suites 11–15 majority pass |
| ❌ Failed | 5 | See Known Failures table below |

#### Known Test Failures (Intentional / Environment-Dependent)

| TC ID | Test Name | Failure Reason | Type |
|---|---|---|---|
| TC-UI-61 | System uptime shows exactly 99.99% | Erai uptime is `99.98%`; test asserts `99.99%` | Spec Mismatch |
| TC-UI-65 | Refresh triggers new API fetch | Route mocks intercept before request listener registers | Timing / Mock Conflict |
| TC-UI-66 | Dam switch triggers fetch for new damId | Route mock intercepts; raw URL listener sees no requests | Timing / Mock Conflict |
| TC-UI-73 | Notification badge visible in header | Badge is CSS-hidden when alert count is zero | Environment State |
| TC-UI-74 | Badge shows exactly 3 unread alerts | API mock returns 0 alerts; `parseInt("")` = NaN ≠ 3 | Data Mismatch |

> Tests TC-UI-55, TC-UI-63, and TC-UI-64 are annotated `[REQUIRES SERVER]` and only pass when the ASP.NET Core app is running locally on `http://localhost:8080`.

---

## Supported Dams

| Dam | ID | Location | Max Capacity | Max Level |
|---|---|---|---|---|
| Erai Dam | `erai` | Chandrapur, MH | 453 M m³ | 23.6 m |
| Khadakwasla Dam | `khadakwasla` | Pune, MH | 97.3 M m³ | 18.9 m |
| Panshet Dam | `panshet` | Pune, MH | 307 M m³ | 57.6 m |
| Mulshi Dam | `mulshi` | Pune, MH | 522 M m³ | 68.7 m |

---

## Environment Configuration

### `appsettings.json`

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

> If the MySQL connection string fails, the application automatically switches to a local SQLite database file (`smart_dam.db` in the project root). No manual configuration is needed for a first run.

### Environment Variables (Docker)

| Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | App environment |
| `ConnectionStrings__DefaultConnection` | MySQL URL | Override DB connection |

---

## License

This project is developed for academic purposes as part of the **Semester 7 Software Testing & Quality Assurance (STQA) Mini-Project**.

---

*Built with .NET 10, ASP.NET Core, xUnit, and Playwright.*
