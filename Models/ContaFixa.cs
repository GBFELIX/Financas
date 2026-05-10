using System.ComponentModel.DataAnnotations.Schema;

namespace Acoes_Fiis.Models
{
    public class ContaFixa
    {
        public int Id { get; set; }
        public string Descricao { get; set; }
        public decimal Valor { get; set; }

        // Indica se essa conta deve ser gerada automaticamente todo mês
        public bool EhRecorrente { get; set; } = true;

        // Relacionamento opcional com a categoria para o ícone roxo bater
        public string Categoria { get; set; } = "Serviços";

        // Controle de Status para o mês atual
        // Nota: Em um sistema real, você pode ter uma tabela de 'PagamentosMensais' 
        // que referencia a ContaFixa para manter histórico.
        [NotMapped]
        public bool PagoNoMesAtual { get; set; }
    }
}