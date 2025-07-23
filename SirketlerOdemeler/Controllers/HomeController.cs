using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using SirketlerOdemeler.Data;
using SirketlerOdemeler.Models;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System;
using System.Linq;

namespace SirketlerOdemeler.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(AppDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
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
                return Json(new { success = false, message = "Dosya yüklenemedi veya boş." });
            }

            Odemeler sonEklenenOdeme = null;

            using (var reader = new StreamReader(Dosya.OpenReadStream()))
            {
                while (!reader.EndOfStream)
                {
                    satirNumarasi++;
                    var satir = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(satir)) continue;

                    satir = satir.TrimEnd(';');
                    var parcalar = satir.Split(';', StringSplitOptions.RemoveEmptyEntries);

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
                                message = $"{satirNumarasi}. satırın {i + 1}. sütununda sayı olmayan bir değer var: '{parca}'"
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
                return Json(new { success = false, message = "Dosyanızda geçerli veri yok." });
            }

            await _context.SaveChangesAsync();

            var sirket = await _context.Sirketler.FindAsync(SKod);

            return Json(new
            {
                success = true,
                message = "Ödenen Tutar Başarıyla Kaydedildi.",
                yeniOdeme = new
                {
                    sirketAd = sirket?.SirketAd ?? "Bilinmeyen",
                    odenenTutar = sonEklenenOdeme?.OdenenTutar ?? 0
                }
            });
        }

        [HttpGet]
        public async Task<JsonResult> TumOdemeleriGetir()
        {
            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        [HttpGet]
        public async Task<JsonResult> SirketOdemeleri(string sirketAd)
        {
            if (string.IsNullOrWhiteSpace(sirketAd))
                return Json(new { success = false, message = "Şirket adı gerekli." });

            var result = await (from o in _context.Odemeler
                                join s in _context.Sirketler on o.SKod equals s.SKod
                                where s.SirketAd == sirketAd
                                select new
                                {
                                    sirketAd = s.SirketAd,
                                    odenenTutar = o.OdenenTutar
                                }).ToListAsync();

            return Json(result);
        }

        [HttpGet]
        public async Task<JsonResult> OzetBilgi()
        {
            var toplamSirket = await _context.Sirketler.CountAsync();
            var toplamOdeme = await _context.Odemeler.SumAsync(o => (int?)o.OdenenTutar) ?? 0;
            return Json(new { toplamSirket, toplamOdeme });
        }

        [HttpGet]
        public async Task<JsonResult> HaberBasliklariGetir(string sirket)
        {
            if (string.IsNullOrWhiteSpace(sirket))
                return Json(new { success = false, message = "Şirket adı gerekli." });

            string url = $"https://www.haberturk.com/arama/{Uri.EscapeDataString(sirket)}?tr={Uri.EscapeDataString(sirket)}";
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var html = await httpClient.GetStringAsync(url);

                var matches = Regex.Matches(html, "<span[^>]*data-name=\\\"title\\\"[^>]*>(.*?)<\\/span>", RegexOptions.IgnoreCase);
                var basliklar = matches
                    .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                    .Where(b => b.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return Json(new { success = true, basliklar, kaynak = "Habertürk", url });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Başlıklar alınamadı.", hata = ex.Message, url });
            }
        }

        //Şirket bazı verilerin geldiği yer.
        [HttpGet]
        public async Task<JsonResult> MicrosoftSirketiOdemeleri()
        {
            return await SirketOdemeleri("Microsoft");
        }

        [HttpGet]
        public async Task<JsonResult> OracleSirketiOdemeleri()
        {
            return await SirketOdemeleri("Oracle");
        }

        [HttpGet]
        public async Task<JsonResult> NvidiaSirketiOdemeleri()
        {
            return await SirketOdemeleri("Nvidia");
        }

        [HttpGet]
        public async Task<JsonResult> FordSirketiOdemeleri()
        {
            return await SirketOdemeleri("Ford");
        }

        [HttpGet]
        public async Task<JsonResult> PegasusSirketiOdemeleri()
        {
            return await SirketOdemeleri("Pegasus");
        }
    }
}
