using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    }
}
