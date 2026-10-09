using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmsApi.Middlewares;
using EmsApi.Models;
using EmsApi.Services;
using EmsApi.Services.Collector;
using EmsApi.Services.Energy;
using EmsApi.Services.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

// 相對路徑（serilog.json、data/meters.json、Logs/）以執行檔所在目錄為準
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("Configurations/serilog.json", optional: false, reloadOnChange: true);
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
builder.Host.UseSerilog();

// 列舉輸出成字串（時段：peak／half／sat_half／off）
builder.Services.AddControllers()
	.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("ConnectionStrings:DefaultConnection 尚未設定");
builder.Services.AddDbContext<EmsDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

var jwtSignKey = builder.Configuration["Jwt:SignKey"];
if (string.IsNullOrWhiteSpace(jwtSignKey) || jwtSignKey.Length < 32) throw new InvalidOperationException("Jwt:SignKey 尚未設定或長度不足 32 字元");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = builder.Configuration["Jwt:Issuer"],
			ValidAudience = builder.Configuration["Jwt:Audience"],
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSignKey)),
			NameClaimType = System.Security.Claims.ClaimTypes.Name,
			RoleClaimType = System.Security.Claims.ClaimTypes.Role,
			ClockSkew = TimeSpan.FromMinutes(1),
		};
	});

// 預設所有 API 都要登入；不需要登入的端點自己標 [AllowAnonymous]
builder.Services.AddAuthorizationBuilder()
	.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
	.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("AllowConfiguredOrigins", p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MeterQueryService>();
builder.Services.AddHostedService<ModbusCollectorService>();
builder.Services.AddSingleton<AggregationService>();
builder.Services.AddScoped<EnergyQueryService>();
builder.Services.AddHostedService<EnergyAggregationWorker>();
builder.Services.AddScoped<DemandService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<OverviewService>();
builder.Services.AddScoped<CarbonService>();
builder.Services.AddScoped<StructureService>();
builder.Services.AddSingleton<RuntimeSettings>();
builder.Services.AddSingleton<CollectorStatus>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AlarmService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddHostedService<AlarmWorker>();
builder.Services.AddHostedService<DemandEventWorker>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo { Title = "EMS API", Version = "v1" });
	options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
	{
		Name = "Authorization",
		In = ParameterLocation.Header,
		Type = SecuritySchemeType.Http,
		Scheme = JwtBearerDefaults.AuthenticationScheme,
		BearerFormat = "JWT",
	});
	options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
	{
		[new OpenApiSecuritySchemeReference("bearer", document)] = new List<string>(),
	});
});

var app = builder.Build();

await DbSeeder.MigrateAndSeedAsync(app.Services, app.Configuration, app.Logger);

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseCors("AllowConfiguredOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
