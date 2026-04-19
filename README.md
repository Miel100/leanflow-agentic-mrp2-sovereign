# LeanFlow Agentic MRP2

A sovereign, fully agentic, open-source Lean MRP2 system built on .NET 9.
Designed as a Microsoft Dynamics 365 alternative for companies that require full data sovereignty.

## What is this?

LeanFlow is a Cloud-Based MRP2 system faithful to the Lean MRP2 philosophy:
- Simplified **Rating File** replaces traditional BOM
- **SFC (Shop Floor Control)** replaces nuclear algorithmic MRP
- Retained Lean modules: Inventory Record, RCCP, CRP, Demand Management, MPS, Sales Order Processing

## Architecture

Multi-agent system powered by LLM (Groq / Mistral):

- **Supervisor Agent** — coordinates all agents, accepts plain English prompts
- **Demand Agent** — forecasting and customer order analysis
- **RCCP Agent** — rough-cut capacity planning
- **CRP Agent** — detailed capacity requirements
- **SFC Agent** — shop floor control using Rating File

## Tech Stack

- .NET 9 / C# — Clean Architecture
- Groq API (Llama 3) — LLM inference
- Hangfire — automated MRP scheduling
- PostgreSQL ready — sovereign database
- OVHcloud ready — EU sovereign deployment
- xUnit — 7 passing tests

## Getting Started

### Prerequisites
- .NET 9 SDK
- Groq API key (free at https://console.groq.com)

### Run locally

`ash
git clone https://github.com/Miel100/leanflow-agentic-mrp2-sovereign.git
cd leanflow-agentic-mrp2-sovereign
set GROQ_API_KEY=your_key_here
dotnet run --project src/LeanFlow.Api/LeanFlow.Api.csproj

## API Endpoints

| Endpoint | Method | Description |
|---|---|---|
| /api/mrp/run | GET | Run full MRP cycle |
| /api/mrp/status | GET | System health + active agents |
| /api/mrp/prompt | POST | Send prompt to Supervisor Agent |
| /hangfire | GET | Scheduler dashboard |

## Roadmap

- [x] Domain layer (RatingFile, WorkOrder, InventoryRecord, DemandForecast)
- [x] Multi-agent team (Demand, RCCP, CRP, SFC, Supervisor)
- [x] Groq LLM integration
- [x] Hangfire automated scheduling
- [x] Frontend dashboard
- [x] Unit tests (7 passing)
- [ ] PostgreSQL persistence
- [ ] OVHcloud deployment
- [ ] Quantum optimization (Pasqal + CUDA-Q)

## License

AGPLv3 — see LICENSE file.

## Author

Georges Tresor Doungala
