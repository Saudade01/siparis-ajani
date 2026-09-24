using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SiparisAjani.Infrastructure.Persistence;

/// <summary>
/// Yalnızca <c>dotnet ef</c> araçları içindir (göç üretmek). Uygulamada kullanılmaz;
/// bağlantı dizesi çalışma zamanında <c>Database:ConnectionString</c> ile verilir.
/// </summary>
public sealed class OrderAgentDbContextFactory : IDesignTimeDbContextFactory<OrderAgentDbContext>
{
    public OrderAgentDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<OrderAgentDbContext> builder = new();
        builder.UseSqlite("Data Source=siparisajani-design.db");
        return new OrderAgentDbContext(builder.Options);
    }
}