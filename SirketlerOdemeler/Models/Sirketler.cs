using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SirketlerOdemeler.Models
{
    public class Sirketler
    {
        [Key]
        public int SKod { get; set; }

        [Required]
        public string SirketAd {  get; set; }

        [Required]
        [EmailAddress]
        public string SirketMail { get; set; }


    }
}
