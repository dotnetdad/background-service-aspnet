-- Оплаченные заказы для выгрузки: 12 000 за октябрь 2026 и 11 000 за сентябрь 2026
WITH o AS (
  INSERT INTO "Orders" ("Id", "UserId", "Status", "CreatedAt")
  SELECT gen_random_uuid(), gen_random_uuid(), 1,
         timestamptz '2026-10-01 00:00+00' + g * interval '2 minutes'
  FROM generate_series(0, 11999) g
  UNION ALL
  SELECT gen_random_uuid(), gen_random_uuid(), 1,
         timestamptz '2026-09-01 00:00+00' + g * interval '3 minutes'
  FROM generate_series(0, 10999) g
  RETURNING "Id"
)
INSERT INTO "OrderItem" ("Product", "Quantity", "Price", "OrderId")
SELECT 'Mug', 1, 450, "Id" FROM o;
