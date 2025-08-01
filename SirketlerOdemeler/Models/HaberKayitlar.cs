using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SirketlerOdemeler.Models
{
    public class HaberKayitlar
    {
        [Key]
        public int HaberKayitId { set; get; }

        public int HaberKategori { set; get; }

        [ForeignKey("KategoriId")]
        public HaberlerKategoriler haberkategori { set; get; }

        public string HaberBaslik { set; get; }

        public string HaberIcerik { set; get; }

        public DateTime HaberTarih { set; get; }

        public string HaberYZ1Yorum { set; get; }

        public string HaberYZ2Yorum { set; get; }

        public string HaberYZ3Yorum { set; get; }

    }
}
