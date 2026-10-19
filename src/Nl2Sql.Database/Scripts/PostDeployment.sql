-- Runs after every deploy. Keeps ai.Examples identical to this list.
MERGE ai.Examples AS target
USING (VALUES
    (1, N'Busiest hour at each location last week',
        N'WITH hourly AS (
    SELECT LocationName, LocalHour, COUNT(*) AS Orders
    FROM ai.Orders
    WHERE LocalDate >= DATEADD(day, -7, CAST(SYSDATETIME() AS date)) AND LocalDate < CAST(SYSDATETIME() AS date)
    GROUP BY LocationName, LocalHour
)
SELECT LocationName, LocalHour, Orders
FROM (SELECT LocationName, LocalHour, Orders, RANK() OVER (PARTITION BY LocationName ORDER BY Orders DESC) AS Ranking FROM hourly) AS ranked
WHERE Ranking = 1
ORDER BY LocationName;',
        N'"Last week" means the 7 days before today. Group by LocalHour, which is already in local time.'),
    (2, N'Net revenue by location last month',
        N'SELECT LocationName, SUM(NetRevenue) AS NetRevenue
FROM ai.Orders
WHERE LocalDate >= DATEADD(month, -1, DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1))
  AND LocalDate < DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1)
GROUP BY LocationName
ORDER BY NetRevenue DESC;',
        N'Revenue questions use NetRevenue: after refunds, excluding tax and tips. "Last month" is the previous calendar month.'),
    (3, N'Oat milk share of lattes, month over month',
        N'SELECT DATEFROMPARTS(YEAR(LocalDate), MONTH(LocalDate), 1) AS Month,
       CAST(1.0 * SUM(CASE WHEN MilkType = ''oat'' THEN Quantity ELSE 0 END) / NULLIF(SUM(Quantity), 0) AS decimal(5, 3)) AS OatShare
FROM ai.OrderLines
WHERE IsLatte = 1
GROUP BY DATEFROMPARTS(YEAR(LocalDate), MONTH(LocalDate), 1)
ORDER BY Month;',
        N'IsLatte covers hot and iced lattes. MilkType is on ai.OrderLines. Shares of drinks count items (SUM(Quantity)), not order lines.'),
    (4, N'Top 5 drinks by net revenue this quarter',
        N'SELECT TOP (5) MenuItemName, SUM(LineNetRevenue) AS NetRevenue
FROM ai.OrderLines
WHERE Category <> ''food''
  AND LocalDate >= DATEFROMPARTS(YEAR(SYSDATETIME()), (DATEPART(quarter, SYSDATETIME()) - 1) * 3 + 1, 1)
GROUP BY MenuItemName
ORDER BY NetRevenue DESC;',
        N'Item-level revenue uses LineNetRevenue. Drinks are everything except Category food.'),
    (5, N'Average tip percentage, mobile vs in-store',
        N'SELECT Channel, CAST(AVG(Tip / NULLIF(Subtotal, 0)) * 100 AS decimal(5, 2)) AS AverageTipPercent
FROM ai.Orders
WHERE Channel IN (''mobile'', ''in_store'')
GROUP BY Channel;',
        N'Tip is a percentage of Subtotal. Channels are mobile, in_store and delivery.'),
    (6, N'Loyalty members with no order in the last 30 days',
        N'SELECT CustomerId, HomeLocationName, LastOrderLocalDate
FROM ai.Customers
WHERE LastOrderLocalDate IS NULL OR LastOrderLocalDate < DATEADD(day, -30, CAST(SYSDATETIME() AS date))
ORDER BY LastOrderLocalDate;',
        N'"Customers" means loyalty members, which is what ai.Customers contains.'),
    (7, N'Iced drinks sold in July vs January',
        N'SELECT DATENAME(month, LocalDate) AS Month, SUM(Quantity) AS IcedDrinks
FROM ai.OrderLines
WHERE IsIced = 1 AND MONTH(LocalDate) IN (1, 7)
GROUP BY DATENAME(month, LocalDate);',
        N'Count items with SUM(Quantity), not COUNT(*), since a line can have more than one item.'),
    (8, N'Refund rate by channel',
        N'SELECT o.Channel,
       COUNT(DISTINCT o.OrderId) AS Orders,
       COUNT(DISTINCT r.OrderId) AS RefundedOrders,
       CAST(100.0 * COUNT(DISTINCT r.OrderId) / COUNT(DISTINCT o.OrderId) AS decimal(5, 2)) AS RefundRatePercent
FROM ai.Orders AS o
LEFT JOIN ai.Refunds AS r ON r.OrderId = o.OrderId
GROUP BY o.Channel;',
        N'Refund rate is refunded orders divided by completed orders.')
) AS source (ExampleId, Question, [Sql], Notes)
ON target.ExampleId = source.ExampleId
WHEN MATCHED AND (target.Question <> source.Question OR target.[Sql] <> source.[Sql] OR ISNULL(target.Notes, N'') <> ISNULL(source.Notes, N''))
    THEN UPDATE SET Question = source.Question, [Sql] = source.[Sql], Notes = source.Notes
WHEN NOT MATCHED BY TARGET
    THEN INSERT (ExampleId, Question, [Sql], Notes) VALUES (source.ExampleId, source.Question, source.[Sql], source.Notes)
WHEN NOT MATCHED BY SOURCE
    THEN DELETE;
