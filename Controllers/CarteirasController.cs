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
        private readonly FinanciamentoService _service;

        public CarteirasController(Acoes_FiisContext context, FinanciamentoService service)
        {
            _context = context;
            _service = service;
        }


        // GET: Carteiras
        public async Task<IActionResult> Index(string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            var agora = DateTime.Now;

            // 1. Inicialização de Queries e Filtro de Visão (Dono)
            var (queryCarteira, queryFinanceiro, queryFinanciamento) = InicializarQueriesCarteira(visao);

            // 2. Carregamento Assíncrono de Dados Base
            var itensBanco = await queryCarteira.ToListAsync();
            var financeiroData = await queryFinanceiro.ToListAsync();
            var financiamento = await queryFinanciamento.FirstOrDefaultAsync();

            var viewModel = new CarteiraTotalViewModel();

            // 3. Processamento dos Ativos e Cálculo das Projeções de Proventos
            var (totalRFLiquido, totalProventosVariaveis) = await ProcessarItensCarteiraAtiva(itensBanco, viewModel);

            // 4. Fluxo Financeiro, Saldos e Rendimento Corrente das Caixinhas
            decimal saldoFinanceiroAteHoje = ProcessarFluxoFinanceiroSaldos(financeiroData, agora, viewModel);
            decimal rendimentoLiquidoCaixinha = await CalcularRendimentoCaixinhaCorrente(saldoFinanceiroAteHoje, viewModel);

            // 5. Execução de Automações e Gravação de Fechamentos no Banco
            await ProcessarAutomacoesFechamento(financeiroData, saldoFinanceiroAteHoje, totalProventosVariaveis, agora, visao);

            // 6. Consolidação de Balanços Futuros e Consolidação Patrimonial
            ProcessarLancamentosFuturos(financeiroData, agora, viewModel);
            viewModel.PatrimonioTotalReal = viewModel.TotalPatrimonio + viewModel.TotalInvestidoRendaFixa + saldoFinanceiroAteHoje + rendimentoLiquidoCaixinha;

            // 7. Radar de Aportes (Sugestões de Ações e FIIs Baratos)
            await ProcessarRadarAportes(viewModel);

            // 8. Processamento do Financiamento Imobiliário e Dados Auxiliares
            ProcessarFinanciamentoImobiliario(financiamento, agora, viewModel);

            decimal totalRealRendaFixa = viewModel.PatrimonioTotalReal - viewModel.TotalPatrimonio;

            decimal totalRealFiis = ViewBag.TotalAcumuladoFiisSalvo ?? 0m;
            decimal totalRealAcoes = ViewBag.TotalAcumuladoAcoesSalvo ?? 0m;

            await CarregarModulosPainelFinanceiro(totalRealRendaFixa, totalRealFiis, totalRealAcoes, viewModel.PatrimonioTotalReal, visao);

            await CarregarDadosAuxiliares(viewModel, visao);

            return View(viewModel);
        }
        private (IQueryable<Carteira>, IQueryable<Financeiro>, IQueryable<Price>) InicializarQueriesCarteira(string visao)
        {
            var queryCarteira = _context.Carteira.AsQueryable();
            var queryFinanceiro = _context.Financeiro.AsQueryable();
            var queryFinanciamento = _context.Financiamentos.Include(p => p.AportesPontuais).AsQueryable();

            if (visao == "Gabriel" || visao == "Ela")
            {
                string donoAlvo = visao == "Gabriel" ? "Gabriel" : "Ela";
                queryCarteira = queryCarteira.Where(x => x.Dono == donoAlvo || x.Dono == "Casal");
                queryFinanceiro = queryFinanceiro.Where(x => x.Dono == donoAlvo || x.Dono == "Casal");
                queryFinanciamento = queryFinanciamento.Where(x => x.Dono == donoAlvo || x.Dono == "Casal");
            }

            return (queryCarteira, queryFinanceiro, queryFinanciamento);
        }

        private async Task<(decimal totalRFLiquido, decimal totalProventosVariaveis)> ProcessarItensCarteiraAtiva(List<Carteira> itensBanco, CarteiraTotalViewModel viewModel)
        {
            decimal totalRFLiquido = 0;
            decimal totalProventosVariaveis = 0;
            decimal totalAcumuladoAcoes = 0;
            decimal totalAcumuladoFiis = 0;
            decimal totalAcumuladoGerais = 0; // Acumulador para BDRs, Criptos, etc.

            // Listas locais para guardar apenas os cadastros dos ativos que o usuário de fato TEM comprado
            var acoesNaCarteira = new List<Recomendacao>();
            var fiisNaCarteira = new List<RecomendacaoFii>();
            var geraisNaCarteira = new List<AtivoGeral>();

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
                    var acao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (acao != null)
                    {
                        viewItem.PrecoAtual = acao.PrecoAtual;
                        viewItem.DividendYield = acao.DividendYield;
                        decimal pl = acao.LPA > 0 ? acao.PrecoAtual / acao.LPA : 0;
                        decimal pvp = acao.VPA > 0 ? acao.PrecoAtual / acao.VPA : 0;

                        if (pl > 0 && pl < 10 && acao.Roe > 12) { viewItem.Recomendacao = "Forte Compra (Barata + ROE Alto)"; viewItem.CorBadge = "success"; }
                        else if (pvp < 1.5m && pl < 15) { viewItem.Recomendacao = "Compra (Preço Justo)"; viewItem.CorBadge = "primary"; }
                        else if (pl > 20 || pvp > 3.0m) { viewItem.Recomendacao = "Venda / Caro"; viewItem.CorBadge = "danger"; }
                        else { viewItem.Recomendacao = "Neutro / Manter"; viewItem.CorBadge = "secondary"; }

                        // Calcula o peso financeiro real atual da ação na carteira
                        totalAcumuladoAcoes += (item.Quantidade * acao.PrecoAtual);
                        acoesNaCarteira.Add(acao);
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

                        // Calcula o peso financeiro real atual do FII na carteira
                        totalAcumuladoFiis += (item.Quantidade * fii.PrecoAtual);
                        fiisNaCarteira.Add(fii);
                    }
                }
                else if (item.TipoAtivo == "Geral")
                {
                    var ativoGeral = await _context.AtivosGerais.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (ativoGeral != null)
                    {
                        viewItem.PrecoAtual = ativoGeral.PrecoAtual;
                        viewItem.Recomendacao = "Não Avaliado";

                        // Calcula o peso financeiro real atual do Ativo Geral na carteira
                        totalAcumuladoGerais += (item.Quantidade * ativoGeral.PrecoAtual);
                        geraisNaCarteira.Add(ativoGeral);
                    }
                }
                viewModel.Itens.Add(viewItem);
                totalProventosVariaveis += viewItem.ProventoMensalEstimado;
            }

            ViewBag.TotalAcumuladoAcoesSalvo = totalAcumuladoAcoes;
            ViewBag.TotalAcumuladoFiisSalvo = totalAcumuladoFiis;

            // ====================================================================
            // CÁLCULO DAS METAS E RADAR DE APORTE (UTILIZANDO AS LINHAS DA TABELA)
            // ====================================================================
            decimal totalRendaVariavelGeral = totalAcumuladoAcoes + totalAcumuladoGerais;
            decimal patrimonioTotal = viewModel.TotalInvestidoRendaFixa + totalAcumuladoFiis + totalRendaVariavelGeral;

            // Recupera a lista de metas que salvamos na ViewBag
            var listaMetas = ViewBag.MetasAlocacaoLista as List<MetaAlocacao>;

            if (patrimonioTotal > 0 && listaMetas != null && listaMetas.Any())
            {
                // 1. Preenche as propriedades atuais da ViewModel
                viewModel.PercentualAtualRendaFixa = (double)((viewModel.TotalInvestidoRendaFixa / patrimonioTotal) * 100);
                viewModel.PercentualAtualFiis = (double)((totalAcumuladoFiis / patrimonioTotal) * 100);
                viewModel.PercentualAtualAcoes = (double)((totalRendaVariavelGeral / patrimonioTotal) * 100);

                double alvoRF = (double)(listaMetas.FirstOrDefault(x => x.Id == 1)?.PercentualAlvo ?? 20.0m);
                double alvoFiis = (double)(listaMetas.FirstOrDefault(x => x.Id == 2)?.PercentualAlvo ?? 40.0m);
                double alvoAcoes = (double)(listaMetas.FirstOrDefault(x => x.Id == 3)?.PercentualAlvo ?? 40.0m);

                double desvioRF = alvoRF - viewModel.PercentualAtualRendaFixa;
                double desvioFiis = alvoFiis - viewModel.PercentualAtualFiis;
                double desvioAcoes = alvoAcoes - viewModel.PercentualAtualAcoes;

                // 4. Decide a sugestão de aporte
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

            // 5. Salva na ViewModel APENAS as listas de ativos reais que existem na carteira.
            // Assim, o Chart.js na Partial View montará os gráficos com base unicamente na carteira do usuário.
            viewModel.ListaTickersAcoes = acoesNaCarteira.OrderBy(x => x.TipoAcao).ThenBy(x => x.Ticker).ToList();
            viewModel.ListaTickersFiis = fiisNaCarteira.OrderBy(x => x.Segmento).ThenBy(x => x.Ticker).ToList();
            viewModel.ListaTickersGerais = geraisNaCarteira.OrderBy(x => x.ClasseAtivo).ThenBy(x => x.Ticker).ToList();

            return (totalRFLiquido, totalProventosVariaveis);
        }

        private async Task CarregarModulosPainelFinanceiro(decimal totalRF, decimal totalFIIs, decimal totalAcoes, decimal patrimonioTotalReal, string visao)
        {
            decimal patrimonioTotal = totalRF + totalFIIs + totalAcoes;

            // ==========================================
            // METAS DE REBALANCEAMENTO
            // ==========================================
            var metas = await _context.MetasAlocacao.ToListAsync();
            decimal alvoRF = metas.FirstOrDefault(m => m.Categoria == "Renda Fixa")?.PercentualAlvo ?? 40m;
            decimal alvoFIIs = metas.FirstOrDefault(m => m.Categoria == "FIIs")?.PercentualAlvo ?? 40m;
            decimal alvoAcoes = metas.FirstOrDefault(m => m.Categoria == "Ações")?.PercentualAlvo ?? 20m;

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

            // ==========================================
            // AGENDA HISTÓRICA DE PROVENTOS
            // ==========================================
            string mesAnoAtual = DateTime.Now.ToString("MM/yyyy");

            var registroMesAtual = await _context.EvolucaoPatrimonial
                .FirstOrDefaultAsync(e => e.MesAno == mesAnoAtual && e.Dono == visao);

            if (registroMesAtual == null)
            {
                _context.EvolucaoPatrimonial.Add(new EvolucaoPatrimonial { MesAno = mesAnoAtual, PatrimonioLiquido = patrimonioTotalReal, Dono = visao });
            }
            else if (registroMesAtual.PatrimonioLiquido != patrimonioTotalReal)
            {
                registroMesAtual.PatrimonioLiquido = patrimonioTotalReal; // Atualiza se o valor mudou ao longo do mês
            }
            await _context.SaveChangesAsync();

            // 2. Busca o histórico completo ordenado para o gráfico de linha
            if (visao != "Casal")
            {
                var historicoDoBanco = await _context.EvolucaoPatrimonial
                .Where(e => e.Dono == visao)
                .ToListAsync();

                // 2. Ordena cronologicamente quebrando a string "MM/AAAA" (Ano primeiro, depois Mês)
                var historicoOrdenado = historicoDoBanco
                    .OrderByDescending(x => x.MesAno.Substring(3, 4))
                    .ThenByDescending(x => x.MesAno.Substring(0, 2))
                    .Take(12)
                    .ToList();

                // 3. Monta as listas para a View na ordem correta (Esquerda -> Direita)
                List<string> labelsEvolucao = historicoOrdenado.Select(x => x.MesAno).ToList();
                List<decimal> valoresEvolucao = historicoOrdenado.Select(x => x.PatrimonioLiquido).ToList();

                ViewBag.HistoricoEvolucaoLabels = labelsEvolucao;
                ViewBag.HistoricoEvolucaoValores = valoresEvolucao;
            }
            else
            {
                var historicoCasal = await _context.EvolucaoPatrimonial
                .Where(x => x.Dono != "Casal")
                .GroupBy(e => e.MesAno)
                .Select(g => new
                {
                    MesAno = g.Key,
                    PatrimonioSomado = g.Sum(x => x.PatrimonioLiquido)
                })
                .ToListAsync();

                var historicoOrdenado = historicoCasal
                    .OrderByDescending(x => x.MesAno.Substring(3, 4)) // Ordena pelo Ano (ex: "2026")
                    .ThenByDescending(x => x.MesAno.Substring(0, 2))  // Ordena pelo Mês (ex: "01")
                    .Take(12) // Pega os últimos 12 meses da linha do tempo cronológica
                    .ToList();

                // Divide os dados ordenados para as ViewBags do Gráfico
                ViewBag.HistoricoEvolucaoLabels = historicoOrdenado.Select(x => x.MesAno).ToList();
                ViewBag.HistoricoEvolucaoValores = historicoOrdenado.Select(x => x.PatrimonioSomado).ToList();
            }

            List<string> labelsProjecao = new List<string>();
            List<decimal> valoresProjecao = new List<decimal>();

            decimal patrimonioAcumulado = patrimonioTotalReal;
            DateTime dataBaseProjecao = DateTime.Now;

            var transacoesFuturas = await _context.Financeiro
                .Where(t => t.Data >= new DateTime(dataBaseProjecao.Year, dataBaseProjecao.Month, 1))
                .ToListAsync();

            if (visao != "Casal")
            {
                transacoesFuturas = transacoesFuturas.Where(t => t.Dono == visao).ToList();
            }

            for (int i = 1; i <= 12; i++)
            {
                DateTime mesAlvo = dataBaseProjecao.AddMonths(i);
                string labelMesAno = mesAlvo.ToString("MM/yyyy");

                var transacoesDoMes = transacoesFuturas
                    .Where(t => t.Data.Month == mesAlvo.Month && t.Data.Year == mesAlvo.Year)
                    .ToList();

                decimal entradas = transacoesDoMes.Where(t => t.Tipo == "Entrada").Sum(t => t.Valor);
                decimal despesas = transacoesDoMes.Where(t => t.Tipo == "Despesa").Sum(t => t.Valor);

                decimal saldoMes = entradas - despesas;
                patrimonioAcumulado += saldoMes;

                labelsProjecao.Add(labelMesAno);
                valoresProjecao.Add(patrimonioAcumulado);
            }

            ViewBag.ProjecaoFuturaLabels = labelsProjecao;
            ViewBag.ProjecaoFuturaValores = valoresProjecao;
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
                .Select(g => new ResumoMesViewModel
                {
                    Ano = g.Key.Year,
                    Mes = g.Key.Month,
                    Entradas = g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor),
                    Saidas = g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor)
                })
                .OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();

            viewModel.EntradasMesCorrente = financeiroData.Where(x => x.Data.Month == agora.Month && x.Data.Year == agora.Year && x.Tipo == "Entrada").Sum(x => x.Valor);
            viewModel.SaidasMesCorrente = financeiroData.Where(x => x.Data.Month == agora.Month && x.Data.Year == agora.Year && x.Tipo == "Despesa").Sum(x => x.Valor);

            return financeiroData.Where(x => x.Data.Date <= agora.Date).Sum(x => x.Tipo == "Entrada" ? x.Valor : -x.Valor);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarParametros(decimal cdiAnual)
        {
            var parametro = await _context.Parametro.FirstOrDefaultAsync();

            if (parametro == null)
            {
                parametro = new Parametro();
                _context.Parametro.Add(parametro);
            }

            // Atualiza os dados
            parametro.CdiAnual = cdiAnual;
            parametro.DataAtualizacao = DateTime.Now;

            await _context.SaveChangesAsync();

            // Redireciona de volta para atualizar os cálculos na tela
            return RedirectToAction("Index");
        }
        private async Task<decimal> CalcularRendimentoCaixinhaCorrente(decimal saldoFinanceiroAteHoje, CarteiraTotalViewModel viewModel)
        {
            var parametro = await _context.Parametro.FirstOrDefaultAsync();
            decimal taxaAnualCaixinhaAtual = parametro?.CdiAnual ?? 14.75m;
            decimal taxaMensalCaixinhaAtual = (taxaAnualCaixinhaAtual / 12) / 100;
            decimal rendimentoLiquidoCaixinha = (saldoFinanceiroAteHoje * taxaMensalCaixinhaAtual) * 0.825m;

            viewModel.RendaMensalTotalConsolidada = viewModel.TotalRendaMensalEstimada + viewModel.RendaFixaMensalLiquida + rendimentoLiquidoCaixinha;
            return rendimentoLiquidoCaixinha;
        }

        private async Task ProcessarAutomacoesFechamento(List<Financeiro> financeiroData, decimal saldoFinanceiroAteHoje, decimal totalProventosVariaveis, DateTime agora, string visao)
        {
            DateTime mesAnterior = agora.AddMonths(-1);
            bool mudouBanco = false;

            // --- AUTOMAÇÃO A: CAIXINHAS / RENDA FIXA ---
            string descricaoRendimentoNode = $"Rendimento Automático Caixinhas - {mesAnterior:MM/yyyy}";
            if (!financeiroData.Any(x => x.Descricao == descricaoRendimentoNode))
            {
                DateTime ultimoSegundoMesAnterior = new DateTime(agora.Year, agora.Month, 1).AddSeconds(-1);
                decimal saldoFinalMesAnterior = financeiroData.Where(x => x.Data.Date <= ultimoSegundoMesAnterior.Date).Sum(x => x.Tipo == "Entrada" ? x.Valor : -x.Valor);

                if (saldoFinalMesAnterior > 0)
                {
                    var parametro = await _context.Parametro.FirstOrDefaultAsync();
                    decimal cdiAnual = parametro?.CdiAnual ?? 14.75m;
                    decimal taxaMensalCaixinha = (cdiAnual / 12) / 100;
                    decimal rendimentoLiquido = (saldoFinalMesAnterior * taxaMensalCaixinha) * 0.825m;

                    if (rendimentoLiquido > 0.01m)
                    {
                        var novoLancamento = new Financeiro
                        {
                            Descricao = descricaoRendimentoNode,
                            Valor = Math.Round(rendimentoLiquido, 2),
                            Data = new DateTime(agora.Year, agora.Month, 1).AddDays(-1),
                            Tipo = "Entrada",
                            Categoria = "Investimento",
                            Pagamento = "Pix",
                            Dono = visao == "Casal" ? "Casal" : visao
                        };
                        _context.Financeiro.Add(novoLancamento);
                        financeiroData.Add(novoLancamento);
                        mudouBanco = true;
                    }
                }
            }

            // --- AUTOMAÇÃO B: DIVIDENDOS / RENDA VARIÁVEL ---
            string descricaoRendimentoVariavel = $"Rendimento Automático Renda Variável - {mesAnterior:MM/yyyy}";
            if (!financeiroData.Any(x => x.Descricao == descricaoRendimentoVariavel) && totalProventosVariaveis > 0.01m)
            {
                var novoLancamentoVariavel = new Financeiro
                {
                    Descricao = descricaoRendimentoVariavel,
                    Valor = Math.Round(totalProventosVariaveis, 2),
                    Data = new DateTime(agora.Year, agora.Month, 1).AddDays(-1),
                    Tipo = "Entrada",
                    Categoria = "Investimento",
                    Pagamento = "Pix",
                    Dono = visao == "Casal" ? "Casal" : visao
                };
                _context.Financeiro.Add(novoLancamentoVariavel);
                financeiroData.Add(novoLancamentoVariavel);
                mudouBanco = true;
            }

            if (mudouBanco)
            {
                await _context.SaveChangesAsync();
            }
        }

        private void ProcessarLancamentosFuturos(List<Financeiro> financeiroData, DateTime agora, CarteiraTotalViewModel viewModel)
        {
            viewModel.EntradasFuturas = financeiroData.Where(x => x.Data.Date > agora.Date && x.Tipo == "Entrada").Sum(x => x.Valor);
            viewModel.SaidasFuturas = financeiroData.Where(x => x.Data.Date > agora.Date && x.Tipo == "Despesa").Sum(x => x.Valor);
        }

        private async Task ProcessarRadarAportes(CarteiraTotalViewModel viewModel)
        {
            var recomendacoesAcoes = await _context.Recomendacao.ToListAsync();
            var acoesBaratas = recomendacoesAcoes
                .Where(x => x.LPA > 0 && (x.PrecoAtual / x.LPA) < 10 && x.Roe > 12)
                .OrderBy(x => x.PrecoAtual / x.LPA).Take(5)
                .Select(x => new RadarAporteViewModel
                {
                    Ticker = x.Ticker,
                    Tipo = "Ação",
                    PrecoAtual = x.PrecoAtual,
                    IndicadorDesconto = x.LPA > 0 ? x.PrecoAtual / x.LPA : 0,
                    Mensagem = "P/L Atrativo + ROE Eficiente"
                }).ToList();

            var recomendacoesFiis = await _context.RecomendacaoFii.ToListAsync();
            var fiisDescontados = recomendacoesFiis
                .Where(x => x.PVP < 0.98m)
                .OrderBy(x => x.PVP).Take(5)
                .Select(x => new RadarAporteViewModel
                {
                    Ticker = x.Ticker,
                    Tipo = "FII",
                    PrecoAtual = x.PrecoAtual,
                    IndicadorDesconto = x.PVP,
                    Mensagem = "Desconto sobre Valor Patrimonial"
                }).ToList();

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

            var parcelaAtual = simulacaoCompleta.FirstOrDefault(p => p.Data.Month == agora.Month && p.Data.Year == agora.Year);
            viewModel.SaldoDevedorAtual = parcelaAtual?.SaldoDevedorRestante ?? financiamento.SaldoDevedorInicial;
            viewModel.PrazoMesesRestantes = simulacaoCompleta.Count(p => p.SaldoDevedorRestante > 0);
        }

        private async Task CarregarDadosAuxiliares(CarteiraTotalViewModel viewModel, string visao)
        {
            //viewModel.MetaAlocacao = await _context.MetasAlocacao.FirstOrDefaultAsync();
            var todasAsMetas = await _context.MetasAlocacao.ToListAsync();

            // Salva na ViewBag para usarmos no método de processamento e na View
            ViewBag.MetasAlocacaoLista = todasAsMetas;

            // Deixa o objeto do Model quieto para não quebrar o resto do código
            viewModel.MetaAlocacao = todasAsMetas.FirstOrDefault() ?? new MetaAlocacao();

            viewModel.ConfiguracaoBackups = await _context.ConfiguracaoBackups.FirstOrDefaultAsync();

            viewModel.Parametro = await _context.Parametro.FirstOrDefaultAsync();

            viewModel.HistoricoTransacoes = await _context.HistoricoAtivos
                .Where(x => x.Dono == visao || visao == "Casal")
                .OrderByDescending(x => x.DataOperacao)
                .ToListAsync();

            viewModel.HistoricoFolhas = await _context.FolhasPagamento
                .Where(f => f.Visao == visao)
                .OrderByDescending(f => f.Ano).ThenByDescending(f => f.Mes).ToListAsync();

            //viewModel.ListaTickersAcoes = await _context.Recomendacao.Select(x => x.Ticker).ToListAsync();
            //viewModel.ListaTickersFiis = await _context.RecomendacaoFii.Select(x => x.Ticker).ToListAsync();
            //viewModel.ListaTickersGerais = await _context.AtivosGerais.Select(x => x.Ticker).ToListAsync();

            viewModel.ListaTickersAcoes = await _context.Recomendacao
                .OrderBy(x => x.TipoAcao).ThenBy(x => x.Ticker).ToListAsync();

            viewModel.ListaTickersFiis = await _context.RecomendacaoFii
                .OrderBy(x => x.Segmento).ThenBy(x => x.Ticker).ToListAsync();


            viewModel.ListaTickersGerais = await _context.AtivosGerais
                .OrderBy(x => x.ClasseAtivo).ThenBy(x => x.Ticker).ToListAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ComprarAtivo(int id, int quantidadeComprada, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (quantidadeComprada <= 0) return RedirectToAction("Index", new { visao = visao });

            var ativo = await _context.Carteira.FindAsync(id);
            if (ativo == null) return NotFound();

            decimal precoExecucao = 0;
            if (ativo.TipoAtivo == "Fii")
            {
                var fii = await _context.RecomendacaoFii.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                if (fii != null) precoExecucao = fii.PrecoAtual;
            }
            else if (ativo.TipoAtivo == "Acao")
            {
                var acao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                if (acao != null) precoExecucao = acao.PrecoAtual;
            }
            if (precoExecucao == 0) precoExecucao = ativo.PrecoMedio;

            decimal valorTotalAporte = quantidadeComprada * precoExecucao;
            DateTime dataHoje = DateTime.Now;

            decimal custoTotalAntigo = ativo.Quantidade * ativo.PrecoMedio;
            ativo.Quantidade += quantidadeComprada;
            ativo.PrecoMedio = (custoTotalAntigo + valorTotalAporte) / ativo.Quantidade;
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
            var registro = await _context.HistoricoAtivos.FindAsync(id);

            if (registro == null)
            {
                return NotFound();
            }

            _context.HistoricoAtivos.Remove(registro);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarRendimentoInline(int id, decimal novoValor)
        {
            var itemCarteira = await _context.Carteira.FindAsync(id);

            if (itemCarteira == null)
            {
                return NotFound("Item não encontrado na carteira.");
            }

            var recomendacaoFii = await _context.RecomendacaoFii
                .FirstOrDefaultAsync(x => x.Ticker == itemCarteira.Ticker);

            if (recomendacaoFii == null)
            {
                return NotFound($"Tabela de recomendações não contém o ticker {itemCarteira.Ticker}.");
            }

            recomendacaoFii.UltimoRendimento = novoValor;

            _context.RecomendacaoFii.Update(recomendacaoFii);
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
            if (ativo == null) return NotFound();
            if (quantidadeVendida > ativo.Quantidade) return BadRequest("Quantidade insuficiente para venda.");

            decimal precoExecucao = 0;
            if (ativo.TipoAtivo == "Fii")
            {
                var fii = await _context.RecomendacaoFii.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                if (fii != null) precoExecucao = fii.PrecoAtual;
            }
            else if (ativo.TipoAtivo == "Acao")
            {
                var acao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                if (acao != null) precoExecucao = acao.PrecoAtual;
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

            string? caminhoSalvo = null;
            if (pdfFile != null && pdfFile.Length > 0)
            {
                if (!pdfFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("", "Apenas arquivos no formato PDF são permitidos.");
                    return RedirectToAction("Index", new { visao = visao }); // Ou retorne para a view com erro
                }

                string pastaDestino = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracheques");
                if (!Directory.Exists(pastaDestino))
                {
                    Directory.CreateDirectory(pastaDestino);
                }

                string nomeArquivo = $"{visao.ToLower()}_{ano}_{mes}_{Guid.NewGuid().ToString().Substring(0, 8)}.pdf";
                string caminhoCompleto = Path.Combine(pastaDestino, nomeArquivo);

                using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
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

            return RedirectToAction("Index", new { visao = visao });
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
                    valorConvertido = Convert.ToDecimal(valorTratado, culturaBr) / 100m;
                }
                else
                {
                    valorConvertido = Convert.ToDecimal(valorTratado, culturaBr);
                }
            }

            if (valorConvertido > 0)
            {
                var financiamento = await _context.Financiamentos.FirstOrDefaultAsync();

                if (financiamento != null)
                {
                    DateTime dataInicioContrato = financiamento.DataInicio;
                    DateTime dataAtual = DateTime.Now;

                    int mesesDeDiferenca = ((dataAtual.Year - dataInicioContrato.Year) * 12) + dataAtual.Month - dataInicioContrato.Month;

                    int parcelaContratualCorreta = mesesDeDiferenca + 1;

                    var novoAporte = new AporteExtra
                    {
                        PriceId = priceId,
                        MesReferencia = parcelaContratualCorreta,
                        Valor = valorConvertido
                    };

                    _context.Add(novoAporte);
                    await _context.SaveChangesAsync();
                }
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

                if (decimal.TryParse(novoMontante, System.Globalization.NumberStyles.Any, culturaBR, out decimal montanteDecimal))
                {
                    ativo.PrecoMedio = montanteDecimal;
                }

                if (decimal.TryParse(novaTaxa, System.Globalization.NumberStyles.Any, culturaBR, out decimal taxaDecimal))
                {
                    ativo.TaxaRentabilidade = taxaDecimal;
                }

                _context.Update(ativo);
                await _context.SaveChangesAsync();
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

            var ativoExistente = await _context.Carteira.FirstOrDefaultAsync(x => x.Ticker == ticker && x.Dono == visao);

            if (ativoExistente != null)
            {
                int qtdAnterior = ativoExistente.Quantidade;
                decimal pmAnterior = ativoExistente.PrecoMedio;
                int quantidadeTotal = qtdAnterior + quantidade;

                decimal novoPrecoMedio = ((qtdAnterior * pmAnterior) + (quantidade * precoMedio)) / quantidadeTotal;

                ativoExistente.Quantidade = quantidadeTotal;
                ativoExistente.PrecoMedio = Math.Round(novoPrecoMedio, 2);

                _context.Update(ativoExistente);
            }
            else
            {
                string tipo = "Geral";
                if (await _context.Recomendacao.AnyAsync(x => x.Ticker == ticker)) tipo = "Acao";
                else if (await _context.RecomendacaoFii.AnyAsync(x => x.Ticker == ticker)) tipo = "Fii";
                else if (ticker.Contains("CDB") || ticker.Contains("TESOURO") || ticker.Contains("LCI") || ticker.Contains("LCA")) tipo = "RendaFixa";

                var novoItem = new Carteira
                {
                    Ticker = ticker,
                    Quantidade = quantidade,
                    PrecoMedio = precoMedio,
                    TipoAtivo = tipo,
                    TaxaRentabilidade = taxaRentabilidade,
                    DataCompra = dataHoje,
                    Dono = visao
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
                Dono = visao == "Casal" ? "Casal" : visao
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
                Dono = visao == "Casal" ? "Casal" : visao
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
        public async Task<IActionResult> AdicionarRendaFixa(Carteira novoItem, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            if (string.IsNullOrEmpty(novoItem.Dono)) novoItem.Dono = visao;

            ModelState.Remove("Dono");

            if (ModelState.IsValid)
            {
                novoItem.TipoAtivo = "RendaFixa";
                novoItem.DataCompra = DateTime.Now;

                _context.Carteira.Add(novoItem);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { visao = novoItem.Dono });
        }
        [HttpPost]
        public async Task<IActionResult> SalvarConfiguracaoBackup(string caminhoPastaLocal, int intervaloHoras)
        {
            var config = await _context.ConfiguracaoBackups.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new ConfiguracaoBackup();
                _context.ConfiguracaoBackups.Add(config);
            }

            config.CaminhoPastaLocal = caminhoPastaLocal;
            config.IntervaloHoras = intervaloHoras;

            await _context.SaveChangesAsync();
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
            if (string.IsNullOrEmpty(carteira.Dono)) carteira.Dono = visao;

            ModelState.Remove("Dono");

            if (ModelState.IsValid)
            {
                _context.Add(carteira);
                await _context.SaveChangesAsync();
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

            var carteira = await _context.Carteira.FindAsync(id);
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

                    _context.Update(registroBanco);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CarteiraExists(carteira.Id)) return NotFound();
                    else throw;
                }
            }

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        public async Task<IActionResult> Delete(int? id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            if (id == null) return NotFound();

            var carteira = await _context.Carteira.FirstOrDefaultAsync(m => m.Id == id);
            if (carteira == null) return NotFound();

            return View(carteira);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var carteira = await _context.Carteira.FindAsync(id);
            if (carteira != null)
            {
                _context.Carteira.Remove(carteira);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        private bool CarteiraExists(int id)
        {
            return _context.Carteira.Any(e => e.Id == id);
        }

        public async Task<IActionResult> BaixarExcel(string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var query = _context.Carteira.AsQueryable();
            if (visao == "Gabriel") query = query.Where(x => x.Dono == "Gabriel" || x.Dono == "Casal");
            else if (visao == "Ela") query = query.Where(x => x.Dono == "Ela" || x.Dono == "Casal");

            var lancamentos = await query.ToListAsync();
            var csv = "Id,Ticker,Quantidade,PrecoMedio,TipoAtivo,Setor,DataCompra,Dono\n" +
                      string.Join("\n", lancamentos.Select(x =>
                          $"{x.Id},{x.Ticker},{x.Quantidade},{x.PrecoMedio},{x.TipoAtivo},{x.Setor},{x.DataCompra:yyyy-MM-dd},{x.Dono}"));

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", $"carteira_{visao.ToLower()}.csv");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirFolhaPagamento(int id, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            var folha = await _context.FolhasPagamento.FindAsync(id);

            if (folha != null)
            {
                if (!string.IsNullOrEmpty(folha.PathPdf))
                {
                    string caminhoArquivoFisico = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folha.PathPdf.TrimStart('/'));

                    if (System.IO.File.Exists(caminhoArquivoFisico))
                    {
                        System.IO.File.Delete(caminhoArquivoFisico);
                    }
                }

                // 2. Remove o registro do banco de dados
                _context.FolhasPagamento.Remove(folha);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", new { visao = visao });
        }
        [HttpPost]
        public async Task<IActionResult> SalvarMetasAlocacao(decimal percentualRF, decimal percentualFIIs, decimal percentualAcoes)
        {
            if ((percentualRF + percentualFIIs + percentualAcoes) != 100m)
                return BadRequest("A soma das alocações deve ser exatamente 100%.");

            // Busca as metas existentes para atualizar ou cria novas
            var todasMetas = await _context.MetasAlocacao.ToListAsync();

            AtualizarOuCriarMeta(todasMetas, "Renda Fixa", percentualRF);
            AtualizarOuCriarMeta(todasMetas, "FIIs", percentualFIIs);
            AtualizarOuCriarMeta(todasMetas, "Ações", percentualAcoes);

            await _context.SaveChangesAsync();
            return RedirectToAction("Index"); // Redireciona de volta para seu painel principal
        }

        private void AtualizarOuCriarMeta(List<MetaAlocacao> lista, string categoria, decimal valor)
        {
            var meta = lista.FirstOrDefault(m => m.Categoria == categoria);
            if (meta == null)
            {
                _context.MetasAlocacao.Add(new MetaAlocacao { Categoria = categoria, PercentualAlvo = valor });
            }
            else
            {
                meta.PercentualAlvo = valor;
            }
        }
    }
}
