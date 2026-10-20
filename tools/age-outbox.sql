-- Не ждать десять минут: неотправленные письма становятся на одиннадцать минут старше
UPDATE "PendingEmails" SET "CreatedAt" = "CreatedAt" - interval '11 minutes' WHERE "SentAt" IS NULL;
