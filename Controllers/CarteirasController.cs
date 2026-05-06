using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Acoes_Fiis.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
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
            var itensBanco = await _context.Carteira.ToListAsync();

            var viewModel = new CarteiraTotalViewModel();
            decimal totalRFLiquido = 0;

            foreach (var item in itensBanco)
            {
                var viewItem = new CarteiraItemViewModel
                {
                    Id = item.Id,
                    Ticker = item.Ticker,
                    Quantidade = item.Quantidade,
                    PrecoMedio = item.PrecoMedio,
                    TipoAtivo = item.TipoAtivo,
                    TaxaRentabilidade = item.TaxaRentabilidade
                };

                if (item.TipoAtivo == "RendaFixa")
                {
                    viewItem.PrecoAtual = item.PrecoMedio;

                    // Cálculo do rendimento mensal: (Montante * (Taxa / 12 meses))
                    decimal taxaMensal = (item.TaxaRentabilidade ?? 0) / 12 / 100;
                    decimal rendimentoBruto = (item.Quantidade * item.PrecoMedio) * taxaMensal;

                    // Aplicando o IR de 17,5%
                    viewItem.UltimoRendimento = (rendimentoBruto / item.Quantidade) * 0.825m;

                    viewModel.TotalInvestidoRendaFixa += (item.Quantidade * item.PrecoMedio);
                    totalRFLiquido += rendimentoBruto * 0.825m;
                }
                if (item.TipoAtivo == "Acao")
                {
                    var acao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (acao != null)
                    {
                        viewItem.PrecoAtual = acao.PrecoAtual;

                        // 1. Cálculo do P/L Atual (Preço / Lucro por Ação)
                        decimal pl = acao.LPA > 0 ? acao.PrecoAtual / acao.LPA : 0;

                        // 2. Cálculo do P/VP Atual (Preço / Valor Patrimonial por Ação)
                        decimal pvp = acao.VPA > 0 ? acao.PrecoAtual / acao.VPA : 0;

                        // 3. Lógica de Recomendação Baseada em Indicadores (Exemplo Graham/Bazin)
                        if (pl > 0 && pl < 10 && acao.Roe > 12)
                        {
                            viewItem.Recomendacao = "Forte Compra (Barata + ROE Alto)";
                            viewItem.CorBadge = "success";
                        }
                        else if (pvp < 1.5m && pl < 15)
                        {
                            viewItem.Recomendacao = "Compra (Preço Justo)";
                            viewItem.CorBadge = "primary";
                        }
                        else if (pl > 20 || pvp > 3.0m)
                        {
                            viewItem.Recomendacao = "Venda / Caro";
                            viewItem.CorBadge = "danger";
                        }
                        else
                        {
                            viewItem.Recomendacao = "Neutro / Manter";
                            viewItem.CorBadge = "secondary";
                        }
                    }
                }
                else if (item.TipoAtivo == "Fii")
                {
                    var fii = await _context.RecomendacaoFii.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (fii != null)
                    {
                        viewItem.UltimoRendimento = fii.UltimoRendimento;
                        viewItem.PrecoAtual = fii.PrecoAtual;
                        viewItem.Recomendacao = fii.PVP switch
                        {
                            < 0.95m => "Forte Compra",
                            < 1.00m => "Compra (Preço Justo)",
                            <= 1.05m => "Neutro / Manter",
                            < 1.10m => "Aguardar / Caro",
                            _ => "Venda / Realizar Lucro" // Maior que 1.10
                        };
                        viewItem.CorBadge = viewItem.Recomendacao switch
                        {
                            "Forte Compra" => "success",
                            "Compra (Preço Justo)" => "primary",
                            "Neutro / Manter" => "secondary",
                            "Aguardar / Caro" => "warning",
                            "Venda / Realizar Lucro" => "danger",
                            _ => "dark"
                        };
                    }
                }
                else if (item.TipoAtivo == "Geral")
                {
                    var fii = await _context.AtivosGerais.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (fii != null)
                    {

                        viewItem.PrecoAtual = fii.PrecoAtual;
                        viewItem.Recomendacao = "Não Avaliado";
                    }
                }

                viewModel.Itens.Add(viewItem);
            }
            var financeiroData = await _context.Financeiro.ToListAsync();

            viewModel.ResumoMensal = financeiroData
                .GroupBy(x => new { x.Data.Year, x.Data.Month })
                .Select(g => new ResumoMesViewModel
                {
                    Ano = g.Key.Year,
                    Mes = g.Key.Month,
                    Entradas = g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor),
                    Saidas = g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor)
                })
                .OrderBy(x => x.Ano)
                .ThenBy(x => x.Mes)
                .ToList();
            // Busca os tickers disponíveis
            viewModel.ListaTickersAcoes = await _context.Recomendacao.Select(x => x.Ticker).ToListAsync();
            viewModel.ListaTickersFiis = await _context.RecomendacaoFii.Select(x => x.Ticker).ToListAsync();
            viewModel.ListaTickersGerais = await _context.AtivosGerais.Select(x => x.Ticker).ToListAsync();

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarTodos()
        {
            var listaCarteira = await _context.Carteira.ToListAsync();
            var service = new YahooService();
            int atualizados = 0;
            int pulados = 0;

            var listaAcoes = await _context.Recomendacao.ToListAsync();
            // Filtra apenas as ações que você tem na carteira
            var acoesParaAtualizar = listaAcoes.Where(r => listaCarteira.Any(c => c.Ticker == r.Ticker)).ToList();

            foreach (var item in acoesParaAtualizar)
            {
                try
                {
                    var dados = await service.ObterDadosAtivo(item.Ticker);
                    if (dados != null)
                    {
                        item.PrecoAtual = dados.PrecoAtual;
                        item.VPA = dados.VPA;
                        item.LPA = dados.LPA;
                        item.Roe = dados.Roe;
                        item.DividendYield = dados.DividendYield;
                        item.DataAtualizacao = DateTime.Now;

                        _context.Update(item);
                        atualizados++;
                    }
                }
                catch { pulados++; }
            }
            var listaFiis = await _context.RecomendacaoFii.ToListAsync();
            var fiisParaAtualizar = listaFiis.Where(r => listaCarteira.Any(c => c.Ticker == r.Ticker)).ToList();

            foreach (var item in fiisParaAtualizar)
            {
                try
                {
                    var dados = await service.ObterDadosAtivo(item.Ticker);
                    if (dados != null)
                    {
                        item.PrecoAtual = dados.PrecoAtual;
                        item.VPA = dados.VPA;
                        item.DataAtualizacao = DateTime.Now;

                        _context.Update(item);
                        atualizados++;
                    }
                }
                catch { pulados++; }
            }

            if (atualizados > 0)
            {
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = $"Sucesso! {atualizados} ativos atualizados e {pulados} pulados.";

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarRendaFixa(int id, string novoMontante, string novaTaxa)
        {
            var ativo = await _context.Carteira.FindAsync(id);

            if (ativo != null)
            {
                // Cultura brasileira para entender a vírgula
                var culturaBR = new System.Globalization.CultureInfo("pt-BR");

                // Tenta converter o Montante. Se conseguir, atualiza o valor.
                if (decimal.TryParse(novoMontante, System.Globalization.NumberStyles.Any, culturaBR, out decimal montanteDecimal))
                {
                    ativo.PrecoMedio = montanteDecimal;
                }

                // Mesma logica
                if (decimal.TryParse(novaTaxa, System.Globalization.NumberStyles.Any, culturaBR, out decimal taxaDecimal))
                {
                    ativo.TaxaRentabilidade = taxaDecimal;
                }

                _context.Update(ativo);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAtivo(string ticker, int quantidade, decimal precoMedio, decimal? taxaRentabilidade)
        {
            //  Verifica se já temos esse ativo na carteira
            var ativoExistente = await _context.Carteira.FirstOrDefaultAsync(x => x.Ticker == ticker);

            if (ativoExistente != null)
            {
                int qtdAnterior = ativoExistente.Quantidade;
                decimal pmAnterior = ativoExistente.PrecoMedio;

                int quantidadeTotal = qtdAnterior + quantidade;

                // CÁLCULO CORRETO: (Patrimônio Antigo + Custo da Nova Compra) / Quantidade Total
                decimal novoPrecoMedio = ((qtdAnterior * pmAnterior) + (quantidade * precoMedio)) / quantidadeTotal;

                ativoExistente.Quantidade = quantidadeTotal;
                ativoExistente.PrecoMedio = Math.Round(novoPrecoMedio, 2); // Arredonda para 2 casas decimais

                _context.Update(ativoExistente);
            }
            else
            {
                string tipo = "Geral";
                if (await _context.Recomendacao.AnyAsync(x => x.Ticker == ticker)) tipo = "Acao";
                else if (await _context.RecomendacaoFii.AnyAsync(x => x.Ticker == ticker)) tipo = "Fii";
                else if (ticker.ToUpper().Contains("CDB") || ticker.ToUpper().Contains("TESOURO")) tipo = "RendaFixa";

                var novoItem = new Carteira
                {
                    Ticker = ticker,
                    Quantidade = quantidade,
                    PrecoMedio = precoMedio,
                    TipoAtivo = tipo,
                    TaxaRentabilidade = taxaRentabilidade,
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
        [HttpPost]
        public async Task<IActionResult> AdicionarRendaFixa(Carteira novoItem)
        {
            if (ModelState.IsValid)
            {
                novoItem.TipoAtivo = "RendaFixa";
                novoItem.DataCompra = DateTime.Now;

                _context.Carteira.Add(novoItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
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
            return PartialView("Edit", carteira);
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
        public async Task<IActionResult> BaixarExcel()
        {
            var lancamentos = await _context.Carteira.ToListAsync();
            var csv = "Id,Ticker,Quantidade,PrecoMedio,TipoAtivo,Setor,DataCompra\n" +
                      string.Join("\n", lancamentos.Select(x =>
                          $"{x.Id},{x.Ticker},{x.Quantidade},{x.PrecoMedio},{x.TipoAtivo},{x.Setor},{x.DataCompra:yyyy-MM-dd}"));
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "carteira.csv");
        }
    }
}
