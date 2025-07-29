using System.ComponentModel.DataAnnotations;

namespace SirketlerOdemeler.Models
{
    public class HaberlerKategoriler
    {
        [Key]
        public int KategoriId { get; set; }

        public String KategoriAd { get; set; }
    }
}
