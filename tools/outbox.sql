-- Таблица исходящих писем: последние двадцать строк, сдавшиеся и заказы без строки письма
SELECT p."OrderId", p."To", p."Attempts", p."NextTryAt", p."SentAt", p."CreatedAt"
FROM "PendingEmails" p ORDER BY p."CreatedAt" DESC LIMIT 20;

SELECT count(*) AS "сдались, ждут разбора"
FROM "PendingEmails" WHERE "SentAt" IS NULL AND "Attempts" >= 5;

SELECT o."Id" AS "заказ без письма", o."CreatedAt"
FROM "Orders" o
WHERE NOT EXISTS (SELECT 1 FROM "PendingEmails" p WHERE p."OrderId" = o."Id")
ORDER BY o."CreatedAt" DESC LIMIT 20;

-- Письмо с адресом, который не исправить: строку удаляют руками (подставь номер заказа)
-- DELETE FROM "PendingEmails" WHERE "OrderId" = '00000000-0000-0000-0000-000000000000' AND "SentAt" IS NULL;
-- Адрес исправили — вернуть письмо в очередь:
-- UPDATE "PendingEmails" SET "To" = 'buyer@example.com', "Attempts" = 0, "NextTryAt" = now()
-- WHERE "OrderId" = '00000000-0000-0000-0000-000000000000' AND "SentAt" IS NULL;
