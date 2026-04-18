using Acoes_Fiis.Data;
using Acoes_Fiis.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Acoes_FiisContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Acoes_FiisContext") ?? throw new InvalidOperationException("Connection string 'Acoes_FiisContext' not found.")));


builder.Services.AddScoped<FinanciamentoService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var defaultCulture = new CultureInfo("pt-BR");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new List<CultureInfo> { defaultCulture },
    SupportedUICultures = new List<CultureInfo> { defaultCulture }
};


var app = builder.Build();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

