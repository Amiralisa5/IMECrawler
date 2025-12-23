using ImeCrawler.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImeCrawler.Api.Data;

public sealed class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

	public DbSet<ImeOffer> ImeOffers => Set<ImeOffer>();
	public DbSet<ImeSnapshot> ImeSnapshots => Set<ImeSnapshot>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ImeOffer>(e =>
		{
			e.HasKey(x => x.Id);
			e.Property(x => x.MainGroupName).HasMaxLength(200);
			e.Property(x => x.ProductName).HasMaxLength(400);
			e.Property(x => x.Symbol).HasMaxLength(100);

			// Avoid duplicates per day+sourcePk (if sourcePk exists)
			e.HasIndex(x => new { x.Day, x.SourcePk }).IsUnique(false);
		});

		modelBuilder.Entity<ImeSnapshot>(e =>
		{
			e.HasKey(x => x.Id);
			e.Property(x => x.MainGroupName).HasMaxLength(200);
			e.Property(x => x.ImageUrl).HasMaxLength(500);
			e.HasIndex(x => new { x.Day, x.MainGroupId });
		});
	}
}
