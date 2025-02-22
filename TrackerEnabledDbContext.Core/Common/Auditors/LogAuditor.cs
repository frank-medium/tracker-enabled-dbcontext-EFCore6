﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TrackerEnabledDbContext.Common.Configuration;
using TrackerEnabledDbContext.Common.Models;
using TrackerEnabledDbContext.Core.Common.Configuration;
using TrackerEnabledDbContext.Core.Common.Interfaces;

namespace TrackerEnabledDbContext.Core.Common.Auditors
{
    internal class LogAuditor : IDisposable
    {
        private readonly EntityEntry _dbEntry;

        internal LogAuditor(EntityEntry dbEntry)
        {
            _dbEntry = dbEntry;
        }

        public void Dispose()
        {
        }

        internal AuditLog CreateLogRecord(object userName, EventType eventType, ITrackerContext context, ExpandoObject metadata)
        {
            Type entityType = _dbEntry.Entity.GetType();

            if (!EntityTrackingConfiguration.IsTrackingEnabled(entityType))
            {
                return null;
            }

            DateTime changeTime = DateTime.UtcNow;

            //changed to static class by Aaron Sulwer 3/16/2018
            List<PropertyConfigurationKey> keyNames = (context as DbContext).GetKeyNames(_dbEntry).ToList();

            var newlog = new AuditLog
            {
                UserName = userName?.ToString(),
                EventDateUTC = changeTime,
                EventType = eventType,
                TypeFullName = entityType.BaseType.FullName,
                RecordId = GetPrimaryKeyValuesOf(_dbEntry, keyNames).ToString()
            };

            var logMetadata = metadata
                .Where(x => x.Value != null)
                .Select(m => new LogMetadata
                {
                    AuditLog = newlog,
                    Key = m.Key,
                    Value = m.Value?.ToString()
                })
            .ToList();

            newlog.Metadata = logMetadata;

            var detailsAuditor = GetDetailsAuditor(eventType, newlog);

            newlog.LogDetails = detailsAuditor.CreateLogDetails().ToList();

            if (newlog.LogDetails.Any())
                return newlog;
            else
                return null;
        }

        private ChangeLogDetailsAuditor GetDetailsAuditor(EventType eventType, AuditLog newlog)
        {
            switch (eventType)
            {
                case EventType.Added:
                    return new AdditionLogDetailsAuditor(_dbEntry, newlog);

                case EventType.Deleted:
                    return new DeletetionLogDetailsAuditor(_dbEntry, newlog);

                case EventType.Modified:
                    return new ChangeLogDetailsAuditor(_dbEntry, newlog);

                case EventType.SoftDeleted:
                    return new SoftDeletedLogDetailsAuditor(_dbEntry, newlog);

                case EventType.UnDeleted:
                    return new UnDeletedLogDetailsAudotor(_dbEntry, newlog);

                default:
                    return null;
            }
        }

        private object GetPrimaryKeyValuesOf(
            EntityEntry dbEntry,
            List<PropertyConfigurationKey> properties)
        {
            if (properties.Count == 1)
            {
                return OriginalValue(properties.First().PropertyName);
            }
            if (properties.Count > 1)
            {
                string output = "[";

                output += string.Join(",",
                    properties.Select(colName => OriginalValue(colName.PropertyName)));

                output += "]";
                return output;
            }
            throw new KeyNotFoundException("key not found for " + dbEntry.Entity.GetType().FullName);
        }

        protected virtual object OriginalValue(string propertyName)
        {
            object originalValue = null;

            if (GlobalTrackingConfig.DisconnectedContext)
            {
                originalValue = _dbEntry.GetDatabaseValues().GetValue<object>(propertyName);
            }
            else
            {
                originalValue = _dbEntry.Property(propertyName).OriginalValue;
            }

            return originalValue;
        }
    }
}