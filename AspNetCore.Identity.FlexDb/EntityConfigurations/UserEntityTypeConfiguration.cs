using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Linq;

namespace AspNetCore.Identity.FlexDb.EntityConfigurations
{
    public class UserEntityTypeConfiguration<TUserEntity, TKey>
        : IEntityTypeConfiguration<TUserEntity>
        where TUserEntity : IdentityUser<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly string _tableName;
        private readonly PersonalDataConverter? _dataConverter;
        private readonly bool _isCosmosDb;

        public UserEntityTypeConfiguration(PersonalDataConverter? dataConverter, string tableName = "Identity", bool isCosmosDb = false)
        {
            _tableName = tableName;
            _dataConverter = dataConverter;
            _isCosmosDb = isCosmosDb;
        }

        public void Configure(EntityTypeBuilder<TUserEntity> builder)
        {
            builder.HasKey(_ => _.Id);
            builder.HasPartitionKey(_ => _.Id);
            builder.Property(_ => _.ConcurrencyStamp).IsETagConcurrency();

            builder.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
            builder.Property(u => u.UserName).HasMaxLength(256);
            builder.Property(u => u.NormalizedUserName).HasMaxLength(256);
            builder.Property(u => u.Email).HasMaxLength(256);
            builder.Property(u => u.NormalizedEmail).HasMaxLength(256);
            builder.Property(u => u.PhoneNumber).HasMaxLength(256);

            if (_dataConverter != null)
            {
                var personalDataProps = typeof(TUserEntity).GetProperties().Where(
                                prop => Attribute.IsDefined(prop, typeof(ProtectedPersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    if (p.PropertyType != typeof(string))
                    {
                        throw new InvalidOperationException("Can only protect strings.");
                    }
                    builder.Property(typeof(string), p.Name).HasConversion(_dataConverter);
                }
            }

            // Add unique indexes to enforce email and username uniqueness
            // Note: dotnet/efcore#35264 - Cosmos DB throws an error when indexes are detected
            // Only apply indexes for non-Cosmos providers (SQLite, SQL Server, MySQL)
            if (!_isCosmosDb)
            {
                builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique();
                builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
            }
            //b.HasMany<TUserClaim>().WithOne().HasForeignKey(uc => uc.UserId).IsRequired();
            //b.HasMany<TUserLogin>().WithOne().HasForeignKey(ul => ul.UserId).IsRequired();
            //b.HasMany<TUserToken>().WithOne().HasForeignKey(ut => ut.UserId).IsRequired();

            builder.ToContainer(_tableName);
        }
    }
}
