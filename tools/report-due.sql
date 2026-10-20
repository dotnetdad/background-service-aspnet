-- Не ждать трёх ночи: время следующего запуска отчёта — минуту назад.
-- На ближайшем тике (раз в минуту) отчёт посчитает один инстанс.
UPDATE "ScheduledJobs" SET "NextRunAt" = now() - interval '1 minute'
WHERE "Name" = 'daily-sales';
