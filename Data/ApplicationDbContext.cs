// File: Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using Demo.Models;

namespace Demo.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                // Primary key
                entity.HasKey(u => u.Id);
                
                // REQUIREMENT 1: Create unique index on email
                entity.HasIndex(u => u.Email)
                      .IsUnique()
                      .HasDatabaseName("IX_Users_Email_Unique");

                // Configure properties with constraints
                entity.Property(u => u.Name)
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasComment("User's full name");

                entity.Property(u => u.Email)
                      .IsRequired()
                      .HasMaxLength(100)
                      .HasComment("User's unique email address");

                entity.Property(u => u.PasswordHash)
                      .IsRequired()
                      .HasMaxLength(255)
                      .HasComment("Hashed password");

                entity.Property(u => u.MobileNumber)
                      .IsRequired()
                      .HasMaxLength(20)
                      .HasComment("User's mobile phone number");

                entity.Property(u => u.Gender)
                      .IsRequired()
                      .HasMaxLength(10)
                      .HasComment("User's gender");

                entity.Property(u => u.DateOfBirth)
                      .IsRequired()
                      .HasComment("User's date of birth");

                entity.Property(u => u.Status)
                      .HasDefaultValue(UserStatus.Active)
                      .HasComment("User account status");

                entity.Property(u => u.LastLogin)
                      .HasComment("User's last login timestamp");

                // Create additional indexes for performance
                entity.HasIndex(u => u.Status)
                      .HasDatabaseName("IX_Users_Status");

                entity.HasIndex(u => u.LastLogin)
                      .HasDatabaseName("IX_Users_LastLogin");

                // Create composite index for common queries
                entity.HasIndex(u => new { u.Status, u.LastLogin })
                      .HasDatabaseName("IX_Users_Status_LastLogin");
            });

            // Configure enum conversion
            modelBuilder.Entity<User>()
                       .Property(u => u.Status)
                       .HasConversion<string>();

            base.OnModelCreating(modelBuilder);
        }

        // No fallback configuration needed - configuration comes from Program.cs

        // Override SaveChanges to ensure data integrity
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry") == true)
            {
                // Handle unique constraint violations
                throw new InvalidOperationException("Email address is already registered.", ex);
            }
        }

        public override int SaveChanges()
        {
            try
            {
                return base.SaveChanges();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry") == true)
            {
                // Handle unique constraint violations
                throw new InvalidOperationException("Email address is already registered.", ex);
            }
        }
    }
}