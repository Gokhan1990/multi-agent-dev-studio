# 🏢 AI Software Company OS — Multi-Agent Dev Studio

**8 AI agents, 2 rooms, autonomous software development.**  
A multi-agent simulation where AI agents collaborate as a real software company — from product strategy to deployment.

## 🧠 Agents

### Strategy Room
| Agent | Name | Role |
|-------|------|------|
| 👑 CEO | Atilla / Arthur | Company management, strategic decisions |
| 📋 Product Strategist | Elif / Elena | Product vision & roadmap |
| 🔬 Research Agent | Kerem / Kevin | Market research & data analysis |
| 🏗️ System Architect | Zeynep / Zara | System design & technology choices |

### Engineering Room
| Agent | Name | Role |
|-------|------|------|
| ⚙️ Backend Developer | Bora / Blake | API & service development |
| 🎨 Frontend Developer | Deniz / Daisy | UI component development |
| ✅ QA Agent | Cem / Chris | Testing & quality assurance |
| 🚀 DevOps Agent | Sibel / Sarah | Infrastructure & deployment |

## ✨ Features

- **🎤 Voice Chat** — Speak to agents, they respond with voice (Turkish & English)
- **💬 Text Chat** — Type messages, agents respond with context-aware AI
- **🔧 Voice → Code** — Say "Bora API'yi güncelle", AI reads the file, edits code, shows diff
- **🔄 Self-Improvement** — System scans code for TODO/FIXME/hardcoded values, suggests & applies fixes
- **📊 Activity Terminal** — Real-time log feed showing agent actions
- **🧠 Episodic Memory** — Agents remember past conversations and improve over time
- **📝 Code Review** — AI reviews every change before applying (syntax, security, performance)
- **🏗️ Auto Deploy** — Frontend changes trigger automatic Docker rebuild

## 🏗️ Architecture

```
User (voice/text) → React Frontend → API (.NET 8) → AI Services (LLM)
                                                    ↓
                                            Code Execution
                                                    ↓
                                            Docker Containers
```

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React + Vite, Tailwind CSS |
| Backend | .NET 8, C# |
| Database | PostgreSQL 16 |
| AI | DeepSeek (OpenRouter), Gemini 2.0 Flash, Groq |
| Audio | Web Speech API (SpeechRecognition + SpeechSynthesis) |
| Container | Docker, Docker Compose |
| CLI Agent | opencode (opencode/deepseek-v4-flash-free) |

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- API keys: OpenRouter, Gemini, Groq (optional)

### Run

```powershell
# 1. Set API keys
$env:OPENROUTER_API_KEY = "sk-or-..."
$env:GEMINI_API_KEY = "AIza..."
$env:GROQ_API_KEY = "gsk_..."

# 2. Build & start
docker build --no-cache -t saasfast-backend -f .\Backend\Dockerfile .\Backend
docker build --no-cache -t saasfast-frontend -f .\frontend\Dockerfile .\frontend
docker tag saasfast-backend testprojem-backend-api
docker tag saasfast-frontend testprojem-frontend
cd Infrastructure; docker compose up -d
```

Or use the helper script:
```powershell
cd Infrastructure
docker compose up -d --build
```

### Access
- **Frontend:** http://localhost:3000
- **Backend API:** http://localhost:5000
- **Health Check:** http://localhost:5000/api/command/ping

## 📡 API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/command/ping | Health check |
| POST | /api/agent/ask | AI chat with agent |
| POST | /api/agent/chime | Moderator intervention |
| GET | /api/agent/voices | Agent voice list |
| POST | /api/chat/send | Send chat message |
| GET | /api/chat/history | Chat history |
| GET | /api/command/feed | Live terminal log feed |
| GET | /api/command/activity | Active & recent commands |
| GET | /api/command/conversations | Conversation history |
| GET | /api/agents | Agent list |
| POST | /api/command | Voice command → code |
| POST | /api/self-improve/scan | Scan codebase for improvements |
| POST | /api/self-improve/apply/{id} | Apply improvement suggestion |

## 🧪 Tests

```powershell
cd Backend.Tests
dotnet test
```

51 unit tests covering command queue, agent routing, domain entities, and orchestrator logic.

## 📁 Project Structure

```
├── Backend/                  # .NET 8 API
│   ├── Application/Services/ # Business logic & AI services
│   ├── Domain/Entities/      # Domain models
│   ├── Infrastructure/       # EF Core, Docker config
│   └── Presentation/         # API Controllers
├── Backend.Tests/            # xUnit tests
├── frontend/                 # React + Vite SPA
│   └── src/components/       # UI components
├── Infrastructure/           # Docker Compose
├── Agents/                   # Agent definitions
├── AgentMemory/              # Conversation & memory persistence
└── Commands/                 # Shared command queue
```
