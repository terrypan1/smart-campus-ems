# Deployment｜WSL Ubuntu 24.04 部署

把 EMS Demo 部署成「接近正式環境」的樣子：**程式在 Windows 編譯，WSL Ubuntu 只負責執行**；backend 與 simulator 由 systemd 常駐，前端由 nginx 提供，PostgreSQL 16 存資料。

```
Windows 開發機
   │  deploy\wsl\publish.ps1（dotnet publish linux-x64、pnpm build）
   ▼
deploy\wsl\out\  ──（WSL 透過 /mnt/c 讀取）──▶  deploy/wsl/install.sh
                                                  │
WSL Ubuntu 24.04                                  ▼
├─ systemd：ems-simulator   Modbus TCP 127.0.0.1:1502
├─ systemd：ems-backend     API＋採集 127.0.0.1:5320
├─ nginx site「ems」        :3020  /  → 前端靜態檔、/api/ → 5320
└─ PostgreSQL 16            角色／資料庫 ems
```

> 這個做法適合單機 Demo 與教學。真正上線還需要 HTTPS、備份排程、監控、防火牆等，不在本文件範圍。

## 1. 一覽

| 項目 | 值 |
|---|---|
| distro | `Ubuntu-24.04`，執行帳號 `demo`（可 `sudo`）；用別的帳號請改 `install.sh` 的 `APPS` 與 systemd `User=` |
| 網頁 | http://localhost:3020 |
| 程式目錄 | `/home/demo/apps/ems/{backend,simulator,web}` |
| 機密 | `/etc/ems/ems.env`（root 600，不進版控），格式見 [`.env.example`](../.env.example) |
| systemd | `ems-backend`、`ems-simulator` |
| nginx | `/etc/nginx/sites-available/ems`（來源 `deploy/wsl/ems.nginx.conf`） |

### Port

| | 部署（WSL） | 開發（Windows，見 README Quick Start） |
|---|---|---|
| 網頁 | 3020 | 3021（`pnpm dev`） |
| 後端 | 5320 | 5321（`dotnet run --launch-profile http`） |
| Modbus 模擬器 | 1502 | 1503（`dotnet run --launch-profile dev`） |

開發與部署用不同 port，兩邊可以同時跑。

## 2. 前置環境

- **Windows**：.NET 10 SDK、Node.js 22＋pnpm
- **WSL**：`wsl --install -d Ubuntu-24.04`，建立使用者 `demo`，然後：

```bash
sudo apt update
sudo apt install -y postgresql nginx openssl curl
```

## 3. 首次安裝

### 3.1 編譯（Windows PowerShell，在 repo 根目錄）

```powershell
deploy\wsl\publish.ps1
```

產出到 `deploy\wsl\out\`（不進版控）：

- `backend/`、`simulator/`：`dotnet publish -c Release -r linux-x64 --self-contained`，WSL 不用裝 .NET
- `web/`：`pnpm build`（含 vue-tsc 型別檢查，型別錯了就不部署）

結尾會印出下一步要執行的 WSL 指令（含 repo 的 `/mnt/...` 路徑）。

### 3.2 安裝（WSL）

```powershell
wsl -d Ubuntu-24.04 -- bash /mnt/c/<repo 路徑>/deploy/wsl/install.sh
```

`install.sh` 依序做：

1. **機密**：`/etc/ems/ems.env` 不存在時才建立——DB 密碼與 JWT 金鑰用 `openssl rand` 產生；之後重跑沿用，不會覆蓋
2. **PostgreSQL**：建立角色與資料庫 `ems`（已存在就只同步密碼）
3. **程式**：停服務 → 換新版（保留後端 `Logs/` 與模擬器 `state/`，否則電度會歸零）
4. **systemd**：寫入 `ems-simulator.service`、`ems-backend.service`（後端以 `EnvironmentFile=/etc/ems/ems.env` 讀機密）
5. **nginx**：安裝 site `ems`，`nginx -t` 通過後 **reload**（不 restart、不動其他 site）
6. 啟動服務，最後打一次 `/api/Health`

後端啟動時會自動套用 EF Core migration，並在資料表是空的時候寫入主檔（`data/meters.json`、契約容量、電價、排碳係數、離峰日）與 Demo 帳號：`sysadmin / EmsSysadmin2026!`、`admin / EmsAdmin2026!`、`viewer / EmsViewer2026!`（公開的 Demo 值，來自 `/etc/ems/ems.env` 的 `Seed__*Password`；只在帳號不存在時建立，登入後請改密碼）。

### 3.3 產生 Demo 歷史資料（第一次）

```powershell
wsl -d Ubuntu-24.04 -- bash /mnt/c/<repo 路徑>/deploy/wsl/backfill.sh --force
```

前提：`ems-backend` 已採集到至少一輪即時讀值（歷史會接在第一筆即時讀值之前）。腳本會產生 13 個月模擬讀值、清空彙總表並重啟後端，後端啟動時全部重算：

- `sudo journalctl -u ems-backend -f` 看到「彙總重算完成」
- 再約 90 秒看到「已補記歷史告警」

### 3.4 讓 WSL 常駐（選用）

WSL 沒有任何 session 時會把 distro 關掉，服務也跟著停。可以用 Windows 排程工作在登入時掛一個 headless session：

```powershell
$a = New-ScheduledTaskAction -Execute 'C:\WINDOWS\System32\conhost.exe' -Argument '--headless wsl.exe -d Ubuntu-24.04 --exec sleep infinity'
$t = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$s = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'EMS_WSL_KeepAlive' -Action $a -Trigger $t -Settings $s
Start-ScheduledTask -TaskName 'EMS_WSL_KeepAlive'
```

移除：`Unregister-ScheduledTask -TaskName EMS_WSL_KeepAlive -Confirm:$false`

## 4. 更新版本

1. （建議）先備份資料庫，見 §7
2. 重做 §3.1、§3.2——`install.sh` 可重複執行，機密與模擬器狀態都會保留
3. 新版有 migration 時，後端啟動會自動套用

下列變更之後要**全部重算彙總**（系統設定 → 契約與係數 →「全部重算彙總」，或 `POST /api/Energy/Recompute`）：時段規則、時段方案、離峰日、彙總或需量的計算方式。改電價、契約、排碳係數不用重算（查詢時才計算）。

## 5. Rollback

`install.sh` 不保留舊版程式，rollback 的做法是**用舊版原始碼重新部署**：

```powershell
git checkout <上一個 tag 或 commit>
deploy\wsl\publish.ps1
wsl -d Ubuntu-24.04 -- bash /mnt/c/<repo 路徑>/deploy/wsl/install.sh
git checkout -   # 回到原本的分支
```

注意：EF Core migration 只會往前套用。若新版加過 migration，舊版程式可能讀不懂新的資料表結構——這時要用更新前的備份還原資料庫（§7）。

## 6. 狀態與 log

```bash
# 服務狀態
systemctl is-active ems-simulator ems-backend nginx
systemctl status ems-backend --no-pager

# log
sudo journalctl -u ems-backend -n 50 --no-pager
sudo journalctl -u ems-backend -f            # 持續追蹤
sudo journalctl -u ems-simulator -n 50 --no-pager
ls /home/demo/apps/ems/backend/Logs/         # Serilog 檔案 log

# API
curl -s http://127.0.0.1:3020/api/Health
```

前端全頁回歸檢查（Windows，headless）：

```powershell
cd tools\playwright; npm install; node check-all.mjs <截圖輸出資料夾>
```

帳密預設用 §3.2 的 Demo 帳號；改過密碼時用環境變數 `EMS_SYSADMIN_PASSWORD`、`EMS_VIEWER_PASSWORD` 指定。

## 7. 備份與還原

```bash
# 備份
sudo -u postgres pg_dump -Fc ems > ~/ems_$(date +%Y%m%d).dump

# 還原（覆蓋）
sudo systemctl stop ems-backend
sudo -u postgres dropdb ems && sudo -u postgres createdb -O ems ems
sudo -u postgres pg_restore -d ems ~/ems_YYYYMMDD.dump
sudo systemctl start ems-backend
```

`db/ems_demo.sql` 是「結構＋主檔」的乾淨起點（不含帳號、讀值），可用來檢視資料表或重建空白 Demo。

## 8. 完全移除（只影響 EMS）

```bash
sudo systemctl disable --now ems-backend ems-simulator
sudo rm /etc/systemd/system/ems-backend.service /etc/systemd/system/ems-simulator.service && sudo systemctl daemon-reload
sudo rm /etc/nginx/sites-enabled/ems /etc/nginx/sites-available/ems && sudo nginx -t && sudo systemctl reload nginx
sudo -u postgres dropdb ems && sudo -u postgres dropuser ems
rm -rf /home/demo/apps/ems && sudo rm -rf /etc/ems
```

## 9. 常見故障排查

| 症狀 | 原因 | 處理 |
|---|---|---|
| 瀏覽器打不開 localhost:3020、採集中斷 | 沒有 WSL session，distro 閒置被關掉 | §3.4 常駐；或開一個 WSL 終端機 |
| 開發後端連不到 WSL 裡的 127.0.0.1:5432 | 同上：distro 沒在跑，localhost 轉發不存在 | 同上 |
| 全新資料庫第一次啟動，log 有一行 `Failed executing DbCommand … __EFMigrationsHistory` | EF 先查 migration 紀錄表，表還不存在 | 正常，接著會自動建立並套用 |
| 服務起不來，log 說 port 被佔用 | 同機其他程式佔用 3020／5320／1502 | 找出佔用者（`ss -ltnp`），或同步修改 `install.sh`、`ems.nginx.conf` 的 port |
| 後端啟動失敗：`ConnectionStrings:DefaultConnection 尚未設定`／`Jwt:SignKey 尚未設定` | `/etc/ems/ems.env` 缺值 | 對照 `.env.example` 補齊，`sudo systemctl restart ems-backend` |
| 部署後登入成功卻停在登入頁 | 舊分頁拿著舊入口檔，要的分頁檔已被新版換掉 | nginx `/assets/` 找不到回 404（已設定）；前端偵測到會自動整頁重新整理 |
| migration 報錯或快照不一致 | 直接刪 migration 檔 | 一律用 `dotnet ef migrations remove` |
| Windows 主控台中文亂碼（publish.ps1） | PowerShell 5.1 預設編碼 | `publish.ps1` 要存成 UTF-8 with BOM；亂碼只影響顯示 |
| 從 Git Bash 呼叫 wsl 時路徑被改掉 | MSYS 路徑轉換 | 指令前加 `MSYS_NO_PATHCONV=1` |
| 重新部署後電度歸零 | 模擬器 `state/` 被覆蓋 | `install.sh` 已保留 `state/`；不要手動刪 |
| 今天的需量／用電顯示「—」或偏低 | 期間起點附近沒有讀值（系統當時沒在跑） | 屬實際斷線，系統照規則處理（不補值）；保持常駐 |
| 電費估算顯示「二段式不提供試算」 | 時段方案切成二段式 | 電價設定切回三段式並重算 |
