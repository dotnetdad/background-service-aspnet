-- Таблица исходящих писем: последние двадцать строк и заказы без строки письма
SELECT p."OrderId", p."To", p."CreatedAt", p."SentAt"
FROM "PendingEmails" p ORDER BY p."CreatedAt" DESC LIMIT 20;

SELECT o."Id" AS "заказ без письма", o."CreatedAt"
FROM "Orders" o
WHERE NOT EXISTS (SELECT 1 FROM "PendingEmails" p WHERE p."OrderId" = o."Id")
ORDER BY o."CreatedAt" DESC LIMIT 20;
