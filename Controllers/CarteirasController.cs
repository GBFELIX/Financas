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
            decimal rendimentoLiquidoCaixinha = CalcularRendimentoCaixinhaCorrente(saldoFinanceiroAteHoje, viewModel);

            // 5. Execução de Automações e Gravação de Fechamentos no Banco
            await ProcessarAutomacoesFechamento(financeiroData, saldoFinanceiroAteHoje, totalProventosVariaveis, agora, visao);

            // 6. Consolidação de Balanços Futuros e Consolidação Patrimonial
            ProcessarLancamentosFuturos(financeiroData, agora, viewModel);
            viewModel.PatrimonioTotalReal = viewModel.TotalPatrimonio + viewModel.TotalInvestidoRendaFixa + saldoFinanceiroAteHoje + rendimentoLiquidoCaixinha;

            // 7. Radar de Aportes (Sugestões de Ações e FIIs Baratos)
            await ProcessarRadarAportes(viewModel);

            // 8. Processamento do Financiamento Imobiliário e Dados Auxiliares
            ProcessarFinanciamentoImobiliario(financiamento, agora, viewModel);
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
                        decimal pl = acao.LPA > 0 ? acao.PrecoAtual / acao.LPA : 0;
                        decimal pvp = acao.VPA > 0 ? acao.PrecoAtual / acao.VPA : 0;

                        if (pl > 0 && pl < 10 && acao.Roe > 12) { viewItem.Recomendacao = "Forte Compra (Barata + ROE Alto)"; viewItem.CorBadge = "success"; }
                        else if (pvp < 1.5m && pl < 15) { viewItem.Recomendacao = "Compra (Preço Justo)"; viewItem.CorBadge = "primary"; }
                        else if (pl > 20 || pvp > 3.0m) { viewItem.Recomendacao = "Venda / Caro"; viewItem.CorBadge = "danger"; }
                        else { viewItem.Recomendacao = "Neutro / Manter"; viewItem.CorBadge = "secondary"; }
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
                    }
                }
                else if (item.TipoAtivo == "Geral")
                {
                    var ativoGeral = await _context.AtivosGerais.FirstOrDefaultAsync(x => x.Ticker == item.Ticker);
                    if (ativoGeral != null)
                    {
                        viewItem.PrecoAtual = ativoGeral.PrecoAtual;
                        viewItem.Recomendacao = "Não Avaliado";
                    }
                }

                viewModel.Itens.Add(viewItem);
                totalProventosVariaveis += viewItem.ProventoMensalEstimado;
            }

            return (totalRFLiquido, totalProventosVariaveis);
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

        private decimal CalcularRendimentoCaixinhaCorrente(decimal saldoFinanceiroAteHoje, CarteiraTotalViewModel viewModel)
        {
            decimal taxaAnualCaixinhaAtual = 10.75m;
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
                    decimal taxaMensalCaixinha = (decimal)(Math.Pow(1 + (14.50 / 100.0), 1.0 / 12.0) - 1);
                    decimal rendimentoLiquido = (saldoFinalMesAnterior * taxaMensalCaixinha) * 0.800m;

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
            viewModel.HistoricoFolhas = await _context.FolhasPagamento
                .Where(f => f.Visao == visao)
                .OrderByDescending(f => f.Ano).ThenByDescending(f => f.Mes).ToListAsync();

            viewModel.ListaTickersAcoes = await _context.Recomendacao.Select(x => x.Ticker).ToListAsync();
            viewModel.ListaTickersFiis = await _context.RecomendacaoFii.Select(x => x.Ticker).ToListAsync();
            viewModel.ListaTickersGerais = await _context.AtivosGerais.Select(x => x.Ticker).ToListAsync();
        }
        [HttpPost]
        public async Task<IActionResult> ComprarAtivo(int id, int quantidadeComprada, string visao)
        {
            if (quantidadeComprada <= 0) return RedirectToAction(nameof(Index), new { visao = visao });

            var ativo = await _context.Carteira.FindAsync(id);
            if (ativo == null) return NotFound();

            // 1. Puxa a cotação de mercado atualizada da tabela de referências
            decimal precoAtualMercado = 0;
            if (ativo.TipoAtivo == "Fii")
            {
                var fii = await _context.RecomendacaoFii.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                if (fii != null) precoAtualMercado = fii.PrecoAtual;
            }
            else if (ativo.TipoAtivo == "Acao")
            {
                var acao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                if (acao != null) precoAtualMercado = acao.PrecoAtual;
            }

            if (precoAtualMercado == 0) precoAtualMercado = ativo.PrecoMedio; // Fallback de proteção

            // 2. Executa o cálculo ponderado de preço médio no C# antes de gravar
            decimal custoTotalAntigo = ativo.Quantidade * ativo.PrecoMedio;
            decimal custoNovoAporte = quantidadeComprada * precoAtualMercado;

            ativo.Quantidade += quantidadeComprada;
            ativo.PrecoMedio = (custoTotalAntigo + custoNovoAporte) / ativo.Quantidade;

            _context.Update(ativo);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { visao = visao });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VenderAtivo(int id, int quantidadeVendida, string visao)
        {
            {
                if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

                var ativo = await _context.Carteira.FindAsync(id);
                if (ativo == null || quantidadeVendida <= 0 || quantidadeVendida > ativo.Quantidade) if (ativo == null || quantidadeVendida <= 0 || quantidadeVendida > ativo.Quantidade)
                    {
                        TempData["Erro"] = "Quantidade inválida para venda ou ativo não encontrado.";
                        return RedirectToAction(nameof(Index), new { visao = visao });
                    }

                decimal precoAtual = 0;

                if (ativo.TipoAtivo == "Acao")
                {
                    var recomendacao = await _context.Recomendacao.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                    precoAtual = recomendacao?.PrecoAtual ?? ativo.PrecoMedio;
                }
                else if (ativo.TipoAtivo == "Fii")
                {
                    var recomendacaoFii = await _context.RecomendacaoFii.FirstOrDefaultAsync(x => x.Ticker == ativo.Ticker);
                    precoAtual = recomendacaoFii?.PrecoAtual ?? ativo.PrecoMedio;
                }
                else
                {
                    precoAtual = ativo.PrecoMedio;
                }

                decimal resultadoPorCota = precoAtual - ativo.PrecoMedio;
                decimal resultadoTotal = resultadoPorCota * quantidadeVendida;

                if (Math.Abs(resultadoTotal) >= 0.01m)
                {
                    var novoLancamento = new Financeiro
                    {
                        Data = DateTime.Now,
                        Categoria = "Investimento",
                        Pagamento = "Pix",
                        Dono = ativo.Dono,
                        Valor = Math.Round(Math.Abs(resultadoTotal), 2)
                    };

                    if (resultadoTotal > 0)
                    {
                        novoLancamento.Descricao = $"Ganho de Capital - Venda {ativo.Ticker} ({quantidadeVendida} qts)";
                        novoLancamento.Tipo = "Entrada";
                    }
                    else
                    {
                        novoLancamento.Descricao = $"Prejuízo de Capital - Venda {ativo.Ticker} ({quantidadeVendida} qts)";
                        novoLancamento.Tipo = "Despesa";
                    }

                    _context.Financeiro.Add(novoLancamento);
                }

                // 4. Atualiza ou remove o ativo da Carteira
                if (quantidadeVendida == ativo.Quantidade)
                {
                    _context.Carteira.Remove(ativo);
                }
                else
                {
                    ativo.Quantidade -= quantidadeVendida; ativo.Quantidade -= quantidadeVendida;
                    _context.Carteira.Update(ativo);
                }

                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"Venda de {quantidadeVendida} cotas de {ativo.Ticker} processada com sucesso!";

                return RedirectToAction(nameof(Index), new { visao = visao });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarFolhaPagamento(int ano, int mes, decimal salarioBruto, decimal descontos, IFormFile? pdfFile, string visao)
        {
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";

            // 1. Processamento e Upload Seguro do PDF
            string? caminhoSalvo = null;
            if (pdfFile != null && pdfFile.Length > 0)
            {
                // Verifica se é realmente um arquivo PDF
                if (!pdfFile.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("", "Apenas arquivos no formato PDF são permitidos.");
                    return RedirectToAction("Index", new { visao = visao }); // Ou retorne para a view com erro
                }

                // Define a pasta de destino dentro da estrutura do projeto (wwwroot/uploads/contracheques)
                string pastaDestino = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracheques");
                if (!Directory.Exists(pastaDestino))
                {
                    Directory.CreateDirectory(pastaDestino);
                }

                // Cria um nome de arquivo único para evitar substituições acidentais
                string nomeArquivo = $"{visao.ToLower()}_{ano}_{mes}_{Guid.NewGuid().ToString().Substring(0, 8)}.pdf";
                string caminhoCompleto = Path.Combine(pastaDestino, nomeArquivo);

                // Salva o arquivo fisicamente no servidor
                using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                // Caminho relativo que será guardado no banco e usado nas tags <a> do HTML
                caminhoSalvo = $"/uploads/contracheques/{nomeArquivo}";
            }

            // 2. Criação do Registro para Persistência
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

                    //Calcula a distância exata de meses entre a assinatura e o dia de hoje
                    int mesesDeDiferenca = ((dataAtual.Year - dataInicioContrato.Year) * 12) + dataAtual.Month - dataInicioContrato.Month;

                    int parcelaContratualCorreta = mesesDeDiferenca + 1;

                    var novoAporte = new AporteExtra
                    {
                        PriceId = priceId,
                        MesReferencia = parcelaContratualCorreta, // Usa o cálculo dinâmico baseado no banco (Ex: 12)
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
                else if (ticker.ToUpper().Contains("CDB") || ticker.ToUpper().Contains("TESOURO")) tipo = "RendaFixa";

                var novoItem = new Carteira
                {
                    Ticker = ticker,
                    Quantidade = quantidade,
                    PrecoMedio = precoMedio,
                    TipoAtivo = tipo,
                    TaxaRentabilidade = taxaRentabilidade,
                    DataCompra = DateTime.Now,
                    Dono = visao
                };
                _context.Add(novoItem);
            }

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

            // Redireciona com precisão mantendo o filtro de perfil ativo
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

            // Busca o registro no banco de dados
            var folha = await _context.FolhasPagamento.FindAsync(id);

            if (folha != null)
            {
                // 1. Se houver um PDF salvo, apaga o arquivo físico do servidor
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

            // Redireciona de volta para a Index mantendo o perfil selecionado
            return RedirectToAction("Index", new { visao = visao });
        }
    }
}
