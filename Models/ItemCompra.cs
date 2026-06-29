using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Acoes_Fiis.Models
{
    [Table("ControleCompras")]
    public class ItemCompra
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;

        public int Quantidade { get; set; } = 1;
        public string Categoria { get; set; } = "Outros"; // Ex: Hortifrúti, Açougue, Limpeza

        public bool Comprado { get; set; } = false;

        public string Dono { get; set; } = "Gabriel";

        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }
}

