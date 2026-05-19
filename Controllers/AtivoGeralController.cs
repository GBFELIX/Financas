using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Acoes_Fiis.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Acoes_Fiis.Controllers
{
    public class AtivoGeralController : Controller
    {
        private readonly Acoes_FiisContext _context;

        public AtivoGeralController(Acoes_FiisContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string filtroClasse)
        {
            var query = _context.AtivosGerais.AsQueryable();
            if (!string.IsNullOrEmpty(filtroClasse))
            {
                query = query.Where(a => a.ClasseAtivo == filtroClasse);
            }

            ViewBag.Classes = new List<string> { "BDR", "ETF", "Cripto", "Ouro", "Moeda" };

            var lista = await query.ToListAsync();
            var service = new YahooService();

            decimal cotacaoDolar = 1;
            try
            {
                var dolarDados = await service.ObterDadosAtivo("USDBRL=X");
                cotacaoDolar = dolarDados.PrecoAtual;
            }
            catch { cotacaoDolar = 5.20m; }

            foreach (var item in lista)
            {
                if (item.DataAtualizacao > DateTime.Now.AddMinutes(-30)) continue;

                try
                {
                    var dados = await service.ObterDadosAtivo(item.Ticker);

                    if (item.Moeda == "USD")
                    {
                        item.PrecoAtual = dados.PrecoAtual * cotacaoDolar;
                    }
                    else
                    {
                        item.PrecoAtual = dados.PrecoAtual;
                    }

                    item.DataAtualizacao = DateTime.Now;
                    _context.Update(item);
                }
                catch { continue; }
            }

            await _context.SaveChangesAsync();

            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> BuscarYahoo(string ticker)
        {
            if (string.IsNullOrEmpty(ticker)) return RedirectToAction(nameof(Index));

            try
            {
                var service = new YahooService();
                string tickerFinal = ticker.ToUpper();

                if (!tickerFinal.Contains("-") && !tickerFinal.Contains("=") && !tickerFinal.EndsWith(".SA"))
                {
                    tickerFinal += ".SA";
                }


                decimal precoEncontrado = await service.ObterPrecoSimples(tickerFinal);

                var novoAtivo = new AtivoGeral
                {
                    Ticker = tickerFinal,
                    PrecoAtual = precoEncontrado,
                    DataAtualizacao = DateTime.Now,
                    Moeda = tickerFinal.Contains("-USD") || tickerFinal.Contains("=F") ? "USD" : "BRL"
                };

                // Identificação Automática de Classe
                if (tickerFinal.Contains("-USD")) novoAtivo.ClasseAtivo = "Cripto";
                else if (tickerFinal.EndsWith("34.SA")) novoAtivo.ClasseAtivo = "BDR";
                else if (tickerFinal.EndsWith("11.SA")) novoAtivo.ClasseAtivo = "ETF";
                else if (tickerFinal.Contains("=X")) novoAtivo.ClasseAtivo = "Moeda";
                else novoAtivo.ClasseAtivo = "ETF";

                return View("Create", novoAtivo);
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Ativo não encontrado: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarTodos(string filtroClasse)
        {
            var query = _context.AtivosGerais.AsQueryable();
            if (!string.IsNullOrEmpty(filtroClasse)) query = query.Where(a => a.ClasseAtivo == filtroClasse);

            var lista = await query.ToListAsync();
            var service = new YahooService();

            decimal cotacaoDolar = 1;
            try
            {
                var dolarDados = await service.ObterDadosAtivo("USDBRL=X");
                cotacaoDolar = dolarDados.PrecoAtual;
            }
            catch { cotacaoDolar = 5.20m; }

            foreach (var item in lista)
            {
                if (item.DataAtualizacao > DateTime.Now.AddMinutes(-30)) continue;

                try
                {
                    var dados = await service.ObterDadosAtivo(item.Ticker);

                    if (item.Moeda == "USD")
                    {
                        item.PrecoAtual = dados.PrecoAtual * cotacaoDolar;
                    }
                    else
                    {
                        item.PrecoAtual = dados.PrecoAtual;
                    }

                    item.DataAtualizacao = DateTime.Now;
                    _context.Update(item);
                }
                catch { continue; }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { filtroClasse = filtroClasse });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Ticker,Nome,PrecoAtual,ClasseAtivo,Moeda")] AtivoGeral ativoGeral)
        {
            if (ModelState.IsValid)
            {
                ativoGeral.DataAtualizacao = DateTime.Now;
                _context.Add(ativoGeral);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(ativoGeral);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var ativoGeral = await _context.AtivosGerais.FindAsync(id);
            if (ativoGeral == null) return NotFound();
            return View(ativoGeral);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Ticker,Nome,PrecoAtual,ClasseAtivo,Moeda")] AtivoGeral ativoGeral)
        {
            if (id != ativoGeral.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    ativoGeral.DataAtualizacao = DateTime.Now;
                    _context.Update(ativoGeral);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AtivoGeralExists(ativoGeral.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(ativoGeral);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var ativoGeral = await _context.AtivosGerais.FirstOrDefaultAsync(m => m.Id == id);
            if (ativoGeral == null) return NotFound();
            return View(ativoGeral);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ativoGeral = await _context.AtivosGerais.FindAsync(id);
            if (ativoGeral != null) _context.AtivosGerais.Remove(ativoGeral);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AtivoGeralExists(int id) => _context.AtivosGerais.Any(e => e.Id == id);
    }
}