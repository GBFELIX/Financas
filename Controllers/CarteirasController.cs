using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Acoes_Fiis.Services;
using Flurl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Acoes_Fiis.Controllers
{
    public class CarteirasController : Controller
    {
        private readonly Acoes_FiisContext _context;
        private readonly FinanciamentoService _service;

        public CarteirasController(Acoes_FiisContext context, FinanciamentoService service)
        {
            _context = context;
            _service = service;
        }


        public async Task<IActionResult> Index(string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            var agora = DateTime.Now;
            var viewModel = new CarteiraTotalViewModel();

            var (queryCarteira, queryFinanceiro, queryFinanciamento) = InicializarQueriesCarteira(visao);

            var itensBanco = await queryCarteira.ToListAsync();
            var financeiroData = await queryFinanceiro.ToListAsync();
            var financiamento = await queryFinanciamento.FirstOrDefaultAsync();

            var parametroAtual = await _context.Parametro.AsNoTracking().FirstOrDefaultAsync();
            viewModel.Parametro = parametroAtual ?? new Parametro();

            var (totalRFLiquido, totalProventosVariaveis) = await ProcessarItensCarteiraAtiva(itensBanco, viewModel);

            decimal saldoFinanceiroAteHoje = ProcessarFluxoFinanceiroSaldos(financeiroData, agora, viewModel);

            //decimal rendimentoLiquidoCaixinha = CalcularRendimentoCaixinhaCorrente(saldoFinanceiroAteHoje, parametroAtual, viewModel);

            await ProcessarAutomacoesFechamento(financeiroData, saldoFinanceiroAteHoje, totalProventosVariaveis, agora, visao, parametroAtual);

            ProcessarLancamentosFuturos(financeiroData, agora, viewModel);


            decimal rendimentoCaixinha = CalcularRendimentoCaixinhaCorrente(saldoFinanceiroAteHoje, parametroAtual, viewModel, financeiroData, agora);

            viewModel.PatrimonioTotalReal = viewModel.TotalPatrimonio + viewModel.TotalInvestidoRendaFixa + saldoFinanceiroAteHoje;

            await ProcessarRadarAportes(viewModel);

            ProcessarFinanciamentoImobiliario(financiamento, agora, viewModel);

            decimal totalRealRendaFixa = viewModel.PatrimonioTotalReal - viewModel.TotalPatrimonio;
            decimal totalRealFiis = ViewBag.TotalAcumuladoFiisSalvo ?? 0m;
            decimal totalRealAcoes = ViewBag.TotalAcumuladoAcoesSalvo ?? 0m;

            await CarregarModulosPainelFinanceiro(totalRealRendaFixa, totalRealFiis, totalRealAcoes, viewModel.PatrimonioTotalReal, visao);

            await CarregarDadosAuxiliares(viewModel, visao, parametroAtual);

            return View(viewModel);
        }
        private (IQueryable<Carteira> Carteira, IQueryable<Financeiro> Financeiro, IQueryable<Price> Financiamento) InicializarQueriesCarteira(string visao)
        {
            var queryCarteira = _context.Carteira.AsNoTracking();
            var queryFinanceiro = _context.Financeiro.AsNoTracking();

            var queryFinanciamento = _context.Financiamentos.Include(p => p.AportesPontuais).AsQueryable();

            if (visao == "Gabriel" || visao == "Ela")
            {
                queryCarteira = queryCarteira.Where(x => x.Dono == visao || x.Dono == "Casal");
                queryFinanceiro = queryFinanceiro.Where(x => x.Dono == visao || x.Dono == "Casal");
                queryFinanciamento = queryFinanciamento.Where(x => x.Dono == visao || x.Dono == "Casal");
            }

            return (queryCarteira, queryFinanceiro, queryFinanciamento);
        }

        private async Task<(decimal totalRFLiquido, decimal totalProventosVariaveis)> ProcessarItensCarteiraAtiva(List<Carteira> itensBanco, CarteiraTotalViewModel viewModel)
        {
            decimal totalRFLiquido = 0;
            decimal totalProventosVariaveis = 0;
            decimal totalAcumuladoAcoes = 0;
            decimal totalAcumuladoFiis = 0;
            decimal totalAcumuladoGerais = 0;

            var tickersCarteira = itensBanco.Select(x => x.Ticker).Distinct().ToList();

            var dicAcoes = await _context.Recomendacao.AsNoTracking()
                .ToDictionaryAsync(x => x.Ticker);

            var dicFiis = await _context.RecomendacaoFii.AsNoTracking()
                .ToDictionaryAsync(x => x.Ticker);

            var dicGerais = await _context.AtivosGerais.AsNoTracking()
                .ToDictionaryAsync(x => x.Ticker);

            var acoesNaCarteira = dicAcoes.Values.ToList();
            var fiisNaCarteira = dicFiis.Values.ToList();
            var geraisNaCarteira = dicGerais.Values.ToList();

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
                    decimal taxaMensal = (item.TaxaRentabilidade ?? 0) / 12 / 100;
                    decimal rendimentoBruto = (item.Quantidade * item.PrecoMedio) * taxaMensal;

                    viewItem.UltimoRendimento = (rendimentoBruto / item.Quantidade) * 0.825m;
                    viewModel.TotalInvestidoRendaFixa += (item.Quantidade * item.PrecoMedio);
                    totalRFLiquido += rendimentoBruto * 0.825m;
                }
                else if (item.TipoAtivo == "Acao")
                {
                    if (dicAcoes.TryGetValue(item.Ticker, out var acao))
                    {
                        viewItem.PrecoAtual = acao.PrecoAtual;
                        viewItem.DividendYield = acao.DividendYield;
                        decimal pl = acao.LPA > 0 ? acao.PrecoAtual / acao.LPA : 0;
                        decimal pvp = acao.VPA > 0 ? acao.PrecoAtual / acao.VPA : 0;

                        if (pl > 0 && pl < 10 && acao.Roe > 12) { viewItem.Recomendacao = "Forte Compra (Barata + ROE Alto)"; viewItem.CorBadge = "success"; }
                        else if (pvp < 1.5m && pl < 15) { viewItem.Recomendacao = "Compra (Preço Justo)"; viewItem.CorBadge = "primary"; }
                        else if (pl > 20 || pvp > 3.0m) { viewItem.Recomendacao = "Venda / Caro"; viewItem.CorBadge = "danger"; }
                        else { viewItem.Recomendacao = "Neutro / Manter"; viewItem.CorBadge = "secondary"; }

                        totalAcumuladoAcoes += (item.Quantidade * acao.PrecoAtual);
                        acoesNaCarteira.Add(acao);
                    }
                }
                else if (item.TipoAtivo == "Fii")
                {
                    if (dicFiis.TryGetValue(item.Ticker, out var fii))
                    {
                        viewItem.UltimoRendimento = fii.UltimoRendimento;
                        viewItem.PrecoAtual = fii.PrecoAtual;

                        viewItem.Recomendacao = fii.PVP switch
                        {
                            < 0.95m => "Forte Compra",
                            < 1.00m => "Compra (Preço Justo)",
                            < 1.05m => "Neutro / Manter",
                            < 1.10m => "Aguardar / Caro",
                            _ => "Venda / Realizar Lucro"
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

                        totalAcumuladoFiis += (item.Quantidade * fii.PrecoAtual);
                        fiisNaCarteira.Add(fii);
                    }
                }
                else if (item.TipoAtivo == "Geral")
                {
                    if (dicGerais.TryGetValue(item.Ticker, out var ativoGeral))
                    {
                        viewItem.PrecoAtual = ativoGeral.PrecoAtual;
                        viewItem.Recomendacao = "Não Avaliado";

                        totalAcumuladoGerais += (item.Quantidade * ativoGeral.PrecoAtual);
                        geraisNaCarteira.Add(ativoGeral);
                    }
                }

                viewModel.Itens.Add(viewItem);
                totalProventosVariaveis += viewItem.ProventoMensalEstimado;
            }

            ViewBag.TotalAcumuladoAcoesSalvo = totalAcumuladoAcoes;
            ViewBag.TotalAcumuladoFiisSalvo = totalAcumuladoFiis;

            decimal totalRendaVariavelGeral = totalAcumuladoAcoes + totalAcumuladoGerais;
            decimal patrimonioTotal = viewModel.TotalInvestidoRendaFixa + totalAcumuladoFiis + totalRendaVariavelGeral;

            var listaMetas = ViewBag.MetasAlocacaoLista as List<MetaAlocacao>;

            if (patrimonioTotal > 0 && listaMetas != null && listaMetas.Count >= 3)
            {
                viewModel.PercentualAtualRendaFixa = (double)((viewModel.TotalInvestidoRendaFixa / patrimonioTotal) * 100);
                viewModel.PercentualAtualFiis = (double)((totalAcumuladoFiis / patrimonioTotal) * 100);
                viewModel.PercentualAtualAcoes = (double)((totalRendaVariavelGeral / patrimonioTotal) * 100);

                double alvoRF = (double)(listaMetas.FirstOrDefault(x => x.Categoria == "Renda Fixa")?.PercentualAlvo ?? 20.0m);
                double alvoFiis = (double)(listaMetas.FirstOrDefault(x => x.Categoria == "FIIs")?.PercentualAlvo ?? 40.0m);
                double alvoAcoes = (double)(listaMetas.FirstOrDefault(x => x.Categoria == "Ações")?.PercentualAlvo ?? 40.0m);

                double desvioRF = alvoRF - viewModel.PercentualAtualRendaFixa;
                double desvioFiis = alvoFiis - viewModel.PercentualAtualFiis;
                double desvioAcoes = alvoAcoes - viewModel.PercentualAtualAcoes;

                if (desvioRF >= desvioFiis && desvioRF >= desvioAcoes)
                {
                    viewModel.SugestaoAporteCategoria = "Renda Fixa";
                    viewModel.SugestaoAporteJustificativa = $"Sua alocação real está em {viewModel.PercentualAtualRendaFixa:N2}%. Como o seu alvo estipulado é {alvoRF:N2}%, direcione o seu aporte aqui para reequilibrar seu colchão de segurança.";
                }
                else if (desvioFiis >= desvioRF && desvioFiis >= desvioAcoes)
                {
                    viewModel.SugestaoAporteCategoria = "Fundos Imobiliários (FIIs)";
                    viewModel.SugestaoAporteJustificativa = $"Seus FIIs representam {viewModel.PercentualAtualFiis:N2}% da carteira, abaixo do alvo desejado de {alvoFiis:N2}%. Foque em ativos deste segmento para aumentar o fluxo mensal de dividendos.";
                }
                else
                {
                    viewModel.SugestaoAporteCategoria = "Ações & Ativos Gerais";
                    viewModel.SugestaoAporteJustificativa = $"Sua fatia de Renda Variável está em {viewModel.PercentualAtualAcoes:N2}%, abaixo da sua meta configurada de {alvoAcoes:N2}%. Foque em setores perenes ou boas geradoras de caixa.";
                }
            }
            else
            {
                viewModel.SugestaoAporteCategoria = "Diversificar Carteira";
                viewModel.SugestaoAporteJustificativa = "Adicione ativos e configure suas metas para ativar a inteligência de rebalanceamento automático.";
            }



            // O resto do código que popula o ViewModel continua igual:
            viewModel.ListaTickersAcoes = acoesNaCarteira.OrderBy(x => x.TipoAcao).ThenBy(x => x.Ticker).ToList();
            viewModel.ListaTickersFiis = fiisNaCarteira.OrderBy(x => x.Segmento).ThenBy(x => x.Ticker).ToList();
            viewModel.ListaTickersGerais = geraisNaCarteira.OrderBy(x => x.ClasseAtivo).ThenBy(x => x.Ticker).ToList();

            return (totalRFLiquido, totalProventosVariaveis);
        }
        private decimal CalcularRendimentoCaixinhaCorrente(decimal saldoFinanceiroAteHoje, Parametro? parametro, CarteiraTotalViewModel viewModel, List<Financeiro> financeiroData, DateTime agora)
        {
            // 1. Identifica o nome exato do nó de rendimento do mês atual
            string sufixoMesAno = agora.ToString("MM/yyyy");
            string descricaoRendimentoNode = $"Rendimento Automático Caixinhas - {sufixoMesAno}";

            var lancamentoFixaExistente = financeiroData.FirstOrDefault(x => x.Descricao == descricaoRendimentoNode);

            decimal rendimentoAcumuladoAteHoje = lancamentoFixaExistente?.Valor ?? 0m;

            viewModel.RendaMensalTotalConsolidada = viewModel.TotalRendaMensalEstimada
                                                 + viewModel.RendaFixaMensalLiquida
                                                 + rendimentoAcumuladoAteHoje;

            // Retorna o valor acumulado para quem chamou o método
            return rendimentoAcumuladoAteHoje;
        }
        private async Task CarregarModulosPainelFinanceiro(decimal totalRF, decimal totalFIIs, decimal totalAcoes, decimal patrimonioTotalReal, string visao)
        {
            decimal patrimonioTotal = totalRF + totalFIIs + totalAcoes;
            DateTime dataAtual = DateTime.Now;
            string mesAnoAtual = dataAtual.ToString("MM/yyyy");
            var primeiroDiaMesAtual = new DateTime(dataAtual.Year, dataAtual.Month, 1);

            var dicionarioMetas = await _context.MetasAlocacao
            .Select(m => new { m.Categoria, m.PercentualAlvo })
            .ToDictionaryAsync(x => x.Categoria, x => x.PercentualAlvo);

            decimal alvoRF = dicionarioMetas.TryGetValue("Renda Fixa", out var rf) ? rf : 40m;
            decimal alvoFIIs = dicionarioMetas.TryGetValue("FIIs", out var fiis) ? fiis : 40m;
            decimal alvoAcoes = dicionarioMetas.TryGetValue("Ações", out var acoes) ? acoes : 20m;

            var painelRebalanceamento = new List<ItemRebalanceamentoViewModel>();
            if (patrimonioTotal > 0)
            {
                painelRebalanceamento.Add(CalcularItemRebalanceamento("Renda Fixa", alvoRF, totalRF, patrimonioTotal));
                painelRebalanceamento.Add(CalcularItemRebalanceamento("FIIs", alvoFIIs, totalFIIs, patrimonioTotal));
                painelRebalanceamento.Add(CalcularItemRebalanceamento("Ações", alvoAcoes, totalAcoes, patrimonioTotal));
            }

            ViewBag.PainelRebalanceamento = painelRebalanceamento;
            ViewBag.MetaRFAtual = alvoRF;
            ViewBag.MetaFIIsAtual = alvoFIIs;
            ViewBag.MetaAcoesAtual = alvoAcoes;

            var registroMesAtual = await _context.EvolucaoPatrimonial
            .FirstOrDefaultAsync(e => e.MesAno == mesAnoAtual && e.Dono == visao);

            if (registroMesAtual == null)
            {
                _context.EvolucaoPatrimonial.Add(new EvolucaoPatrimonial { MesAno = mesAnoAtual, PatrimonioLiquido = patrimonioTotalReal, Dono = visao });
            }
            else if (registroMesAtual.PatrimonioLiquido != patrimonioTotalReal)
            {
                registroMesAtual.PatrimonioLiquido = patrimonioTotalReal;
            }
            await _context.SaveChangesAsync();

            List<EvolucaoPatrimonialDto> historicoOrdenado;

            if (visao != "Casal")
            {
                var historicoDoBanco = await _context.EvolucaoPatrimonial
                .Where(e => e.Dono == visao)
                .Select(e => new EvolucaoPatrimonialDto { MesAno = e.MesAno, Patrimonio = e.PatrimonioLiquido })
                .ToListAsync();

                historicoOrdenado = historicoDoBanco
                    .OrderByDescending(x => x.Ano).ThenByDescending(x => x.Mes)
                    .Take(12).ToList();
            }
            else
            {
                var historicoTodos = await _context.EvolucaoPatrimonial
                .Where(e => e.Dono != "Casal")
                .Select(e => new EvolucaoPatrimonialDto { MesAno = e.MesAno, Patrimonio = e.PatrimonioLiquido })
                .ToListAsync();

                historicoOrdenado = historicoTodos
                    .GroupBy(e => e.MesAno)
                    .Select(g => new EvolucaoPatrimonialDto
                    {
                        MesAno = g.Key,
                        Patrimonio = g.Sum(x => x.Patrimonio)
                    })
                    .OrderByDescending(x => x.Ano).ThenByDescending(x => x.Mes)
                    .Take(12).ToList();
            }

            ViewBag.HistoricoEvolucaoLabels = historicoOrdenado.Select(x => x.MesAno).ToList();
            ViewBag.HistoricoEvolucaoValores = historicoOrdenado.Select(x => x.Patrimonio).ToList();

            var queryFinanceiro = _context.Financeiro.AsQueryable();
            if (visao != "Casal")
                queryFinanceiro = queryFinanceiro.Where(t => t.Dono == visao);

            var todasTransacoes = await queryFinanceiro
            .Select(t => new TransacaoDto
            {
                Data = t.Data,
                Tipo = t.Tipo,
                Valor = t.Valor,
                Categoria = t.Categoria,
                Descricao = t.Descricao
            })
            .ToListAsync();

            var transacoesPassadas = todasTransacoes.Where(t => t.Data < primeiroDiaMesAtual).ToList();

            decimal mediaEntradasHistorica = 0m, mediaDespesasHistorica = 0m;
            var mesesComDados = transacoesPassadas.GroupBy(t => new { t.Data.Year, t.Data.Month }).Count();

            if (mesesComDados > 0)
            {
                mediaEntradasHistorica = transacoesPassadas.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor) / mesesComDados;
                mediaDespesasHistorica = transacoesPassadas.Where(t => t.Tipo == "Despesa").Sum(t => t.Valor) / mesesComDados;
            }

            List<string> labelsProjecao = new List<string>();
            List<decimal> valoresProjecao = new List<decimal>();
            decimal patrimonioAcumulado = patrimonioTotalReal;

            var transacoesFuturas = todasTransacoes.Where(t => t.Data >= primeiroDiaMesAtual).ToList();

            for (int i = 1; i <= 12; i++)
            {
                DateTime mesAlvo = dataAtual.AddMonths(i);
                var transacoesDoMes = transacoesFuturas.Where(t => t.Data.Month == mesAlvo.Month && t.Data.Year == mesAlvo.Year).ToList();

                decimal entradasAvulsasPlanejadas = transacoesDoMes.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor);
                decimal entradas = mediaEntradasHistorica + entradasAvulsasPlanejadas;

                decimal despesasAvulsasPlanejadas = transacoesDoMes.Where(t => t.Tipo == "Despesa").Sum(t => t.Valor);
                decimal despesas = mediaDespesasHistorica + despesasAvulsasPlanejadas;

                patrimonioAcumulado += (entradas - despesas);
                labelsProjecao.Add(mesAlvo.ToString("MM/yyyy"));
                valoresProjecao.Add(patrimonioAcumulado);
            }
            ViewBag.ProjecaoFuturaLabels = labelsProjecao;
            ViewBag.ProjecaoFuturaValores = valoresProjecao;

            DateTime inicioHistorico = primeiroDiaMesAtual.AddMonths(-11);
            var todosRendimentos = todasTransacoes
                .Where(x => x.Categoria == "Investimento" && x.Tipo == "Entrada" && x.Descricao.Contains("Rendimento Automático"))
                .ToList();

            List<string> labelsRendimentosPassado = new List<string>();
            List<decimal> valoresFixaPassado = new List<decimal>();
            List<decimal> valoresVariavelPassado = new List<decimal>();

            for (int i = 0; i < 12; i++)
            {
                DateTime mesAnalise = inicioHistorico.AddMonths(i);
                var rendimentosMes = todosRendimentos.Where(x => x.Data.Month == mesAnalise.Month && x.Data.Year == mesAnalise.Year).ToList();

                labelsRendimentosPassado.Add(mesAnalise.ToString("MM/yyyy"));
                valoresFixaPassado.Add(rendimentosMes.Where(x => x.Descricao.Contains("Caixinhas")).Sum(x => x.Valor));
                valoresVariavelPassado.Add(rendimentosMes.Where(x => x.Descricao.Contains("Renda Variável")).Sum(x => x.Valor));
            }

            decimal mediaFixaHistorica = 0m, mediaVariavelHistorica = 0m;
            var transacoesPassadasRendimento = todosRendimentos.Where(x => x.Data < primeiroDiaMesAtual).ToList();
            var mesesComRendimento = transacoesPassadasRendimento.GroupBy(x => new { x.Data.Year, x.Data.Month }).Count();

            if (mesesComRendimento > 0)
            {
                mediaFixaHistorica = transacoesPassadasRendimento.Where(x => x.Descricao.Contains("Caixinhas")).Sum(x => x.Valor) / mesesComRendimento;
                mediaVariavelHistorica = transacoesPassadasRendimento.Where(x => x.Descricao.Contains("Renda Variável")).Sum(x => x.Valor) / mesesComRendimento;
            }

            List<string> labelsRendimentosFuturo = new List<string>();
            List<decimal> valoresFixaFuturo = new List<decimal>();
            List<decimal> valoresVariavelFuturo = new List<decimal>();

            for (int i = 1; i <= 12; i++)
            {
                DateTime mesAlvo = dataAtual.AddMonths(i);
                var rendimentosFuturosMes = todosRendimentos.Where(x => x.Data.Month == mesAlvo.Month && x.Data.Year == mesAlvo.Year).ToList();

                decimal fixaProjetada = !rendimentosFuturosMes.Any(x => x.Descricao.Contains("Caixinhas")) ? mediaFixaHistorica : rendimentosFuturosMes.Where(x => x.Descricao.Contains("Caixinhas")).Sum(x => x.Valor);
                decimal variavelProjetada = !rendimentosFuturosMes.Any(x => x.Descricao.Contains("Renda Variável")) ? mediaVariavelHistorica : rendimentosFuturosMes.Where(x => x.Descricao.Contains("Renda Variável")).Sum(x => x.Valor);

                labelsRendimentosFuturo.Add(mesAlvo.ToString("MM/yyyy"));
                valoresFixaFuturo.Add(fixaProjetada);
                valoresVariavelFuturo.Add(variavelProjetada);
            }

            ViewBag.RendimentosLabelsPassado = labelsRendimentosPassado;
            ViewBag.RendimentosLabelsFuturo = labelsRendimentosFuturo;
            ViewBag.FixaValoresPassado = valoresFixaPassado;
            ViewBag.FixaValoresFuturo = valoresFixaFuturo;
            ViewBag.VariavelValoresPassado = valoresVariavelPassado;
            ViewBag.VariavelValoresFuturo = valoresVariavelFuturo;
        }

        private ItemRebalanceamentoViewModel CalcularItemRebalanceamento(string categoria, decimal alvo, decimal valorAtual, decimal total)
        {
            decimal pctAtual = (valorAtual / total) * 100m;
            decimal desvio = pctAtual - alvo;

            string status = "⚖️ No Alvo";
            if (desvio < -2.0m) status = "🟢 APORTAR";
            if (desvio > 2.0m) status = "⏳ AGUARDAR";

            return new ItemRebalanceamentoViewModel
            {
                Categoria = categoria,
                PercentualAlvo = alvo,
                ValorAtual = valorAtual,
                PercentualAtual = Math.Round(pctAtual, 2),
                Desvio = Math.Round(desvio, 2),
                Status = status
            };
        }
        private decimal ProcessarFluxoFinanceiroSaldos(List<Financeiro> financeiroData, DateTime agora, CarteiraTotalViewModel viewModel)
        {
            viewModel.ResumoMensal = financeiroData
            .GroupBy(x => new { x.Data.Year, x.Data.Month })
            .Select(g =>
            {
                decimal entradas = 0;
                decimal despesas = 0;

                foreach (var item in g)
                {
                    if (item.Tipo == "Entrada") entradas += item.Valor;
                    else if (item.Tipo == "Despesa") despesas += item.Valor;
                }

                return new ResumoMesViewModel
                {
                    Ano = g.Key.Year,
                    Mes = g.Key.Month,
                    Entradas = entradas,
                    Saidas = despesas
                };
            })
            .OrderBy(x => x.Ano).ThenBy(x => x.Mes)
            .ToList();

            var resumoMesAtual = viewModel.ResumoMensal
            .FirstOrDefault(x => x.Ano == agora.Year && x.Mes == agora.Month);

            viewModel.EntradasMesCorrente = resumoMesAtual?.Entradas ?? 0m;
            viewModel.SaidasMesCorrente = resumoMesAtual?.Saidas ?? 0m;

            decimal saldoHistorico = 0m;
            var dataCorte = agora.Date;
            foreach (var x in financeiroData)
            {
                if (x.Data <= dataCorte)
                {
                    if (x.Tipo == "Entrada") saldoHistorico += x.Valor;
                    else if (x.Tipo == "Despesa") saldoHistorico -= x.Valor;
                }
            }

            return saldoHistorico;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarParametros(decimal cdiAnual)
        {
            if (cdiAnual < 0 || cdiAnual > 100)
            {
                return BadRequest("O valor do CDI informado é inválido. Insira um percentual entre 0 e 100.");
            }

            var parametro = await _context.Parametro.FirstOrDefaultAsync();

            if (parametro == null)
            {
                parametro = new Parametro
                {
                    CdiAnual = cdiAnual,
                    DataAtualizacao = DateTime.Now
                };
                _context.Parametro.Add(parametro);
            }
            else
            {
                if (parametro.CdiAnual == cdiAnual)
                {
                    return RedirectToAction("Index");
                }

                parametro.CdiAnual = cdiAnual;
                parametro.DataAtualizacao = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
        private async Task ProcessarAutomacoesFechamento(List<Financeiro> financeiroData, decimal saldoFinanceiroAteHoje, decimal totalProventosVariaveis, DateTime agora, string visao, Parametro? parametro)
        {
            bool mudouBanco = false;

            DateTime ultimoDiaMesAtual = new DateTime(agora.Year, agora.Month, 1).AddMonths(1).AddDays(-1);
            string sufixoMesAno = agora.ToString("MM/yyyy");
            string donoDestino = visao == "Casal" ? "Casal" : visao;

            string descricaoRendimentoNode = $"Rendimento Automático Caixinhas - {sufixoMesAno}";

            decimal cdiAnual = parametro?.CdiAnual ?? 14.75m;
            double taxaAnualDouble = (double)(cdiAnual / 100m);

            decimal taxaDiariaCaixinha = (decimal)(Math.Pow(1.0 + taxaAnualDouble, 1.0 / 252.0) - 1.0);

            var lancamentoFixaExistente = financeiroData.FirstOrDefault(x => x.Descricao == descricaoRendimentoNode);

            if (lancamentoFixaExistente == null)
            {
                bool hojeEhDiaUtil = agora.DayOfWeek != DayOfWeek.Saturday && agora.DayOfWeek != DayOfWeek.Sunday;
                decimal rendimentoDiarioLiquido = hojeEhDiaUtil ? (saldoFinanceiroAteHoje * taxaDiariaCaixinha * 0.825m) : 0m;
                decimal valorInicial = Math.Round(rendimentoDiarioLiquido > 0.01m ? rendimentoDiarioLiquido : 0m, 2);

                if (valorInicial > 0m)
                {
                    var novoLancamento = new Financeiro
                    {
                        Descricao = descricaoRendimentoNode,
                        Valor = valorInicial,
                        Data = agora.Date,
                        Tipo = "Entrada",
                        Categoria = "Investimento",
                        Pagamento = "Pix",
                        Dono = donoDestino,
                        DataRegistro = agora
                    };
                    _context.Financeiro.Add(novoLancamento);
                    financeiroData.Add(novoLancamento);
                    mudouBanco = true;
                }
            }
            else
            {
                DateTime dataUltimaAtualizacao = lancamentoFixaExistente.DataRegistro ?? agora.AddDays(-1);

                int diasUteisDecorridos = 0;
                DateTime dataChecagem = dataUltimaAtualizacao.Date.AddDays(1);

                while (dataChecagem <= agora.Date)
                {
                    if (dataChecagem.DayOfWeek != DayOfWeek.Saturday && dataChecagem.DayOfWeek != DayOfWeek.Sunday)
                    {
                        diasUteisDecorridos++;
                    }
                    dataChecagem = dataChecagem.AddDays(1);
                }

                double fracaoHorasExtra = 0;
                if (dataUltimaAtualizacao.Date == agora.Date && agora.DayOfWeek != DayOfWeek.Saturday && agora.DayOfWeek != DayOfWeek.Sunday)
                {
                    fracaoHorasExtra = (agora - dataUltimaAtualizacao).TotalDays;
                }

                double multiplicadorDiasFinais = diasUteisDecorridos + fracaoHorasExtra;

                if (multiplicadorDiasFinais > 0.01)
                {
                    decimal rendimentoPeriodo = saldoFinanceiroAteHoje * (taxaDiariaCaixinha * (decimal)multiplicadorDiasFinais) * 0.825m;
                    decimal incrementoFinal = Math.Round(rendimentoPeriodo, 4);

                    if (incrementoFinal > 0m)
                    {
                        lancamentoFixaExistente.Valor = Math.Round(lancamentoFixaExistente.Valor + incrementoFinal, 2);
                        lancamentoFixaExistente.Data = agora.Date;
                        lancamentoFixaExistente.DataRegistro = agora;

                        _context.Financeiro.Update(lancamentoFixaExistente);
                        mudouBanco = true;
                    }
                }
            }

            // ====================================================================
            // --- AUTOMAÇÃO B: DIVIDENDOS / RENDA VARIÁVEL (ESTIMATIVA MENSAL) ---
            // ====================================================================
            string descricaoRendimentoVariavel = $"Rendimento Automático Renda Variável - {sufixoMesAno}";
            decimal valorVariavelFinal = Math.Round(totalProventosVariaveis > 0.01m ? totalProventosVariaveis : 0m, 2);

            var lancamentoVariavelExistente = financeiroData.FirstOrDefault(x => x.Descricao == descricaoRendimentoVariavel);

            if (lancamentoVariavelExistente == null)
            {
                if (valorVariavelFinal > 0m)
                {
                    var novoLancamentoVariavel = new Financeiro
                    {
                        Descricao = descricaoRendimentoVariavel,
                        Valor = valorVariavelFinal,
                        Data = ultimoDiaMesAtual,
                        Tipo = "Entrada",
                        Categoria = "Investimento",
                        Pagamento = "Pix",
                        Dono = donoDestino,
                        DataRegistro = agora
                    };
                    _context.Financeiro.Add(novoLancamentoVariavel);
                    financeiroData.Add(novoLancamentoVariavel);
                    mudouBanco = true;
                }
            }
            else if (lancamentoVariavelExistente.Valor != valorVariavelFinal)
            {
                lancamentoVariavelExistente.Valor = valorVariavelFinal;
                lancamentoVariavelExistente.Data = ultimoDiaMesAtual;
                lancamentoVariavelExistente.DataRegistro = agora;
                _context.Financeiro.Update(lancamentoVariavelExistente);
                mudouBanco = true;
            }

            if (mudouBanco)
            {
                await _context.SaveChangesAsync();
            }
        }

        private void ProcessarLancamentosFuturos(List<Financeiro> financeiroData, DateTime agora, CarteiraTotalViewModel viewModel)
        {
            DateTime dataCorte = agora.Date;

            decimal entradasFuturas = 0m;
            decimal saidasFuturas = 0m;

            foreach (var x in financeiroData)
            {
                if (x.Data > dataCorte)
                {
                    if (x.Tipo == "Entrada")
                    {
                        entradasFuturas += x.Valor;
                    }
                    else if (x.Tipo == "Despesa")
                    {
                        saidasFuturas += x.Valor;
                    }
                }
            }

            viewModel.EntradasFuturas = entradasFuturas;
            viewModel.SaidasFuturas = saidasFuturas;
        }

        private async Task ProcessarRadarAportes(CarteiraTotalViewModel viewModel)
        {
            var acoesBaratas = await _context.Recomendacao.AsNoTracking()
                .Where(x => x.LPA > 0 && (x.PrecoAtual / x.LPA) < 10 && x.Roe > 12)
                .OrderBy(x => x.PrecoAtual / x.LPA)
                .Take(5)
                .Select(x => new RadarAporteViewModel
                {
                    Ticker = x.Ticker,
                    Tipo = "Ação",
                    PrecoAtual = x.PrecoAtual,
                    IndicadorDesconto = x.LPA > 0 ? x.PrecoAtual / x.LPA : 0,
                    Mensagem = "P/L Atrativo + ROE Eficiente"
                })
                .ToListAsync();

            var fiisDescontados = await _context.RecomendacaoFii.AsNoTracking()
                .Where(x => x.VPA > 0 && (x.PrecoAtual / x.VPA) < 0.98m).OrderBy(x => x.PrecoAtual / x.VPA).Take(5)
                .Select(x => new RadarAporteViewModel
                {
                    Ticker = x.Ticker,
                    Tipo = "FII",
                    PrecoAtual = x.PrecoAtual,
                    IndicadorDesconto = x.VPA > 0 ? x.PrecoAtual / x.VPA : 0,
                    Mensagem = "Desconto sobre Valor Patrimonial"
                })
                .ToListAsync();

            viewModel.SugestoesAporte.Clear();
            viewModel.SugestoesAporte.AddRange(acoesBaratas);
            viewModel.SugestoesAporte.AddRange(fiisDescontados);
        }

        private void ProcessarFinanciamentoImobiliario(Price financiamento, DateTime agora, CarteiraTotalViewModel viewModel)
        {
            if (financiamento == null) return;

            var simulacaoCompleta = _service.GerarSimulacao(financiamento);
            viewModel.ValorImovel = financiamento.ValorImovel;
            viewModel.ValorEntrada = financiamento.ValorEntrada;
            viewModel.TaxaJurosAnual = financiamento.TaxaJurosAnual;
            viewModel.ProjecaoFinanciamento = simulacaoCompleta;

            int mesAlvo = agora.Month;
            int anoAlvo = agora.Year;

            int indexParcelaAtual = simulacaoCompleta.FindIndex(p => p.Data.Month == mesAlvo && p.Data.Year == anoAlvo);

            if (indexParcelaAtual != -1)
            {
                var parcelaAtual = simulacaoCompleta[indexParcelaAtual];
                viewModel.SaldoDevedorAtual = parcelaAtual.SaldoDevedorRestante;

                viewModel.PrazoMesesRestantes = simulacaoCompleta.Count - indexParcelaAtual;
            }
            else
            {
                var primeiraParcela = simulacaoCompleta.FirstOrDefault();
                viewModel.SaldoDevedorAtual = primeiraParcela?.SaldoDevedorRestante ?? financiamento.SaldoDevedorInicial;
                viewModel.PrazoMesesRestantes = simulacaoCompleta.Count;
            }
        }

        private async Task CarregarDadosAuxiliares(CarteiraTotalViewModel viewModel, string visao, Parametro? parametroAtual)
        {
            viewModel.Parametro = parametroAtual ?? new Parametro();

            var todasAsMetas = await _context.MetasAlocacao.AsNoTracking().ToListAsync();
            ViewBag.MetasAlocacaoLista = todasAsMetas;
            viewModel.MetaAlocacao = todasAsMetas.FirstOrDefault() ?? new MetaAlocacao();

            viewModel.ConfiguracaoBackups = await _context.ConfiguracaoBackups.AsNoTracking().FirstOrDefaultAsync();

            var queryHistorico = _context.HistoricoAtivos.AsNoTracking();
            if (visao != "Casal")
            {
                queryHistorico = queryHistorico.Where(x => x.Dono == visao || x.Dono == "Casal");
            }
            viewModel.HistoricoTransacoes = await queryHistorico.OrderByDescending(x => x.DataOperacao).ToListAsync();

            viewModel.HistoricoFolhas = await _context.FolhasPagamento.AsNoTracking()
            .Where(f => f.Visao == visao)
            .OrderByDescending(f => f.Ano)
            .ThenByDescending(f => f.Mes)
            .ToListAsync();

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComprarAtivo(int id, int quantidadeComprada, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (quantidadeComprada <= 0) return RedirectToAction("Index", new { visao = visao });

            var ativo = await _context.Carteira.FindAsync(id);
            if (ativo == null) return RedirectToAction("Index", new { visao = visao });
            decimal precoExecucao = 0;
            if (ativo.TipoAtivo == "Fii")
            {
                precoExecucao = await _context.RecomendacaoFii.AsNoTracking()
                    .Where(x => x.Ticker == ativo.Ticker)
                    .Select(x => x.PrecoAtual)
                    .FirstOrDefaultAsync();
            }
            else if (ativo.TipoAtivo == "Acao")
            {
                precoExecucao = await _context.Recomendacao.AsNoTracking()
                    .Where(x => x.Ticker == ativo.Ticker)
                    .Select(x => x.PrecoAtual)
                    .FirstOrDefaultAsync();
            }

            if (precoExecucao == 0) precoExecucao = ativo.PrecoMedio;

            decimal valorTotalAporte = quantidadeComprada * precoExecucao;
            DateTime dataHoje = DateTime.Now;

            decimal custoTotalAntigo = ativo.Quantidade * ativo.PrecoMedio;
            ativo.Quantidade += quantidadeComprada;

            ativo.PrecoMedio = ativo.Quantidade > 0
                ? (custoTotalAntigo + valorTotalAporte) / ativo.Quantidade
                : precoExecucao;

            _context.Update(ativo);

            var registroHistorico = new HistoricoAtivo
            {
                Ticker = ativo.Ticker,
                TipoOperacao = "Compra",
                Quantidade = quantidadeComprada,
                PrecoUnidade = precoExecucao,
                DataOperacao = dataHoje,
                Dono = ativo.Dono
            };
            _context.HistoricoAtivos.Add(registroHistorico);

            var lancamentoFinanceiro = new Financeiro
            {
                Descricao = $"Compra de Ativo - {ativo.Ticker} ({quantidadeComprada} un)",
                Valor = Math.Round(valorTotalAporte, 2),
                Data = dataHoje,
                Tipo = "Despesa",
                Categoria = "Investimento",
                Pagamento = "Pix",
                Dono = ativo.Dono
            };
            _context.Financeiro.Add(lancamentoFinanceiro);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { visao = visao });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirHistoricoAjax(int id)
        {
            var registro = new HistoricoAtivo { Id = id };

            _context.Entry(registro).State = Microsoft.EntityFrameworkCore.EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarRendimentoInline(int id, decimal novoValor)
        {
            var ticker = await _context.Carteira.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.Ticker)
            .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(ticker))
            {
                return NotFound("Item não encontrado na carteira.");
            }

            var recomendacaoFii = await _context.RecomendacaoFii
            .FirstOrDefaultAsync(x => x.Ticker == ticker);

            if (recomendacaoFii == null)
            {
                return NotFound($"Tabela de recomendações não contém o ticker {ticker}.");
            }

            if (recomendacaoFii.UltimoRendimento == novoValor)
            {
                return Ok();
            }

            recomendacaoFii.UltimoRendimento = novoValor;

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VenderAtivo(int id, int quantidadeVendida, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (quantidadeVendida <= 0) return RedirectToAction("Index", new { visao = visao });

            var ativo = await _context.Carteira.FindAsync(id);
            if (ativo == null) return RedirectToAction("Index", new { visao = visao });
            if (quantidadeVendida > ativo.Quantidade) return RedirectToAction("Index", new { visao = visao });

            decimal precoExecucao = 0;
            if (ativo.TipoAtivo == "Fii")
            {
                precoExecucao = await _context.RecomendacaoFii.AsNoTracking()
                    .Where(x => x.Ticker == ativo.Ticker)
                    .Select(x => x.PrecoAtual)
                    .FirstOrDefaultAsync();
            }
            else if (ativo.TipoAtivo == "Acao")
            {
                precoExecucao = await _context.Recomendacao.AsNoTracking()
                    .Where(x => x.Ticker == ativo.Ticker)
                    .Select(x => x.PrecoAtual)
                    .FirstOrDefaultAsync();
            }

            if (precoExecucao == 0) precoExecucao = ativo.PrecoMedio;

            decimal valorTotalVenda = quantidadeVendida * precoExecucao;
            DateTime dataHoje = DateTime.Now;

            decimal custoMedioLoteVendido = quantidadeVendida * ativo.PrecoMedio;
            decimal resultadoDiferenca = valorTotalVenda - custoMedioLoteVendido;
            string tipoResultado = resultadoDiferenca >= 0 ? "Ganho de Capital" : "Prejuízo de Capital";

            ativo.Quantidade -= quantidadeVendida;
            if (ativo.Quantidade == 0)
            {
                _context.Carteira.Remove(ativo);
            }
            else
            {
                _context.Update(ativo);
            }

            var registroHistorico = new HistoricoAtivo
            {
                Ticker = ativo.Ticker,
                TipoOperacao = "Venda",
                Quantidade = quantidadeVendida,
                PrecoUnidade = precoExecucao,
                DataOperacao = dataHoje,
                Dono = ativo.Dono
            };
            _context.HistoricoAtivos.Add(registroHistorico);

            var lancamentoFinanceiro = new Financeiro
            {
                Descricao = $"Venda de Ativo - {ativo.Ticker} ({tipoResultado})",
                Valor = Math.Round(valorTotalVenda, 2),
                Data = dataHoje,
                Tipo = "Entrada",
                Categoria = "Investimento",
                Pagamento = "Pix",
                Dono = ativo.Dono
            };
            _context.Financeiro.Add(lancamentoFinanceiro);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { visao = visao });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarFolhaPagamento(int ano, int mes, decimal salarioBruto, decimal descontos, IFormFile? pdfFile, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            // Força a cultura pt-BR na requisição de salvamento para garantir o bind correto de decimais
            var cultureBR = new System.Globalization.CultureInfo("pt-BR");
            System.Threading.Thread.CurrentThread.CurrentCulture = cultureBR;
            System.Threading.Thread.CurrentThread.CurrentUICulture = cultureBR;

            if (ano < 2000 || mes < 1 || mes > 12 || salarioBruto < 0 || descontos < 0)
            {
                TempData["Error"] = "Dados informados para a folha de pagamento são inválidos.";
                return RedirectToAction("Index", new { visao = visao });
            }

            string? caminhoSalvo = null;

            if (pdfFile != null && pdfFile.Length > 0)
            {
                string extensao = Path.GetExtension(pdfFile.FileName).ToLower();
                bool isPdfMime = pdfFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

                if (!isPdfMime || extensao != ".pdf")
                {
                    TempData["Error"] = "Apenas arquivos no formato PDF legítimo são permitidos.";
                    return RedirectToAction("Index", new { visao = visao });
                }

                string pastaDestino = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracheques");
                if (!Directory.Exists(pastaDestino))
                {
                    Directory.CreateDirectory(pastaDestino);
                }

                string nomeArquivo = $"{visao.ToLower()}_{ano}_{mes}_{Guid.NewGuid().ToString().Substring(0, 8)}.pdf";
                string caminhoCompleto = Path.Combine(pastaDestino, nomeArquivo);

                using (var stream = new FileStream(caminhoCompleto, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                caminhoSalvo = $"/uploads/contracheques/{nomeArquivo}";
            }

            var novaFolha = new FolhaPagamento
            {
                Ano = ano,
                Mes = mes,
                SalarioBruto = salarioBruto,
                Descontos = descontos,
                PathPdf = caminhoSalvo,
                Visao = visao,
                DataRegistro = DateTime.Now
            };

            _context.FolhasPagamento.Add(novaFolha);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Folha de pagamento de {mes:D2}/{ano} registrada com sucesso!";
            return RedirectToAction("Index", new { visao = visao });
        }

        [HttpPost]
        public IActionResult ExtrairDadosHolerite(IFormFile? pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
                return Json(new { sucesso = false, mensagem = "Nenhum arquivo foi enviado." });

            string extensao = Path.GetExtension(pdfFile.FileName).ToLower();
            bool isPdfMime = pdfFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

            if (!isPdfMime || extensao != ".pdf")
            {
                return Json(new { sucesso = false, mensagem = "Apenas arquivos no formato PDF legítimo são permitidos." });
            }

            try
            {
                string textoExtraido = "";

                using (var stream = pdfFile.OpenReadStream())
                using (var document = UglyToad.PdfPig.PdfDocument.Open(stream))
                {
                    foreach (var page in document.GetPages())
                    {
                        textoExtraido += page.Text + " ";
                    }
                }

                string padraoBruto = @"Total\s+de\s+Proventos\s*:\s*([\d\.]+\,\d{2})";
                string padraoDesconto = @"Total\s+de\s+Descontos\s*:\s*([\d\.]+\,\d{2})";
                string padraoAdiantamento = @"ADIANTAMENTO\s+QUINZENAL.*?([\d\.]+\,\d{2})";

                var matchBruto = Regex.Match(textoExtraido, padraoBruto, RegexOptions.IgnoreCase);
                var matchDesconto = Regex.Match(textoExtraido, padraoDesconto, RegexOptions.IgnoreCase);
                var matchAdiantamento = Regex.Match(textoExtraido, padraoAdiantamento, RegexOptions.IgnoreCase);

                if (matchBruto.Success && matchDesconto.Success)
                {
                    var cultureBR = new System.Globalization.CultureInfo("pt-BR");

                    decimal valorBruto = decimal.Parse(matchBruto.Groups[1].Value, cultureBR);
                    decimal valorDesconto = decimal.Parse(matchDesconto.Groups[1].Value, cultureBR);
                    decimal valorAdiantamento = 0m;

                    if (matchAdiantamento.Success)
                    {
                        valorAdiantamento = decimal.Parse(matchAdiantamento.Groups[1].Value, cultureBR);
                    }

                    decimal descontoReal = valorDesconto - valorAdiantamento;

                    return Json(new
                    {
                        sucesso = true,
                        bruto = valorBruto.ToString("F2", cultureBR),
                        desconto = descontoReal.ToString("F2", cultureBR)
                    });
                }

                return Json(new { sucesso = false, mensagem = "Não foi possível mapear os valores com o layout do PDF." });
            }
            catch (Exception ex)
            {
                return Json(new { sucesso = false, mensagem = $"Erro ao processar o arquivo: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirFolhaPagamento(int id)
        {
            var folha = await _context.FolhasPagamento.FindAsync(id);

            if (folha == null)
            {
                return Json(new { sucesso = false, mensagem = "Folha de pagamento não encontrada." });
            }

            try
            {
                if (!string.IsNullOrEmpty(folha.PathPdf))
                {
                    string caminhoArquivoFisico = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folha.PathPdf.TrimStart('/'));

                    if (System.IO.File.Exists(caminhoArquivoFisico))
                    {
                        System.IO.File.Delete(caminhoArquivoFisico);
                    }
                }

                _context.FolhasPagamento.Remove(folha);
                await _context.SaveChangesAsync();

                return Json(new { sucesso = true });
            }
            catch (Exception ex)
            {
                return Json(new { sucesso = false, mensagem = $"Erro ao excluir do banco de dados: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObterHistoricoPorAnoJson(int ano, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var historico = await _context.FolhasPagamento
                .Where(f => f.Ano == ano && f.Visao == visao)
                .OrderByDescending(f => f.Mes)
                .Select(f => new
                {
                    id = f.Id,
                    mes = f.Mes,
                    ano = f.Ano,
                    salarioBruto = f.SalarioBruto,
                    descontos = f.Descontos,
                    pathPdf = f.PathPdf
                })
                .ToListAsync();

            return Json(historico);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarAporteExtra(int priceId, string valor, int mesReferencia, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            decimal valorConvertido = 0;
            if (!string.IsNullOrEmpty(valor))
            {
                var culturaBr = new System.Globalization.CultureInfo("pt-BR");
                string valorTratado = valor.Trim();

                if (!valorTratado.Contains(",") && !valorTratado.Contains("."))
                {
                    if (decimal.TryParse(valorTratado, System.Globalization.NumberStyles.Any, culturaBr, out decimal resultado))
                    {
                        valorConvertido = resultado / 100m;
                    }
                }
                else
                {
                    decimal.TryParse(valorTratado, System.Globalization.NumberStyles.Any, culturaBr, out valorConvertido);
                }
            }

            if (valorConvertido <= 0)
            {
                TempData["Error"] = "O valor do aporte extra informado é inválido.";
                return RedirectToAction(nameof(Index), new { visao = visao });
            }

            var financiamentoData = await _context.Financiamentos.AsNoTracking()
            .Where(f => f.Id == priceId)
            .Select(f => new { f.DataInicio })
            .FirstOrDefaultAsync();

            if (financiamentoData != null)
            {
                DateTime dataInicioContrato = financiamentoData.DataInicio;
                DateTime dataAtual = DateTime.Now;

                int mesesDeDiferenca = ((dataAtual.Year - dataInicioContrato.Year) * 12) + dataAtual.Month - dataInicioContrato.Month;
                int parcelaContratualCorreta = mesesDeDiferenca + 1;

                if (parcelaContratualCorreta <= 0 && mesReferencia > 0)
                {
                    parcelaContratualCorreta = mesReferencia;
                }

                var novoAporte = new AporteExtra
                {
                    PriceId = priceId,
                    MesReferencia = parcelaContratualCorreta,
                    Valor = Math.Round(valorConvertido, 2)
                };

                _context.Add(novoAporte);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Aporte extra de R$ {valorConvertido:N2} registrado com sucesso para a parcela {parcelaContratualCorreta}!";
            }
            else
            {
                TempData["Error"] = "Financiamento correspondente não foi localizado.";
            }

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarTodos(string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var queryCarteira = _context.Carteira.AsQueryable();
            if (visao == "Gabriel") queryCarteira = queryCarteira.Where(x => x.Dono == "Gabriel" || x.Dono == "Casal");
            else if (visao == "Ela") queryCarteira = queryCarteira.Where(x => x.Dono == "Ela" || x.Dono == "Casal");

            var listaCarteira = await queryCarteira.ToListAsync();
            var service = new YahooService();
            int atualizados = 0;
            int pulados = 0;

            var listaAcoes = await _context.Recomendacao.ToListAsync();
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

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtulizarRendaFixa(int id, string novoMontante, string novaTaxa, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var ativo = await _context.Carteira.FindAsync(id);

            if (ativo != null)
            {
                var culturaBR = new System.Globalization.CultureInfo("pt-BR");
                bool mudouAlgo = false;

                if (decimal.TryParse(novoMontante, System.Globalization.NumberStyles.Any, culturaBR, out decimal montanteDecimal))
                {
                    if (ativo.PrecoMedio != montanteDecimal || ativo.Quantidade != 1m)
                    {
                        ativo.PrecoMedio = montanteDecimal;
                        ativo.Quantidade = 1; mudouAlgo = true;
                    }
                }

                if (decimal.TryParse(novaTaxa, System.Globalization.NumberStyles.Any, culturaBR, out decimal taxaDecimal))
                {
                    if (ativo.TaxaRentabilidade != taxaDecimal)
                    {
                        ativo.TaxaRentabilidade = taxaDecimal;
                        mudouAlgo = true;
                    }
                }

                if (mudouAlgo)
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Renda Fixa atualizada com sucesso!";
                }
            }
            else
            {
                TempData["Error"] = "Ativo de Renda Fixa não foi localizado.";
            }

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAtivo(string ticker, int quantidade, decimal precoMedio, decimal? taxaRentabilidade, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (quantidade <= 0) return RedirectToAction(nameof(Index), new { visao = visao });

            ticker = ticker.Trim().ToUpper();
            DateTime dataHoje = DateTime.Now;
            decimal valorTotalAporte = quantidade * precoMedio;
            string donoAlvo = visao == "Casal" ? "Casal" : visao;

            var ativoExistente = await _context.Carteira
            .FirstOrDefaultAsync(x => x.Ticker == ticker && x.Dono == donoAlvo);

            if (ativoExistente != null)
            {
                int qtdAnterior = ativoExistente.Quantidade;
                decimal pmAnterior = ativoExistente.PrecoMedio;
                int quantidadeTotal = qtdAnterior + quantidade;

                decimal novoPrecoMedio = ((qtdAnterior * pmAnterior) + (quantidade * precoMedio)) / quantidadeTotal;

                ativoExistente.Quantidade = quantidadeTotal;
                ativoExistente.PrecoMedio = Math.Round(novoPrecoMedio, 2);

            }
            else
            {
                string tipo = "Geral";

                if (ticker.Contains("CDB") || ticker.Contains("TESOURO") || ticker.Contains("LCI") || ticker.Contains("LCA"))
                {
                    tipo = "RendaFixa";
                }
                else
                {
                    bool ehAcao = await _context.Recomendacao.AsNoTracking().AnyAsync(x => x.Ticker == ticker);
                    if (ehAcao)
                    {
                        tipo = "Acao";
                    }
                    else
                    {
                        bool ehFii = await _context.RecomendacaoFii.AsNoTracking().AnyAsync(x => x.Ticker == ticker);
                        if (ehFii) tipo = "Fii";
                    }
                }

                var novoItem = new Carteira
                {
                    Ticker = ticker,
                    Quantidade = quantidade,
                    PrecoMedio = precoMedio,
                    TipoAtivo = tipo,
                    TaxaRentabilidade = taxaRentabilidade,
                    DataCompra = dataHoje,
                    Dono = donoAlvo
                };
                _context.Add(novoItem);
            }

            var registroHistorico = new HistoricoAtivo
            {
                Ticker = ticker,
                TipoOperacao = "Compra",
                Quantidade = quantidade,
                PrecoUnidade = precoMedio,
                DataOperacao = dataHoje,
                Dono = donoAlvo
            };
            _context.HistoricoAtivos.Add(registroHistorico);

            var lancamentoFinanceiro = new Financeiro
            {
                Descricao = $"Compra de Ativo - {ticker} ({quantidade} un)",
                Valor = Math.Round(valorTotalAporte, 2),
                Data = dataHoje,
                Tipo = "Despesa",
                Categoria = "Investimento",
                Pagamento = "Pix",
                Dono = donoAlvo
            };
            _context.Financeiro.Add(lancamentoFinanceiro);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        public IActionResult Create(string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarRendaFixa(Carteira novoItem, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            string donoAlvo = visao == "Casal" ? "Casal" : visao;
            novoItem.Dono = donoAlvo;

            ModelState.Remove("Dono");
            ModelState.Remove("TipoAtivo");
            ModelState.Remove("Quantidade");

            if (ModelState.IsValid)
            {
                novoItem.Quantidade = 1;
                novoItem.TipoAtivo = "RendaFixa";
                novoItem.DataCompra = DateTime.Now;

                _context.Carteira.Add(novoItem);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Título de Renda Fixa '{novoItem.Ticker}' adicionado com sucesso!";
            }
            else
            {
                TempData["Error"] = "Falha ao adicionar Renda Fixa. Verifique os campos preenchidos.";
            }

            return RedirectToAction(nameof(Index), new { visao = donoAlvo });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarConfiguracaoBackup(string caminhoPastaLocal, int intervaloHoras)
        {
            if (string.IsNullOrWhiteSpace(caminhoPastaLocal) || intervaloHoras <= 0)
            {
                TempData["Error"] = "Caminho da pasta ou intervalo de horas inválido.";
                return RedirectToAction("Index", "Carteiras");
            }

            var config = await _context.ConfiguracaoBackups.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracaoBackup
                {
                    CaminhoPastaLocal = caminhoPastaLocal.Trim(),
                    IntervaloHoras = intervaloHoras
                };
                _context.ConfiguracaoBackups.Add(config);
            }
            else
            {
                if (config.CaminhoPastaLocal != caminhoPastaLocal || config.IntervaloHoras != intervaloHoras)
                {
                    config.CaminhoPastaLocal = caminhoPastaLocal.Trim();
                    config.IntervaloHoras = intervaloHoras;

                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Configurações de backup atualizadas com sucesso!";

            return RedirectToAction("Index", "Carteiras");
        }

        [HttpPost]
        public async Task<IActionResult> ForcarBackupAgora()
        {
            var config = await _context.ConfiguracaoBackups.FirstOrDefaultAsync();
            if (config == null || !Directory.Exists(config.CaminhoPastaLocal))
            {
                return BadRequest("Caminho da pasta do Google Drive inválido.");
            }

            try
            {
                string nomeBanco = "Investimentos";
                string nomeArquivoFixo = $"Backup_{nomeBanco}.bak";

                string pastaTemporaria = @"C:\Users\Public\Documents\BackupsTemp";
                if (!Directory.Exists(pastaTemporaria))
                {
                    Directory.CreateDirectory(pastaTemporaria);
                }

                string caminhoTemporarioSql = Path.Combine(pastaTemporaria, nomeArquivoFixo);
                string caminhoFinalDrive = Path.Combine(config.CaminhoPastaLocal, nomeArquivoFixo);

                if (System.IO.File.Exists(caminhoTemporarioSql)) System.IO.File.Delete(caminhoTemporarioSql);

                string queryBackup = $"BACKUP DATABASE [{nomeBanco}] TO DISK = '{caminhoTemporarioSql}' WITH FORMAT;";
                await _context.Database.ExecuteSqlRawAsync(queryBackup);

                if (System.IO.File.Exists(caminhoTemporarioSql))
                {
                    if (System.IO.File.Exists(caminhoFinalDrive))
                    {
                        System.IO.File.Delete(caminhoFinalDrive);
                    }

                    System.IO.File.Move(caminhoTemporarioSql, caminhoFinalDrive);
                }

                config.UltimoBackup = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao substituir backup: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Carteira carteira, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (string.IsNullOrEmpty(carteira.Dono)) carteira.Dono = visao == "Casal" ? "Casal" : visao;

            ModelState.Remove("Dono");

            if (ModelState.IsValid)
            {
                _context.Add(carteira);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ativo cadastrado com sucesso!";
                return RedirectToAction(nameof(Index), new { visao = carteira.Dono });
            }

            ViewBag.VisaoAtual = visao;
            return View(carteira);
        }

        public async Task<IActionResult> Edit(int? id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            if (id == null) return NotFound();

            var carteira = await _context.Carteira.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (carteira == null) return NotFound();

            return PartialView("Edit", carteira);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Carteira carteira, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (id != carteira.Id) return NotFound();

            ModelState.Remove("Dono");
            ModelState.Remove("TaxaRentabilidade");

            if (ModelState.IsValid)
            {
                try
                {
                    var registroBanco = await _context.Carteira.FindAsync(id);
                    if (registroBanco == null) return NotFound();

                    registroBanco.Quantidade = carteira.Quantidade;
                    registroBanco.PrecoMedio = carteira.PrecoMedio;

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Ativo atualizado com sucesso!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    return NotFound();
                }
            }

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        public async Task<IActionResult> Delete(int? id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            if (id == null) return NotFound();

            var carteira = await _context.Carteira.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (carteira == null) return NotFound();

            return View(carteira);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var carteira = new Carteira { Id = id };
            _context.Entry(carteira).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ativo removido da carteira.";
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        public async Task<IActionResult> BaixarExcel(string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var query = _context.Carteira.AsNoTracking();
            if (visao != "Casal")
            {
                query = query.Where(x => x.Dono == visao || x.Dono == "Casal");
            }

            var lancamentos = await query.ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,Ticker,Quantidade,PrecoMedio,TipoAtivo,Setor,DataCompra,Dono");

            foreach (var x in lancamentos)
            {
                string setorTratado = x.Setor?.Contains(",") == true ? $"\"{x.Setor}\"" : x.Setor;

                sb.AppendLine($"{x.Id},{x.Ticker},{x.Quantidade.ToString(System.Globalization.CultureInfo.InvariantCulture)},{x.PrecoMedio.ToString(System.Globalization.CultureInfo.InvariantCulture)},{x.TipoAtivo},{setorTratado},{x.DataCompra:yyyy-MM-dd},{x.Dono}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"carteira_{visao.ToLower()}.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarMetasAlocacao(decimal percentualRF, decimal percentualFIIs, decimal percentualAcoes)
        {
            if ((percentualRF + percentualFIIs + percentualAcoes) != 100m)
            {
                TempData["Error"] = "A soma das alocações deve ser exatamente 100%.";
                return RedirectToAction("Index");
            }

            var categoriasAlvo = new[] { "Renda Fixa", "FIIs", "Ações" };
            var todasMetas = await _context.MetasAlocacao
                .Where(m => categoriasAlvo.Contains(m.Categoria))
                .ToListAsync();

            AtualizarOuCriarMeta(todasMetas, "Renda Fixa", percentualRF);
            AtualizarOuCriarMeta(todasMetas, "FIIs", percentualFIIs);
            AtualizarOuCriarMeta(todasMetas, "Ações", percentualAcoes);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Metas de alocacao atualizadas!";
            return RedirectToAction("Index");
        }

        private void AtualizarOuCriarMeta(List<MetaAlocacao> lista, string categoria, decimal valor)
        {
            var meta = lista.FirstOrDefault(m => m.Categoria == categoria);
            if (meta == null)
            {
                _context.MetasAlocacao.Add(new MetaAlocacao { Categoria = categoria, PercentualAlvo = valor });
            }
            else if (meta.PercentualAlvo != valor)
            {
                meta.PercentualAlvo = valor;
            }
        }
    }
}