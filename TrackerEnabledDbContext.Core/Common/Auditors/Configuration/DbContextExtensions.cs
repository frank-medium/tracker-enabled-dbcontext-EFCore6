using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TrackerEnabledDbContext.Common.Configuration;

namespace TrackerEnabledDbContext.Core.Common.Configuration
{
    //https://stackoverflow.com/questions/30688909/how-to-get-primary-key-value-with-entity-framework-core
    internal static class DbContextExtensions
    {
        public static IEnumerable<PropertyConfigurationKey> GetKeyNames(this DbContext context, EntityEntry entityEntry)
        {
            var entityType = entityEntry.Entity.GetType();

            var fullName = entityType.BaseType.FullName;
            return entityEntry.Metadata.FindPrimaryKey().Properties.Select(x => new PropertyConfigurationKey(x.Name, fullName));
        }
    }
}
