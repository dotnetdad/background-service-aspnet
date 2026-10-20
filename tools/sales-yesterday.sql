-- Пять оплаченных заказов вчера в полдень по Москве — чтобы отчёту было что считать
WITH o AS (
  INSERT INTO "Orders" ("Id", "UserId", "Status", "CreatedAt")
  SELECT gen_random_uuid(), gen_random_uuid(), 1,
         (date_trunc('day', now() AT TIME ZONE 'Europe/Moscow') - interval '12 hours')
           AT TIME ZONE 'Europe/Moscow'
  FROM generate_series(1, 5)
  RETURNING "Id"
)
INSERT INTO "OrderItem" ("Product", "Quantity", "Price", "OrderId")
SELECT 'Mug', 2, 450, "Id" FROM o;
