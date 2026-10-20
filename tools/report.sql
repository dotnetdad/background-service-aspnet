-- Строка расписания отчёта и отчёты по дням (время — по Москве, как у магазина)
SELECT "Name", "NextRunAt" AT TIME ZONE 'Europe/Moscow' AS "следующий запуск, Москва"
FROM "ScheduledJobs";

SELECT "Day", "Orders", "Revenue", "CreatedAt" AT TIME ZONE 'Europe/Moscow' AS "посчитан, Москва"
FROM "DailySales" ORDER BY "CreatedAt";
