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
    public class PricesController : Controller
    {
        private readonly Acoes_FiisContext _context;
        private readonly FinanciamentoService _service;

        public PricesController(Acoes_FiisContext context, FinanciamentoService service)
        {
            _context = context;
            _service = service;
        }
        [HttpPost]
        public async Task<IActionResult> AdicionarAporte(int priceId, int mes, decimal valor)
        {
            var aporte = new AporteExtra
            {
                PriceId = priceId,
                MesReferencia = mes,
                Valor = valor
            };

            _context.AporteExtras.Add(aporte);
            await _context.SaveChangesAsync();

            // Redireciona de volta para a tela de detalhes para ver o novo cálculo
            return RedirectToAction(nameof(Details), new { id = priceId });
        }
        // GET: Prices
        public async Task<IActionResult> Index(string visao)
        {
            // 1. Mantém o perfil padrão e joga na ViewBag para o Layout funcionar
            if (string.IsNullOrEmpty(visao)) visao = "Gabriel";
            ViewBag.VisaoAtual = visao;

            // 2. Prepara a query em modo IQueryable
            var query = _context.Financiamentos.AsQueryable();

            // 3. Aplica o isolamento por Dono
            if (visao == "Gabriel")
            {
                query = query.Where(x => x.Dono == "Gabriel" || x.Dono == "Casal");
            }
            else if (visao == "Ela")
            {
                query = query.Where(x => x.Dono == "Ela" || x.Dono == "Casal");
            }
            // No modo "Casal" traz todos os financiamentos cadastrados sem filtro

            // 4. Executa a busca e envia para a View
            var listaFinanciamentos = await query.ToListAsync();

            return View(listaFinanciamentos);
        }

        // GET: Prices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var price = await _context.Financiamentos
                .Include(p => p.AportesPontuais) // Essencial para carregar os aportes do SQL
                .FirstOrDefaultAsync(m => m.Id == id);

            if (price == null) return NotFound();

            // Chama a service para calcular a projeção de parcelas
            // Supondo que você adicionou a propriedade 'Projecao' na Model 'Price'
            price.Projecao = _service.GerarSimulacao(price);

            return View(price);
        }

        // GET: Prices/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Prices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ValorImovel,ValorEntrada,TaxaJurosAnual,PrazoMeses,AporteExtraMensal,ValorPrestação,DataInicio")] Price price)
        {
            if (ModelState.IsValid)
            {
                _context.Add(price);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(price);
        }

        [HttpPost]
        public async Task<IActionResult> RemoverAporte(int aporteId, int priceId)
        {
            var aporte = await _context.AporteExtras.FindAsync(aporteId);
            if (aporte != null)
            {
                _context.AporteExtras.Remove(aporte);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = priceId });
        }

        // GET: Prices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            // Adicione o .Include para carregar os aportes no Edit
            var price = await _context.Financiamentos
                .Include(p => p.AportesPontuais)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (price == null) return NotFound();

            return View(price);
        }

        // POST: Prices/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ValorImovel,ValorEntrada,TaxaJurosAnual,PrazoMeses,AporteExtraMensal,ValorPrestação,DataInicio")] Price price)
        {
            if (id != price.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(price);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PriceExists(price.Id))
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
            return View(price);
        }

        // GET: Prices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var price = await _context.Financiamentos
                .FirstOrDefaultAsync(m => m.Id == id);
            if (price == null)
            {
                return NotFound();
            }

            return View(price);
        }

        // POST: Prices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var price = await _context.Financiamentos.FindAsync(id);
            if (price != null)
            {
                _context.Financiamentos.Remove(price);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PriceExists(int id)
        {
            return _context.Financiamentos.Any(e => e.Id == id);
        }
    }
}
