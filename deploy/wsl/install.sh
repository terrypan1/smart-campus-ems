#!/bin/bash
# 在 WSL Ubuntu-24.04 內執行：把 publish.ps1 的產出部署成 systemd 服務 ＋ nginx 前端
#   wsl -d Ubuntu-24.04 -- bash /mnt/c/<repo 路徑>/deploy/wsl/install.sh
#
# ⚠ 這個 distro 可能還有其他服務（其他 nginx site、PostgreSQL 16 的其他資料庫）。
#   本腳本只新增／更新 ems 自己的東西：ems-* 服務、nginx site「ems」、PostgreSQL 角色與資料庫「ems」。
#   nginx 只 reload、不 restart，也不刪除任何其他 site。
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=$HERE/out
APPS=/home/demo/apps/ems
ENV_FILE=/etc/ems/ems.env
PG_PORT=5432

[ -d "$OUT/web" ] || { echo "找不到 $OUT，先在 Windows 跑 deploy\\wsl\\publish.ps1"; exit 1; }

# ---------- 機密：第一次部署時產生，之後沿用 ----------
if ! sudo test -f $ENV_FILE; then
  DB_PWD=$(openssl rand -hex 16)
  JWT_KEY=$(openssl rand -hex 32)
  sudo mkdir -p /etc/ems
  sudo tee $ENV_FILE >/dev/null <<EOF
# EMS 部署機密（root 600，不進版控）。改了之後：sudo systemctl restart ems-backend
EMS_DB_PASSWORD=$DB_PWD
ConnectionStrings__DefaultConnection=Host=127.0.0.1;Port=$PG_PORT;Database=ems;Username=ems;Password=$DB_PWD
Jwt__SignKey=$JWT_KEY
# Demo 預設帳號的初始密碼（公開的 Demo 值）：只在資料庫還沒有該帳號時使用；登入後請改密碼
Seed__AdminPassword=EmsAdmin2026!
Seed__ViewerPassword=EmsViewer2026!
Seed__SysadminPassword=EmsSysadmin2026!
EOF
  sudo chmod 600 $ENV_FILE
  echo "已建立 $ENV_FILE"
fi
DB_PWD=$(sudo grep '^EMS_DB_PASSWORD=' $ENV_FILE | cut -d= -f2-)

# ---------- PostgreSQL：角色與資料庫（已存在就只同步密碼） ----------
sudo -u postgres psql -p $PG_PORT -v ON_ERROR_STOP=1 -q <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ems') THEN
    CREATE ROLE ems LOGIN PASSWORD '$DB_PWD';
  ELSE
    ALTER ROLE ems PASSWORD '$DB_PWD';
  END IF;
END \$\$;
SQL
sudo -u postgres psql -p $PG_PORT -Atc "SELECT 1 FROM pg_database WHERE datname='ems'" | grep -q 1 \
  || sudo -u postgres createdb -p $PG_PORT -O ems ems

# ---------- 程式 ----------
sudo systemctl stop ems-backend ems-simulator 2>/dev/null || true
mkdir -p $APPS
# 保留：後端 log、模擬器累積電度狀態（不然重新部署電度會歸零）
keep() { [ -d "$APPS/$1" ] && mv "$APPS/$1" "/tmp/ems-keep-$2" || true; }
restore() { [ -d "/tmp/ems-keep-$2" ] && { rm -rf "$APPS/$1"; mv "/tmp/ems-keep-$2" "$APPS/$1"; } || true; }
rm -rf /tmp/ems-keep-*
keep backend/Logs logs; keep simulator/state state
rm -rf $APPS/backend $APPS/simulator $APPS/web
cp -r $OUT/backend $APPS/backend
cp -r $OUT/simulator $APPS/simulator
cp -r $OUT/web $APPS/web
restore backend/Logs logs; restore simulator/state state
chmod +x $APPS/backend/EmsApi $APPS/simulator/EmsSimulator
chmod o+x /home/demo /home/demo/apps $APPS
chmod -R o+rX $APPS/web

# ---------- systemd ----------
sudo tee /etc/systemd/system/ems-simulator.service >/dev/null <<EOF
[Unit]
Description=EMS 模擬電表（Modbus TCP 127.0.0.1:1502）
After=network.target

[Service]
Type=notify
User=demo
WorkingDirectory=$APPS/simulator
ExecStart=$APPS/simulator/EmsSimulator
Environment=DOTNET_ENVIRONMENT=Production
Environment=Simulator__BindAddress=127.0.0.1
Environment=Simulator__Port=1502
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

sudo tee /etc/systemd/system/ems-backend.service >/dev/null <<EOF
[Unit]
Description=EMS 後端 API＋採集（127.0.0.1:5320）
After=network.target postgresql.service ems-simulator.service
Wants=postgresql.service

[Service]
User=demo
WorkingDirectory=$APPS/backend
ExecStart=$APPS/backend/EmsApi
EnvironmentFile=$ENV_FILE
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5320
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

# ---------- nginx：只加 ems 這個 site ----------
sudo install -m 644 $HERE/ems.nginx.conf /etc/nginx/sites-available/ems
sudo ln -sf /etc/nginx/sites-available/ems /etc/nginx/sites-enabled/ems
sudo nginx -t

sudo systemctl daemon-reload
sudo systemctl enable ems-simulator ems-backend >/dev/null
sudo systemctl start ems-simulator
sudo systemctl start ems-backend
sudo systemctl reload nginx

sleep 5
systemctl is-active ems-simulator ems-backend nginx
curl -fsS http://127.0.0.1:3020/api/Health && echo
echo "完成：http://localhost:3020"
