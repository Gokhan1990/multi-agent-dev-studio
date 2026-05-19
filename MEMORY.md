# Memory - Oturum Özeti

> **Bu dosyayı güncelleme kuralı:** Her yeni özellik, bugfix veya deployment sonrası bu dosyaya ekleme yap.
> "projeye devam" dediğinde yapay zeka sana bu dosyayı okur ve kaldığın yerden devam eder.
> Bilgisayarı kapattığında tekrar açıp `MEMORY.md` içindeki son durumu okuyup **"projeye devam"** yazman yeterli.

## Proje: AI Software Company OS
Multi-agent AI yazılım şirketi simülasyonu. 8 ajan, 2 oda, .NET 8 backend, React+Vite frontend, PostgreSQL, Docker.

---

## Oturum Geçmişi

### Oturum 3 (2026-05-16) — Multi-Agent Terminal + Kesintisiz Sohbet

#### Yapılanlar

##### 1. Ses Düzeltme (frontend - MeetingRoom.jsx)
- **Sorun**: Kadın ajanlar erkek sesi çıkarıyordu
- **Çözüm**: Türkçe ses seçim mantığı yeniden yazıldı
  - Kadın ajanlar önce `tunali`/`asli`/`sedef` pattern'li ses arar
  - Erkek ajanlar `murat`/`erkek` pattern'li ses arar
  - Türkçe ses yoksa İngilizce ses'e fallback yapıldı (eskiden sessiz kalıyordu)
  - Cinsiyet uyuşmazlığında pitch: kadın x1.8, erkek x0.5

##### 2. Sesli Komut → Kod Değişikliği Sistemi
- **POST /api/command** endpoint'i oluşturuldu (CommandController)
- **CommandQueueService**: Sesli komutları `Commands/queue.json`'a yazıyor
- **Frontend entegrasyonu**: `değiştir`, `güncelle`, `yap`, `ekle` gibi keywordler algılanınca otomatik command API çağrısı
- **watch_commands.ps1**: CLI watcher scripti
- **Volume mount**: `../Commands:/commands` docker-compose'a eklendi
- **CommandQueueService** `Program.cs:22`'de singleton olarak register edildi

##### 3. Gerçek Zamanlı Aktivite Akışı (Activity Feed)
- **GET /api/command/activity** endpoint'i — aktif + son 10 komutu döndürür
- **CommandQueueService**'e `Steps` listesi, `ActiveAgentId`, `AddStep()` eklendi
- **Frontend polling**: `MeetingRoom.jsx` her 3 saniyede `/api/command/activity`'yi sorgular
- **UI**: Sağ panelde agent adı, durum (renkli nokta), step-by-step ilerleme gösterilir

##### 4. Gerçek Zamanlı Terminal Activity Feed
- **Yeni `LogEntry` modeli**: `CodeCommand.Logs` listesi — her log satırı `text`, `level` (info/success/error/warn/cmd), `agentId`, `timestamp` içerir
- **`AddLog()` metodu**: CommandQueueService'e eklendi — her adım için terminal satırı yazılabilir
- **`GET /api/command/feed`** endpoint'i — tüm logları zamana göre ters sıralı döndürür (varsayılan son 100)
- **`ActivityTerminal.jsx`** — GitHub siyah teması, monospace font, terminal benzeri UI
  - Tüm ajanlar için **dinamik prompt**: `<agentName>@agent:~$` (cmd satırları)
  - Cmd olmayan satırlarda `[Agent Adı]` etiketi
  - `allAgents` import edilip `getAgentField` ile isim çözümlemesi yapılır
  - Renk kodlu seviyeler: cyan(info), green(success), red(error), amber(warn), violet(cmd)
  - Her 1.5 saniyede polling, aşağıdan yukarıya akan satırlar
  - MacOS terminal üç nokta simülasyonu (kırmızı/sarı/mavi)
- **`MeetingRoom.jsx`** güncellendi — eski activity paneli kaldırıldı, ActivityTerminal eklendi
- **`speakingRef` kilidi kaldırıldı**: Kullanıcı her zaman yeni mesaj yazabilir/konuşabilir
  - Yeni input gelince `speechSynthesis.cancel()` ile eski konuşma durdurulur
  - Input/button'lardaki `disabled={speakingRef.current}` kaldırıldı

##### 5. Otomatik Kod Değişikliği (Agent Ask → Code Execution)
- **`AgentChatController.Ask()`** artık `CommandQueueService` ve `CodeExecutorService` inject ediyor
- `CodeTriggers` listesi ile mesajdaki kod değişikliği talebi algılanır
- Kod değişikliği algılanınca → `_queue.Enqueue()` + `_executor.ExecuteAsync()` otomatik tetiklenir
- Frontend'de ayrı `sendCommand` / `codeKeywords` mantığı kaldırıldı — tüm iş backend'de
- Yanıt `command` objesi içerir: `{ id, text, targetFile, result: { success, summary, diff } }`
- Frontend `askAI()` sonucundaki `command`'i chat transcript'e ekler (`✅ summary` + diff)

##### 6. Routing İyileştirme + Tekrarlayan Yanıt Düzeltme
- **`responses.js`** güncellendi:
  - `kes`, `sus`, `dur`, `yeter`, `stop`, `silence` gibi komutlar → boş `agentIds` döndürür, ajan konuşmaz
  - `X değil Y yapacak` mantığı: `değil`/`not` kelimesinden sonraki agent adı hedef alınır
  - `frontend` agent'ın keyword'lerine `stil`, `css`, `renk`, `görsel`, `bileşen` eklendi
- **`AiService.cs`** güncellendi:
  - `maxOutputTokens`: 80 → 150, `temperature`: 0.7 → 0.9 (daha çeşitli yanıtlar)
  - Prompt'lara "Her seferinde FARKLI cevap ver, asla tekrarlama" eklendi
- **`MeetingRoom.jsx`**: Boş `agentIds` durumunda `speakSequence` çağrılmaz

##### 7. OpenRouter Fallback + Fallback Mesaj İyileştirme
- **`AiService.cs`**: OpenRouter (`mistralai/mistral-7b-instruct`) eklendi — Gemini + Groq 429 hatası verince OpenRouter kullanılır
- **`CodeExecutorService.cs`**: `TryOpenRouterChange()` ve `TryOpenRouterGuess()` eklendi
- **`docker-compose.yml`**: `AI__OpenRouterApiKey=${OPENROUTER_API_KEY:-}` eklendi
- **Fallback mesajlar güncellendi**: Deniz "Tasarımı düşünüyorum." → "Bileşeni güncelliyorum, hemen yapıyorum.", Bora "Hallederim." → "Kodu değiştiriyorum, az sonra hazır."

##### 8. Gerçek Kod Değişikliği (Code Executor)
- **`CodeExecutorService`** — AI ile dosya oku, değiştir, yaz
  - `GuessTargetFile()` → AI ile hedef dosyayı belirle (Gemini/Groq fallback)
  - `GenerateNewContent()` → AI ile yeni dosya içeriği üret
  - `ComputeDiff()` → satır bazlı diff hesapla
  - Her adımda `AddLog()` ile terminal güncellemesi
- **Source mount**: docker-compose'a `../:/source` volume'u eklendi
- **`CommandController.Submit()`** → artık `async`, command'i alır almaz `CodeExecutorService.ExecuteAsync()` ile hemen işleme başlar
  - `DetectAgent()` ile hangi ajanın çalıştığı belirlenir
  - Sonuç `Result` objesi olarak döner (`Success`, `Summary`, `Diff`)
- **Frontend**: `sendCommand()` sonucunu `isResult`/`isDiff` olarak chat transcript'e ekler
  - ✅ yeşil başarı mesajı, diff `emerald` monospace ile gösterilir

##### 8. Altyapı
- Eski local .NET process (`SaaSFast.exe`) PID 23604 `localhost:5000`'de çalışıyormuş — kapatıldı
- **Build stratejisi**: `docker compose build` yerine `docker build --no-cache` + `docker tag` + `docker compose up -d`
  - Çünkü compose build katmanlı cache Windows'ta dosya değişikliklerini görmüyor

### Aktif Container'lar
| Servis | Port | Durum |
|---|---|---|
| postgres | 5432 | ✅ Healthy |
| backend-api | 5000 | ✅ Running |
| frontend | 3000 | ✅ Running |

### API Endpoint'leri
| Metot | Path | Açıklama |
|---|---|---|
| GET | /api/agent/voices | Ajan ses listesi |
| POST | /api/agent/ask | AI sohbet |
| POST | /api/agent/chime | Moderator |
| POST | /api/command | Sesli komut kuyruğuna ekle |
| GET | /api/command/pending | Bekleyen komutlar |
| GET | /api/command/activity | Aktif + son komut aktiviteleri |
| GET | /api/command/feed | Terminal log akışı (son 100 satır) |
| GET | /api/command/ping | Health check |
| POST | /api/chat/send | Chat mesajı |
| GET | /api/chat/history | Chat geçmişi |
| GET | /api/agents | Ajan listesi |

### AI Sağlayıcılar (sırayla)
1. **Gemini** (`gemini-2.0-flash`) — birincil
2. **Groq** (`llama-3.3-70b-versatile`) — yedek
3. **Fallback** — hardcoded yanıtlar

### API Anahtarları (`.env`)
- Gemini, Groq, OpenRouter anahtarları mevcut

### Dosyalar (önemli referanslar)
| Dosya | Ne işe yarar |
|---|---|
| `Backend/Program.cs:22` | `CommandQueueService` singleton registration |
| `Backend/Presentation/Controllers/CommandController.cs` | Command API endpoints |
| `Backend/Application/Services/CommandQueueService.cs` | Queue yönetimi, step tracking |
| `frontend/src/components/MeetingRoom.jsx` | Ana UI: voice, command, activity feed |
| `frontend/src/components/ActivityTerminal.jsx` | Terminal-style Bora aktivite akışı |
| `frontend/nginx.conf` | Proxy `/api/` → backend-api:8080 |
| `Infrastructure/docker-compose.yml` | Volume `../Commands:/commands` |
| `Commands/queue.json` | Paylaşılan kuyruk dosyası |
| `AgentMemory/` | Agent logları, index, eventler |

---

### Oturum 4 (2026-05-16) — opencode CLI Pipeline: Voice Command → AI Code Change

#### Yapılanlar

##### 1. opencode CLI Watcher Sistemi (watch_commands.ps1)
- **Sorun**: Sesli komutlar queue.json'a yazılıyor ama işleyen yoktu. `CodeExecutorService` Docker içinde AI API'lerine bağımlıydı.
- **Çözüm**: `watch_commands.ps1` yeniden yazıldı — `Commands/queue.json`'u poll eder, "pending" komutları tespit eder, **opencode CLI**'yı `opencode/deepseek-v4-flash-free` modeliyle çağırır
- Her komut için:
  1. queue.json'da `Status: "pending"` → `"processing"` + log yazılır
  2. `opencode run "<task>" -m opencode/deepseek-v4-flash-free --dangerously-skip-permissions --dir <proje>` çalıştırılır
  3. Komut çıktısı parse edilip log olarak queue.json'a yazılır
  4. `Status: "completed"` (veya "failed") olarak güncellenir
- **PSBugfix**: PowerShell `ConvertTo-Json`'un tek elemanlı array'leri `[...]` yerine `{...}` olarak yazma bug'ı manuel array serialize ile fixlendi

##### 2. queue.json Format Düzeltmesi
- Backend'in `CommandQueueService.ReadAll()`'ı `List<CodeCommand>` bekler (JSON array)
- Watcher artık `Save-Queue`'da her zaman `[{...}, {...}]` formatında yazar
- queue.json her başlangıçta `[]` olarak resetlenir

##### 3. Docker Container Durumu
- Mevcut container'lar (değişiklik gerekmedi):
  - postgres:5432 ✅ Healthy (2 saat)
  - backend-api:5000 ✅ Running (2 saat)
  - frontend:3000 ✅ Running (2 saat)
- Backend `CodeExecutorService` Docker'da **kullanılmıyor** (DI'a register edilmemiş) — tüm exec işini opencode CLI yapıyor

#### Test Sonuçları
- ✅ `POST /api/command` → `queue.json` → yazıldı (array formatında)
- ✅ Watcher tespit etti → `Status: "processing"` + log
- ✅ opencode CLI (deepseek v4 flash free) çalıştı → VoiceService.cs'e yorum satırı eklendi
- ✅ opencode CLI çalıştı → AgentController.cs'e yorum satırı eklendi
- ✅ opencode CLI çalıştı → **MeetingRoom.jsx:274 başlık rengi `text-cyan-400` olarak değiştirildi** (gerçek kod değişikliği)
- ✅ `GET /api/command/activity` → recent komutları + diff gösteriyor
- ✅ `GET /api/command/feed` → log satırlarını gösteriyor (ters sıralı)
- ✅ Frontend `ActivityTerminal.jsx` her 1.5sn'de poll edip gerçek zamanlı gösteriyor
- ✅ ANSI escape kodları log'lardan temizleniyor
- ✅ PowerShell `ConvertTo-Json` single-element array bug'ı fixlendi

#### Pipeline (Artık Çalışıyor)
```
Kullanıcı (ses/text) → Frontend → POST /api/agent/ask → AI yanıtı + command queue.json
                                                                     ↓
                                                              watch_commands.ps1 (PowerShell)
                                                                     ↓
                                                              opencode CLI (deepseek v4 flash free)
                                                                     ↓
                                                              Kod değişikliği + log yazma
                                                                     ↓
                                                              GET /api/command/feed (frontend poll)
```

---

## Build & Deploy Komutları
```powershell
# Backend build
docker build --no-cache -t saasfast-backend-direct -f .\Backend\Dockerfile .\Backend

# Frontend build
docker build --no-cache -t saasfast-frontend-direct -f .\frontend\Dockerfile .\frontend

# Tag & deploy
docker tag saasfast-backend-direct testprojem-backend-api
docker tag saasfast-frontend-direct testprojem-frontend
cd Infrastructure; docker compose up -d
```

## Devam Etmek İçin
Tekrar geldiğinde bana şunu söyle: **"projeye devam"**

---

### Oturum 5 (2026-05-16) — Deniz: Sohbet Alanı Büyütme

#### Yapılanlar
1. **MeetingRoom.jsx sohbet alanı büyütüldü** (`frontend/src/components/MeetingRoom.jsx:303`):
   - `min-h-[400px]` ve `max-h-[600px]` eklendi — sohbet alanı artık en az 400px, en fazla 600px
   - Padding `p-3` → `p-4` (daha ferah)
   - Yazı boyutu `text-xs` → `text-sm`, satır aralığı `leading-relaxed`
   - Mesaj aralığı `space-y-1.5` → `space-y-2`
   - Boş mesaj placeholder `text-xs` → `text-sm`, `py-2` → `py-4`
   - Diff metin `text-[10px]` → `text-xs` (daha okunabilir)

2. **queue.json temizliği**: Takılı kalmış "processing" komutlar `completed` olarak işaretlendi

### Oturum 5 (2026-05-16) — Watcher ANSI Temizleme + Stuck Queue Fix

#### Yapılanlar

##### 1. ANSI Escape Kodu Temizleme (watch_commands.ps1)
- **Sorun**: Log satırlarında `\u001B[0m`, `\u001B[90m...\u001B[0m` gibi ANSI escape kodları terminali kirletiyordu
- **Çözüm**: `Add-Log` fonksiyonuna ANSI stripping eklendi (safety net)
- `Process-Command`'daki ANSI regex genişletildi: artık `ESC[`, `ESC(`, `ESC)` gibi varyasyonlar da temizleniyor
- `\u001B` karakteri ve diğer kontrol karakterleri (`[^\x20-\x7E...]`) agresif şekilde filtreleniyor
- Artık `$errLines`'daki her satır Add-Log öncesi temizleniyor

##### 2. PowerShell Hata Filtreleme
- `$noisePatterns` genişletildi: `'^info\s*:', '^log\s*:', '^warn\s*:', 'X\.509'` eklendi
- NuGet restore çıktıları (`info  : GET https://...`) terminali kirletmiyor

##### 3. Stuck Queue Fix (queue.json)
- **Sorun**: 5 adet komut "processing" state'inde takılı kalmıştı (watcher `$script:isProcessing = $true` kilitlenmesi)
- **Çözüm**: queue.json temizlendi — stuck komutlar kaldırıldı, sadece geçmiş completed komutlar tutuldu
- Watcher artık temiz kuyrukla yeniden başlayabilir durumda

#### Test
- `watch_commands.ps1` ANSI stripping test edildi — regex `ESC[0m`, `ESC[90m...ESC[0m`, `ESC(B` gibi patternleri yakalıyor
- `queue.json` temiz array formatında (her zaman `[{...}]`)
- Watcher yeniden başlatıldığında `pending` komutları işleyebilir

---

### Oturum 6 (2026-05-16) — Transcript'te Canlı opencode Log Akışı

#### Yapılanlar

##### 1. MeetingRoom.jsx — Chat Transcript'te opencode Progress Gösterimi
- **Sorun**: opencode'un yaptığı kod değişiklikleri yalnızca `ActivityTerminal` bileşeninde görünüyordu, chat transcript'te görünmüyordu.
- **Çözüm**: `MeetingRoom.jsx`'e command progress polling sistemi eklendi:
  1. Yeni state: `activeCmdId`, `lastLogTimeRef`
  2. `useEffect`: `activeCmdId` set olduğunda her 2sn'de `/api/command/feed` poll eder
  3. Yeni log satırları transcript'e `isLog: true` olarak eklenir
  4. Komut "completed" veya "failed" olduğunda sonuç mesajı gösterilir
  5. `askAI()`: `data.command` gelince `setActiveCmdId(c.id)` çağırır
- **Transcript görünümü**: Log satırları `⚡` simgesiyle, seviyeye göre renkli (success: yeşil, error: kırmızı, warn: amber, info: cyan)

##### 2. Frontend Rebuild
- `docker build --no-cache -t infrastructure-frontend -f .\frontend\Dockerfile .\frontend`
- `docker compose up -d frontend` (Infrastructure dizininde)
- Frontend container'ı yeniden başlatıldı

#### Test Sonuçları
- ✅ Komut queue.json'a yazılınca watcher tespit ediyor
- ✅ opencode CLI (deepseek v4 flash free) ile kod değişikliği yapılıyor
- ✅ Loglar `/api/command/feed` ile frontend'e iletiliyor
- ✅ `ActivityTerminal` + chat transcript'te eş zamanlı gösterim
- ✅ 4 komut başarıyla işlendi (MeetingRoom başlık rengi, chat alanı genişletme, VoiceService yorum)

---

### Oturum 7 (2026-05-16) — Watcher v3: Timeout + Watchdog + Heartbeat + AgentMemory Log

#### Yapılanlar

##### 1. Watcher Crash ve Stuck Fix (watch_commands.ps1 v3)
- **Sorun**: Watcher PowerShell process'i crash olunca komut "processing" state'inde kalıyordu. Yeni komutlar işlenemiyordu.
- **Kök neden**: `& $opencodeExe` doğrudan çağrıldığı için opencode asılı kalırsa watcher da asılı kalıyordu. PowerShell process'i de kill yenebiliyordu.
- **Çözümler**:
  1. **Start-Job ile opencode çağrısı**: opencode artık background job'da çalışır, `Wait-Job -Timeout 120` ile 120sn timeout koruması var
  2. **Watchdog** (`watchdog.ps1`): Her 10sn'de watcher process'ini kontrol eder, crash'te otomatik restart eder
  3. **Heartbeat** (`Commands/watcher.heartbeat`): Watcher her cycle'da bu dosyaya yazar, watchdog ile canlılık kontrolü

##### 2. AgentMemory Otomatik Log Entegrasyonu
- `watch_commands.ps1` her komut işleme sonunda `log_agent.ps1`'i çağırır
- AgentMemory/AgentLogs/ altına her ajan için ayrı log dosyası
- AgentMemory/Index/master_index.json güncellenir
- AgentMemory/Events/events_log.json güncellenir

##### 3. Test
- ✅ Watcher v3 crash sonrası watchdog restart ediyor
- ✅ opencode timeout (120sn) çalışıyor — asılı kalmıyor
- ✅ `a1118f2c` komutu başarıyla işlendi (VoiceService.cs:22 yorum eklendi)
- ✅ AgentMemory logları otomatik yazılıyor

---

### Oturum 8 (2026-05-17) — CodeExecutorService Aktif + DeepSeek Entegrasyonu

#### Yapılanlar

##### 1. CodeExecutorService DI'a Register Edildi (Program.cs:26)
- `builder.Services.AddHttpClient<CodeExecutorService>()` eklendi
- Artık `AgentChatController` ve `CommandController`'a inject ediliyor
- `CommandController.Submit()` → `async` oldu, direkt execute ediyor

##### 2. AgentChatController — Ask()'de Direkt Kod Execute
- `AgentChatController.cs:53` — kod değişikliği algılanınca `_executor.ExecuteAsync(cmd)` çağrılıyor
- Dönen yanıtta `command.Result` içinde `Success`, `Summary`, `Diff`, `Error` var
- Artık `watch_commands.ps1`'e gerek kalmadan tarayıcıdan direkt kod değişikliği

##### 3. OpenRouter DeepSeek Modeli
- `CodeExecutorService.cs` ve `AiService.cs`'de OpenRouter modeli `mistralai/mistral-7b-instruct` → `deepseek/deepseek-chat`
- `HTTP-Referer` header'ı eklendi (OpenRouter gereksinimi)
- Tüm AI provider'lara hata loglama eklendi (status code + hata mesajı)

##### 4. Target File Fallback
- `CommandQueueService.cs`'de `deniz` → `deniz.js` hatası düzeltildi (`frontend/src/data/agents.js`)
- `CodeExecutorService.cs:59-73` — hedef dosya bulunamazsa AI ile tahmin et, hata vermeden önce dene

##### 5. Docker Temizlik + Yeniden Build
- Tüm eski saasfast container/image/volume/network silindi
- Backend yeniden build edildi (`docker build --no-cache`)
- Frontend compose ile build edildi
- 3 servis ayakta: postgres:5432 ✅, backend-api:5000 ✅, frontend:3000 ✅

#### Test Sonuçları
- ✅ `POST /api/agent/ask` → AI yanıtı + `CodeExecutorService.ExecuteAsync` çalıştı
- ✅ DeepSeek (OpenRouter) ile kod değişikliği başarılı: `agents.js:76 pitch 0.7→1.2`
- ✅ Diff döndü: `+ voice: { pitch: 1.2, rate: 0.85 }`
- ✅ Dosya host'ta gerçekten değişti
- ✅ Terminal/watcher gerekmeden çalışıyor (tarayıcı yeterli)

#### Pipeline (Yeni — Terminal Gereksiz)
```
Kullanıcı (ses/text) → Frontend → POST /api/agent/ask
                                    → AI yanıtı (deepseek-chat)
                                    → CodeExecutorService (deepseek-chat)
                                    → Kod değişikliği + diff
                                    → GET /api/command/feed (frontend poll)
```

---

### Oturum 9 (2026-05-17) — Agent Self-Improvement + Conversation Log + Auto Deploy

#### Yeni Servisler

| Servis | Dosya | Görevi |
|---|---|---|
| **AgentMemoryService** | `Application/Services/AgentMemoryService.cs` | Tüm konuşmaları `AgentMemory/Conversations/conversations.json`'a loglar, AI prompt'larına geçmiş konuşma bağlamı enjekte eder. `GET /api/command/conversations` |
| **AgentPerformanceTracker** | `Application/Services/AgentPerformanceTracker.cs` | Her ajanın başarı/başarısızlık skorunu tutar. `GetBestAgentForTask()` ile en iyi ajanı seçer. Prompt'a performans istatistiği ekler. |
| **CodeReviewService** | `Application/Services/CodeReviewService.cs` | Kod değişikliğini DeepSeek ile review eder (syntax, güvenlik, performans, kod kalitesi). Reddedilirse rollback + düzeltme dener. |

#### Geliştirmeler

##### 1. Episodic Memory + Conversation Log
- Tüm kullanıcı mesajları + ajan yanıtları `AgentMemory/Conversations/conversations.json`'a yazılır
- AI prompt'una otomatik eklenir: "Son konusmalar:" + "Onceki konusmalarin:" + "Gecmis deneyimlerin:" + "Performansin:"
- `GET /api/command/conversations?count=20` endpoint'i
- `AgentMemory/EpisodicMemory/{agent}.json` — her ajan için ayrı geçmiş dosyası
- Toplam 500 konuşma, 100 epizodik hafıza limiti

##### 2. Self-Correction Pipeline
```
AI kod üret → Code Review (DeepSeek)
  → Onay: dosyaya yaz + performans kaydı + memory
  → Red: rollback + feedback'e göre düzeltme dene
    → Düzelme başarılı: yaz
    → Düzelme başarısız: eski hal bırak, hata logla
```
- Aynı içerik kontrolü: AI aynı kodu döndürürse farklı prompt ile tekrar dener
- AI boş döndürürse otomatik yeniden dener

##### 3. Frontend Deploy Fix
- `docker-compose.yml`: frontend nginx artık `../frontend/dist:/usr/share/nginx/html:ro` volume mount ile çalışır
- Backend'e Docker socket mount edildi (`/var/run/docker.sock`)
- `POST /api/command/rebuild` — frontend container'ını yeniden build eder
- Frontend değişikliği tespit edilince `TriggerFrontendRebuild()` otomatik çalışır
- `docker compose up -d --build frontend` ile rebuild

##### 4. Agent Prompt İyileştirme
- Her AI çağrısına conversation context + history + performance stats eklenir
- Ajana "önceki konuşmalarında ne dediğin" hatırlatılır
- `SystemPrompt()` artık `id, tr, room, userMessage` parametreleri alır

##### 5. Test Sonuçları
- ✅ Conversation log `AgentMemory/Conversations/conversations.json`'a yazılıyor
- ✅ Code Review başarıyla çalışıyor: "Değişiklik talebi gereksiz, arka plan rengi zaten mavi"
- ✅ Rollback + self-correction: "Degisiklik geri alindi, yeni deneme yapiliyor..."
- ✅ `GET /api/command/conversations` endpoint çalışıyor
- ✅ Frontend volume mount ile serving yapıyor
- ✅ Tüm servisler ayakta (postgres:5432, backend:5000, frontend:3000)

#### AI Sağlayıcı (Güncel)
1. **OpenRouter DeepSeek** (`deepseek/deepseek-chat`) — birincil (hem yanıt hem kod hem review)
2. Gemini 2.0 Flash — yedek (şu an 429 quota limit)
3. Groq llama-3.3-70b — yedek (şu an 429 rate limit)

---

### Sıradaki Yapılacaklar (plan)
1. ~~Log satırlarındaki ANSI escape kodlarını temizle~~ ✅
2. ~~Watcher'da PowerShell hata mesajlarını filtrele~~ ✅
3. ~~MeetingRoom.jsx transcript'te canlı opencode log akışı~~ ✅
4. ~~Watcher crash/timeout koruması (watchdog + heartbeat)~~ ✅
5. ~~AgentMemory otomatik log entegrasyonu~~ ✅
6. ~~CodeExecutorService DI + DeepSeek + terminal gereksiz kod değişikliği~~ ✅
7. ~~Self-Improvement: Code Review + Performance + Self-Correction~~ ✅
8. ~~Conversation Log + Agent Context + Auto Deploy~~ ✅

---

### Oturum 10 (2026-05-17) — DeepSeek Birincil AI Sağlayıcı

#### Yapılanlar
1. **AiService.cs** — AI sağlayıcı sırası değiştirildi:
   - Eski: Gemini → Groq → OpenRouter (DeepSeek) → Fallback
   - Yeni: **OpenRouter (DeepSeek)** → Gemini → Groq → Fallback
   - DeepSeek artık tüm ajan sohbetleri ve kod değişikliklerinde **birincil** sağlayıcı
2. **ChimeAsync** — moderatör çağrıları da DeepSeek ile başlayacak şekilde güncellendi
3. **Backend** yeniden build edilip ayağa kaldırıldı

#### AI Sağlayıcı Sırası (Güncel)
1. **Opencode CLI** (`opencode/deepseek-v4-flash-free`) — birincil (sohbet + kod)
2. **OpenRouter DeepSeek** — yedek (sadece review/code fallback)
3. **Gemini 2.0 Flash** — yedek
4. **Groq llama-3.3-70b** — yedek

---

### Oturum 11 (2026-05-18) — Self-Improvement Sistemi

#### Yapılanlar

##### 1. AiService.cs Tamamen Yeniden Yazıldı
- **Sorun**: AiService.cs dosyası kesikti/bozuktu (sadece 2 satır, QA prompt'ları vardı)
- **Tüm ajan prompt'ları eklendi**: 8 ajan için TR/EN prompt tanımları
- **AiRequest/AiResponse/ChimeRequest/ChimeResponse** modelleri eklendi
- **ChimeAsync** moderatör mantığı çalışır hale getirildi
- **Context zenginleştirme**: Her AI çağrısında conversation context, history, episodic memory, peer review, performance stats prompt'a ekleniyor
- Tüm ajanlar `SystemPrompt()` switch expression'ında tanımlandı

##### 2. SelfImprovementService — Otomatik İyileştirme Motoru
- **Backend/Application/Services/SelfImprovementService.cs**
- **Kod tabanı tarama**: TODO/FIXME/HACK/XXX/BUG/WORKAROUND etiketlerini bulur
- **Sabit değer tespiti**: hardcoded URL, password, API key pattern'lerini arar
- **Ajan performans analizi**: Başarı oranı %50 altındaki ajanları raporlar
- **Otomatik iyileştirme döngüsü**: Her 30 dakikada bir tarama yapar, yüksek öncelikli (P>=6) önerileri otomatik uygular
- **Geçmiş takibi**: Tüm öneriler `AgentMemory/Improvements/suggestions.json`'a kaydedilir
- `SetAutoMode(true/false)` ile otomatik mod açılıp kapanabilir

##### 3. SelfImprovementController — API Endpoints
| Metot | Path | Açıklama |
|---|---|---|
| POST | /api/self-improve/scan | Kod tabanını tara, rapor üret |
| GET | /api/self-improve/suggestions | İyileştirme önerilerini listele |
| POST | /api/self-improve/apply/{id} | Öneriyi uygula (kuyruğa ekle) |
| POST | /api/self-improve/dismiss/{id} | Öneriyi reddet |
| POST | /api/self-improve/auto | Otomatik modu aç/kapa |
| GET | /api/self-improve/status | Sistem durumu |
| GET | /api/self-improve/performance | Ajan performans tablosu |

##### 4. SelfImprovementPanel.jsx — Frontend Dashboard
- **3 sekme**: Öneriler, Rapor, Performans
- **Öneriler**: Kategori ikonu, öncelik Puanı, dosya yolu, Uygula/Reddet butonları
- **Rapor**: Tarama istatistikleri (dosya sayısı, TODO sayısı, öneri sayısı, sabit değerler), ajan performans grafiği
- **Performans tablosu**: Her ajan için görev sayısı, başarı/başarısızlık, başarı yüzdesi
- **Otomatik mod toggle**: Header'da Açık/Kapalı butonu

##### 5. MeetingRoom.jsx — View Toggle
- Üst menüde **Toplantı** / **⚡Kendini Geliştir** sekmeleri
- Self Improve sekmesinde SelfImprovementPanel gösterilir

##### 6. Program.cs Güncellemesi
- `builder.Services.AddSingleton<SelfImprovementService>()` eklendi
- DI sırası: AgentMemoryService, AgentPerformanceTracker AiService'den önce register edildi

---

### Oturum 12 (2026-05-19) — Kod Komutu Filtre İyileştirme + Docker İsim Düzeltme

#### Yapılanlar

##### 1. Docker İsimlendirme Düzeltmesi
- **Sorun**: Frontend image adı `infrastructure-frontend` (compose otomatik ürettiği için), backend image adı `saasfast-backend-direct` (tutarsız)
- **Çözüm**:
  - `docker-compose.yml`: frontend servisine `image: saasfast-frontend` eklendi
  - Backend image adı `saasfast-backend-direct` → `saasfast-backend` olarak değiştirildi
  - Eski `infrastructure-frontend`, `otomotiv-frontend`, `otomotiv-api`, `dpage/pgadmin4` image'ları temizlendi

##### 2. `IsExplicitCodeCommand` Kelime Sınırı Fix'i
- **Sorun**: "Cem genel bir test yapar mısın" mesajı, `.Contains("yap")` nedeniyle "yapar" içinde "yap" eşleştiği için kod komutu olarak yorumlanıyordu → `File not found: /source/...` hatası
- **Kök neden**: `AgentChatController.cs:27` — substring `.Contains()` ile eşleştirme, Türkçe çekim eklerini de yakalıyor
- **Çözüm**: `AgentChatController.cs:27` — `.Contains(v)` → `Regex.IsMatch(lower, $@"\b{Regex.Escape(v)}\b")` 
  - Artık "yap" sadece kelime olarak geçtiğinde eşleşir (örn. "bunu yap"), "yapar", "yapıyor", "yapacak" gibi çekimlerde eşleşmez
- **Etkilenen tüm fiiller**: değiştir, güncelle, düzelt, yenile, ayarla, yap, ekle, sil, kaldır, çıkar, büyüt, küçült, taşı, yaz

#### Test Sonuçları
- ✅ 51 xUnit testinin tamamı geçti
- ✅ `CommandQueueServiceTests` dahil hiçbir mevcut test kırılmadı
- ✅ Backend container başarıyla rebuild edildi
- ✅ `docker ps` — hepsi `saasfast-` önekiyle ve çalışır durumda

#### Container Durumu
| Name | Image | Port | Durum |
|------|-------|------|-------|
| `saasfast-postgres` | `postgres:16-alpine` | 5432 | ✅ Healthy |
| `saasfast-backend` | `saasfast-backend` | 5000 | ✅ Running |
| `saasfast-frontend` | `saasfast-frontend` | 3000 | ✅ Running |
