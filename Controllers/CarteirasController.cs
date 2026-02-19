using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Acoes_Fiis.Controllers
{
    public class CarteirasController : Controller
    {
        private readonly Acoes_FiisContext _context;

        public CarteirasController(Acoes_FiisContext context)
        {
            _context = context;
        }

        // GET: Carteiras
        public async Task<IActionResult> Index()
        {
            // 1. Busca os itens reais da sua carteira no banco
            var itensDoBanco = await _context.Carteira.ToListAsync();

            // 2. Cria a ViewModel que vai para a View
            var viewModel = new CarteiraTotalViewModel();

            foreach (var item in itensDoBanco)
            {
                var viewItem = new CarteiraItemViewModel
                {
                    Id = item.Id,
                    Ticker = item.Ticker,
                    Quantidade = item.Quantidade,
                    PrecoMedio = item.PrecoMedio
                };

                // 3. Busca o Preço Atual e Status nas tabelas de recomendação
                if (item.TipoAtivo == "Acao")
                {
                    var acao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (acao != null)
                    {
                        viewItem.PrecoAtual = acao.PrecoAtual;
                        viewItem.Recomendacao = acao.Status;
                        viewItem.CorBadge = acao.CorClasse;
                    }
                }
                else if (item.TipoAtivo == "Fii")
                {
                    var fii = await _context.RecomendacaoFii.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (fii != null)
                    {
                        viewItem.PrecoAtual = fii.PrecoAtual;
                        viewItem.Recomendacao = fii.PVP < 0.98m ? "Compra" : "Neutra";
                        viewItem.CorBadge = fii.PVP < 0.98m ? "badge bg-success" : "badge bg-secondary";
                    }
                }

                viewModel.Itens.Add(viewItem);
            }

            // --- ADIÇÃO PARA O SELETOR DO MODAL ---
            // Busca os tickers disponíveis para você escolher na hora de adicionar
            viewModel.ListaTickersAcoes = await _context.Recomendacao.Select(x => x.Ticker).ToListAsync();
            viewModel.ListaTickersFiis = await _context.RecomendacaoFii.Select(x => x.Ticker).ToListAsync();
            viewModel.ListaTickersGerais = await _context.AtivosGerais.Select(x => x.Ticker).ToListAsync();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAtivo(string ticker, decimal quantidade, decimal precoMedio)
        {
            // 1. Verifica se já temos esse ativo na carteira
            var ativoExistente = await _context.Carteira.FirstOrDefaultAsync(x => x.Ticker == ticker);

            if (ativoExistente != null)
            {
                // LÓGICA DE PREÇO MÉDIO: (Qtd Antiga * PM Antigo + Qtd Nova * PM Novo) / Qtd Total
                decimal quantidadeTotal = ativoExistente.Quantidade + quantidade;
                decimal novoPrecoMedio = ((ativoExistente.Quantidade * ativoExistente.PrecoMedio) + (quantidade * precoMedio)) / quantidadeTotal;

                ativoExistente.Quantidade = quantidadeTotal;
                ativoExistente.PrecoMedio = novoPrecoMedio;
                _context.Update(ativoExistente);
            }
            else
            {
                // Se for um ativo novo, identifica o tipo e adiciona
                string tipo = "Geral";
                if (await _context.Recomendacao.AnyAsync(x => x.Ticker == ticker)) tipo = "Acao";
                else if (await _context.RecomendacaoFii.AnyAsync(x => x.Ticker == ticker)) tipo = "Fii";

                var novoItem = new Carteira
                {
                    Ticker = ticker,
                    Quantidade = quantidade,
                    PrecoMedio = precoMedio,
                    TipoAtivo = tipo,
                    DataCompra = DateTime.Now
                };
                _context.Add(novoItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Carteiras/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Carteiras/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Ticker,Quantidade,PrecoMedio,TipoAtivo,Setor,DataCompra")] Carteira carteira)
        {
            if (ModelState.IsValid)
            {
                _context.Add(carteira);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(carteira);
        }

        // GET: Carteiras/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var carteira = await _context.Carteira.FindAsync(id);
            if (carteira == null)
            {
                return NotFound();
            }
            return View(carteira);
        }

        // POST: Carteiras/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Ticker,Quantidade,PrecoMedio,TipoAtivo,Setor,DataCompra")] Carteira carteira)
        {
            if (id != carteira.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(carteira);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CarteiraExists(carteira.Id))
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
            return View(carteira);
        }

        // GET: Carteiras/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var carteira = await _context.Carteira
                .FirstOrDefaultAsync(m => m.Id == id);
            if (carteira == null)
            {
                return NotFound();
            }

            return View(carteira);
        }

        // POST: Carteiras/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var carteira = await _context.Carteira.FindAsync(id);
            if (carteira != null)
            {
                _context.Carteira.Remove(carteira);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CarteiraExists(int id)
        {
            return _context.Carteira.Any(e => e.Id == id);
        }
    }
}
