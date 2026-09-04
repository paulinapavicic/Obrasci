using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Obrasci.Models;
using System.Collections.Generic;

namespace Obrasci.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Photo> Photos { get; set; } = default!;

       
        public DbSet<UserActionLog> UserActionLogs { get; set; } = default!;

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");

                entity.HasKey(token => token.Id);

                entity.HasIndex(token => token.TokenHash)
                    .IsUnique();

                entity.HasIndex(token => token.FamilyId);

                entity.HasIndex(token => token.UserId);

                entity.Property(token => token.TokenHash)
                    .IsRequired();

                entity.Property(token => token.UserId)
                    .IsRequired();

                entity.Property(token => token.FamilyId)
                    .IsRequired();
            });
        }
    }
}
