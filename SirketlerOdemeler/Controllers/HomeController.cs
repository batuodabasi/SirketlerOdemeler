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

            var httpClient = _httpClientFactory.CreateClient();
            var haberler = new System.Collections.Generic.Dictionary<string, object>();
            var kaynaklar = new System.Collections.Generic.List<string>();
            var hatalar = new System.Collections.Generic.Dictionary<string, string>();

            // Habertürk
            string urlHaberturk = $"https://www.haberturk.com/arama/{Uri.EscapeDataString(sirket)}?tr={Uri.EscapeDataString(sirket)}";
            try
            {
                var httpClientWithUA = _httpClientFactory.CreateClient();
                httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var html = await httpClientWithUA.GetStringAsync(urlHaberturk);

                // Yeni HTML yapısına göre haberleri çek
                var kartlar = Regex.Matches(html, "<div[^>]*data-type=\"box-type3-search\"[^>]*>[\\s\\S]*?</div>\\s*</div>");
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                
                // Her kaynaktan 5'er haber al
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                
                foreach (Match kart in kartlar.Cast<Match>())
                {
                    if (haberCount >= MAX_HABER_PER_SOURCE) break; // En fazla 5 haber
                    
                    var kartHtml = kart.Value;
                    
                    // Başlık
                    var baslikMatch = Regex.Match(kartHtml, "<span[^>]*data-name=\"title\"[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
                    var baslik = baslikMatch.Success ? Regex.Replace(baslikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    
                    // Tarih
                    var dateMatch = Regex.Match(kartHtml, "<span[^>]*data-name=\"date\"[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
                    var date = dateMatch.Success ? Regex.Replace(dateMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    
                    // URL
                    var urlMatch = Regex.Match(kartHtml, "<a[^>]*data-name=\"url\"[^>]*href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    var link = urlMatch.Success ? "https://www.haberturk.com" + urlMatch.Groups[1].Value : null;
                    
                    // Resim
                    var imgMatch = Regex.Match(kartHtml, "<img[^>]*src=\"([^\"]+)\"[^>]*alt=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    var imgSrc = imgMatch.Success ? imgMatch.Groups[1].Value : null;
                    var imgAlt = imgMatch.Success ? imgMatch.Groups[2].Value : null;

                    if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                    {
                        basliklarWithImg.Add(new {
                            kaynak = "Habertürk",
                            baslik = baslik,
                            imgSrc = imgSrc,
                            imgAlt = imgAlt,
                            link = link,
                            tarih = date
                        });
                        haberCount++;
                    }
                }
                
                if (basliklarWithImg.Count == 0)
                {
                    hatalar["Habertürk"] = "Hiç başlık bulunamadı veya sayfa yapısı değişmiş olabilir.";
                }
                haberler["Habertürk"] = new { basliklar = basliklarWithImg, url = urlHaberturk };
                kaynaklar.Add("Habertürk");
            }
            catch (Exception ex)
            {
                hatalar["Habertürk"] = ex.Message;
                haberler["Habertürk"] = new { basliklar = new System.Collections.Generic.List<object>(), url = urlHaberturk };
                kaynaklar.Add("Habertürk");
            }

            // NTV
            string sirketUrlPart = sirket.ToLowerInvariant();
            string urlNtv = $"https://www.ntv.com.tr/{sirketUrlPart}";
            try
            {
                var httpClientWithUA = _httpClientFactory.CreateClient();
                httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var html = await httpClientWithUA.GetStringAsync(urlNtv);
                
                // Önce tüm kartları bul
                var kartlar = Regex.Matches(html, "<div class=\"card card--md\">[\\s\\S]*?</div>\\s*</div>");
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                
                // Her kaynaktan 5'er haber al
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                
                foreach (Match kart in kartlar.Cast<Match>())
                {
                    if (haberCount >= MAX_HABER_PER_SOURCE) break; // En fazla 5 haber
                    
                    var kartHtml = kart.Value;
                    
                    // Card-link ile başlık ve link bilgisi
                    var cardLinkMatch = Regex.Match(kartHtml, 
                        "<a href=\"([^\"]+)\"[^>]*class=\"card-link[^\"]*\"[^>]*title=\"([^\"]+)\"", 
                        RegexOptions.IgnoreCase);
                    
                    // Card-text-link ile başlık ve link bilgisi (alternatif)
                    var cardTextLinkMatch = Regex.Match(kartHtml, 
                        "<a href=\"([^\"]+)\"[^>]*class=\"card-text-link[^\"]*\"[^>]*title=\"([^\"]+)\"", 
                        RegexOptions.IgnoreCase);
                    
                    // İki match'ten birini seç
                    var linkMatch = cardLinkMatch.Success ? cardLinkMatch : cardTextLinkMatch;
                    
                    string link = null;
                    string baslik = null;
                    
                    if (linkMatch.Success)
                    {
                        link = "https://www.ntv.com.tr" + linkMatch.Groups[1].Value;
                        baslik = linkMatch.Groups[2].Value.Trim();
                    }
                    
                    // Görsel bilgisi
                    var imgMatch = Regex.Match(kartHtml, "<img[^>]*src=\"([^\"]+)\"[^>]*alt=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    string imgSrc = null;
                    string imgAlt = null;
                    
                    if (imgMatch.Success)
                    {
                        imgSrc = imgMatch.Groups[1].Value;
                        imgAlt = imgMatch.Groups[2].Value;
                    }
                    else
                    {
                        // Alternatif olarak source tag'ından görsel URL'i çek
                        var sourceMatch = Regex.Match(kartHtml, "<source[^>]*srcset=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                        if (sourceMatch.Success)
                        {
                            // URL'den ? işaretine kadar olan kısmı al (parametreleri temizle)
                            var srcsetValue = sourceMatch.Groups[1].Value;
                            var questionMarkIndex = srcsetValue.IndexOf('?');
                            imgSrc = questionMarkIndex > 0 ? srcsetValue.Substring(0, questionMarkIndex) : srcsetValue;
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                    {
                        basliklarWithImg.Add(new {
                            kaynak = "NTV",
                            baslik = baslik,
                            imgSrc = imgSrc,
                            imgAlt = imgAlt,
                            link = link,
                            tarih = (string)null // NTV'de tarih bilgisi yok
                        });
                        haberCount++;
                    }
                }
                
                if (basliklarWithImg.Count == 0)
                {
                    // Hiç başlık bulunamadıysa, debug için HTML'in bir kısmını hata mesajına ekle
                    hatalar["NTV"] = $"Hiç başlık bulunamadı. Sayfa yapısı değişmiş olabilir veya şirket adı sayfada yok. URL: {urlNtv}";
                }
                
                haberler["NTV"] = new { basliklar = basliklarWithImg, url = urlNtv };
                kaynaklar.Add("NTV");
            }
            catch (Exception ex)
            {
                hatalar["NTV"] = $"Hata: {ex.Message}. URL: {urlNtv}";
                haberler["NTV"] = new { basliklar = new System.Collections.Generic.List<object>(), url = urlNtv };
                kaynaklar.Add("NTV");
            }

            bool anySuccess = haberler.Any(kv => ((dynamic)kv.Value).basliklar is System.Collections.ICollection list && list.Count > 0);
            return Json(new
            {
                success = anySuccess,
                haberler,
                hatalar,
                kaynaklar
            });
        }

        [HttpGet]
        public async Task<JsonResult> TumSirketlerHaberleriGetir()
        {
            var sirketler = await _context.Sirketler.Select(s => s.SirketAd).ToListAsync();
            var httpClient = _httpClientFactory.CreateClient();
            var sonuc = new System.Collections.Generic.Dictionary<string, object>();

            foreach (var sirket in sirketler)
            {
                var haberler = new System.Collections.Generic.Dictionary<string, object>();
                var kaynaklar = new System.Collections.Generic.List<string>();
                var hatalar = new System.Collections.Generic.Dictionary<string, string>();

                // Habertürk
                string urlHaberturk = $"https://www.haberturk.com/arama/{Uri.EscapeDataString(sirket)}?tr={Uri.EscapeDataString(sirket)}";
                try
                {
                    var html = await httpClient.GetStringAsync(urlHaberturk);
                    var matches = Regex.Matches(html, "<span[^>]*data-name=\\\"title\\\"[^>]*>(.*?)<\\/span>", RegexOptions.IgnoreCase);
                    var basliklar = matches
                        .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                        .Where(b => b.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                        .Take(5)
                        .ToList();
                    if (basliklar.Count == 0)
                        basliklar.Add("Burada bu şirket ile ilgili haber yok");
                    haberler["Habertürk"] = new { basliklar, url = urlHaberturk };
                    kaynaklar.Add("Habertürk");
                }
                catch (Exception ex)
                {
                    hatalar["Habertürk"] = ex.Message;
                }

                // NTV
                string sirketUrlPart = sirket.ToLowerInvariant();
                string urlNtv = $"https://www.ntv.com.tr/{sirketUrlPart}";
                try
                {
                    var html = await httpClient.GetStringAsync(urlNtv);
                    var matches = Regex.Matches(html, "<a[^>]*class=\\\"card-text-link text-elipsis-3\\\"[^>]*>(.*?)<\\/a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var basliklar = matches
                        .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                        .Where(b => b.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                        .Take(5)
                        .ToList();
                    if (basliklar.Count == 0)
                        basliklar.Add("Burada bu şirket ile ilgili haber yok");
                    haberler["NTV"] = new { basliklar, url = urlNtv };
                    kaynaklar.Add("NTV");
                }
                catch (Exception ex)
                {
                    hatalar["NTV"] = ex.Message;
                }

                bool anySuccess = haberler.Any(kv => ((dynamic)kv.Value).basliklar is System.Collections.Generic.List<string> list && list.Count > 0);
                sonuc[sirket] = new
                {
                    success = anySuccess,
                    haberler,
                    hatalar,
                    kaynaklar
                };
            }
            return Json(sonuc);
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
