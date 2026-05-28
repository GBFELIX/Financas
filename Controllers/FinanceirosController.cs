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
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Acoes_Fiis.Controllers
{
    public class FinanceirosController : Controller
    {
        private readonly Acoes_FiisContext _context;
        private readonly FinanciamentoService _service;

        public FinanceirosController(Acoes_FiisContext context, FinanciamentoService service)
        {
            _context = context;
            _service = service;
        }

        // GET: Financeiros
        public async Task<IActionResult> Index(int? mes, int? ano, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            int filtroMes = mes ?? DateTime.Now.Month;
            int filtroAno = ano ?? DateTime.Now.Year;

            DateTime dozeMesesAtras = DateTime.Now.AddMonths(-12);
            DateTime primeiroDiaMesAtual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // 1. Inicialização de Queries e Filtro de Visão (Dono)
            var (queryFinanceiro, queryContasFixas, queryCarteira) = InicializarQueriesFiltradas(visao);

            // 2. Carregamento Assíncrono de Dados Base
            var lancamentos = await queryFinanceiro.Where(x => x.Data.Month == filtroMes && x.Data.Year == filtroAno).ToListAsync() ?? new List<Financeiro>();
            var contasFixas = await queryContasFixas.ToListAsync();
            var ativosRF = await queryCarteira.Where(x => x.TipoAtivo == "RendaFixa").ToListAsync();
            var financeiroData = await queryFinanceiro.ToListAsync();

            // 3. Processamento das Médias de Sobra de Perfil (ViewBags)
            ProcessarMediasSobraUsuarios(financeiroData, dozeMesesAtras);

            // 4. Processamento do Financiamento Imobiliário
            var (valorParcelaFinal, totalAmortizadoNoMes) = await ProcessarParcelaFinanciamento(filtroMes, filtroAno, visao);

            // 5. Mapeamento de Status de Pagamento das Contas Fixas e Cálculo do Pendente
            bool parcelaPaga = lancamentos.Any(l => (l.Descricao.Contains("Financiamento") || l.Categoria == "Moradia") && l.Tipo == "Despesa");
            decimal pendente = ProcessarStatusContasFixas(contasFixas, lancamentos, valorParcelaFinal, parcelaPaga);

            // 6. Consolidação de Balanços e Cálculos de Rendimento
            decimal totalRendimentoliquido = CalcularRendimentoAtivosRF(ativosRF);
            decimal totalPagoCasa = lancamentos.Where(l => l.Tipo == "Despesa" && (l.Categoria == "Moradia" || l.Categoria == "Serviços" || l.Categoria == "Farmácia")).Sum(l => l.Valor);

            var resumoMensal = GerarResumoMensal(financeiroData);
            decimal entradasMesCorrente = financeiroData.Where(x => x.Data.Month == filtroMes && x.Data.Year == filtroAno && x.Tipo == "Entrada").Sum(x => x.Valor);
            decimal saidasMesCorrente = financeiroData.Where(x => x.Data.Month == filtroMes && x.Data.Year == filtroAno && x.Tipo == "Despesa").Sum(x => x.Valor);

            // 7. Cálculos de Patrimônio Líquido Real
            decimal saldoFinanceiroAteHoje = financeiroData.Where(x => x.Data.Date <= DateTime.Now.Date).Sum(x => x.Tipo == "Entrada" ? x.Valor : -x.Valor);
            decimal rendimentoLiquidoCaixinha = (saldoFinanceiroAteHoje * (10.75m / 12 / 100)) * 0.825m;

            decimal totalInvestidoRendaVariavel = await queryCarteira.Where(x => x.TipoAtivo == "Acao" || x.TipoAtivo == "Fii").SumAsync(x => x.Quantidade * x.PrecoMedio);
            decimal totalInvestidoRendaFixa = await queryCarteira.Where(x => x.TipoAtivo == "RendaFixa").SumAsync(x => x.Quantidade * x.PrecoMedio);
            decimal patrimonioTotalReal = totalInvestidoRendaVariavel + totalInvestidoRendaFixa + saldoFinanceiroAteHoje + rendimentoLiquidoCaixinha;

            // 8. Histórico Geral e Construção dos Gráficos de 12 Meses
            decimal mediaSobraHistorica = CalcularMediaSobraHistorica(financeiroData, dozeMesesAtras, primeiroDiaMesAtual);
            GerarDadosGraficos12Meses(financeiroData, totalRendimentoliquido, rendimentoLiquidoCaixinha);

            // 9. Montagem Final da ViewModel
            var viewModel = new FluxoCaixaViewModel
            {
                Lancamentos = lancamentos,
                MesAtual = filtroMes,
                AnoAtual = filtroAno,
                ContasFixas = contasFixas,
                ValorParcelaAtual = valorParcelaFinal,
                ParcelaPaga = parcelaPaga,
                TotalAmortizacaoMes = totalAmortizadoNoMes,
                TotalPagoCasa = totalPagoCasa,
                TotalPendenteCasa = pendente,
                MediaSobraHistorica = mediaSobraHistorica,
                ResumoMensal = resumoMensal,
                EntradasMesCorrente = entradasMesCorrente,
                SaidasMesCorrente = saidasMesCorrente,
                RendimentoRendaFixaMes = totalRendimentoliquido + rendimentoLiquidoCaixinha,
                PatrimonioTotalReal = patrimonioTotalReal,
                TotalRendaVariavel = totalInvestidoRendaVariavel
            };

            return View(viewModel);
        }

        private (IQueryable<Financeiro>, IQueryable<ContaFixa>, IQueryable<Carteira>) InicializarQueriesFiltradas(string visao)
        {
            var queryFinanceiro = _context.Financeiro.AsQueryable();
            var queryContasFixas = _context.ContasFixas.AsQueryable();
            var queryCarteira = _context.Carteira.AsQueryable();

            if (visao == "Gabriel" || visao == "Ela")
            {
                string donoAlvo = visao == "Gabriel" ? "Gabriel" : "Ela";
                queryFinanceiro = queryFinanceiro.Where(x => x.Dono == donoAlvo || x.Dono == "Casal");
                queryContasFixas = queryContasFixas.Where(x => x.Dono == donoAlvo || x.Dono == "Casal");
                queryCarteira = queryCarteira.Where(x => x.Dono == donoAlvo || x.Dono == "Casal");
            }

            return (queryFinanceiro, queryContasFixas, queryCarteira);
        }

        private void ProcessarMediasSobraUsuarios(List<Financeiro> financeiroData, DateTime dozeMesesAtras)
        {
            var agrupadoGabriel = financeiroData
                .Where(f => f.Dono == "Gabriel" && f.Data >= dozeMesesAtras)
                .GroupBy(f => new { f.Data.Year, f.Data.Month })
                .Select(g => g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor) - g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor)).ToList();

            ViewBag.MediaSobraGabriel = agrupadoGabriel.Any() ? agrupadoGabriel.Average() : 0m;

            var agrupadoSuely = financeiroData
                .Where(f => f.Dono == "Ela" && f.Data >= dozeMesesAtras)
                .GroupBy(f => new { f.Data.Year, f.Data.Month })
                .Select(g => g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor) - g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor)).ToList();

            ViewBag.MediaSobraSuely = agrupadoSuely.Any() ? agrupadoSuely.Average() : 0m;
        }

        private async Task<(decimal valorParcela, decimal amortizacao)> ProcessarParcelaFinanciamento(int filtroMes, int filtroAno, string visao)
        {
            var financiamento = await _context.Financiamentos.Include(f => f.AportesPontuais).FirstOrDefaultAsync();
            if (financiamento == null) return (0, 0);

            var projecao = _service.GerarSimulacao(financiamento);
            var parcelaDoMes = projecao.FirstOrDefault(p => p.Data.Month == filtroMes && p.Data.Year == filtroAno);
            if (parcelaDoMes == null) return (0, 0);

            bool dividirPorDois = visao == "Gabriel" || visao == "Ela";
            decimal parcela = dividirPorDois ? parcelaDoMes.ValorParcela / 2 : parcelaDoMes.ValorParcela;
            decimal amortizacao = dividirPorDois ? parcelaDoMes.Amortizacao / 2 : parcelaDoMes.Amortizacao;

            return (parcela, amortizacao);
        }

        private decimal ProcessarStatusContasFixas(List<ContaFixa> contasFixas, List<Financeiro> lancamentos, decimal valorParcelaFinal, bool parcelaPaga)
        {
            foreach (var conta in contasFixas)
            {
                conta.PagoNoMesAtual = lancamentos.Any(l => l.Descricao.Contains(conta.Descricao) && l.Tipo == "Despesa");
            }

            decimal pendente = contasFixas.Where(c => !c.PagoNoMesAtual).Sum(c => c.Valor);
            if (!parcelaPaga) pendente += valorParcelaFinal;

            return pendente;
        }

        private decimal CalcularRendimentoAtivosRF(List<Carteira> ativosRF)
        {
            decimal total = 0;
            foreach (var rf in ativosRF)
            {
                decimal taxaMensal = (rf.TaxaRentabilidade ?? 0) / 12 / 100;
                total += (rf.Quantidade * rf.PrecoMedio) * taxaMensal * 0.825m;
            }
            return total;
        }

        private List<ResumoMesViewModel> GerarResumoMensal(List<Financeiro> financeiroData)
        {
            return financeiroData
                .GroupBy(x => new { x.Data.Year, x.Data.Month })
                .Select(g => new ResumoMesViewModel
                {
                    Ano = g.Key.Year,
                    Mes = g.Key.Month,
                    Entradas = g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor),
                    Saidas = g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor)
                })
                .OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();
        }

        private decimal CalcularMediaSobraHistorica(List<Financeiro> financeiroData, DateTime dozeMesesAtras, DateTime primeiroDiaMesAtual)
        {
            var dadosHistorico = financeiroData.Where(x => x.Data >= dozeMesesAtras && x.Data < primeiroDiaMesAtual).ToList();
            if (!dadosHistorico.Any()) return 0m;

            var mesesAgrupados = dadosHistorico
                .GroupBy(x => new { x.Data.Month, x.Data.Year })
                .Select(g => new { Sobra = g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor) - g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor) })
                .ToList();

            return mesesAgrupados.Average(m => m.Sobra);
        }

        private void GerarDadosGraficos12Meses(List<Financeiro> financeiroData, decimal totalRendimentoliquido, decimal rendimentoLiquidoCaixinha)
        {
            var listaGanhosHistoricos = new List<decimal>();
            var listaLabelsHistoricos = new List<string>();
            var HistoricoLabelsGraficoRV = new List<string>();
            var HistoricoGanhosGraficoRV = new List<decimal>();

            decimal mediaRendimentoFallback = totalRendimentoliquido + rendimentoLiquidoCaixinha;
            DateTime agora = DateTime.Now;

            for (int i = 11; i >= 0; i--)
            {
                var dataAlvo = agora.AddMonths(-i);
                string sufixoMesAno = $"{dataAlvo:MM/yyyy}";

                // Gráfico RF
                var rendimentoRealMes = financeiroData.FirstOrDefault(x => x.Tipo == "Entrada" && x.Categoria == "Investimento" && x.Descricao != null && x.Descricao.ToUpper().Contains("RENDIMENTO AUTOMÁTICO CAIXINHAS") && x.Descricao.Contains(sufixoMesAno));
                if (rendimentoRealMes != null) listaGanhosHistoricos.Add(rendimentoRealMes.Valor);
                else if (i == 0) listaGanhosHistoricos.Add(Math.Round(totalRendimentoliquido + rendimentoLiquidoCaixinha, 2));
                else listaGanhosHistoricos.Add(Math.Round(mediaRendimentoFallback, 2));

                // Gráfico RV
                var proventosDoMesRV = financeiroData.Where(x => x.Tipo == "Entrada" && x.Categoria == "Investimento" && x.Descricao != null && x.Descricao.ToUpper().Contains("RENDIMENTO AUTOMÁTICO RENDA VARIÁVEL") && x.Descricao.Contains(sufixoMesAno)).ToList();
                if (proventosDoMesRV.Any()) HistoricoGanhosGraficoRV.Add(proventosDoMesRV.Sum(x => x.Valor));
                else HistoricoGanhosGraficoRV.Add(i == 0 ? 0.00m : 0.00m); // Ajuste caso queira injetar projeções futuras

                string nomeMesLabel = dataAlvo.ToString("MMM", System.Globalization.CultureInfo.CreateSpecificCulture("pt-BR")).ToUpper().Replace(".", "");
                string labelFinal = $"{nomeMesLabel}/{dataAlvo:yy}";

                listaLabelsHistoricos.Add(labelFinal);
                HistoricoLabelsGraficoRV.Add(labelFinal);
            }

            ViewBag.HistoricoGanhosGrafico = listaGanhosHistoricos;
            ViewBag.HistoricoLabelsGrafico = listaLabelsHistoricos;
            ViewBag.HistoricoLabelsGraficoRV = HistoricoLabelsGraficoRV;
            ViewBag.HistoricoGanhosGraficoRV = HistoricoGanhosGraficoRV;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarContaFixa(string descricao, decimal valor, string categoria, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            if (string.IsNullOrEmpty(descricao) || valor <= 0)
            {
                return RedirectToAction(nameof(Index), new { visao = visao });
            }

            var novaConta = new ContaFixa
            {
                Descricao = descricao,
                Valor = valor,
                Categoria = string.IsNullOrEmpty(categoria) ? "Serviços" : categoria,
                EhRecorrente = true,
                Dono = visao
            };

            try
            {
                _context.ContasFixas.Add(novaConta);
                await _context.SaveChangesAsync();
                TempData["Casa"] = "Conta agendada com sucesso!";
            }
            catch
            {
                TempData["Erro"] = "Erro ao salvar a conta.";
            }

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        public async Task<IActionResult> EditarContaFixa(int id, string descricao, decimal valor, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var conta = await _context.ContasFixas.FindAsync(id);
            if (conta != null)
            {
                conta.Descricao = descricao;
                conta.Valor = valor;
                _context.Update(conta);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        public async Task<IActionResult> ExcluirContaFixa(int id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var conta = await _context.ContasFixas.FindAsync(id);
            if (conta != null)
            {
                _context.ContasFixas.Remove(conta);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        public async Task<IActionResult> PagarContaCasa(string descricao, decimal valor, string categoria, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var novoGasto = new Financeiro
            {
                Data = DateTime.Now,
                Descricao = descricao,
                Valor = valor,
                Tipo = "Despesa",
                Categoria = categoria,
                Pagamento = "Débito",
                Dono = visao
            };

            _context.Financeiro.Add(novoGasto);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        public async Task<IActionResult> Adicionar(Financeiro model, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.Dono)) model.Dono = visao;

                _context.Financeiro.Add(model);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { mes = model.Data.Month, ano = model.Data.Year, visao = visao });
        }

        public async Task<IActionResult> Details(int? id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            if (id == null) return NotFound();

            var financeiro = await _context.Financeiro.FirstOrDefaultAsync(m => m.Id == id);
            if (financeiro == null) return NotFound();

            return View(financeiro);
        }

        public IActionResult Create(string visao)
        {
            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Financeiro lancamento, string visao, int numParcelas = 1)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            if (string.IsNullOrEmpty(lancamento.Dono))
            {
                lancamento.Dono = visao;
            }

            ModelState.Remove("Dono");

            if (numParcelas < 1) numParcelas = 1;

            if (ModelState.IsValid)
            {
                decimal valorTotal = lancamento.Valor;
                decimal valorParcela = valorTotal / numParcelas;

                for (int i = 0; i < numParcelas; i++)
                {
                    var novaParcela = new Financeiro
                    {
                        Descricao = numParcelas > 1
                            ? $"{lancamento.Descricao} ({i + 1:D2}/{numParcelas:D2})"
                            : lancamento.Descricao,
                        Valor = valorParcela,
                        Data = lancamento.Data.AddMonths(i),
                        Categoria = lancamento.Categoria,
                        Tipo = lancamento.Tipo,
                        Pagamento = lancamento.Pagamento,
                        Dono = lancamento.Dono
                    };

                    _context.Add(novaParcela);
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { mes = lancamento.Data.Month, ano = lancamento.Data.Year, visao = lancamento.Dono });
            }

            TempData["Erro"] = "Preencha todos os campos do lançamento corretamente.";
            return RedirectToAction(nameof(Index), new { mes = lancamento.Data.Month, ano = lancamento.Data.Year, visao = visao });
        }

        public async Task<IActionResult> Edit(int? id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            if (id == null) return NotFound();

            var financeiro = await _context.Financeiro.FindAsync(id);
            if (financeiro == null) return NotFound();

            return View(financeiro);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Financeiro financeiro, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            if (id != financeiro.Id) return NotFound();

            ModelState.Remove("Dono");

            if (ModelState.IsValid)
            {
                try
                {
                    var registroOriginal = await _context.Financeiro
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (registroOriginal != null)
                    {
                        financeiro.Dono = registroOriginal.Dono;
                    }
                    else
                    {
                        return NotFound();
                    }

                    _context.Update(financeiro);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FinanceiroExists(financeiro.Id)) return NotFound();
                    else throw;
                }

                return RedirectToAction(nameof(Index), new { mes = financeiro.Data.Month, ano = financeiro.Data.Year, visao = visao });
            }

            TempData["Erro"] = "Falha ao validar os dados da edição.";
            return RedirectToAction(nameof(Index), new { mes = financeiro.Data.Month, ano = financeiro.Data.Year, visao = visao });
        }

        public async Task<IActionResult> Delete(int? id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            if (id == null) return NotFound();

            var financeiro = await _context.Financeiro.FirstOrDefaultAsync(m => m.Id == id);
            if (financeiro == null) return NotFound();

            return View(financeiro);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var financeiro = await _context.Financeiro.FindAsync(id);
            int mesRedirect = DateTime.Now.Month;
            int anoRedirect = DateTime.Now.Year;

            if (financeiro != null)
            {
                mesRedirect = financeiro.Data.Month;
                anoRedirect = financeiro.Data.Year;
                _context.Financeiro.Remove(financeiro);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { mes = mesRedirect, ano = anoRedirect, visao = visao });
        }

        private bool FinanceiroExists(int id)
        {
            return _context.Financeiro.Any(e => e.Id == id);
        }
    }
}