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
        public async Task<IActionResult> Index(int? mes, int? ano)
        {
            int filtroMes = mes ?? DateTime.Now.Month;
            int filtroAno = ano ?? DateTime.Now.Year;

            // 1. Lançamentos do mês
            var lancamentos = await _context.Financeiro
                .Where(x => x.Data.Month == filtroMes && x.Data.Year == filtroAno)
                .ToListAsync() ?? new List<Financeiro>();

            // 2. Busca as Contas Fixas
            var contasFixas = await _context.ContasFixas.ToListAsync();

            // 3. Busca o Financiamento COM OS APORTES (O Pulo do Gato está no Include)
            var financiamento = await _context.Financiamentos
                .Include(f => f.AportesPontuais) // Carrega os aportes extras para o service ver
                .FirstOrDefaultAsync();

            decimal valorParcelaFinal = 0;
            decimal totalAmortizadoNoMes = 0;

            if (financiamento != null)
            {
                var projecao = _service.GerarSimulacao(financiamento);
                var parcelaDoMes = projecao.FirstOrDefault(p => p.Data.Month == filtroMes && p.Data.Year == filtroAno);

                if (parcelaDoMes != null)
                {
                    // ValorParcela aqui já é: Prestação + Juros + Aportes Extras (do seu Service)
                    valorParcelaFinal = parcelaDoMes.ValorParcela;

                    // TotalAmortizacaoMes pode ser usado para mostrar o "extra" na tela
                    totalAmortizadoNoMes = parcelaDoMes.Amortizacao;
                }
            }

            // 4. Lógica de Cruzamento
            foreach (var conta in contasFixas)
            {
                conta.PagoNoMesAtual = lancamentos.Any(l =>
                    l.Descricao.Contains(conta.Descricao) && l.Tipo == "Despesa");
            }

            bool parcelaPaga = lancamentos.Any(l =>
                (l.Descricao.Contains("Financiamento") || l.Categoria == "Moradia") && l.Tipo == "Despesa");

            // 5. Cálculo do Pendente (Usando o valor TOTAL que o Service deu)
            decimal pendente = contasFixas.Where(c => !c.PagoNoMesAtual).Sum(c => c.Valor);
            if (!parcelaPaga) pendente += valorParcelaFinal;

            // 6. Cálculo do Total Pago Casa
            decimal totalPagoCasa = lancamentos
                .Where(l => l.Tipo == "Despesa" && (l.Categoria == "Moradia" || l.Categoria == "Serviços" || l.Categoria == "Farmácia"))
                .Sum(l => l.Valor);

            // Renda Fixa (seu código original)
            var ativosRF = await _context.Carteira.Where(x => x.TipoAtivo == "RendaFixa").ToListAsync();
            decimal totalRendimentoliquido = 0;
            foreach (var rf in ativosRF)
            {
                decimal taxaMensal = (rf.TaxaRentabilidade ?? 0) / 12 / 100;
                totalRendimentoliquido += (rf.Quantidade * rf.PrecoMedio) * taxaMensal * 0.825m;
            }

            var viewModel = new FluxoCaixaViewModel
            {
                Lancamentos = lancamentos,
                MesAtual = filtroMes,
                AnoAtual = filtroAno,
                RendimentoRendaFixaMes = totalRendimentoliquido,
                ContasFixas = contasFixas,
                ValorParcelaAtual = valorParcelaFinal, // Já vai com o aporte extra embutido
                ParcelaPaga = parcelaPaga,
                TotalAmortizacaoMes = totalAmortizadoNoMes,
                TotalPagoCasa = totalPagoCasa,
                TotalPendenteCasa = pendente
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken] // Boa prática de segurança
        public async Task<IActionResult> AdicionarContaFixa(string descricao, decimal valor, string categoria)
        {
            if (string.IsNullOrEmpty(descricao) || valor <= 0)
            {
                // Você pode adicionar um TempData para exibir um erro se quiser
                return RedirectToAction(nameof(Index));
            }

            var novaConta = new ContaFixa
            {
                Descricao = descricao,
                Valor = valor,
                Categoria = string.IsNullOrEmpty(categoria) ? "Serviços" : categoria,
                EhRecorrente = true // Por padrão, definimos como fixa/recorrente
            };

            try
            {
                _context.ContasFixas.Add(novaConta);
                await _context.SaveChangesAsync();

                // Feedback visual opcional
                TempData["Casa"] = "Conta agendada com sucesso!";
            }
            catch (Exception ex)
            {
                // Logar erro se necessário
                TempData["Erro"] = "Erro ao salvar a conta.";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> EditarContaFixa(int id, string descricao, decimal valor)
        {
            var conta = await _context.ContasFixas.FindAsync(id);
            if (conta != null)
            {
                conta.Descricao = descricao;
                conta.Valor = valor;
                _context.Update(conta);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> ExcluirContaFixa(int id)
        {
            var conta = await _context.ContasFixas.FindAsync(id);
            if (conta != null)
            {
                _context.ContasFixas.Remove(conta);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> PagarContaCasa(string descricao, decimal valor, string categoria)
        {
            var novoGasto = new Financeiro
            {
                Data = DateTime.Now,
                Descricao = descricao,
                Valor = valor,
                Tipo = "Despesa",
                Categoria = categoria,
                Pagamento = "Débito" // Padrão para contas de casa
            };

            _context.Financeiro.Add(novoGasto);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Adicionar(Financeiro model, int numParcelas = 1)
        {
            if (ModelState.IsValid)
            {
                _context.Financeiro.Add(model);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { mes = model.Data.Month, ano = model.Data.Year });
        }

        // GET: Financeiros/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var financeiro = await _context.Financeiro
                .FirstOrDefaultAsync(m => m.Id == id);
            if (financeiro == null)
            {
                return NotFound();
            }

            return View(financeiro);
        }

        // GET: Financeiros/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Financeiros/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public async Task<IActionResult> Create(Financeiro lancamento, int numParcelas = 1)
        {
            if (numParcelas < 1) numParcelas = 1;

            if (ModelState.IsValid)
            {
                // Guardamos o valor total para não perdê-lo durante o loop
                decimal valorTotal = lancamento.Valor;
                decimal valorParcela = valorTotal / numParcelas;

                for (int i = 0; i < numParcelas; i++)
                {
                    var novaParcela = new Financeiro
                    {
                        // Formatação: "Compra (01/05)"
                        Descricao = numParcelas > 1
                            ? $"{lancamento.Descricao} ({i + 1:D2}/{numParcelas:D2})"
                            : lancamento.Descricao,

                        Valor = valorParcela,

                        // Avança o mês automaticamente
                        Data = lancamento.Data.AddMonths(i),

                        Categoria = lancamento.Categoria,

                        // Usamos o Tipo que vem do formulário (Entrada ou Despesa)
                        // em vez de fixar "Despesa"
                        Tipo = lancamento.Tipo,

                        Pagamento = lancamento.Pagamento
                    };

                    _context.Add(novaParcela);
                }

                await _context.SaveChangesAsync();

                // Redireciona para o mês onde a primeira parcela foi criada
                return RedirectToAction(nameof(Index), new { mes = lancamento.Data.Month, ano = lancamento.Data.Year });
            }

            return View(lancamento);
        }

        // GET: Financeiros/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var financeiro = await _context.Financeiro.FindAsync(id);
            if (financeiro == null)
            {
                return NotFound();
            }
            return View(financeiro);
        }

        // POST: Financeiros/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Financeiro financeiro)
        {
            if (id != financeiro.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(financeiro);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FinanceiroExists(financeiro.Id))
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
            return View(financeiro);
        }

        // GET: Financeiros/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var financeiro = await _context.Financeiro
                .FirstOrDefaultAsync(m => m.Id == id);
            if (financeiro == null)
            {
                return NotFound();
            }

            return View(financeiro);
        }


        // POST: Financeiros/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var financeiro = await _context.Financeiro.FindAsync(id);
            if (financeiro != null)
            {
                _context.Financeiro.Remove(financeiro);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FinanceiroExists(int id)
        {
            return _context.Financeiro.Any(e => e.Id == id);
        }
    }
}
