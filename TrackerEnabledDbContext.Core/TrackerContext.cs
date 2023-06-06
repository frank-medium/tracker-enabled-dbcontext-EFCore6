using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrackerEnabledDbContext.Common.Configuration;
using TrackerEnabledDbContext.Common.EventArgs;
using TrackerEnabledDbContext.Common.Models;
using TrackerEnabledDbContext.Core.Common;
using TrackerEnabledDbContext.Core.Common.Interfaces;

namespace TrackerEnabledDbContext.Core
{
    public class TrackerContext : DbContext, ITrackerContext
    {
        private readonly CoreTracker _coreTracker;
        private Func<string> _usernameFactory;
        private string _defaultUsername;
        private Action<dynamic> _metadataConfiguration;
        private bool _additionTrackingEnabled = true;
        private bool _modificationTrackingEnabled = true;
        private bool _deletionTrackingEnabled = true;

        public bool TrackingEnabled
        {
            get
            {
                return GlobalTrackingConfig.Enabled && (_additionTrackingEnabled || _modificationTrackingEnabled || _deletionTrackingEnabled);
            }
            set
            {
                _additionTrackingEnabled = value;
                _modificationTrackingEnabled = value;
                _deletionTrackingEnabled = value;
            }
        }
        public bool AdditionTrackingEnabled
        {
            get
            {
                return GlobalTrackingConfig.AdditionsEnabled && _additionTrackingEnabled;
            }
            set
            {
                _additionTrackingEnabled = value;
            }
        }
        public bool ModificationTrackingEnabled
        {
            get
            {
                return GlobalTrackingConfig.ModificationsEnabled && _modificationTrackingEnabled;
            }
            set
            {
                _modificationTrackingEnabled = value;
            }
        }
        public bool DeletionTrackingEnabled
        {
            get
            {
                return GlobalTrackingConfig.DeletionsEnabled && _deletionTrackingEnabled;
            }
            set
            {
                _deletionTrackingEnabled = value;
            }
        }

        public virtual DbSet<AuditLog> AuditLogs { get; set; }
        public virtual DbSet<AuditLogDetail> AuditLogDetails { get; set; }

        public virtual event EventHandler<AuditLogGeneratedEventArgs> OnAuditLogGenerated
        {
            add { _coreTracker.OnAuditLogGenerated += value; }
            remove { _coreTracker.OnAuditLogGenerated -= value; }
        }

        public TrackerContext()
        {
            _coreTracker = new CoreTracker(this);
        }
        public TrackerContext(DbContextOptions options) : base(options)
        {
            _coreTracker = new CoreTracker(this);
        }

        public virtual void ConfigureUsername(Func<string> usernameFactory)
        {
            _usernameFactory = usernameFactory;
        }
        public virtual void ConfigureUsername(string defaultUsername)
        {
            _defaultUsername = defaultUsername;
        }
        public virtual void ConfigureMetadata(Action<dynamic> metadataConfiguration)
        {
            _metadataConfiguration = metadataConfiguration;
        }
        
        /// <summary>
        ///     Get all logs for the given model type
        /// </summary>
        /// <typeparam name="TEntity">Type of domain model</typeparam>
        /// <returns></returns>
        public virtual IQueryable<AuditLog> GetLogs<TEntity>()
        {
            return _coreTracker.GetLogs<TEntity>();
        }
        /// <summary>
        ///     Get all logs for the given entity name
        /// </summary>
        /// <param name="entityName">full name of entity</param>
        /// <returns></returns>
        public virtual IQueryable<AuditLog> GetLogs(string entityName)
        {
            return _coreTracker.GetLogs(entityName);
        }
        /// <summary>
        ///     Get all logs for the given model type for a specific record
        /// </summary>
        /// <typeparam name="TEntity">Type of domain model</typeparam>
        /// <param name="primaryKey">primary key of record</param>
        /// <returns></returns>
        public virtual IQueryable<AuditLog> GetLogs<TEntity>(object primaryKey)
        {
            return _coreTracker.GetLogs<TEntity>(primaryKey);
        }
        /// <summary>
        ///     Get all logs for the given entity name for a specific record
        /// </summary>
        /// <param name="entityName">full name of entity</param>
        /// <param name="primaryKey">primary key of record</param>
        /// <returns></returns>
        public virtual IQueryable<AuditLog> GetLogs(string entityName, object primaryKey)
        {
            return _coreTracker.GetLogs(entityName, primaryKey);
        }

        /// <summary>
        ///     This method saves the model changes to the database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        ///     Always use this method instead of SaveChanges() whenever possible.
        /// </summary>
        /// <param name="userName">Username of the logged in identity</param>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual int SaveChanges(object userName)
        {
            if (!TrackingEnabled)
                return base.SaveChanges();

            OnBeforeSaving();

            dynamic metaData = new ExpandoObject();
            _metadataConfiguration?.Invoke(metaData);

            if (ModificationTrackingEnabled)
                _coreTracker.AuditModifications(userName, metaData);
            if (DeletionTrackingEnabled)
                _coreTracker.AuditDeletions(userName, metaData);

            int result;
            if (AdditionTrackingEnabled)
            {
                IEnumerable<EntityEntry> addedEntries = _coreTracker.GetAdditions();
                // Call the original SaveChanges(), which will save both the changes made and the audit records...Note that added entry auditing is still remaining.
                result = base.SaveChanges();
                //By now., we have got the primary keys of added entries of added entiries because of the call to savechanges.

                _coreTracker.AuditAdditions(userName, addedEntries, metaData);
                //save changes to audit of added entries
                base.SaveChanges();
            }
            else
            {
                //save changes
                result = base.SaveChanges();
            }

            return result;
        }
        /// <summary>
        ///     This method saves the model changes to the database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        /// </summary>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public override int SaveChanges()
        {
            if (!TrackingEnabled)
                return base.SaveChanges();

            OnBeforeSaving();

            return SaveChanges(_usernameFactory?.Invoke() ?? _defaultUsername);
        }

        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        /// </summary>
        /// <param name="userName">Username of the logged in identity</param>
        /// <param name="cancellationToken">
        ///     A System.Threading.CancellationToken to observe while waiting for the task
        ///     to complete.
        /// </param>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual async Task<int> SaveChangesAsync(object userName, CancellationToken cancellationToken)
        {
            if (!TrackingEnabled)
                return await base.SaveChangesAsync(cancellationToken);

            OnBeforeSaving();

            if (cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();

            dynamic metadata = new ExpandoObject();
            _metadataConfiguration?.Invoke(metadata);

            if (ModificationTrackingEnabled)
                _coreTracker.AuditModifications(userName, metadata);
            if (DeletionTrackingEnabled)
                _coreTracker.AuditDeletions(userName, metadata);

            int result;
            if (AdditionTrackingEnabled)
            {
                IEnumerable<EntityEntry> addedEntries = _coreTracker.GetAdditions();

                // Call the original SaveChanges(), which will save both the changes made and the audit records...Note that added entry auditing is still remaining.
                result = await base.SaveChangesAsync(cancellationToken);

                //By now., we have got the primary keys of added entries of added entiries because of the call to savechanges.
                _coreTracker.AuditAdditions(userName, addedEntries, metadata);

                //save changes to audit of added entries
                await base.SaveChangesAsync(cancellationToken);
            }
            else
            {
                //save changes
                result = await base.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        ///     Always use this method instead of SaveChangesAsync() whenever possible.
        /// </summary>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual async Task<int> SaveChangesAsync(int userId)
        {
            if (!TrackingEnabled)
                return await base.SaveChangesAsync(CancellationToken.None);

            OnBeforeSaving();

            return await SaveChangesAsync(userId, CancellationToken.None);
        }
        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        ///     Always use this method instead of SaveChangesAsync() whenever possible.
        /// </summary>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual async Task<int> SaveChangesAsync(string userName)
        {
            if (!TrackingEnabled)
                return await base.SaveChangesAsync(CancellationToken.None);

            OnBeforeSaving();

            return await SaveChangesAsync(userName, CancellationToken.None);
        }        
        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table with null UserName.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A System.Threading.CancellationToken to observe while waiting for the task
        ///     to complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous save operation.  The task result
        ///     contains the number of objects written to the underlying database.
        /// </returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!TrackingEnabled)
                return await base.SaveChangesAsync(cancellationToken);

            OnBeforeSaving();

            return await SaveChangesAsync(_usernameFactory?.Invoke() ?? _defaultUsername, cancellationToken);
        }
        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table with null UserName.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous save operation.  The task result
        ///     contains the number of objects written to the underlying database.
        /// </returns>        
        public virtual async Task<int> SaveChangesAsync()
        {
            if (!TrackingEnabled)
                return await base.SaveChangesAsync(CancellationToken.None);

            OnBeforeSaving();

            return await SaveChangesAsync(_usernameFactory?.Invoke() ?? _defaultUsername, CancellationToken.None);
        }

        private void OnBeforeSaving()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                var isSoftDeletable = GlobalTrackingConfig.SoftDeletableType?.IsInstanceOfType(entry);
                if (isSoftDeletable.HasValue && entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.CurrentValues[GlobalTrackingConfig.SoftDeletablePropertyName] = true;
                }
            }
        }
    }
}
