# 在 Windows 上編譯 ems_backend、ems_simulator（linux-x64 self-contained）與 ems_web，輸出到 deploy\wsl\out\
# 之後在 WSL 執行 install.sh 部署（見檔尾提示）
# 不用 ErrorActionPreference=Stop：PowerShell 5.1 會把 pnpm 寫到 stderr 的一般訊息當成錯誤。改看 $LASTEXITCODE
# ⚠ 本檔要存成 UTF-8 with BOM，PowerShell 5.1 才不會把中文讀成亂碼
$root = Resolve-Path "$PSScriptRoot\..\.."
$out = "$PSScriptRoot\out"
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue

$projects = @(
    @{ Path = 'ems_backend\EmsApi'; Out = 'backend' },
    @{ Path = 'ems_simulator\EmsSimulator'; Out = 'simulator' }
)
foreach ($p in $projects) {
    dotnet publish "$root\$($p.Path)" -c Release -r linux-x64 --self-contained true -o "$out\$($p.Out)"
    if ($LASTEXITCODE -ne 0) { throw "publish $($p.Path) 失敗" }
}

# 前端：用 build（含 vue-tsc 型別檢查），型別錯了就不部署
Push-Location "$root\ems_web"
pnpm install --frozen-lockfile --config.confirmModulesPurge=false
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "ems_web pnpm install 失敗" }
pnpm build
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "ems_web build 失敗" }
Pop-Location
Copy-Item -Recurse "$root\ems_web\dist" "$out\web"

Write-Host "完成：$out"
$wslRoot = '/mnt/' + $root.Path.Substring(0, 1).ToLower() + ($root.Path.Substring(2) -replace '\\', '/')
Write-Host "下一步：wsl -d Ubuntu-24.04 -- bash $wslRoot/deploy/wsl/install.sh"
