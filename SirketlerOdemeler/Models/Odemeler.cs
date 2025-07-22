using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SirketlerOdemeler.Models
{
    public class Odemeler
    {
        [Key]
        public int OdemeId { get; set; }

        [ForeignKey("Sirketler")]
        public int SKod { get; set; }

        public int OdenenTutar { get; set; }
    }
}
