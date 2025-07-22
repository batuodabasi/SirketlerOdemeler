using Microsoft.EntityFrameworkCore;
using SirketlerOdemeler.Models;

namespace SirketlerOdemeler.Data
{
    public class AppDbContext : DbContext
    {
        // Constructor: DbContextOptions parametresi alır ve base sınıfa iletir
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Veritabanında Sirketler adlı tabloyu bu tanımlayacak.
        public DbSet<Sirketler> Sirketler { get; set; }

        // Veritabanında Odemeler adlı tabloyu bu tablo tanımlayacack.
        public DbSet<Odemeler> Odemeler { get; set; }
    }
}
