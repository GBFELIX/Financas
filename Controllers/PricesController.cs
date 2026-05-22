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

        [HttpPost]
        public async Task<IActionResult> RemoverAporte(int aporteId, int priceId)
        {
            var aporte = await _context.AporteExtras.FindAsync(aporteId);
            if (aporte != null)
            {
                _context.AporteExtras.Remove(aporte);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id = priceId });
        }

        // GET: Prices/Create
        public IActionResult Create(string visao)
        {
            ViewBag.VisaoAtual = string.IsNullOrEmpty(visao) ? "Gabriel" : visao;
            return View();
        }

        // POST: Prices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Price price, string visao)
        {
            // Remove a propriedade de relacionamento da validação automática para não travar o salvamento
            ModelState.Remove("AportesPontuais");

            if (ModelState.IsValid)
            {
                // Se a sua model exige um Dono e o formulário não enviou, injeta o filtro ativo da tela
                if (string.IsNullOrEmpty(price.Dono))
                {
                    price.Dono = visao == "Casal" ? "Casal" : visao;
                }

                _context.Add(price);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { visao = visao });
            }

            ViewBag.VisaoAtual = string.IsNullOrEmpty(visao) ? "Gabriel" : visao;
            return View(price);
        }

        // GET: Prices/Edit/5
        public async Task<IActionResult> Edit(int? id, string visao)
        {
            if (id == null) return NotFound();

            var price = await _context.Financiamentos
                .Include(p => p.AportesPontuais)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (price == null) return NotFound();

            ViewBag.VisaoAtual = string.IsNullOrEmpty(visao) ? "Gabriel" : visao;
            return View(price);
        }

        // POST: Prices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Price price, string visao)
        {
            if (id != price.Id)
            {
                return NotFound();
            }

            // Remove a propriedade de relacionamento da validação automática na edição também
            ModelState.Remove("AportesPontuais");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(price);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { visao = visao });
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
            }

            ViewBag.VisaoAtual = string.IsNullOrEmpty(visao) ? "Gabriel" : visao;
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
