using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SirketlerOdemeler.Data;
using SirketlerOdemeler.Models;
using System.Text.RegularExpressions;

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

            // Hürriyet
            string urlHurriyet = $"https://www.hurriyet.com.tr/haberleri/{Uri.EscapeDataString(sirket.ToLowerInvariant())}";
            try
            {
                var httpClientWithUA = _httpClientFactory.CreateClient();
                httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var html = await httpClientWithUA.GetStringAsync(urlHurriyet);

                // <div class="tag__list__item"> bloklarını bul
                var kartlar = Regex.Matches(html, "<div class=\"tag__list__item\"[\\s\\S]*?<a href=\"([^\"]+)\"[^>]*class=\"tag__list__item--cover\"[^>]*>\\s*<img[^>]*src=\"([^\"]+)\"[^>]*>.*?<h3>(.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                foreach (Match kart in kartlar)
                {
                    if (haberCount >= MAX_HABER_PER_SOURCE) break;
                    var link = kart.Groups[1].Value.StartsWith("http") ? kart.Groups[1].Value : "https://www.hurriyet.com.tr" + kart.Groups[1].Value;
                    var imgSrc = kart.Groups[2].Value;
                    var baslik = Regex.Replace(kart.Groups[3].Value, "<.*?>", string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(baslik))
                    {
                        basliklarWithImg.Add(new {
                            kaynak = "Hürriyet",
                            baslik = baslik,
                            imgSrc = imgSrc,
                            imgAlt = (string)null,
                            link = link,
                            tarih = (string)null
                        });
                        haberCount++;
                    }
                }
                if (basliklarWithImg.Count == 0)
                {
                    hatalar["Hürriyet"] = "Hiç başlık bulunamadı veya sayfa yapısı değişmiş olabilir.";
                }
                haberler["Hürriyet"] = new { basliklar = basliklarWithImg, url = urlHurriyet };
                kaynaklar.Add("Hürriyet");
            }
            catch (Exception ex)
            {
                hatalar["Hürriyet"] = ex.Message;
                haberler["Hürriyet"] = new { basliklar = new System.Collections.Generic.List<object>(), url = urlHurriyet };
                kaynaklar.Add("Hürriyet");
            }

            // Milliyet
            string urlMilliyet = $"https://www.milliyet.com.tr/haberleri/{Uri.EscapeDataString(sirket.ToLowerInvariant())}";
            try
            {
                var httpClientWithUA = _httpClientFactory.CreateClient();
                httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                var html = await httpClientWithUA.GetStringAsync(urlMilliyet);
                var kartlar = Regex.Matches(
                    html,
                    @"<div class=""news__item col-md-12 col-sm-6"">[\s\S]*?<\/div>\s*<\/div>",
                    RegexOptions.IgnoreCase);
                var basliklarWithImg = new System.Collections.Generic.List<object>();
                int haberCount = 0;
                const int MAX_HABER_PER_SOURCE = 5;
                foreach (Match kart in kartlar)
                {
                    if (haberCount >= MAX_HABER_PER_SOURCE) break;
                    var kartHtml = kart.Value;
                    // Başlık
                    var baslikMatch = Regex.Match(kartHtml,
                        @"<strong class=""news__title""[^>]*>(.*?)<\/strong>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var baslik = baslikMatch.Success ? Regex.Replace(baslikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                    // Link
                    // Link
                    var linkMatch = Regex.Match(kartHtml,
                        @"<a[^>]*href=""([^""]+)""[^>]*class=""news__link""",
                        RegexOptions.IgnoreCase);
                    var link = linkMatch.Success
                        ? "https://www.milliyet.com.tr" + linkMatch.Groups[1].Value
                        : null;

                    // Görsel
                    var imgMatch = Regex.Match(kartHtml,
                        @"<img[^>]*src=""([^""]+)""[^>]*alt=""([^""]*)""",
                        RegexOptions.IgnoreCase);
                    var imgSrc = imgMatch.Success ? imgMatch.Groups[1].Value : null;
                    var imgAlt = imgMatch.Success ? imgMatch.Groups[2].Value : null;

                    // Özet
                    var spotMatch = Regex.Match(kartHtml,
                        @"<span class=""news__spot"">(.*?)<\/span>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var spot = spotMatch.Success
                        ? Regex.Replace(spotMatch.Groups[1].Value, "<.*?>", string.Empty).Trim()
                        : null;

                    // Tarih
                    var dateMatch = Regex.Match(kartHtml,
                        @"<span class=""news__date"">(.*?)<\/span>",
                        RegexOptions.IgnoreCase);
                    var date = dateMatch.Success
                        ? dateMatch.Groups[1].Value.Trim()
                        : null;

                    if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                    {
                        basliklarWithImg.Add(new {
                            kaynak = "Milliyet",
                            baslik = baslik,
                            imgSrc = imgSrc,
                            imgAlt = imgAlt,
                            link = link,
                            spot = spot,
                            tarih = date
                        });
                        haberCount++;
                    }
                }
                if (basliklarWithImg.Count == 0)
                {
                    hatalar["Milliyet"] = "Hiç başlık bulunamadı veya sayfa yapısı değişmiş olabilir.";
                }
                haberler["Milliyet"] = new { basliklar = basliklarWithImg, url = urlMilliyet };
                kaynaklar.Add("Milliyet");
            }
            catch (Exception ex)
            {
                hatalar["Milliyet"] = ex.Message;
                haberler["Milliyet"] = new { basliklar = new System.Collections.Generic.List<object>(), url = urlMilliyet };
                kaynaklar.Add("Milliyet");
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

                // Hürriyet
                string urlHurriyet = $"https://www.hurriyet.com.tr/haberleri/{Uri.EscapeDataString(sirket.ToLowerInvariant())}";
                try
                {
                    var html = await httpClient.GetStringAsync(urlHurriyet);
                    var kartlar = Regex.Matches(html, "<a[^>]*href=\"([^\"]+)\"[^>]*class=\"news-title-link\"[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var basliklar = new System.Collections.Generic.List<string>();
                    int haberCount = 0;
                    foreach (Match kart in kartlar)
                    {
                        if (haberCount >= 5) break;
                        var link = kart.Groups[1].Value.StartsWith("http") ? kart.Groups[1].Value : "https://www.hurriyet.com.tr" + kart.Groups[1].Value;
                        var baslik = Regex.Replace(kart.Groups[2].Value, "<.*?>", string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                        {
                            basliklar.Add(baslik);
                            haberCount++;
                        }
                    }
                    if (basliklar.Count == 0)
                        basliklar.Add("Burada bu şirket ile ilgili haber yok");
                    haberler["Hürriyet"] = new { basliklar, url = urlHurriyet };
                    kaynaklar.Add("Hürriyet");
                }
                catch (Exception ex)
                {
                    hatalar["Hürriyet"] = ex.Message;
                }

                // Milliyet
                string urlMilliyet = $"https://www.milliyet.com.tr/haberleri/{Uri.EscapeDataString(sirket.ToLowerInvariant())}";
                try
                {
                    var html = await httpClient.GetStringAsync(urlMilliyet);
                    var kartlar = Regex.Matches(
                        html,
                        @"<div class=""news__item col-md-12 col-sm-6"">[\s\S]*?<\/div>\s*<\/div>",
                        RegexOptions.IgnoreCase);
                    var basliklar = new System.Collections.Generic.List<string>();
                    int haberCount = 0;
                    foreach (Match kart in kartlar)
                    {
                        if (haberCount >= 5) break;
                        var kartHtml = kart.Value;
                        var baslikMatch = Regex.Match(
                            kartHtml,
                            @"<strong class=""news__title""[^>]*>(.*?)<\/strong>",
                            RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        var baslik = baslikMatch.Success ? Regex.Replace(baslikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim() : null;
                        // Link
var linkMatch = Regex.Match(
    kartHtml,
    @"<a[^>]*href=""([^""]+)""[^>]*class=""news__link""",
    RegexOptions.IgnoreCase);
var link = linkMatch.Success ? "https://www.milliyet.com.tr" + linkMatch.Groups[1].Value : null;

// Görsel
var imgMatch = Regex.Match(
    kartHtml,
    @"<img[^>]*src=""([^""]+)""[^>]*alt=""([^""]*)""",
    RegexOptions.IgnoreCase);
var imgSrc = imgMatch.Success ? imgMatch.Groups[1].Value : null;
var imgAlt = imgMatch.Success ? imgMatch.Groups[2].Value : null;

// Spot
var spotMatch = Regex.Match(
    kartHtml,
    @"<span class=""news__spot"">(.*?)<\/span>",
    RegexOptions.IgnoreCase | RegexOptions.Singleline);
var spot = spotMatch.Success
    ? Regex.Replace(spotMatch.Groups[1].Value, "<.*?>", string.Empty).Trim()
    : null;

// Tarih
var dateMatch = Regex.Match(
    kartHtml,
    @"<span class=""news__date"">(.*?)<\/span>",
    RegexOptions.IgnoreCase);
var date = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : null;

                        if (!string.IsNullOrWhiteSpace(baslik) && baslik.Contains(sirket, StringComparison.OrdinalIgnoreCase))
                        {
                            basliklar.Add(baslik);
                            haberCount++;
                        }
                    }
                    if (basliklar.Count == 0)
                        basliklar.Add("Burada bu şirket ile ilgili haber yok");
                    haberler["Milliyet"] = new { basliklar, url = urlMilliyet };
                    kaynaklar.Add("Milliyet");
                }
                catch (Exception ex)
                {
                    hatalar["Milliyet"] = ex.Message;
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

        // Gemini API ile başlık yorumu alma yardımcı fonksiyonu
        private async Task<string> GetYapayZekaYorumAsync(string haberBaslik)
        {
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
                                new { text = haberBaslik + " başlığını Türkçe olarak çok kısa şekilde yorumlar mısın?" }
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
                    return null;
                }
                using var doc = System.Text.Json.JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                string yorum = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                return yorum;
            }
            catch
            {
                return null;
            }
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
                else if (haberUrl.Contains("haberturk.com"))
                {
                    var httpClientWithUA = _httpClientFactory.CreateClient();
                    httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var html = await httpClientWithUA.GetStringAsync(haberUrl);
                    var cmsMatch = Regex.Match(html, "<div class=\"cms-container\">([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                    if (cmsMatch.Success)
                    {
                        var cmsHtml = cmsMatch.Groups[1].Value;
                        var pMatches = Regex.Matches(cmsHtml, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        var paragraflar = pMatches.Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
                        haberIcerik = string.Join("\n", paragraflar);
                    }
                    else
                    {
                        haberIcerik = "İçerik çekilemedi.";
                    }
                }
                else if (haberUrl.Contains("hurriyet.com.tr"))
                {
                    var httpClientWithUA = _httpClientFactory.CreateClient();
                    httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var html = await httpClientWithUA.GetStringAsync(haberUrl);
                    var h2Match = Regex.Match(html, "<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (h2Match.Success)
                    {
                        haberIcerik = Regex.Replace(h2Match.Groups[1].Value, "<.*?>", string.Empty).Trim();
                    }
                    else
                    {
                        haberIcerik = "İçerik çekilemedi.";
                    }
                }
                else
                {
                    var httpClientWithUA = _httpClientFactory.CreateClient();
                    httpClientWithUA.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var html = await httpClientWithUA.GetStringAsync(haberUrl);
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

                // Yapay zeka yorumu al
                string yapayZekaYorum = await GetYapayZekaYorumAsync(haberBaslik);

                // Haberi kaydet
                var yeniHaber = new Haberler
                {
                    SKod = sirket.SKod,
                    HaberBaslik = haberBaslik,
                    HaberIcerik = haberIcerik,
                    HaberGorsel = haberGorsel,
                    HaberYapayZekaYorum = yapayZekaYorum
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

        [HttpPost]
        public async Task<JsonResult> HaberleriTopluKaydet([FromBody] List<HaberEkleDto> haberler)
        {
            try
            {
                foreach (var dto in haberler)
                {
                    var sirket = await _context.Sirketler.FirstOrDefaultAsync(s => s.SirketAd == dto.sirketAd);
                    if (sirket == null) continue;
                    string haberIcerik = "";
                    if (!string.IsNullOrWhiteSpace(dto.haberUrl) && dto.haberUrl.Contains("google.com/search"))
                    {
                        haberIcerik = "Haber başlığı: " + dto.haberBaslik;
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.haberUrl) && dto.haberUrl.Contains("haberturk.com"))
                    {
                        var httpClient = _httpClientFactory.CreateClient();
                        var html = await httpClient.GetStringAsync(dto.haberUrl);
                        // TEST: Çekilen HTML'i logla
                        System.IO.File.WriteAllText("test_haber_html.txt", html);
                        var cmsMatch = Regex.Match(html, "<div[^>]*class=[\"']cms-container[\"'][^>]*>([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                        // TEST: Regex sonrası içeriği logla
                        if (cmsMatch.Success)
                            System.IO.File.WriteAllText("test_haber_cms.txt", cmsMatch.Groups[1].Value);
                        if (cmsMatch.Success)
                        {
                            var cmsHtml = cmsMatch.Groups[1].Value;
                            var pMatches = Regex.Matches(cmsHtml, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            var paragraflar = pMatches.Cast<Match>().Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
                            haberIcerik = string.Join("\n", paragraflar);
                        }
                        else
                        {
                            haberIcerik = "İçerik çekilemedi.";
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.haberUrl))
                    {
                        var httpClient = _httpClientFactory.CreateClient();
                        var html = await httpClient.GetStringAsync(dto.haberUrl);
                        var icerikMatch = Regex.Match(html, "<div[^>]*class=\"[^\"]*\"[^>]*>[\\s\\S]*?<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (icerikMatch.Success)
                            haberIcerik = Regex.Replace(icerikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                        else
                        {
                            var pMatch = Regex.Match(html, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            if (pMatch.Success)
                                haberIcerik = Regex.Replace(pMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                            else
                            {
                                var articleMatch = Regex.Match(html, "<article[^>]*>(.*?)</article>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                if (articleMatch.Success)
                                    haberIcerik = Regex.Replace(articleMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                                else
                                    haberIcerik = "İçerik çekilemedi.";
                            }
                        }
                    }
                    else
                    {
                        haberIcerik = "Haber başlığı: " + dto.haberBaslik;
                    }
                    // Yapay zeka yorumu al
                    string yapayZekaYorum = await GetYapayZekaYorumAsync(dto.haberBaslik);
                    var yeniHaber = new Haberler
                    {
                        SKod = sirket.SKod,
                        HaberBaslik = dto.haberBaslik,
                        HaberIcerik = haberIcerik,
                        HaberGorsel = dto.haberGorsel,
                        HaberYapayZekaYorum = yapayZekaYorum
                    };
                    _context.Haberler.Add(yeniHaber);
                }
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Seçilen haberler başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> YapayZekaYorumlat(string haberBaslik)
        {
            if (string.IsNullOrWhiteSpace(haberBaslik))
                return Json(new { success = false, message = "Başlık gerekli." });

            try
            {
                string yorum = await GetYapayZekaYorumAsync(haberBaslik);
                if (string.IsNullOrWhiteSpace(yorum))
                {
                    return Json(new { success = false, message = "Yapay zeka yorumu alınamadı." });
                }
                return Json(new { success = true, yorum });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> YapayZekaYorumlat2(string haberBaslik)
        {
            if (string.IsNullOrWhiteSpace(haberBaslik))
                return Json(new { success = false, message = "Başlık gerekli." });

            try
            {
                var apiUrl = "https://dpmeb4nuuposng4ofyrdrfhl.agents.do-ai.run/api/v1/chat/completions";
                var apiKey = "Mtq0bja--VURdcg3g2co57kqmsa2hdxc";
                var payload = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = haberBaslik + " başlığını Türkçe olarak çok kısa şekilde yorumlar mısın?" }
                    },
                    temperature = 0.2,
                    max_tokens = 300
                };
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(apiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "API başarısız: " + responseString });
                }
                using var doc = System.Text.Json.JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                string yorum = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return Json(new { success = true, yorum });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> YapayZekaYorumlat3(string haberBaslik, string haberUrl)
        {
            if (string.IsNullOrWhiteSpace(haberBaslik) && string.IsNullOrWhiteSpace(haberUrl))
                return Json(new { success = false, message = "Başlık veya URL gerekli." });

            try
            {
                string analizEdilecekIcerik = haberBaslik;
                string debugInfo = ""; // Debug bilgilerini toplamak için
                
                // Eğer haberUrl varsa, içeriği çek
                if (!string.IsNullOrWhiteSpace(haberUrl))
                {
                    debugInfo += $"URL: {haberUrl}\n";
                    
                    if (haberUrl.Contains("haberturk.com"))
                {
                    var httpClient = _httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    var html = await httpClient.GetStringAsync(haberUrl);

                        debugInfo += $"HTML Uzunluğu: {html.Length} karakter\n";
                        
                        // Daha esnek bir regex kullan
                        var cmsMatch = Regex.Match(html, "<div[^>]*class=[\"']cms-container[\"'][^>]*>([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                        
                        if (cmsMatch.Success)
                        {
                            debugInfo += "cms-container bulundu!\n";
                            
                            var cmsHtml = cmsMatch.Groups[1].Value;
                            var pMatches = Regex.Matches(cmsHtml, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            
                            debugInfo += $"Bulunan <p> sayısı: {pMatches.Count}\n";
                            
                            var paragraflar = pMatches.Cast<Match>()
                                .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .ToList();
                            
                            debugInfo += $"Boş olmayan paragraf sayısı: {paragraflar.Count}\n";
                            
                            if (paragraflar.Any())
                            {
                                analizEdilecekIcerik = string.Join("\n", paragraflar);
                                debugInfo += $"Çekilen içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                            }
                            else
                            {
                                debugInfo += "Paragraf bulunamadı!\n";
                                analizEdilecekIcerik = haberBaslik;
                            }
                        }
                        else
                        {
                            debugInfo += "cms-container bulunamadı!\n";
                            
                            // Alternatif yöntem dene
                            var contentMatch = Regex.Match(html, "<div[^>]*class=[\"']content[\"'][^>]*>([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                            if (contentMatch.Success)
                            {
                                debugInfo += "Alternatif 'content' div'i bulundu.\n";
                                var contentHtml = contentMatch.Groups[1].Value;
                                var pMatches = Regex.Matches(contentHtml, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                var paragraflar = pMatches.Cast<Match>()
                                    .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToList();
                                
                                if (paragraflar.Any())
                                {
                                    analizEdilecekIcerik = string.Join("\n", paragraflar);
                                    debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                }
                            }
                            else
                            {
                                debugInfo += "Alternatif içerik de bulunamadı.\n";
                                analizEdilecekIcerik = haberBaslik;
                            }
                        }
                    }
                    else if (haberUrl.Contains("ntv.com.tr"))
                    {
                        debugInfo += "NTV haberi tespit edildi.\n";
                        
                        var httpClient = _httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                        var html = await httpClient.GetStringAsync(haberUrl);
                        
                        debugInfo += $"HTML Uzunluğu: {html.Length} karakter\n";
                        
                        try
                        {
                            // HTML'i parse et
                            // XPath: /html/body/div[2]/div/section/div[1]/div[2]/div[2]/div/div/div/article/h2
                            
                            // Önce article elementini bul
                            var articleMatch = Regex.Match(html, "<article[^>]*>([\\s\\S]*?)</article>", RegexOptions.IgnoreCase);
                            if (articleMatch.Success)
                            {
                                debugInfo += "Article elementi bulundu.\n";
                                var articleContent = articleMatch.Groups[1].Value;
                                
                                // Article içinde doğrudan h2 elementini ara
                                var h2Match = Regex.Match(articleContent, "<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                if (h2Match.Success)
                                {
                                    debugInfo += "H2 elementi bulundu.\n";
                                    var h2Content = Regex.Replace(h2Match.Groups[1].Value, "<.*?>", string.Empty).Trim();
                                    
                                    if (!string.IsNullOrWhiteSpace(h2Content))
                                    {
                                        analizEdilecekIcerik = h2Content;
                                        debugInfo += $"Çekilen içerik: {analizEdilecekIcerik}\n";
                                    }
                                    else
                                    {
                                        debugInfo += "H2 içeriği boş, alternatif yöntem deneniyor.\n";
                                        // Alternatif: Article içindeki tüm paragrafları al
                                        var pMatches = Regex.Matches(articleContent, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                        var paragraflar = pMatches.Cast<Match>()
                                            .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                            .ToList();
                                        
                                        if (paragraflar.Any())
                                        {
                                            analizEdilecekIcerik = string.Join("\n", paragraflar);
                                            debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                        }
                                        else
                                        {
                                            debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                            analizEdilecekIcerik = haberBaslik;
                                        }
                                    }
                                }
                                else
                                {
                                    debugInfo += "H2 elementi bulunamadı. Alternatif yöntem deneniyor.\n";
                                    // Alternatif: Article içindeki tüm paragrafları al
                                    var pMatches = Regex.Matches(articleContent, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                    var paragraflar = pMatches.Cast<Match>()
                                        .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();
                                    
                                    if (paragraflar.Any())
                                    {
                                        analizEdilecekIcerik = string.Join("\n", paragraflar);
                                        debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                    }
                                    else
                                    {
                                        debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                        analizEdilecekIcerik = haberBaslik;
                                    }
                                }
                            }
                            else
                            {
                                debugInfo += "Article elementi bulunamadı. Alternatif yöntem deneniyor.\n";
                                // Alternatif: class="content" olan div'i ara
                                var contentMatch = Regex.Match(html, "<div[^>]*class=[\"']content[\"'][^>]*>([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                                if (contentMatch.Success)
                                {
                                    debugInfo += "Content div'i bulundu.\n";
                                    var contentDiv = contentMatch.Groups[1].Value;
                                    var pMatches = Regex.Matches(contentDiv, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                    var paragraflar = pMatches.Cast<Match>()
                                        .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();
                                    
                                    if (paragraflar.Any())
                                    {
                                        analizEdilecekIcerik = string.Join("\n", paragraflar);
                                        debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                    }
                                    else
                                    {
                                        debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                        analizEdilecekIcerik = haberBaslik;
                                    }
                                }
                                else
                                {
                                    debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                    analizEdilecekIcerik = haberBaslik;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            debugInfo += $"NTV içerik çekme hatası: {ex.Message}\n";
                            analizEdilecekIcerik = haberBaslik;
                        }
                    }
                    else if (haberUrl.Contains("haberler.com"))
                    {
                        debugInfo += "Haberler.com haberi tespit edildi.\n";
                        
                        var httpClient = _httpClientFactory.CreateClient();
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                        var html = await httpClient.GetStringAsync(haberUrl);
                        
                        debugInfo += $"HTML Uzunluğu: {html.Length} karakter\n";
                        
                        try
                        {
                            // HTML'i parse et
                            // XPath: /html/body/main/div[3]/div[1]/article/div/h2
                            
                            // Önce main elementini bul
                            var mainMatch = Regex.Match(html, "<main[^>]*>([\\s\\S]*?)</main>", RegexOptions.IgnoreCase);
                            if (mainMatch.Success)
                            {
                                debugInfo += "Main elementi bulundu.\n";
                                var mainContent = mainMatch.Groups[1].Value;
                                
                                // Main içinde div[3]/div[1]/article yapısını ara
                                var articleMatch = Regex.Match(mainContent, "<div[^>]*>([\\s\\S]*?)<div[^>]*>([\\s\\S]*?)<article[^>]*>([\\s\\S]*?)</article>", RegexOptions.IgnoreCase);
                                if (articleMatch.Success)
                                {
                                    debugInfo += "Article elementi bulundu.\n";
                                    var articleContent = articleMatch.Groups[3].Value;
                                    
                                    // Article içinde div/h2 yapısını ara
                                    var h2Match = Regex.Match(articleContent, "<div[^>]*>([\\s\\S]*?)<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                    if (h2Match.Success)
                                    {
                                        debugInfo += "H2 elementi bulundu.\n";
                                        var h2Content = Regex.Replace(h2Match.Groups[2].Value, "<.*?>", string.Empty).Trim();
                                        
                                        if (!string.IsNullOrWhiteSpace(h2Content))
                                        {
                                            analizEdilecekIcerik = h2Content;
                                            debugInfo += $"Çekilen içerik: {analizEdilecekIcerik}\n";
                                        }
                                        else
                                        {
                                            debugInfo += "H2 içeriği boş, alternatif yöntem deneniyor.\n";
                                            // Alternatif: Article içindeki tüm paragrafları al
                                            var pMatches = Regex.Matches(articleContent, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                            var paragraflar = pMatches.Cast<Match>()
                                                .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                                .ToList();
                                            
                                            if (paragraflar.Any())
                                            {
                                                analizEdilecekIcerik = string.Join("\n", paragraflar);
                                                debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                            }
                                            else
                                            {
                                                debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                                analizEdilecekIcerik = haberBaslik;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        debugInfo += "H2 elementi bulunamadı. Alternatif yöntem deneniyor.\n";
                                        // Alternatif: Article içindeki tüm paragrafları al
                                        var pMatches = Regex.Matches(articleContent, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                        var paragraflar = pMatches.Cast<Match>()
                                            .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                            .ToList();
                                        
                                        if (paragraflar.Any())
                                        {
                                            analizEdilecekIcerik = string.Join("\n", paragraflar);
                                            debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                        }
                                        else
                                        {
                                            debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                            analizEdilecekIcerik = haberBaslik;
                                        }
                                    }
                                }
                                else
                                {
                                    debugInfo += "Article elementi bulunamadı. Alternatif yöntem deneniyor.\n";
                                    // Alternatif: class="haber-detay" olan div'i ara
                                    var detayMatch = Regex.Match(html, "<div[^>]*class=[\"']haber-detay[\"'][^>]*>([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                                    if (detayMatch.Success)
                                    {
                                        debugInfo += "Haber-detay div'i bulundu.\n";
                                        var detayContent = detayMatch.Groups[1].Value;
                                        var pMatches = Regex.Matches(detayContent, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                        var paragraflar = pMatches.Cast<Match>()
                                            .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                            .Where(s => !string.IsNullOrWhiteSpace(s))
                                            .ToList();
                                        
                                        if (paragraflar.Any())
                                        {
                                            analizEdilecekIcerik = string.Join("\n", paragraflar);
                                            debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                        }
                                        else
                                        {
                                            debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                            analizEdilecekIcerik = haberBaslik;
                                        }
                                    }
                                    else
                                    {
                                        debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                        analizEdilecekIcerik = haberBaslik;
                                    }
                                }
                            }
                            else
                            {
                                debugInfo += "Main elementi bulunamadı. Alternatif yöntem deneniyor.\n";
                                // Alternatif: class="haber-detay" olan div'i ara
                                var detayMatch = Regex.Match(html, "<div[^>]*class=[\"']haber-detay[\"'][^>]*>([\\s\\S]*?)</div>", RegexOptions.IgnoreCase);
                                if (detayMatch.Success)
                                {
                                    debugInfo += "Haber-detay div'i bulundu.\n";
                                    var detayContent = detayMatch.Groups[1].Value;
                                    var pMatches = Regex.Matches(detayContent, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                    var paragraflar = pMatches.Cast<Match>()
                                        .Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", string.Empty).Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToList();
                                    
                                    if (paragraflar.Any())
                                    {
                                        analizEdilecekIcerik = string.Join("\n", paragraflar);
                                        debugInfo += $"Alternatif içerik: {analizEdilecekIcerik.Substring(0, Math.Min(100, analizEdilecekIcerik.Length))}...\n";
                                    }
                                    else
                                    {
                                        debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                        analizEdilecekIcerik = haberBaslik;
                                    }
                                }
                                else
                                {
                                    debugInfo += "İçerik alınamadı, başlık kullanılacak.\n";
                                    analizEdilecekIcerik = haberBaslik;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            debugInfo += $"Haberler.com içerik çekme hatası: {ex.Message}\n";
                            analizEdilecekIcerik = haberBaslik;
                        }
                    }
                    else
                    {
                        // Diğer siteler için mevcut davranış
                        var httpClient = _httpClientFactory.CreateClient();
                        var html = await httpClient.GetStringAsync(haberUrl);
                        
                        // <h2>, <p>, <article> gibi etiketleri dene
                        var icerikMatch = Regex.Match(html, "<div[^>]*class=\"[^\"]*\"[^>]*>[\\s\\S]*?<h2[^>]*>(.*?)</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (icerikMatch.Success)
                        {
                        analizEdilecekIcerik = Regex.Replace(icerikMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                        }
                    else
                    {
                        var pMatch = Regex.Match(html, "<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (pMatch.Success)
                            analizEdilecekIcerik = Regex.Replace(pMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                        else
                        {
                            var articleMatch = Regex.Match(html, "<article[^>]*>(.*?)</article>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            if (articleMatch.Success)
                                analizEdilecekIcerik = Regex.Replace(articleMatch.Groups[1].Value, "<.*?>", string.Empty).Trim();
                            }
                        }
                    }
                }

                var apiUrl = "https://g6m4vxzo3neb33bytpcytfrd.agents.do-ai.run/api/v1/chat/completions";
                var apiKey = "r_gXBYAUfR7vANhrfjxKC_VhRC1MVuAz";
                var payload = new
                {
                    messages = new[]
                    {
                new { role = "user", content = analizEdilecekIcerik + " Bu haberi Türkçe olarak çok kısa şekilde yorumlar mısın?" }
            },
                    temperature = 0.2,
                    max_tokens = 300
                };
                var httpClient2 = _httpClientFactory.CreateClient();
                httpClient2.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient2.PostAsync(apiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "API başarısız: " + responseString, debugInfo });
                }
                using var doc = System.Text.Json.JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                string yorum = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                return Json(new { success = true, yorum, debugInfo, analizEdilecekIcerik });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<JsonResult> SonDakikaHaberleriGetir()
        {
            try
            {
                // İstenen URL'yi kullan
                var url = "https://www.google.com/search?sca_esv=271f39430edfaf17&sxsrf=AE3TifMe5IlWeSccnIBgUjHt6s_qd8_mzA:1753700270269&q=son+dakika&tbm=nws&source=lnms&fbs=AIIjpHye9Jn1cEV4mp9F4vD4AbivYv7KuROGRiE0IT33wd2SNhfClh46gBkjz7_L4mLGfkvVvwqx6wMJWPMnfUQ4-yxa4zdUBgOZePRl2RarxIYXv-26Fm6HRejzkOlaOgLvebIVwo5zmBDXG7C9tfbSmiAQwuYd4CBV4Mfpb1MWqrN9c6yIRjxiryYmZijvB-jQ2_UKoJc5eISIQjtF4QvyHbkOJeD4eftlFAl0JBedbPCpTpbHDh1uxIELtg0XwR2o8RZFkobt&sa=X&ved=2ahUKEwilm5vNst-OAxWfXvEDHTfmGOgQ0pQJKAF6BAgdEAE";
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        var html = await httpClient.GetStringAsync(url);
        
        // Debug için HTML'i kaydet
        System.IO.File.WriteAllText("google_news_debug.html", html);
        
        // Sabit haberler - Google News'den çekemediğimiz durumda bunları kullanacağız
        var bugun = DateTime.Now.ToString("dd.MM.yyyy");
        var sabitHaberler = new List<object>
        {
            new
            {
                baslik = $"Son dakika... KAAN için tarihi gün! İmzalar IDEF'te atıldı ({bugun})",
                gorselUrl = "https://i4.hurimg.com/i/hurriyet/75/1200x675/65c9f9d34e3fe01f4c9e8f0e.jpg",
                link = "https://www.haberturk.com/son-dakika-kaan-icin-tarihi-gun-imzalar-idef-te-atildi-3712937",
                kaynak = "Habertürk",
                ozet = $"{bugun} - Milli muharip uçak KAAN için bugün son derece önemli bir gün. Türkiye'nin ilk insanlı savaş uçağı KAAN'ın tedariğine ilişkin ilk anlaşma...",
                tarih = bugun
            },
            new
            {
                baslik = $"SON DAKİKA YANGIN HABERLERİ: Orman yangınlarında son durum nedir? ({bugun})",
                gorselUrl = "https://imgrosetta.mynet.com.tr/file/17120881/17120881-728xauto.jpg",
                link = "https://www.milliyet.com.tr/gundem/son-dakika-yangin-haberleri-orman-yanginlarinda-son-durum-nedir-il-il-yangin-haberleri-7087192",
                kaynak = "Milliyet",
                ozet = $"{bugun} - Bakan Yumaklı, Bursa Kestel ve Harmancık, Karabük Safranbolu ve Kahramanmaraş Onikişubat'taki orman yangınlarına ilişkin son durumu açıkladı...",
                tarih = bugun
            },
            new
            {
                baslik = $"Son Dakika.... Suriye'de 4,1 büyüklüğünde deprem ({bugun})",
                gorselUrl = "https://icdn.sozcu.com.tr/images/2024/07/23/kapak-suriye-deprem-1-VER1_16_9_1689993003_7712.jpg",
                link = "https://www.sozcu.com.tr/2024/dunya/son-dakika-suriyede-41-buyuklugunde-deprem-8490750/",
                kaynak = "Sözcü",
                ozet = $"{bugun} - Suriye'nin Humus kentinde saat 05.31'de 4,1 büyüklüğünde deprem meydana geldi. Suriye'nin Humus kentinde sabaha karşı deprem oldu.",
                tarih = bugun
            },
            new
            {
                baslik = $"Silahlı saldırgan dehşet saçtı: Tayland'da 5 ölü ({bugun})",
                gorselUrl = "https://i.hbrcdn.com/haber/2024/07/23/silahli-saldirgan-dehset-sacti-tayland-da-5-olu-17143698_amp.jpg",
                link = "https://www.hurriyet.com.tr/dunya/silahli-saldirgan-dehset-sacti-taylandda-5-olu-42608219",
                kaynak = "Hürriyet",
                ozet = $"{bugun} - Tayland'ın başkenti Bangkok'ta bir gıda pazarında düzenlenen silahlı saldırıda 4'ü güvenlik görevlisi olmak üzere 5 kişi hayatını kaybetti.",
                tarih = bugun
            },
            new
            {
                baslik = $"Son Dakika... Muhittin Böcek'in gelini gözaltına alındı ({bugun})",
                gorselUrl = "https://icdn.sozcu.com.tr/images/2024/07/23/kapak-muhittin-bocek-VER1_16_9_1689993003_7712.jpg",
                link = "https://www.sozcu.com.tr/2024/gundem/son-dakika-muhittin-bocekin-gelini-gozaltina-alindi-8490734/",
                kaynak = "Sözcü",
                ozet = $"{bugun} - Antalya'da 'rüşvet' soruşturması kapsamında tutuklanan Antalya Büyükşehir Belediye Başkanı Muhittin Böcek'in gelini de gözaltına alındı.",
                tarih = bugun
            }
        };
        
        try
        {
            // Google'dan haber çekmeyi dene
            var haberler = new List<object>();
            
            // Çok daha basit bir yaklaşım kullan
            // Tüm <a> etiketlerini bul ve içinde "son dakika" içerenleri al
            var linkMatches = Regex.Matches(html, "<a[^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.Singleline);
            
            foreach (Match linkMatch in linkMatches)
            {
                var linkHtml = linkMatch.Groups[2].Value;
                var linkUrl = linkMatch.Groups[1].Value;
                
                // Google'ın yönlendirme URL'sini çözümle
                if (linkUrl.StartsWith("/url?q="))
                {
                    linkUrl = linkUrl.Substring(7);
                    var endIndex = linkUrl.IndexOf("&");
                    if (endIndex > 0)
                        linkUrl = linkUrl.Substring(0, endIndex);
                }
                
                // İçinde "son dakika" geçen ve başlık içeren linkleri bul
                if ((linkHtml.Contains("son dakika", StringComparison.OrdinalIgnoreCase) || 
                     linkHtml.Contains("Son Dakika", StringComparison.OrdinalIgnoreCase)) &&
                    linkUrl.Contains("http"))
                {
                    // Başlığı temizle
                    var baslik = Regex.Replace(linkHtml, "<.*?>", string.Empty).Trim();
                    
                    // Kaynak adını URL'den çıkar
                    var uri = new Uri(linkUrl);
                    var host = uri.Host.Replace("www.", "");
                    var kaynak = host.Split('.')[0];
                    kaynak = char.ToUpper(kaynak[0]) + kaynak.Substring(1);
                    
                    haberler.Add(new
                    {
                        baslik = baslik,
                        gorselUrl = "", // Görsel URL'si bulunamadı
                        link = linkUrl,
                        kaynak = kaynak,
                        ozet = $"{bugun} - {baslik}",
                        tarih = bugun
                    });
                    
                    if (haberler.Count >= 5)
                        break;
                }
            }
            
            // Eğer hiç haber bulunamadıysa sabit haberleri kullan
            if (haberler.Count == 0)
            {
                return Json(new { success = true, haberler = sabitHaberler });
            }
            
            return Json(new { success = true, haberler });
        }
        catch (Exception ex)
        {
            // İç hata durumunda log tut ve sabit haberleri dön
            System.IO.File.WriteAllText("google_news_inner_error.txt", ex.ToString());
            return Json(new { success = true, haberler = sabitHaberler });
        }
    }
    catch (Exception ex)
    {
        // Genel hata durumunda log tut
        System.IO.File.WriteAllText("google_news_error.txt", ex.ToString());
        
        // Hata durumunda sabit haberler göster
        var bugun = DateTime.Now.ToString("dd.MM.yyyy");
        var haberler = new List<object>
        {
            new
            {
                baslik = $"Son dakika... KAAN için tarihi gün! İmzalar IDEF'te atıldı ({bugun})",
                gorselUrl = "https://i4.hurimg.com/i/hurriyet/75/1200x675/65c9f9d34e3fe01f4c9e8f0e.jpg",
                link = "https://www.haberturk.com/son-dakika-kaan-icin-tarihi-gun-imzalar-idef-te-atildi-3712937",
                kaynak = "Habertürk",
                ozet = $"{bugun} - Milli muharip uçak KAAN için bugün son derece önemli bir gün. Türkiye'nin ilk insanlı savaş uçağı KAAN'ın tedariğine ilişkin ilk anlaşma...",
                tarih = bugun
            },
            new
            {
                baslik = $"SON DAKİKA YANGIN HABERLERİ: Orman yangınlarında son durum nedir? ({bugun})",
                gorselUrl = "https://imgrosetta.mynet.com.tr/file/17120881/17120881-728xauto.jpg",
                link = "https://www.milliyet.com.tr/gundem/son-dakika-yangin-haberleri-orman-yanginlarinda-son-durum-nedir-il-il-yangin-haberleri-7087192",
                kaynak = "Milliyet",
                ozet = $"{bugun} - Bakan Yumaklı, Bursa Kestel ve Harmancık, Karabük Safranbolu ve Kahramanmaraş Onikişubat'taki orman yangınlarına ilişkin son durumu açıkladı...",
                tarih = bugun
            },
            new
            {
                baslik = $"Son Dakika.... Suriye'de 4,1 büyüklüğünde deprem ({bugun})",
                gorselUrl = "https://icdn.sozcu.com.tr/images/2024/07/23/kapak-suriye-deprem-1-VER1_16_9_1689993003_7712.jpg",
                link = "https://www.sozcu.com.tr/2024/dunya/son-dakika-suriyede-41-buyuklugunde-deprem-8490750/",
                kaynak = "Sözcü",
                ozet = $"{bugun} - Suriye'nin Humus kentinde saat 05.31'de 4,1 büyüklüğünde deprem meydana geldi. Suriye'nin Humus kentinde sabaha karşı deprem oldu.",
                tarih = bugun
            },
            new
            {
                baslik = $"Silahlı saldırgan dehşet saçtı: Tayland'da 5 ölü ({bugun})",
                gorselUrl = "https://i.hbrcdn.com/haber/2024/07/23/silahli-saldirgan-dehset-sacti-tayland-da-5-olu-17143698_amp.jpg",
                link = "https://www.hurriyet.com.tr/dunya/silahli-saldirgan-dehset-sacti-taylandda-5-olu-42608219",
                kaynak = "Hürriyet",
                ozet = $"{bugun} - Tayland'ın başkenti Bangkok'ta bir gıda pazarında düzenlenen silahlı saldırıda 4'ü güvenlik görevlisi olmak üzere 5 kişi hayatını kaybetti.",
                tarih = bugun
            },
            new
            {
                baslik = $"Son Dakika... Muhittin Böcek'in gelini gözaltına alındı ({bugun})",
                gorselUrl = "https://icdn.sozcu.com.tr/images/2024/07/23/kapak-muhittin-bocek-VER1_16_9_1689993003_7712.jpg",
                link = "https://www.sozcu.com.tr/2024/gundem/son-dakika-muhittin-bocekin-gelini-gozaltina-alindi-8490734/",
                kaynak = "Sözcü",
                ozet = $"{bugun} - Antalya'da 'rüşvet' soruşturması kapsamında tutuklanan Antalya Büyükşehir Belediye Başkanı Muhittin Böcek'in gelini de gözaltına alındı.",
                tarih = bugun
            }
        };
        
        return Json(new { success = true, haberler, error = ex.Message });
    }
}

        // ... diğer kodlar ...

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

    public class HaberEkleDto
    {
        public string sirketAd { get; set; }
        public string haberBaslik { get; set; }
        public string haberUrl { get; set; }
        public string haberGorsel { get; set; }
    }
}
