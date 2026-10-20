-- Заказы с письмами разом, по умолчанию шестьдесят. Другое число:
--   docker compose exec -T db psql -U shop -v n=1000 < tools/seed.sql
-- Письма — на seed@example.com, в теме письма номер заказа: дубль виден по теме.
\if :{?n}
\else
  \set n 60
\endif
WITH o AS (
  INSERT INTO "Orders" ("Id", "UserId", "Status", "CreatedAt")
  SELECT gen_random_uuid(), gen_random_uuid(), 0, now() + g * interval '1 millisecond'
  FROM generate_series(1, :n) g
  RETURNING "Id", "CreatedAt"
), i AS (
  INSERT INTO "OrderItem" ("Product", "Quantity", "Price", "OrderId")
  SELECT 'Mug', 1, 450, "Id" FROM o
)
INSERT INTO "PendingEmails" ("Id", "OrderId", "To", "Attempts", "NextTryAt", "CreatedAt")
SELECT gen_random_uuid(), "Id", 'seed@example.com', 0, "CreatedAt", "CreatedAt" FROM o;
