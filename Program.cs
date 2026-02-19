using Acoes_Fiis.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<Acoes_FiisContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Acoes_FiisContext") ?? throw new InvalidOperationException("Connection string 'Acoes_FiisContext' not found.")));

// Add services to the container.
builder.Services.AddControllersWithViews();

var supportedCultures = new[] { "pt-BR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recomendacaos}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "fiis",
    pattern: "{controller=RecomendacaoFiis}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "Geral",
    pattern: "{controller=AtivoGeral}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "Carteiras",
    pattern: "{controller=Carteiras}/{action=Index}/{id?}");

app.UseRequestLocalization(localizationOptions);


app.Run();
