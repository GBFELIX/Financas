using Acoes_Fiis.Data;
using Acoes_Fiis.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Acoes_Fiis.Controllers
{
    public class DiaDiaController : Controller
    {
        private readonly Acoes_FiisContext _context;

        public DiaDiaController(Acoes_FiisContext context)
        {
            _context = context;
        }

        // GET: DiaDia ou DiaDia/Index
        public async Task<IActionResult> Index(string visao = "Casal")
        {
            ViewBag.VisaoAtual = visao;

            ViewBag.WhatsPreenchido = true;
            ViewBag.FoneUm = "5521991944621";
            ViewBag.FoneDois = "5521971681788";

            var itens = await _context.ItensCompras
                .Where(x => x.Dono == visao)
                .OrderBy(x => x.Comprado)
                .ThenBy(x => x.Categoria)
                .ToListAsync();

            // Gerar links rápidos de envio para o WhatsApp
            ViewBag.LinkWhatsUm = GerarLinkWhatsapp(itens, ViewBag.FoneUm);
            ViewBag.LinkWhatsDois = GerarLinkWhatsapp(itens, ViewBag.FoneDois);

            // Histórico global de nomes de itens para alimentar o autocomplete da Index
            ViewBag.HistoricoItens = await _context.ItensCompras
                .Select(x => x.Nome)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return View(itens);
        }
        [HttpPost]
        public async Task<IActionResult> AtualizarQuantidade(int id, int quantidade)
        {
            if (quantidade < 1) return BadRequest();

            var item = await _context.ItensCompras.FindAsync(id);
            if (item == null) return NotFound();

            item.Quantidade = quantidade;
            _context.ItensCompras.Update(item);
            await _context.SaveChangesAsync();

            return Ok();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarItem(string nome, int quantidade, string categoria, string visao)
        {
            if (string.IsNullOrEmpty(nome)) return BadRequest();

            var novoItem = new ItemCompra
            {
                Nome = nome.Trim(),
                Quantidade = quantidade > 0 ? quantidade : 1, // FIX: Corrigido de quantity para quantidade
                Categoria = string.IsNullOrEmpty(categoria) ? "Outros" : categoria,
                Dono = string.IsNullOrEmpty(visao) ? "Casal" : visao,
                Comprado = false,
                DataCriacao = DateTime.Now
            };

            _context.ItensCompras.Add(novoItem);
            await _context.SaveChangesAsync();

            return Json(new
            {
                id = novoItem.Id,
                nome = novoItem.Nome,
                quantidade = novoItem.Quantidade,
                categoria = novoItem.Categoria
            });
        }


        [HttpGet]
        public async Task<IActionResult> ObterLinksWhatsapp(string visao = "Casal")
        {
            var itens = await _context.ItensCompras
                .Where(x => x.Dono == visao)
                .ToListAsync();

            // FIX: Alinhado com os números de cima da Index
            string foneUm = "5521991944621";
            string foneDois = "5521971681788";

            return Json(new
            {
                linkUm = GerarLinkWhatsapp(itens, foneUm),
                linkDois = GerarLinkWhatsapp(itens, foneDois)
            });
        }

        // POST: DiaDia/AlternarComprado
        [HttpPost]
        public async Task<IActionResult> AlternarComprado(int id)
        {
            var item = await _context.ItensCompras.FindAsync(id);
            if (item == null) return NotFound();

            item.Comprado = !item.Comprado;
            _context.ItensCompras.Update(item);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // POST: DiaDia/DeletarItem
        [HttpPost]
        public async Task<IActionResult> DeletarItem(int id)
        {
            var item = await _context.ItensCompras.FindAsync(id);
            if (item == null) return NotFound();

            _context.ItensCompras.Remove(item);
            await _context.SaveChangesAsync();

            return Ok();
        }
        private string GerarLinkWhatsapp(List<ItemCompra> itens, string telefone)
        {
            if (!itens.Any(x => !x.Comprado)) return "";

            var sb = new StringBuilder();
            sb.AppendLine("🛒 *LISTA DE COMPRAS* 🛒");
            sb.AppendLine($"📅 _Atualizada em: {DateTime.Now:dd/MM/yyyy HH:mm}_");
            sb.AppendLine();

            var itensPendentes = itens.Where(x => x.Comprado).GroupBy(x => x.Categoria);

            foreach (var grupo in itensPendentes)
            {
                sb.AppendLine($"🔹 *{grupo.Key.ToUpper()}*");
                foreach (var item in grupo)
                {
                    sb.AppendLine($"▪️ {item.Quantidade}x {item.Nome}");
                }
                sb.AppendLine();
            }

            string textoCodificado = HttpUtility.UrlEncode(sb.ToString());
            return $"https://api.whatsapp.com/send?phone={telefone}&text={textoCodificado}";
        }
    }
}