-- Неоплаченные заказы становятся старше получаса — чтобы не ждать: очистка отменит их на ближайшем проходе
UPDATE "Orders" SET "CreatedAt" = now() - interval '31 minutes' WHERE "Status" = 0;
