using MedicalFeaturePrototype.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicalFeaturePrototype.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<ProcessingRequest> ProcessingRequests => Set<ProcessingRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(64).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<ProcessingRequest>(entity =>
        {
            entity.ToTable("processing_requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.RequestId).HasMaxLength(80).IsRequired();
            entity.Property(request => request.Status).HasMaxLength(32).IsRequired();
            entity.Property(request => request.InternalMode).HasMaxLength(120).IsRequired();
            entity.Property(request => request.ErrorMessage).HasMaxLength(4000);
            entity.HasIndex(request => request.RequestId);
            entity.HasIndex(request => new { request.UserId, request.CreatedAt });

            entity.HasOne(request => request.User)
                .WithMany(user => user.ProcessingRequests)
                .HasForeignKey(request => request.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
