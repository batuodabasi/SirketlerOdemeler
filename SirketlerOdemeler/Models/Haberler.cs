using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SirketlerOdemeler.Models
{
    public class Haberler
    {
        [Key]
        public int HaberId { get; set; }

        public int SKod { get; set; }

        [ForeignKey("SKod")]
        public Sirketler Sirket { get; set; }

        [MaxLength(50)]
        public string HaberBaslik { get; set; }

        [MaxLength(600)]
        public string HaberIcerik { get; set; }
    }
}
