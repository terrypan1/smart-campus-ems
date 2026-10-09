# CLAUDE.md

Claude Code 進入這個 repo 時的專案脈絡與規則。人類讀者請先看 [README.md](README.md)。

## 專案是什麼

校園能源管理系統（EMS）教學 Demo：模擬智慧電表 → Modbus TCP 採集 → PostgreSQL → ASP.NET Core API → Vue 3 網頁。名稱與資料皆為虛構／模擬，**不是正式產品**。

## 三個子專案

| 目錄 | 內容 | 解決方案／設定 |
|---|---|---|
| `ems_simulator/` | Modbus TCP Server，一個 port 用 Unit ID 模擬 51 顆電表；`backfill` 子指令寫歷史讀值 | `EmsSimulator.slnx` |
| `ems_backend/` | `EmsApi`：Modbus 採集、彙總、需量、電費、碳排、告警、設定、JWT、REST API；`EmsApi.Tests`：xUnit | `EmsBackend.slnx` |
| `ems_web/` | Vue 3＋Vite＋TypeScript＋Quasar＋ECharts，pnpm | `package.json` |

共用資料：`data/meters.json`（電表主檔，backend 與 simulator 都讀這一份）。

## 指令

```powershell
# build / test
cd ems_backend;   dotnet build EmsBackend.slnx;   dotnet test EmsBackend.slnx
cd ems_simulator; dotnet build EmsSimulator.slnx; dotnet test EmsSimulator.slnx
cd ems_web;       pnpm install; pnpm build        # build 內含 vue-tsc 型別檢查

# 本機執行順序：simulator → backend → web
cd ems_simulator\EmsSimulator; dotnet run --launch-profile dev    # Modbus 1503
cd ems_backend\EmsApi;          dotnet run --launch-profile http   # http://localhost:5321
cd ems_web;                     pnpm dev                           # http://localhost:3021
```

部署（WSL＋systemd＋nginx）見 `docs/DEPLOYMENT.md`。

## 資料流（改程式前要知道）

1. `ModbusCollectorService` 每 60 秒讀每顆電表 26 個 Holding Register → `meter_readings`
2. `EnergyAggregationWorker`／`AggregationService`：每小時用電（整點 ±20 分鐘取最接近的累積電度，找不到就「不可計算」、不補值）、日／月／年彙總、15 分鐘需量
3. `DemandEventWorker`、`AlarmWorker`：需量事件與告警
4. 電費（`BillCalculator`）、碳排（`CarbonService`）在查詢時計算

## 不可改動的原則

- **時間一律以 UTC 儲存**；顯示與時段判斷才轉台北時間。
- **Register 對照表兩邊必須一致**：`ems_simulator/EmsSimulator/RegisterMap.cs` ↔ `ems_backend/EmsApi/Services/Collector/MeterRegisterDecoder.cs`。改一邊就要改另一邊並跑測試。
- **缺資料不補值**：不可計算就標示出來；斷線期間總量記「未分時段」。
- 資料庫只用 PostgreSQL；schema 變更一律透過 EF Core migration（`dotnet ef migrations add`／`remove`，不要手刪 migration 檔）。
- 計算規則依 `docs/依據文件索引.md`；畫面用語依 `docs/名詞對照.md`。
- 不要為了任務順手重構無關程式；不要改架構，除非任務明確要求。

## 安全

- **不把任何 secret 寫進 repo**：連線字串、JWT 金鑰、密碼一律用環境變數（部署機 `/etc/ems/ems.env`）。`appsettings.Development.json` 只能放開發用假值。
- 不加入真實校園名稱、真實電表編號、內網 IP、客戶文件或個資。範例資料一律用合成資料。
- `docs/實作/`、`docs/回報/` 是本機私人工作紀錄（已在 .gitignore），**不可加入版控**（不要 `git add -f`）。
- 公開說明見 `SECURITY.md`。

## 工作方式

- 修改前先讀相關檔案（尤其是上面列出的對應檔）。
- 修改後依範圍跑 build／test（backend → `dotnet test`；simulator → `dotnet test`；web → `pnpm build`），並在回報中列出結果。
- 只動這個 repo；不改其他專案、不改 WSL 上其他服務。
- **不要自動 commit 或 push**，除非使用者明確要求。
- 回報時列出：修改的檔案、驗證方式與結果、未完成或未驗證的項目。
