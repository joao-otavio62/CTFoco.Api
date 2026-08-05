using CTFoco.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CTFoco.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RegistroPonto> RegistrosPonto => Set<RegistroPonto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<RegistroPonto>(entity =>
        {
            entity.HasOne(r => r.Usuario)
                  .WithMany(u => u.RegistrosPonto)
                  .HasForeignKey(r => r.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict); // Registros nunca podem "sumir" ao excluir usuário (auditoria)

            entity.HasIndex(r => new { r.UsuarioId, r.DataHoraRegistro });
        });
    }
}
