-- Сколько писем ушло, сколько ждёт и сколько с ошибкой
SELECT count(*) FILTER (WHERE "SentAt" IS NOT NULL) AS "ушло",
       count(*) FILTER (WHERE "SentAt" IS NULL AND "Attempts" = 0) AS "ждёт",
       count(*) FILTER (WHERE "SentAt" IS NULL AND "Attempts" > 0) AS "с ошибкой, ждёт повтора",
       min("SentAt") AS "первое", max("SentAt") AS "последнее"
FROM "PendingEmails";
