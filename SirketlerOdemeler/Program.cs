using Microsoft.EntityFrameworkCore;
using SirketlerOdemeler.Data;
using SirketlerOdemeler.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Veritabanýný migrate et
    context.Database.Migrate();

    // Sirketler tablosuna seed
    if (!context.Sirketler.Any())
    {
        context.Sirketler.AddRange(
            new Sirketler { SirketAd = "ABC Sigorta", SirketMail = "abc@mail.com"},
            new Sirketler { SirketAd = "DEF Sigorta", SirketMail = "def@mail.com"},
            new Sirketler { SirketAd = "GHI Sigorta", SirketMail = "ghý@mail.com" },
            new Sirketler { SirketAd = "JKL Sigorta", SirketMail = "jkl@mail.com" }


        );
        context.SaveChanges();
    }

    // Odemeler tablosuna seed
    if (!context.Odemeler.Any())
    {
        context.Odemeler.AddRange(
            new Odemeler { SKod = 1, OdenenTutar = 130400},
            new Odemeler { SKod = 1, OdenenTutar = 180400},
            new Odemeler { SKod = 2, OdenenTutar = 204000},
            new Odemeler { SKod = 2, OdenenTutar = 236000},
            new Odemeler { SKod = 2, OdenenTutar = 294000},
            new Odemeler { SKod = 3, OdenenTutar = 400040},
            new Odemeler { SKod = 4, OdenenTutar = 304000},
            new Odemeler { SKod = 4, OdenenTutar = 302000},
            new Odemeler { SKod = 4, OdenenTutar = 300000},
            new Odemeler { SKod = 4, OdenenTutar = 307000},
            new Odemeler { SKod = 4, OdenenTutar = 382000}
        );
        context.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
