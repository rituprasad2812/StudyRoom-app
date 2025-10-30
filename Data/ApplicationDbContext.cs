using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyRoom.Models;

namespace StudyRoom.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<StudySession> StudySessions => Set<StudySession>();
        public DbSet<Badge> Badges => Set<Badge>();
        public DbSet<UserBadge> UserBadges => Set<UserBadge>();

        public DbSet<RoomTask> RoomTasks => Set<RoomTask>();

        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<PollOption> PollOptions => Set<PollOption>();
        public DbSet<PollVote> PollVotes => Set<PollVote>();
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // RoomMember composite key + relations
            b.Entity<RoomMember>().HasKey(x => new { x.RoomId, x.UserId });
            b.Entity<RoomMember>()
                .HasOne(x => x.Room)
                .WithMany(r => r.Members)
                .HasForeignKey(x => x.RoomId);
            b.Entity<RoomMember>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Room owner (restrict delete if owner)
            b.Entity<Room>()
                .HasOne(r => r.Owner)
                .WithMany()
                .HasForeignKey(r => r.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Helpful indexes
            b.Entity<Message>().HasIndex(m => new { m.RoomId, m.CreatedAt });
            b.Entity<StudySession>().HasIndex(s => new { s.UserId, s.StartedAt });
            b.Entity<StudySession>().HasIndex(s => new { s.RoomId, s.StartedAt });

            // Badges: composite PK and relations
            b.Entity<UserBadge>().HasKey(ub => new { ub.UserId, ub.BadgeId });
            b.Entity<UserBadge>()
                .HasOne(ub => ub.User)
                .WithMany()
                .HasForeignKey(ub => ub.UserId);
            b.Entity<UserBadge>()
                .HasOne(ub => ub.Badge)
                .WithMany()
                .HasForeignKey(ub => ub.BadgeId);

            b.Entity<RoomTask>()
            .HasOne(t => t.Room)
            .WithMany()
            .HasForeignKey(t => t.RoomId);

            b.Entity<RoomTask>()
            .HasOne(t => t.Creator)
            .WithMany()
            .HasForeignKey(t => t.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

            b.Entity<RoomTask>()
            .HasIndex(t => new { t.RoomId, t.Status, t.DueAt });

            b.Entity<RoomTask>()
            .Property(t => t.Status)
            .HasMaxLength(12);

            // Poll relations
            b.Entity<Poll>()
            .HasOne(p => p.Room)
            .WithMany()
            .HasForeignKey(p => p.RoomId);

            b.Entity<Poll>()
            .HasOne(p => p.Creator)
            .WithMany()
            .HasForeignKey(p => p.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

            b.Entity<Poll>()
            .HasIndex(p => new { p.RoomId, p.CreatedAt });

            // Options
            b.Entity<PollOption>()
            .HasOne(o => o.Poll)
            .WithMany(p => p.Options)
            .HasForeignKey(o => o.PollId)
            .OnDelete(DeleteBehavior.Cascade);

            b.Entity<PollOption>()
            .HasIndex(o => new { o.PollId, o.Position });

            // Votes
            b.Entity<PollVote>()
            .HasKey(v => new { v.PollId, v.UserId }); // single-choice unique per poll

            b.Entity<PollVote>()
            .HasOne(v => v.Poll)
            .WithMany(p => p.Votes)
            .HasForeignKey(v => v.PollId)
            .OnDelete(DeleteBehavior.Cascade);

            b.Entity<PollVote>()
            .HasOne(v => v.Option)
            .WithMany()
            .HasForeignKey(v => v.OptionId)
            .OnDelete(DeleteBehavior.Cascade);
        }

    }
}