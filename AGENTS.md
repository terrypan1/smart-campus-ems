# AGENTS.md

Codex 與其他 coding agent 的執行規則。專案脈絡見 [README.md](README.md) 與 [CLAUDE.md](CLAUDE.md)。

## 規則

1. 開始前先讀 `README.md`、`CLAUDE.md`、`AGENTS.md`。
2. 不改架構，除非任務明確要求。
3. 技術邊界：前端 Vue 3＋Quasar（`ems_web/`）；後端 .NET 10（`ems_backend/`、`ems_simulator/`）；資料庫 PostgreSQL（schema 只能透過 EF Core migration 變更）。
4. 所有 secrets 使用環境變數；不得寫入任何進版控的檔案。
5. 修改 backend 要跑：`cd ems_backend; dotnet build EmsBackend.slnx; dotnet test EmsBackend.slnx`
6. 修改 frontend 要跑：`cd ems_web; pnpm build`
7. 修改 simulator 要跑：`cd ems_simulator; dotnet build EmsSimulator.slnx; dotnet test EmsSimulator.slnx`
8. 改到 Modbus Register 編碼／解碼時，simulator 與 backend 都要改、兩邊測試都要跑。
9. 時間一律以 UTC 儲存。
10. 不碰 repo 以外的檔案；不改其他專案或主機上的其他服務。`docs/實作/`、`docs/回報/` 是私人紀錄，不可加入版控。
11. 不自動 commit／push。
12. 回報列出：變更檔案、驗證方式與結果、未完成事項。

## 快速對照

| 要改的東西 | 位置 |
|---|---|
| API | `ems_backend/EmsApi/Controllers/`、`Services/` |
| Modbus 採集 | `ems_backend/EmsApi/Services/Collector/` |
| 計算（用電、需量、電費、碳排） | `ems_backend/EmsApi/Services/Energy/` |
| 資料表 | `ems_backend/EmsApi/Models/`＋`Migrations/` |
| 模擬電表 | `ems_simulator/EmsSimulator/` |
| 頁面 | `ems_web/src/pages/`；選單 `ems_web/src/router/menu.ts` |
| 主題色與狀態色 | `ems_web/src/theme/` |
| 部署 | `deploy/wsl/`、`docs/DEPLOYMENT.md` |
