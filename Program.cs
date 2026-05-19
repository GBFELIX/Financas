using Acoes_Fiis.Data;
using Acoes_Fiis.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Acoes_FiisContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Acoes_FiisContext") ?? throw new InvalidOperationException("Connection string 'Acoes_FiisContext' not found.")));

//builder.Services.AddDbContext<Acoes_FiisContext>(options =>
//    options.UseSqlite("Data Source=Planejamento.db"));

builder.Services.AddScoped<FinanciamentoService>();
builder.Services.AddHostedService<AtivosBackgroundService>();
//builder.Services.AddHostedService<YahooService>();

builder.Services.AddControllersWithViews();

var defaultCulture = new CultureInfo("pt-BR");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new List<CultureInfo> { defaultCulture },
    SupportedUICultures = new List<CultureInfo> { defaultCulture }
};


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<Acoes_FiisContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Erro ao criar/atualizar banco SQLite: " + ex.Message);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseRequestLocalization(localizationOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Carteiras}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "controle",
    pattern: "{controller=Financeiros}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "fiis",
    pattern: "{controller=RecomendacaoFiis}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "Geral",
    pattern: "{controller=AtivoGeral}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "Acoes",
    pattern: "{controller=Recomendacaos}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "Price",
    pattern: "{controller=Prices}/{action=Index}/{id?}");

app.UseRequestLocalization(localizationOptions);

app.Run();

