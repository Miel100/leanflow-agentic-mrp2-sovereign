using LeanFlow.Application.Agents;
using LeanFlow.Application.Services;
using LeanFlow.Api.Scheduling;
using Hangfire;
using Hangfire.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<LeanMRP2Service>();
builder.Services.AddSingleton<AlertService>();
builder.Services.AddSingleton<WatchdogService>();
builder.Services.AddSingleton<EventEngine>();
builder.Services.AddSingleton<MQTTBridgeService>();
builder.Services.AddScoped<DemandAgent>();
builder.Services.AddScoped<RCCPAgent>();
builder.Services.AddScoped<CRPAgent>();
builder.Services.AddScoped<SFCAgent>();
builder.Services.AddScoped<SupervisorAgent>();
builder.Services.AddScoped<FactoryConfigAgent>();
builder.Services.AddScoped<MRPScheduler>();
builder.Services.AddHangfire(config => config.UseInMemoryStorage());
builder.Services.AddHangfireServer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<MRPScheduler>("daily-mrp-cycle", x => x.RunDailyMRPCycleAsync(), Cron.Daily);
RecurringJob.AddOrUpdate<MRPScheduler>("watchdog-check", x => x.RunWatchdogCheckAsync(), "*/15 * * * *");
app.UseAuthorization();
app.MapControllers();
app.Run();
