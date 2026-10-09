# AI Workflow｜用 Claude Code／Codex 開發這個專案

這個 Demo 大部分程式是在人類主導下，搭配 AI coding agent 完成的。這份文件整理實際有效的做法，讓你可以用同樣的方式繼續開發，或套用到自己的專案。

## 1. 核心觀念：先給規則，再給任務

AI agent 每次進來都是「新同事」。與其每次在 prompt 裡重講一遍，不如把規則寫進 repo：

| 檔案 | 給誰 | 內容 |
|---|---|---|
| `CLAUDE.md` | Claude Code（自動載入） | 專案是什麼、資料流、指令、不可改動的原則 |
| `AGENTS.md` | Codex 等 agent | 精簡的執行規則與檔案位置對照 |
| `README.md` | 人與 agent | 專案總覽、Quick Start |

規則要寫「**為什麼**」：例如「Register 對照表兩邊必須一致」比「不要改 RegisterMap」有用，agent 才能在沒寫到的情況下做對判斷。

## 2. 依工作型態分工

兩種工具都能寫程式，差別在**任務的形狀**，不是誰比較強。

**適合交給 Claude Code 的工作**（範圍大、需要先理解再動手）：

- 大範圍 repo 盤點、架構理解
- 跨多個檔案／多個子專案的修改（例如新增一個功能：migration＋API＋頁面）
- README、部署文件、規格文件整理
- Feature 實作、重構

**適合交給 Codex 的工作**（範圍明確、可以直接驗證）：

- 單一明確任務、明確的 TODO
- Debug 一個已知的錯誤
- 小範圍 UI 修改
- 補測試
- 跑 build／lint／test 並修到通過

同一個功能也可以接力：Claude Code 先寫規格與主要實作，Codex 補測試與修小問題。

## 3. 本專案的開發節奏

每個階段（例如「需量管理與電費估算」）都照同一個循環：

```text
需求（人）
↓
任務書：要做什麼、不做什麼、計算依據、完成條件
↓
Claude Code／Codex 實作
↓
Build／Test
↓
Review（人看 diff 與畫面）
↓
回報文件：改了哪些檔案、怎麼驗證、哪些沒驗證
↓
Git Commit（人決定）
```

幾條固定的規則：

- **AI 不直接決定需求**：條文或需求沒寫清楚的地方，AI 先列出「解讀」與選項，由人拍板。
- **修改前先讀任務書**與 `CLAUDE.md`／`AGENTS.md`。
- **修改後一定要驗證**（build／test，必要時實際操作畫面），沒驗證到的要明講。
- **AI 不可自行提交敏感資訊**：帳密、金鑰、客戶資料不進 repo，也不貼進 prompt。
- **由人決定是否 commit／push**。

> 任務書與回報是個人工作紀錄，沒有放進公開 repo；這裡只分享方法。

這樣做的好處：

- 決策留在文件裡，下一個 agent（或三個月後的自己）不用重猜。
- 「驗收條件」讓 agent 能自己判斷什麼時候算完成，不會做一半就停、也不會越做越多。
- 回報裡的「未驗證項」逼出誠實的進度，避免「應該可以」。

## 4. 好任務的寫法

一個好的任務描述包含四件事：**範圍、限制、驗證、回報格式**。

```text
目標：……
範圍：只改 ……；不要改 ……
完成條件：……（可驗證）
驗證：跑 ……
回報：修改檔案、驗證結果、未完成事項
```

## 5. 任務範例

### 範例 1：小範圍 UI 修改（適合 Codex）

```text
請先閱讀 README.md、CLAUDE.md、AGENTS.md。
只修改 ems_web 的登入頁（src/pages/LoginPage.vue），不要改 API 與其他頁面。
需求：帳號欄位在頁面載入後自動取得焦點。
完成後執行 pnpm build，並回報修改檔案與驗證結果。
```

### 範例 2：唯讀盤點（適合 Claude Code）

```text
請盤點 ems_backend 的 Modbus 採集流程。
不要修改任何程式。
輸出：資料流（從輪詢到寫入 meter_readings）、主要 class 與職責、
連線失敗與逾時的錯誤處理方式、目前測試涵蓋到哪裡、測試缺口。
```

### 範例 3：補測試（適合 Codex）

```text
請先閱讀 AGENTS.md。
為 ems_backend/EmsApi/Services/Energy/DemandCalculator.cs 補單元測試，
涵蓋：區塊頭尾找不到讀值、剛好落在 ±5 分鐘邊界、跨午夜。
只新增測試檔，不改產品程式；若發現產品程式有 bug，回報即可、不要修。
完成後執行 dotnet test EmsBackend.slnx 並回報結果。
```

### 範例 4：跨專案功能（適合 Claude Code）

```text
請先閱讀 README.md、CLAUDE.md。
新增功能：電表新增一個量測值「三相電流不平衡率」。
需要同時修改 simulator 的 RegisterMap 與 backend 的 MeterRegisterDecoder（兩邊必須一致），
資料表透過 EF Core migration 新增欄位，電表清單頁顯示這個欄位。
先列出你打算修改的檔案與 Register 位址配置，等我確認後再實作。
完成後 backend、simulator 跑 dotnet test，web 跑 pnpm build，回報結果。
```

### 範例 5：除錯（兩者皆可）

```text
現象：部署後電表清單全部顯示「斷線」，但 ems-simulator 服務是 active。
請先不要改程式，依序檢查：simulator 監聽的 port、backend 設定的 Modbus 位址、
journalctl 的錯誤訊息，找出原因後提出修改方案，等我確認。
```

## 6. 安全守則（給人，也給 agent）

- 不要把 API key、密碼、連線字串貼進 prompt 或寫進 repo；用環境變數。
- 不要讓 agent 讀取或複製客戶原始文件到公開 repo；需要參考時，先人工整理成去識別化的規則。
- 不要讓 agent 自動 push；commit 前自己看過 `git diff`。
- 公開 repo 前跑一次 secret 掃描（例如 `gitleaks`），並檢查 git 歷史——只刪目前的檔案不夠。
