Добавление миграции: 

Add-Migration {MigrationName} -Project DbMigrator -StartupProject DbMigrator

Update-Database -Project DbMigrator -StartupProject DbMigrator
