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
            // 1. Define o perfil padrão e joga para a ViewBag manter o estado no Layout global
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            int filtroMes = mes ?? DateTime.Now.Month;
            int filtroAno = ano ?? DateTime.Now.Year;

            DateTime dozeMesesAtras = DateTime.Now.AddMonths(-12);
            DateTime primeiroDiaMesAtual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            // 2. Prepara as consultas em modo IQueryable para aplicar o isolamento de perfis antes de ir ao banco
            var queryFinanceiro = _context.Financeiro.AsQueryable();
            var queryContasFixas = _context.ContasFixas.AsQueryable();
            var queryCarteira = _context.Carteira.AsQueryable();

            // 3. Aplica a Regra de Negócio de Isolamento (Individual vs Compartilhado)
            if (visao == "Gabriel")
            {
                queryFinanceiro = queryFinanceiro.Where(x => x.Dono == "Gabriel" || x.Dono == "Casal");
                queryContasFixas = queryContasFixas.Where(x => x.Dono == "Gabriel" || x.Dono == "Casal");
                queryCarteira = queryCarteira.Where(x => x.Dono == "Gabriel" || x.Dono == "Casal");
            }
            else if (visao == "Ela")
            {
                queryFinanceiro = queryFinanceiro.Where(x => x.Dono == "Ela" || x.Dono == "Casal");
                queryContasFixas = queryContasFixas.Where(x => x.Dono == "Ela" || x.Dono == "Casal");
                queryCarteira = queryCarteira.Where(x => x.Dono == "Ela" || x.Dono == "Casal");
            }
            // No modo "Casal", as consultas não recebem filtros de Dono, consolidando tudo

            // 4. Executa as buscas filtradas por período e escopo de perfil
            var lancamentos = await queryFinanceiro
                .Where(x => x.Data.Month == filtroMes && x.Data.Year == filtroAno)
                .ToListAsync() ?? new List<Financeiro>();

            var contasFixas = await queryContasFixas.ToListAsync();

            var ativosRF = await queryCarteira
                .Where(x => x.TipoAtivo == "RendaFixa")
                .ToListAsync();

            var historico = await queryFinanceiro
                .Where(x => x.Data >= dozeMesesAtras && x.Data < primeiroDiaMesAtual)
                .ToListAsync();

            // 5. Busca o Financiamento (Global do casal)
            var financiamento = await _context.Financiamentos
                .Include(f => f.AportesPontuais)
                .FirstOrDefaultAsync();

            decimal valorParcelaFinal = 0;
            decimal totalAmortizadoNoMes = 0;

            if (financiamento != null)
            {
                var projecao = _service.GerarSimulacao(financiamento);
                var parcelaDoMes = projecao.FirstOrDefault(p => p.Data.Month == filtroMes && p.Data.Year == filtroAno);

                if (parcelaDoMes != null)
                {
                    valorParcelaFinal = parcelaDoMes.ValorParcela;
                    totalAmortizadoNoMes = parcelaDoMes.Amortizacao;
                }
            }

            // 6. Cruzamento de Contas Pagas (Baseado estritamente nos lançamentos da visão ativa)
            foreach (var conta in contasFixas)
            {
                conta.PagoNoMesAtual = lancamentos.Any(l =>
                    l.Descricao.Contains(conta.Descricao) && l.Tipo == "Despesa");
            }

            bool parcelaPaga = lancamentos.Any(l =>
                (l.Descricao.Contains("Financiamento") || l.Categoria == "Moradia") && l.Tipo == "Despesa");

            // 7. Cálculo do Pendente e totais do lar com base no escopo ativo
            decimal pendente = contasFixas.Where(c => !c.PagoNoMesAtual).Sum(c => c.Valor);
            if (!parcelaPaga) pendente += valorParcelaFinal;

            decimal totalPagoCasa = lancamentos
                .Where(l => l.Tipo == "Despesa" && (l.Categoria == "Moradia" || l.Categoria == "Serviços" || l.Categoria == "Farmácia"))
                .Sum(l => l.Valor);

            // 8. Cálculo de Rendimento Líquido de Renda Fixa Isolado
            decimal totalRendimentoliquido = 0;
            foreach (var rf in ativosRF)
            {
                decimal taxaMensal = (rf.TaxaRentabilidade ?? 0) / 12 / 100;
                totalRendimentoliquido += (rf.Quantidade * rf.PrecoMedio) * taxaMensal * 0.825m;
            }

            // 9. Cálculo da Média de Sobra Histórica com base no dono selecionado
            decimal mediaSobraHistorica = 0;
            if (historico.Any())
            {
                var mesesAgrupados = historico
                    .GroupBy(x => new { x.Data.Month, x.Data.Year })
                    .Select(g => new
                    {
                        Sobra = g.Where(x => x.Tipo == "Entrada").Sum(x => x.Valor) -
                                g.Where(x => x.Tipo == "Despesa").Sum(x => x.Valor)
                    })
                    .ToList();

                mediaSobraHistorica = mesesAgrupados.Average(m => m.Sobra);
            }

            // 10. Alimenta a ViewModel final
            var viewModel = new FluxoCaixaViewModel
            {
                Lancamentos = lancamentos,
                MesAtual = filtroMes,
                AnoAtual = filtroAno,
                RendimentoRendaFixaMes = totalRendimentoliquido,
                ContasFixas = contasFixas,
                ValorParcelaAtual = valorParcelaFinal,
                ParcelaPaga = parcelaPaga,
                TotalAmortizacaoMes = totalAmortizadoNoMes,
                TotalPagoCasa = totalPagoCasa,
                TotalPendenteCasa = pendente,
                MediaSobraHistorica = mediaSobraHistorica
            };

            return View(viewModel);
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

            if (ModelState.IsValid)
            {
                try
                {
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