using Microsoft.EntityFrameworkCore;
using PositiveNews.Core.Entities;

namespace PositiveNews.Core.Data;

public sealed class PositiveNewsDbContext(DbContextOptions<PositiveNewsDbContext> options)
    : DbContext(options)
{
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PipelineStep> PipelineSteps => Set<PipelineStep>();
    public DbSet<PipelineCandidate> PipelineCandidates => Set<PipelineCandidate>();
    public DbSet<NewsStory> NewsStories => Set<NewsStory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PipelineRun>()
            .HasIndex(r => r.RunDate)
            .IsUnique();

        modelBuilder.Entity<PipelineRun>()
            .Property(r => r.Status)
            .HasConversion<string>();

        modelBuilder.Entity<PipelineRun>()
            .HasMany(r => r.Steps)
            .WithOne(s => s.Run)
            .HasForeignKey(s => s.PipelineRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PipelineRun>()
            .HasMany(r => r.Candidates)
            .WithOne(c => c.Run)
            .HasForeignKey(c => c.PipelineRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PipelineRun>()
            .HasMany(r => r.Stories)
            .WithOne(s => s.Run)
            .HasForeignKey(s => s.PipelineRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PipelineStep>()
            .Property(s => s.Status)
            .HasConversion<string>();

        // One candidate per (run, URL) — SearchAgent already dedupes within a single
        // search pass, but a resumed run re-searching must not create duplicate rows.
        modelBuilder.Entity<PipelineCandidate>()
            .HasIndex(c => new { c.PipelineRunId, c.Url })
            .IsUnique();
    }
}
