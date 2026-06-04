using System.ComponentModel.DataAnnotations.Schema;


namespace Acoes_Fiis.Models
{

    [Table("ConfiguracoesBackup")]
    public class ConfiguracaoBackup
    {
        public int Id { get; set; }
        public string CaminhoPastaLocal { get; set; }
        public int IntervaloHoras { get; set; }
        public DateTime? UltimoBackup { get; set; }
    }
}
