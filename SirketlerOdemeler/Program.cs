using Microsoft.EntityFrameworkCore;
using SirketlerOdemeler.Data;
using SirketlerOdemeler.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Veritabanını migrate et
    context.Database.Migrate();

    // Sirketler tablosuna seed
    if (!context.Sirketler.Any())
    {
        context.Sirketler.AddRange(
            new Sirketler { SirketAd = "Microsoft", SirketMail = "microsoft@mail.com" },
            new Sirketler { SirketAd = "Oracle", SirketMail = "oracle@mail.com" },
            new Sirketler { SirketAd = "Nvidia", SirketMail = "nvidia@mail.com" },
            new Sirketler { SirketAd = "Ford", SirketMail = "ford@mail.com" },
            new Sirketler { SirketAd = "Pegasus", SirketMail = "pegasus@mail.com" }
        );
        context.SaveChanges();
    }

    // Odemeler tablosuna seed
    if (!context.Odemeler.Any())
    {
        context.Odemeler.AddRange(
            new Odemeler { SKod = 1, OdenenTutar = 130400 },
            new Odemeler { SKod = 1, OdenenTutar = 180400 },
            new Odemeler { SKod = 1, OdenenTutar = 189400 },
            new Odemeler { SKod = 2, OdenenTutar = 204000 },
            new Odemeler { SKod = 2, OdenenTutar = 236000 },
            new Odemeler { SKod = 2, OdenenTutar = 236900 },
            new Odemeler { SKod = 2, OdenenTutar = 294000 },
            new Odemeler { SKod = 3, OdenenTutar = 400040 },
            new Odemeler { SKod = 3, OdenenTutar = 200040 },
            new Odemeler { SKod = 3, OdenenTutar = 410040 },
            new Odemeler { SKod = 3, OdenenTutar = 401040 },
            new Odemeler { SKod = 4, OdenenTutar = 304000 },
            new Odemeler { SKod = 4, OdenenTutar = 302000 },
            new Odemeler { SKod = 4, OdenenTutar = 300000 },
            new Odemeler { SKod = 4, OdenenTutar = 307000 },
            new Odemeler { SKod = 4, OdenenTutar = 382000 }
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
app.UseStaticFiles(); // Bu satırla wwwroot içeriğini servis et

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
