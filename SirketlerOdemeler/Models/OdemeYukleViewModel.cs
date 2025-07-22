using Microsoft.AspNetCore.Http;

namespace SirketlerOdemeler.Models
{
    public class OdemeYukleViewModel
    {
        public int SKod { get; set; }
        public IFormFile Dosya { get; set; }
        public string Mesaj { get; set; }
    }
}
