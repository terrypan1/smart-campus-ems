using EmsSimulator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 相對路徑（電表主檔、狀態檔）以執行檔所在目錄為準
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// Demo 專用指令：產生模擬歷史資料（見 BackfillCommand）
if (args.Length > 0 && args[0] == "backfill") return await BackfillCommand.RunAsync(args[1..]);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSystemd();
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection("Simulator"));
builder.Services.AddHostedService<ModbusSimulatorService>();

builder.Build().Run();
return 0;
