using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Acoes_Fiis.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Acoes_Fiis.Controllers
{

    public class RecomendacaosController : Controller
    {
        private readonly Acoes_FiisContext _context;

        public RecomendacaosController(Acoes_FiisContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> BuscarYahoo(string ticker)
        {
            if (string.IsNullOrEmpty(ticker)) return RedirectToAction(nameof(Index));

            try
            {
                var service = new YahooService();
                var dados = await service.ObterDadosAtivo(ticker.ToUpper());

                // Retorna para a View Create.cshtml passando os dados
                return View("Create", dados);
            }
            catch (Exception)
            {
                TempData["Erro"] = "Ativo não encontrado.";
                return RedirectToAction(nameof(Index));
            }
        }
        [HttpPost]
        public async Task<IActionResult> UpdateSegmento(int id, string novoSegmento)
        {
            var Acao = await _context.Recomendacao.FindAsync(id);
            if (Acao != null)
            {
                Acao.TipoAcao = novoSegmento;
                _context.Update(Acao);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> AtualizarTodos(string filtroTipo)
        {
            var query = _context.Recomendacao.AsQueryable();

            if (!string.IsNullOrEmpty(filtroTipo))
            {
                query = query.Where(r => r.TipoAtivo == filtroTipo);
            }

            var listaParaAtualizar = await query.ToListAsync();
            var service = new YahooService();
            int atualizados = 0;
            int pulados = 0;

            foreach (var item in listaParaAtualizar)
            {
                // VERIFICAÇÃO: Se foi atualizado há menos de 30 minutos, pula este ativo
                if (item.DataAtualizacao > DateTime.Now.AddMinutes(30))
                {
                    pulados++;
                    continue;
                }

                try
                {
                    var dadosAtualizados = await service.ObterDadosAtivo(item.Ticker);

                    item.Nome = dadosAtualizados.Nome;
                    item.Setor = dadosAtualizados.Setor;
                    //item.TipoAcao = dadosAtualizados.TipoAcao;
                    item.PrecoAtual = dadosAtualizados.PrecoAtual;
                    item.VPA = dadosAtualizados.VPA;
                    item.LPA = dadosAtualizados.LPA;
                    item.Roe = dadosAtualizados.Roe;
                    item.DividendYield = dadosAtualizados.DividendYield;

                    item.RegularMarketOpen = dadosAtualizados.RegularMarketOpen;
                    item.RegularMarketPreviousClose = dadosAtualizados.RegularMarketPreviousClose;
                    item.RegularMarketDayLow = dadosAtualizados.RegularMarketDayLow;
                    item.RegularMarketDayHigh = dadosAtualizados.RegularMarketDayHigh;
                    item.FiftyTwoWeekLow = dadosAtualizados.FiftyTwoWeekLow;
                    item.FiftyTwoWeekHigh = dadosAtualizados.FiftyTwoWeekHigh;
                    item.ForwardPE = dadosAtualizados.ForwardPE;
                    item.PriceToBook = dadosAtualizados.PriceToBook;
                    item.MarketCap = dadosAtualizados.MarketCap;
                    item.RegularMarketVolume = dadosAtualizados.RegularMarketVolume;

                    item.DataAtualizacao = DateTime.Now;

                    _context.Update(item);
                    atualizados++;
                }
                catch { continue; }
            }

            await _context.SaveChangesAsync();

            // Mensagem de feedback personalizada
            TempData["Sucesso"] = $"{atualizados} ativos atualizados. {pulados} mantidos (atualizados recentemente).";

            return RedirectToAction(nameof(Index), new { filtroTipo = filtroTipo });
        }
        // GET: Recomendacaos
        public async Task<IActionResult> Index(string filtroTipo)
        {
            var query = _context.Recomendacao.AsQueryable();

            // 2. Aplica a lógica do filtro se houver parâmetro
            if (!string.IsNullOrEmpty(filtroTipo))
            {
                query = query.Where(s => s.TipoAtivo == filtroTipo);
            }

            // 3. Monta a ViewBag para a View renderizar o select
            ViewBag.Tipos = new List<string> { "Setor Perene", "Dividendos", "Crescimento" };

            // 4. Executa a busca no banco de dados de forma instantânea
            var listaFiltrada = await query.ToListAsync();

            return View(listaFiltrada);
        }
        [HttpPost]
        public async Task<IActionResult> AlterarTipoRapido(int id, string novoTipo)
        {
            var recomendacao = await _context.Recomendacao.FindAsync(id);

            if (recomendacao != null)
            {
                recomendacao.TipoAtivo = novoTipo;
                recomendacao.DataAtualizacao = DateTime.Now;
                _context.Update(recomendacao);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Recomendacaos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recomendacao = await _context.Recomendacao
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recomendacao == null)
            {
                return NotFound();
            }

            return View(recomendacao);
        }

        // GET: Recomendacaos/Create
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Recomendacao recomendacao)
        {

            ModelState.Remove("Id");
            ModelState.Remove("DataAtualizacao");
            ModelState.Remove("Setor");

            if (ModelState.IsValid)
            {
                recomendacao.DataAtualizacao = DateTime.Now;
                _context.Add(recomendacao);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(recomendacao);
        }

        // GET: Recomendacaos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recomendacao = await _context.Recomendacao.FindAsync(id);
            if (recomendacao == null)
            {
                return NotFound();
            }
            return View(recomendacao);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Recomendacao model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Nome");
            ModelState.Remove("Setor");
            ModelState.Remove("TipoAcao");
            ModelState.Remove("RegularMarketOpen");
            ModelState.Remove("RegularMarketPreviousClose");
            ModelState.Remove("RegularMarketDayLow");
            ModelState.Remove("RegularMarketDayHigh");
            ModelState.Remove("FiftyTwoWeekLow");
            ModelState.Remove("FiftyTwoWeekHigh");
            ModelState.Remove("ForwardPE");
            ModelState.Remove("PriceToBook");
            ModelState.Remove("MarketCap");
            ModelState.Remove("RegularMarketVolume");

            if (ModelState.IsValid)
            {
                try
                {
                    var recomendacaoBanco = await _context.Recomendacao.FindAsync(id);
                    if (recomendacaoBanco == null)
                    {
                        return NotFound();
                    }

                    recomendacaoBanco.PrecoAtual = model.PrecoAtual;
                    recomendacaoBanco.VPA = model.VPA;
                    recomendacaoBanco.LPA = model.LPA;
                    recomendacaoBanco.Roe = model.Roe;
                    recomendacaoBanco.DividendYield = model.DividendYield;
                    recomendacaoBanco.DataAtualizacao = DateTime.Now;

                    _context.Update(recomendacaoBanco);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecomendacaoExists(model.Id))
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

            return View(model);
        }

        // GET: Recomendacaos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recomendacao = await _context.Recomendacao
                .FirstOrDefaultAsync(m => m.Id == id);
            if (recomendacao == null)
            {
                return NotFound();
            }

            return View(recomendacao);
        }

        // POST: Recomendacaos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recomendacao = await _context.Recomendacao.FindAsync(id);
            if (recomendacao != null)
            {
                _context.Recomendacao.Remove(recomendacao);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RecomendacaoExists(int id)
        {
            return _context.Recomendacao.Any(e => e.Id == id);
        }
    }
}
