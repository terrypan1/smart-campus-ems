#!/bin/bash
# Demo 專用：在 WSL 部署環境產生 13 個月的模擬歷史資料，然後重算彙總（docs/實作/02 §3）
#   wsl -d Ubuntu-24.04 -- bash /mnt/c/<repo 路徑>/deploy/wsl/backfill.sh [--force]
# 前提：install.sh 已部署、ems-backend 已採集到至少一輪即時讀值（歷史會接在第一筆即時讀值之前）
set -e
APPS=/home/demo/apps/ems
ENV_FILE=/etc/ems/ems.env
CONN=$(sudo grep '^ConnectionStrings__DefaultConnection=' $ENV_FILE | cut -d= -f2-)
DB_PWD=$(sudo grep '^EMS_DB_PASSWORD=' $ENV_FILE | cut -d= -f2-)

ConnectionStrings__DefaultConnection="$CONN" $APPS/simulator/EmsSimulator backfill "$@"

# 清掉彙總、重啟後端：啟動時發現彙總表是空的就會全部重算（13 個月約 30–60 秒）
PGPASSWORD=$DB_PWD psql -h 127.0.0.1 -U ems -d ems -q -c 'TRUNCATE meter_hourly_energy, meter_energy_summary'
sudo systemctl restart ems-backend
echo "已重啟 ems-backend，全部重算中：sudo journalctl -u ems-backend -f 看到「彙總重算完成」即可"
