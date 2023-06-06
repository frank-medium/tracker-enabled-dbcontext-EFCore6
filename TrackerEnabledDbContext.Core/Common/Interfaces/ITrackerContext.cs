using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TrackerEnabledDbContext.Common.EventArgs;
using TrackerEnabledDbContext.Common.Models;

namespace TrackerEnabledDbContext.Core.Common.Interfaces
{
    public interface ITrackerContext : IDisposable
    {
        #region Not Sure how these were used

        DatabaseFacade Database { get; }
        IModel Model { get; }

        EntityEntry Entry(object entity);
        EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

        DbSet<TEntity> Set<TEntity>() where TEntity : class;

        #endregion

        ChangeTracker ChangeTracker { get; }

        DbSet<AuditLog> AuditLogs { get; set; }
        DbSet<AuditLogDetail> AuditLogDetails { get; set; }
        bool TrackingEnabled { get; set; }
        bool AdditionTrackingEnabled { get; set; }
        bool ModificationTrackingEnabled { get; set; }
        bool DeletionTrackingEnabled { get; set; }

        event EventHandler<AuditLogGeneratedEventArgs> OnAuditLogGenerated;
        
        void ConfigureUsername(Func<string> usernameFactory);
        void ConfigureUsername(string defaultUsername);
        void ConfigureMetadata(Action<dynamic> metadataConfiguration);

        IQueryable<AuditLog> GetLogs(string entityFullName);
        IQueryable<AuditLog> GetLogs(string entityFullName, object primaryKey);
        IQueryable<AuditLog> GetLogs<TEntity>();
        IQueryable<AuditLog> GetLogs<TEntity>(object primaryKey);

        int SaveChanges();
        int SaveChanges(object userName);

        Task<int> SaveChangesAsync();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
        Task<int> SaveChangesAsync(object userName, CancellationToken cancellationToken);
        Task<int> SaveChangesAsync(int userId);
        Task<int> SaveChangesAsync(string userName);
    }
}