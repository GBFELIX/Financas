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
    public class RecomendacaoFiisController : Controller
    {
        private readonly Acoes_FiisContext _context;

        public RecomendacaoFiisController(Acoes_FiisContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string filtroSegmento)
        {
            var query = _context.RecomendacaoFii.AsQueryable();

            if (!string.IsNullOrEmpty(filtroSegmento))
            {
                query = query.Where(f => f.Segmento == filtroSegmento);
            }

            ViewBag.Segmentos = new List<string> { "Logística", "Recebíveis", "Shopping", "Lajes Corporativas", "Híbrido", "Outros" };

            var listaFiltrada = await query.ToListAsync();

            return View(listaFiltrada);
        }
        [HttpPost]
        public async Task<IActionResult> UpdateRendimento(int id, string novoRendimento)
        {
            var fii = await _context.RecomendacaoFii.FindAsync(id);
            if (fii != null && decimal.TryParse(novoRendimento.Replace(".", ","), out decimal valor))
            {
                fii.UltimoRendimento = valor;
                _context.Update(fii);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> UpdateTipo(int id, string novoTipo)
        {
            var fii = await _context.RecomendacaoFii.FindAsync(id);
            if (fii != null)
            {
                fii.TipoFii = novoTipo;
                _context.Update(fii);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSegmento(int id, string novoSegmento)
        {
            var fii = await _context.RecomendacaoFii.FindAsync(id);
            if (fii != null)
            {
                fii.Segmento = novoSegmento;
                _context.Update(fii);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        // BUSCA NO YAHOO FINANCE (Específico para FIIs)
        public async Task<IActionResult> BuscarYahoo(string ticker)
        {
            if (string.IsNullOrEmpty(ticker)) return RedirectToAction(nameof(Index));

            try
            {
                // Garante o sufixo .SA para FIIs brasileiros
                string tickerFormatado = ticker.ToUpper().EndsWith(".SA") ? ticker.ToUpper() : ticker.ToUpper() + ".SA";

                var service = new YahooService();
                var dados = await service.ObterDadosAtivo(tickerFormatado);

                //objeto FII com os dados brutos encontrados
                var novoFii = new RecomendacaoFii
                {
                    Ticker = ticker.ToUpper(),
                    PrecoAtual = dados.PrecoAtual,
                    VPA = dados.VPA,
                    DataAtualizacao = DateTime.Now
                };

                return View("Create", novoFii);
            }
            catch (Exception ex)
            {
                TempData["Erro"] = "Erro ao buscar FII: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // ATUALIZAR TODOS (Com trava de 30 min e respeito ao filtro)
        [HttpPost]
        public async Task<IActionResult> AtualizarTodos(string filtroSegmento)
        {
            var query = _context.RecomendacaoFii.AsQueryable();

            if (!string.IsNullOrEmpty(filtroSegmento))
            {
                query = query.Where(f => f.Segmento == filtroSegmento);
            }

            var lista = await query.ToListAsync();
            var service = new YahooService();
            int atualizados = 0;

            foreach (var item in lista)
            {
                // Trava de 30 minutos para evitar bloqueio do Yahoo
                if (item.DataAtualizacao > DateTime.Now.AddMinutes(-30)) continue;

                try
                {
                    string tickerFormatado = item.Ticker.EndsWith(".SA") ? item.Ticker : item.Ticker + ".SA";
                    var dados = await service.ObterDadosAtivo(tickerFormatado);

                    item.PrecoAtual = dados.PrecoAtual;
                    item.VPA = dados.VPA;
                    item.DataAtualizacao = DateTime.Now;

                    _context.Update(item);
                    atualizados++;
                }
                catch { continue; }
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = $"{atualizados} FIIs atualizados com sucesso!";
            return RedirectToAction(nameof(Index), new { filtroSegmento = filtroSegmento });
        }

        // POST: Alteração Rápida de Segmento/Tipo na Index
        [HttpPost]
        public async Task<IActionResult> AlterarTipoRapido(int id, string novoSegmento)
        {
            var fii = await _context.RecomendacaoFii.FindAsync(id);
            if (fii != null)
            {
                fii.Segmento = novoSegmento;
                _context.Update(fii);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Create (Ajustado para salvar a data)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Ticker,PrecoAtual,VPA,UltimoRendimento,Vacancia,TipoFii,Segmento")] RecomendacaoFii recomendacaoFii)
        {
            if (ModelState.IsValid)
            {
                recomendacaoFii.DataAtualizacao = DateTime.Now;
                _context.Add(recomendacaoFii);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(recomendacaoFii);
        }
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var recomendacaoFii = await _context.RecomendacaoFii
                .FirstOrDefaultAsync(m => m.Id == id);

            if (recomendacaoFii == null) return NotFound();

            return View(recomendacaoFii);
        }

        // GET: RecomendacaoFiis/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recomendacaoFii = await _context.RecomendacaoFii.FindAsync(id);

            if (recomendacaoFii == null) return NotFound();

            return View(recomendacaoFii);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Ticker,PrecoAtual,VPA,UltimoRendimento,Vacancia,TipoFii,Segmento,DataAtualizacao")] RecomendacaoFii recomendacaoFii)
        {
            if (id != recomendacaoFii.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Opcional: Atualiza a data para "agora" pois houve uma mudança manual
                    recomendacaoFii.DataAtualizacao = DateTime.Now;

                    _context.Update(recomendacaoFii);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecomendacaoFiiExists(recomendacaoFii.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(recomendacaoFii);
        }
        // GET: RecomendacaoFiis/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recomendacaoFii = await _context.RecomendacaoFii
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recomendacaoFii == null)
            {
                return NotFound();
            }

            return View(recomendacaoFii);
        }

        // POST: RecomendacaoFiis/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recomendacaoFii = await _context.RecomendacaoFii.FindAsync(id);
            if (recomendacaoFii != null)
            {
                _context.RecomendacaoFii.Remove(recomendacaoFii);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RecomendacaoFiiExists(int id)
        {
            return _context.RecomendacaoFii.Any(e => e.Id == id);
        }
    }
}
