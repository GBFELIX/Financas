using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
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

        public FinanceirosController(Acoes_FiisContext context)
        {
            _context = context;
        }

        // GET: Financeiros
        public async Task<IActionResult> Index(int? mes, int? ano)
        {
            int filtroMes = mes ?? DateTime.Now.Month;
            int filtroAno = ano ?? DateTime.Now.Year;


            var lancamentos = await _context.Financeiro
                .Where((Financeiro x) => x.Data.Month == filtroMes && x.Data.Year == filtroAno)
                .ToListAsync() ?? new List<Financeiro>();


            var ativosRF = await _context.Carteira
                .Where(x => x.TipoAtivo == "RendaFixa")
                .ToListAsync();

            decimal TotalAnual = await _context.Financeiro
            .Where(x => x.Tipo == "Entrada")
            .SumAsync(x => x.Valor);

            decimal totalRendimentoliquido = 0;
            foreach (var rf in ativosRF)
            {
                decimal taxaMensal = (rf.TaxaRentabilidade ?? 0) / 12 / 100;
                decimal montanteInvestido = rf.Quantidade * rf.PrecoMedio;

                totalRendimentoliquido += montanteInvestido * taxaMensal * 0.825m;
            }

            // 4. Monta a ViewModel para a View
            var viewModel = new FluxoCaixaViewModel
            {
                Lancamentos = lancamentos,
                MesAtual = filtroMes,
                AnoAtual = filtroAno,
                RendimentoRendaFixaMes = totalRendimentoliquido
            };

            return View(viewModel);
        }
        [HttpPost]
        public async Task<IActionResult> Adicionar(Financeiro model)
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Descricao,Pagamento,Valor,Data,Tipo,Categoria")] Financeiro financeiro)
        {
            if (ModelState.IsValid)
            {
                _context.Add(financeiro);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(financeiro);
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Descricao,Pagamento,Valor,Data,Tipo,Categoria")] Financeiro financeiro)
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
