using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SirketlerOdemeler.Data;
using SirketlerOdemeler.Models;
using System.IO;
using System.Threading.Tasks;

namespace SirketlerOdemeler.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> OdemeYukleAjax(int SKod, IFormFile Dosya)
        {
            int toplamSatir = 0;
            int satirNumarasi = 0;

            if (Dosya == null || Dosya.Length == 0)
            {
                return Json(new { success = false, message = "Dosya yüklenemedi veya boþ." });
            }

            Odemeler sonEklenenOdeme = null;

            using (var reader = new StreamReader(Dosya.OpenReadStream()))
            {
                while (!reader.EndOfStream)
                {
                    satirNumarasi++;
                    var satir = await reader.ReadLineAsync();
                    satir = satir.TrimEnd(';');
                    var parcalar = satir.Split(';');

                    int toplam = 0;

                    for (int i = 0; i < parcalar.Length; i++)
                    {
                        var parca = parcalar[i];
                        if (int.TryParse(parca, out int sayi))
                        {
                            toplam += sayi;
                        }
                        else
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"{satirNumarasi}. satýrýn {i + 1}. sütununda sayý olmayan bir deðer var: '{parca}'"
                            });
                        }
                    }

                    if (parcalar.Length > 0)
                    {
                        var odeme = new Odemeler
                        {
                            SKod = SKod,
                            OdenenTutar = toplam
                        };

                        _context.Odemeler.Add(odeme);
                        sonEklenenOdeme = odeme;
                        toplamSatir++;
                    }
                }
            }

            if (toplamSatir == 0)
            {
                return Json(new { success = false, message = "Dosyanýzda geçerli veri yok." });
            }

            await _context.SaveChangesAsync();

            var sirket = await _context.Sirketler.FindAsync(SKod);

            return Json(new
            {
                success = true,
                message = "Ödenen tutar baþarýyla kaydedildi.",
                yeniOdeme = new
                {
                    sirketAd = sirket?.SirketAd ?? "Bilinmeyen",
                    odenenTutar = sonEklenenOdeme?.OdenenTutar ?? 0
                }
            });
        }

        //Tüm Ödemeler
        [HttpGet]
        public async Task<JsonResult> TumOdemeleriGetir()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler
                                on o.SKod equals s.SKod
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        //Microsoft Ödemeleri
        [HttpGet]
        public async Task<JsonResult> MicrosoftSirketiOdemeleri()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                where s.SirketAd == "Microsoft"
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        //Oracle Ödemeleri
        [HttpGet]
        public async Task<JsonResult> OracleSirketiOdemeleri()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                where s.SirketAd == "Oracle"
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        //Nvidia Ödemeleri
        [HttpGet]
        public async Task<JsonResult> NvidiaSirketiOdemeleri()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                where s.SirketAd == "Nvidia"
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        //Ford Ödemeleri
        [HttpGet]
        public async Task<JsonResult> FordSirketiOdemeleri()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                where s.SirketAd == "Ford"
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        //Pegasus Ödemeleri
        [HttpGet]
        public async Task<JsonResult> PegasusSirketiOdemeleri()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                where s.SirketAd == "Pegasus"
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }
    }
}
