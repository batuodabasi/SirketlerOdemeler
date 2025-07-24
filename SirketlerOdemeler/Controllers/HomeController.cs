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
                var kartlar = Regex.Matches(html, "<div[^>]*data-type=\\\"box-type3-search\\\"[^>]*>[\\s\\S]*?</div>\\s*</div>", RegexOptions.IgnoreCase);
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                foreach (Match kart in kartlar.Cast<Match>())
                {
                    if (haberCount >= MAX_HABER_PER_SOURCE) break;
                    var kartHtml = kart.Value;
                    var baslikMatch = Regex.Match(kartHtml, "<span[^>]*data-name=\"title\"[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
                    var baslik = baslikMatch.Success ? Regex.Replace(baslikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    var dateMatch = Regex.Match(kartHtml, "<span[^>]*data-name=\"date\"[^>]*>(.*?)</span>", RegexOptions.IgnoreCase);
                    var date = dateMatch.Success ? Regex.Replace(dateMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    var urlMatch = Regex.Match(kartHtml, "<a[^>]*data-name=\"url\"[^>]*href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    var link = urlMatch.Success ? "https://www.haberturk.com" + urlMatch.Groups[1].Value : null;
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
                var kartlar = Regex.Matches(html, "<div class=\\\"card card--md\\\">[\\s\\S]*?</div>\\s*</div>", RegexOptions.IgnoreCase);
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                foreach (Match kart in kartlar.Cast<Match>())
                {
                    if (haberCount >= MAX_HABER_PER_SOURCE) break;
                    var kartHtml = kart.Value;
                    var cardLinkMatch = Regex.Match(kartHtml, "<a href=\"([^\"]+)\"[^>]*class=\"card-link[^\"]*\"[^>]*title=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    var cardTextLinkMatch = Regex.Match(kartHtml, "<a href=\"([^\"]+)\"[^>]*class=\"card-text-link[^\"]*\"[^>]*title=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    var linkMatch = cardLinkMatch.Success ? cardLinkMatch : cardTextLinkMatch;
                    string link = null;
                    string baslik = null;
                    if (linkMatch.Success)
                    {
                        link = "https://www.ntv.com.tr" + linkMatch.Groups[1].Value;
                        baslik = linkMatch.Groups[2].Value.Trim();
                    }
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
                        var sourceMatch = Regex.Match(kartHtml, "<source[^>]*srcset=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                        if (sourceMatch.Success)
                        {
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
                            tarih = (string)null
                        });
                        haberCount++;
                    }
                }
                if (basliklarWithImg.Count == 0)
                {
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

            // Haberler.com
            string urlHaberlerCom = $"https://www.haberler.com/{Uri.EscapeDataString(sirket.ToLowerInvariant())}/";
            try
            {
                var httpClientWithUA = _httpClientFactory.CreateClient();
                httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var html = await httpClientWithUA.GetStringAsync(urlHaberlerCom);
                
                // Daha basit yaklaşım: Her kartı ayrı ayrı işle
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                
                // Önce tüm kartları bul
                var kartlarMatches = Regex.Matches(html, "<div class=\"new3card\"[^>]*>[\\s\\S]*?</div>\\s*</div>", RegexOptions.IgnoreCase);
                
                foreach (Match kart in kartlarMatches.Cast<Match>().Take(MAX_HABER_PER_SOURCE))
                {
                    var kartHtml = kart.Value;
                    
                    // Başlık
                    var h3Match = Regex.Match(kartHtml, "<h3>(.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var baslik = h3Match.Success ? Regex.Replace(h3Match.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    
                    // Link
                    var linkMatch = Regex.Match(kartHtml, "<a[^>]*href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    var link = linkMatch.Success ? "https://www.haberler.com" + linkMatch.Groups[1].Value : null;
                    
                    // Kartın içindeki ilk <img ...> etiketini bul
                    var imgTagMatch = Regex.Match(kartHtml, "<img[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    string imgSrc = null, imgAlt = null;
                    if (imgTagMatch.Success)
                    {
                        var imgTag = imgTagMatch.Value;
                        var srcMatch = Regex.Match(imgTag, "src=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                        var altMatch = Regex.Match(imgTag, "alt=\"([^\"]*)\"", RegexOptions.IgnoreCase);
                        imgSrc = srcMatch.Success ? srcMatch.Groups[1].Value : null;
                        imgAlt = altMatch.Success ? altMatch.Groups[1].Value : null;
                    }
                    
                    // Tarih
                    var tarihMatch = Regex.Match(kartHtml, "<div class=\"hbbiText\">(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var tarih = tarihMatch.Success ? Regex.Replace(tarihMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    
                    if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                    {
                        basliklarWithImg.Add(new {
                            kaynak = "Haberler.com",
                            baslik = baslik,
                            imgSrc = imgSrc,
                            imgAlt = imgAlt,
                            link = link,
                            tarih = tarih
                        });
                    }
                }
                
                // Yedek olarak eski yöntemi de dene
                if (basliklarWithImg.Count == 0)
                {
                    var eskiMatches = Regex.Matches(html, "<a[^>]*class=\"hb-title\"[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    foreach (Match m in eskiMatches)
                    {
                        var baslik = Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                        {
                            basliklarWithImg.Add(new {
                                kaynak = "Haberler.com",
                                baslik = baslik,
                                imgSrc = (string)null,
                                imgAlt = (string)null,
                                link = urlHaberlerCom,
                                tarih = (string)null
                            });
                            if (basliklarWithImg.Count >= MAX_HABER_PER_SOURCE) break;
                        }
                    }
                }
                
                if (basliklarWithImg.Count == 0)
                {
                    hatalar["Haberler.com"] = "Hiç başlık bulunamadı veya sayfa yapısı değişmiş olabilir.";
                }
                haberler["Haberler.com"] = new { basliklar = basliklarWithImg, url = urlHaberlerCom };
                kaynaklar.Add("Haberler.com");
            }
            catch (Exception ex)
            {
                hatalar["Haberler.com"] = ex.Message;
                haberler["Haberler.com"] = new { basliklar = new System.Collections.Generic.List<object>(), url = urlHaberlerCom };
                kaynaklar.Add("Haberler.com");
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

                // Haberler.com
                string urlHaberlerCom = $"https://www.haberler.com/{Uri.EscapeDataString(sirket.ToLowerInvariant())}/";
                try
                {
                    var html = await httpClient.GetStringAsync(urlHaberlerCom);
                    var kartlar = Regex.Matches(html, "<div class=\"new3card\"[\\s\\S]*?</div>\\s*</div>", RegexOptions.IgnoreCase);
                    var basliklar = new System.Collections.Generic.List<string>();
                    int haberCount = 0;
                    foreach (Match kart in kartlar)
                    {
                        if (haberCount >= 5) break;
                        var kartHtml = kart.Value;
                        var aMatch = Regex.Match(kartHtml, "<a[^>]*href=\"([^\"]+)\"[^>]*title=\"([^\"]+)\"[\\s\\S]*?<img[^>]*src=\"([^\"]+)\"[^>]*alt=\"([^\"]*)\"[\\s\\S]*?<h3>(.*?)</h3>[\\s\\S]*?<div class=\"hbbiText\">(.*?)</div>", RegexOptions.IgnoreCase);
                        if (aMatch.Success)
                        {
                            var h3Baslik = Regex.Replace(aMatch.Groups[5].Value, "<.*?>", string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(h3Baslik) && h3Baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                            {
                                basliklar.Add(h3Baslik);
                                haberCount++;
                            }
                        }
                    }
                    if (basliklar.Count == 0)
                    {
                        var eskiMatches = Regex.Matches(html, "<a[^>]*class=\"hb-title\"[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        foreach (Match m in eskiMatches)
                        {
                            var baslik = Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                            {
                                basliklar.Add(baslik);
                                if (basliklar.Count >= 5) break;
                            }
                        }
                    }
                    if (basliklar.Count == 0)
                        basliklar.Add("Burada bu şirket ile ilgili haber yok");
                    haberler["Haberler.com"] = new { basliklar, url = urlHaberlerCom };
                    kaynaklar.Add("Haberler.com");
                }
                catch (Exception ex)
                {
                    hatalar["Haberler.com"] = ex.Message;
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

        [HttpPost]
        public async Task<JsonResult> HaberKaydet(string sirketAd, string haberBaslik, string haberUrl, string haberGorsel)
        {
            if (string.IsNullOrWhiteSpace(sirketAd) || string.IsNullOrWhiteSpace(haberBaslik) || string.IsNullOrWhiteSpace(haberUrl))
            {
                return Json(new { success = false, message = "Şirket adı, haber başlığı ve URL gereklidir." });
            }

            try
            {
                // Şirket kodunu bul
                var sirket = await _context.Sirketler.FirstOrDefaultAsync(s => s.SirketAd == sirketAd);
                if (sirket == null)
                {
                    return Json(new { success = false, message = "Şirket bulunamadı." });
                }

                // Haber içeriğini çek
                string haberIcerik = "";
                
                // Google araması URL'i mi kontrol et
                if (haberUrl.Contains("google.com/search"))
                {
                    // Google aramasından içerik çekmek yerine başlığı içerik olarak kullan
                    haberIcerik = "Haber başlığı: " + haberBaslik;
                }
                else
                {
                    // Normal haber URL'inden içerik çek
                    var httpClientWithUA = _httpClientFactory.CreateClient();
                    httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var html = await httpClientWithUA.GetStringAsync(haberUrl);

                    // Haber içeriğini çıkar
                    var icerikMatch = Regex.Match(html, "<div[^>]*class=\"[^\"]*\"[^>]*>[\\s\\S]*?<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (icerikMatch.Success)
                    {
                        haberIcerik = Regex.Replace(icerikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                    }
                    else
                    {
                        haberIcerik = "İçerik çekilemedi.";
                    }
                }

                // Haberi kaydet
                var yeniHaber = new Haberler
                {
                    SKod = sirket.SKod,
                    HaberBaslik = haberBaslik,
                    HaberIcerik = haberIcerik,
                    HaberGorsel = haberGorsel
                };

                _context.Haberler.Add(yeniHaber);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Haber başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Haber kaydedilirken hata oluştu: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> YapayZekaYorumlat(string haberBaslik)
        {
            if (string.IsNullOrWhiteSpace(haberBaslik))
                return Json(new { success = false, message = "Başlık gerekli." });

            try
            {
                var apiKey = "AIzaSyAYlPijAICSq9OcdHtJY9-f_KU8dItZkXA";
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";
                var payload = new
                {
                    contents = new[]
                    {
                        new {
                            parts = new[]
                            {
                                new { text = haberBaslik + " bu başlığı analiz et" }
                            }
                        }
                    }
                };
                var httpClient = _httpClientFactory.CreateClient();
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "API hatası: " + responseString });
                }
                // Gemini cevabını çöz
                using var doc = System.Text.Json.JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                string yorum = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                return Json(new { success = true, yorum });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> HaberYapayZekaYorumEkle(string haberBaslik, string yorum)
        {
            if (string.IsNullOrWhiteSpace(haberBaslik) || string.IsNullOrWhiteSpace(yorum))
                return Json(new { success = false, message = "Başlık ve yorum gerekli." });

            try
            {
                var haber = await _context.Haberler.FirstOrDefaultAsync(h => h.HaberBaslik == haberBaslik);
                if (haber == null)
                {
                    return Json(new { success = false, message = "Haber bulunamadı." });
                }
                haber.HaberYapayZekaYorum = yorum;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Yapay zeka yorumu başarıyla eklendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Yorum eklenirken hata oluştu: " + ex.Message });
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
