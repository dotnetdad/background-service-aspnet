-- Двадцать писем на несуществующий адрес, созданных час назад: они самые старые в очереди
WITH o AS (
  INSERT INTO "Orders" ("Id", "UserId", "Status", "CreatedAt")
  SELECT gen_random_uuid(), gen_random_uuid(), 0, now() - interval '1 hour'
  FROM generate_series(1, 20)
  RETURNING "Id", "CreatedAt"
), i AS (
  INSERT INTO "OrderItem" ("Product", "Quantity", "Price", "OrderId")
  SELECT 'Mug', 1, 450, "Id" FROM o
)
INSERT INTO "PendingEmails" ("Id", "OrderId", "To", "Attempts", "NextTryAt", "CreatedAt")
SELECT gen_random_uuid(), "Id", 'typo@example.invalid', 0, "CreatedAt", "CreatedAt" FROM o;
