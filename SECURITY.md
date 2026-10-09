# Security Policy

## 範圍

這是**教學用 Demo**，不是正式產品，不提供安全更新保證。請不要把它直接部署到可從網際網路存取的環境。

## 機密的處理方式

- 連線字串、JWT 金鑰、帳號初始密碼一律從**環境變數**讀取；部署機放在 `/etc/ems/ems.env`（root 600），變數清單見根目錄 `.env.example`。
- `appsettings.Development.json` 只含本機開發用的假值（`ems_dev_only`、`*-dev`）。
- README 與 docs/DEPLOYMENT.md 列出的 Demo 帳密是公開值，只適合本機 Demo；任何共用環境請在第一次登入後修改密碼。
- 密碼以 ASP.NET Core Identity 的 `PasswordHasher` 雜湊後儲存。

## 資料

- 學校、大樓、電表名稱皆為虛構；讀值由模擬器產生。
- `data/` 下的 PDF 是台電與經濟部能源署公開文件，作為計算依據。
- `db/ems_demo.sql` 只含結構與主檔，不含帳號、稽核紀錄或讀值。

## 回報問題

發現安全問題請**不要開公開 issue**，改用 GitHub 的 Private vulnerability reporting（Security → Report a vulnerability），或私下聯絡 repository 擁有者。
