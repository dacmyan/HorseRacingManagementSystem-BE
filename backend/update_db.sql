-- Delta script for AddHorseSoftDelete and AddWalletConcurrencyToken
-- Manually generated to resolve EF Migrations history sync issue with Azure DB.

BEGIN TRANSACTION;

-- Add soft delete fields to Horse
ALTER TABLE [Horse] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
ALTER TABLE [Horse] ADD [DeletedAt] datetime2 NULL;

-- Add concurrency token to Wallet
ALTER TABLE [Wallet] ADD [RowVersion] rowversion NOT NULL;

COMMIT;
