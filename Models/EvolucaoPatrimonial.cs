namespace Acoes_Fiis.Models
{
    public class EvolucaoPatrimonial
    {
        public int Id { get; set; }
        public string MesAno { get; set; }
        public decimal PatrimonioLiquido { get; set; }
        public string Dono { get; set; }
    }
}
