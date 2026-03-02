using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoTrails.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                BEGIN TRY
                    IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                    AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'TrailsCatalog')
                    BEGIN
                        EXEC('CREATE FULLTEXT CATALOG TrailsCatalog AS DEFAULT;');
                    END
                END TRY
                BEGIN CATCH
                    PRINT CONCAT('Skipping full-text catalog creation: ', ERROR_MESSAGE());
                END CATCH
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                BEGIN TRY
                    IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes fi
                        INNER JOIN sys.objects o ON fi.object_id = o.object_id
                        WHERE o.name = 'Trails'
                    )
                    BEGIN
                        EXEC('CREATE FULLTEXT INDEX ON dbo.Trails(Name LANGUAGE 0, Description LANGUAGE 0, Location LANGUAGE 0) KEY INDEX PK_Trails;');
                    END
                END TRY
                BEGIN CATCH
                    PRINT CONCAT('Skipping full-text index creation: ', ERROR_MESSAGE());
                END CATCH
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                BEGIN TRY
                    IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                    AND EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes fi
                        INNER JOIN sys.objects o ON fi.object_id = o.object_id
                        WHERE o.name = 'Trails'
                    )
                    BEGIN
                        EXEC('DROP FULLTEXT INDEX ON dbo.Trails;');
                    END
                END TRY
                BEGIN CATCH
                    PRINT CONCAT('Skipping full-text index drop: ', ERROR_MESSAGE());
                END CATCH
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                BEGIN TRY
                    IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
                    AND EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'TrailsCatalog')
                    BEGIN
                        EXEC('DROP FULLTEXT CATALOG TrailsCatalog;');
                    END
                END TRY
                BEGIN CATCH
                    PRINT CONCAT('Skipping full-text catalog drop: ', ERROR_MESSAGE());
                END CATCH
                """,
                suppressTransaction: true);
        }
    }
}
