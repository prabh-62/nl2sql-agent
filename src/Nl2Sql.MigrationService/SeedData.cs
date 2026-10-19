using System.Data;

namespace Nl2Sql.MigrationService;

/// <summary>
/// Generates the demo data: three coffee brands (real names, invented numbers) and twelve months of orders ending the day before
/// <see cref="AnchorDate"/>. The same anchor date always produces exactly the same rows.
/// </summary>
internal sealed class SeedData
{
    private const int RandomSeed = 20261019;

    // Real brand names, invented data: none of these numbers describe the real companies.
    private static readonly Brand[] Brands =
    [
        // Commuter coffee and Timbits, strong morning rush, mostly in store.
        new(1, "Tim Hortons", DailyOrders: 24, WeekendFactor: 0.8, CustomerShare: 0.55, Channels: [("in_store", 0.55), ("mobile", 0.35), ("delivery", 0.10)],
            Hours: [(6, 6), (7, 11), (8, 12), (9, 8), (10, 4), (11, 3), (12, 5), (13, 4), (14, 3), (15, 3), (16, 2), (17, 2), (18, 1)],
            Menu: TimHortonsMenu()),
        // Espresso drinks, mobile ordering and oat milk, steady through the day.
        new(2, "Starbucks", DailyOrders: 18, WeekendFactor: 1.0, CustomerShare: 0.60, Channels: [("mobile", 0.50), ("in_store", 0.40), ("delivery", 0.10)],
            Hours: [(7, 6), (8, 8), (9, 7), (10, 6), (11, 5), (12, 6), (13, 5), (14, 6), (15, 5), (16, 3), (17, 2), (18, 1)],
            Menu: StarbucksMenu()),
        // Iced coffee all year and donuts, morning-heavy.
        new(3, "Dunkin'", DailyOrders: 14, WeekendFactor: 0.9, CustomerShare: 0.50, Channels: [("mobile", 0.45), ("in_store", 0.45), ("delivery", 0.10)],
            Hours: [(6, 5), (7, 9), (8, 10), (9, 7), (10, 5), (11, 4), (12, 4), (13, 3), (14, 3), (15, 3), (16, 2), (17, 1)],
            Menu: DunkinMenu()),
    ];

    private static readonly Location[] Locations =
    [
        new(101, 1, "Queen St W", "Toronto", "America/Toronto", "Eastern Standard Time", "CAD", 0.13m),
        new(102, 1, "Union Station", "Toronto", "America/Toronto", "Eastern Standard Time", "CAD", 0.13m),
        new(103, 1, "Yonge & Eglinton", "Toronto", "America/Toronto", "Eastern Standard Time", "CAD", 0.13m),
        new(104, 1, "Square One", "Mississauga", "America/Toronto", "Eastern Standard Time", "CAD", 0.13m),
        new(201, 2, "Robson St", "Vancouver", "America/Vancouver", "Pacific Standard Time", "CAD", 0.05m),
        new(202, 2, "Gastown", "Vancouver", "America/Vancouver", "Pacific Standard Time", "CAD", 0.05m),
        new(203, 2, "Kitsilano", "Vancouver", "America/Vancouver", "Pacific Standard Time", "CAD", 0.05m),
        new(301, 3, "Downtown Crossing", "Boston", "America/New_York", "Eastern Standard Time", "USD", 0.0625m),
        new(302, 3, "Back Bay", "Boston", "America/New_York", "Eastern Standard Time", "USD", 0.0625m),
    ];

    private static readonly (int Id, string Name, decimal Delta)[] Sizes = [(1, "small", 0m), (2, "medium", 0.60m), (3, "large", 1.10m)];

    private static readonly (int Id, string Name, string Kind, decimal Price)[] Modifiers =
    [
        (1, "whole", "milk", 0m), (2, "skim", "milk", 0m), (3, "oat", "milk", 0.80m), (4, "almond", "milk", 0.80m), (5, "soy", "milk", 0.70m),
        (6, "vanilla", "syrup", 0.75m), (7, "caramel", "syrup", 0.75m), (8, "hazelnut", "syrup", 0.75m), (9, "extra_shot", "extra_shot", 1.00m),
    ];

    private readonly Random _random = new(RandomSeed);

    public SeedData(DateOnly anchorDate) => AnchorDate = anchorDate;

    public DateOnly AnchorDate { get; }

    public DateOnly FirstDay => AnchorDate.AddDays(-365);

    public int OrderCount => Orders.Rows.Count;

    public DataTable BrandsTable { get; } = Table("Brands", ("BrandId", typeof(int)), ("Name", typeof(string)));

    public DataTable LocationsTable { get; } = Table("Locations", ("LocationId", typeof(int)), ("BrandId", typeof(int)), ("Name", typeof(string)), ("City", typeof(string)),
        ("TimeZoneIana", typeof(string)), ("TimeZoneWindows", typeof(string)), ("Currency", typeof(string)), ("OpenedOn", typeof(DateTime)));

    public DataTable SizesTable { get; } = Table("Sizes", ("SizeId", typeof(int)), ("Name", typeof(string)), ("PriceDelta", typeof(decimal)));

    public DataTable ModifiersTable { get; } = Table("Modifiers", ("ModifierId", typeof(int)), ("Name", typeof(string)), ("Kind", typeof(string)), ("Price", typeof(decimal)));

    public DataTable MenuItems { get; } = Table("MenuItems", ("MenuItemId", typeof(int)), ("BrandId", typeof(int)), ("Name", typeof(string)), ("Category", typeof(string)),
        ("Family", typeof(string)), ("IsIced", typeof(bool)), ("BasePrice", typeof(decimal)));

    public DataTable Customers { get; } = Table("Customers", ("CustomerId", typeof(long)), ("BrandId", typeof(int)), ("HomeLocationId", typeof(int)),
        ("IsLoyaltyMember", typeof(bool)), ("JoinedOn", typeof(DateTime)));

    public DataTable Orders { get; } = Table("Orders", ("OrderId", typeof(long)), ("BrandId", typeof(int)), ("LocationId", typeof(int)), ("CustomerId", typeof(long)),
        ("Channel", typeof(string)), ("Status", typeof(string)), ("PlacedAtUtc", typeof(DateTime)));

    public DataTable OrderLines { get; } = Table("OrderLines", ("OrderLineId", typeof(long)), ("OrderId", typeof(long)), ("BrandId", typeof(int)), ("LocationId", typeof(int)),
        ("MenuItemId", typeof(int)), ("SizeId", typeof(int)), ("Quantity", typeof(int)), ("UnitPrice", typeof(decimal)));

    public DataTable OrderLineModifiers { get; } = Table("OrderLineModifiers", ("OrderLineId", typeof(long)), ("ModifierId", typeof(int)), ("BrandId", typeof(int)),
        ("LocationId", typeof(int)), ("Price", typeof(decimal)));

    public DataTable Payments { get; } = Table("Payments", ("PaymentId", typeof(long)), ("OrderId", typeof(long)), ("BrandId", typeof(int)), ("LocationId", typeof(int)),
        ("Subtotal", typeof(decimal)), ("Tax", typeof(decimal)), ("Tip", typeof(decimal)));

    public DataTable Refunds { get; } = Table("Refunds", ("RefundId", typeof(long)), ("OrderId", typeof(long)), ("BrandId", typeof(int)), ("LocationId", typeof(int)),
        ("Amount", typeof(decimal)), ("RefundedAtUtc", typeof(DateTime)), ("Reason", typeof(string)));

    /// <summary>Tables in foreign-key order.</summary>
    public IEnumerable<DataTable> AllTables =>
        [BrandsTable, LocationsTable, SizesTable, ModifiersTable, MenuItems, Customers, Orders, OrderLines, OrderLineModifiers, Payments, Refunds];

    public static SeedData Generate(DateOnly anchorDate)
    {
        var data = new SeedData(anchorDate);
        data.Build();
        return data;
    }

    private void Build()
    {
        foreach (var (id, name, delta) in Sizes)
        {
            SizesTable.Rows.Add(id, name, delta);
        }

        foreach (var (id, name, kind, price) in Modifiers)
        {
            ModifiersTable.Rows.Add(id, name, kind, price);
        }

        long customerId = 0, orderId = 0, orderLineId = 0, refundId = 0;
        foreach (var brand in Brands)
        {
            BrandsTable.Rows.Add(brand.Id, brand.Name);

            var menu = brand.Menu.Select((item, index) => item with { Id = brand.Id * 100 + index + 1 }).ToArray();
            foreach (var item in menu)
            {
                MenuItems.Rows.Add(item.Id, brand.Id, item.Name, item.Category, (object?)item.Family ?? DBNull.Value, item.IsIced, item.BasePrice);
            }

            var locations = Locations.Where(l => l.BrandId == brand.Id).ToArray();
            var customersByLocation = new Dictionary<int, List<long>>();
            foreach (var location in locations)
            {
                LocationsTable.Rows.Add(location.Id, brand.Id, location.Name, location.City, location.TimeZoneIana, location.TimeZoneWindows, location.Currency,
                    FirstDay.AddYears(-2 - _random.Next(4)).ToDateTime(TimeOnly.MinValue));

                var pool = customersByLocation[location.Id] = [];
                var customerCount = (int)(brand.DailyOrders * 40 * brand.CustomerShare);
                for (var i = 0; i < customerCount; i++)
                {
                    var isMember = _random.NextDouble() < 0.7;
                    var joined = isMember ? FirstDay.AddDays(_random.Next(-900, 330)).ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value;
                    Customers.Rows.Add(++customerId, brand.Id, location.Id, isMember, joined);
                    pool.Add(customerId);
                }
            }

            var hourWeights = brand.Hours.Select(h => ((int)h.Hour, (double)h.Weight)).ToArray();
            foreach (var location in locations)
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneIana);
                for (var day = FirstDay; day < AnchorDate; day = day.AddDays(1))
                {
                    var isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    var expected = brand.DailyOrders * (isWeekend ? brand.WeekendFactor : 1.0) * (0.85 + _random.NextDouble() * 0.3);
                    var orders = (int)Math.Round(expected);
                    var progress = (day.DayNumber - FirstDay.DayNumber) / 365.0;
                    var summer = (1 - Math.Cos(2 * Math.PI * (day.DayOfYear - 15) / 365.0)) / 2; // 0 mid-January, 1 mid-July

                    for (var n = 0; n < orders; n++)
                    {
                        var local = day.ToDateTime(new TimeOnly(Pick(hourWeights), _random.Next(60), _random.Next(60)));
                        var placedAtUtc = TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
                        var channel = Pick(brand.Channels);
                        var status = _random.NextDouble() switch
                        {
                            < 0.015 => "voided",
                            < 0.025 when channel != "in_store" => "cancelled",
                            _ => "completed",
                        };
                        var pool = customersByLocation[location.Id];
                        var customer = _random.NextDouble() < brand.CustomerShare ? (object)pool[_random.Next(pool.Count)] : DBNull.Value;

                        Orders.Rows.Add(++orderId, brand.Id, location.Id, customer, channel, status, placedAtUtc);

                        var subtotal = 0m;
                        var lineCount = _random.NextDouble() switch { < 0.60 => 1, < 0.88 => 2, < 0.97 => 3, _ => 4 };
                        for (var line = 0; line < lineCount; line++)
                        {
                            subtotal += AddLine(++orderLineId, orderId, brand.Id, location.Id, menu, summer, progress);
                        }

                        var tip = channel switch
                        {
                            "mobile" when _random.NextDouble() < 0.4 => Round(subtotal * (0.10m + _random.Next(6) / 100m)),
                            "in_store" when _random.NextDouble() < 0.5 => Round(subtotal * (0.10m + _random.Next(11) / 100m)),
                            "delivery" when _random.NextDouble() < 0.7 => Round(subtotal * 0.15m),
                            _ => 0m,
                        };
                        Payments.Rows.Add(orderId, orderId, brand.Id, location.Id, subtotal, Round(subtotal * location.TaxRate), tip);

                        var refundRate = channel == "delivery" ? 0.03 : 0.01;
                        if (status == "completed" && _random.NextDouble() < refundRate)
                        {
                            var amount = _random.NextDouble() < 0.6 ? subtotal : Round(subtotal * 0.5m);
                            var reason = channel == "delivery" && _random.NextDouble() < 0.5 ? "late_delivery" : _random.NextDouble() < 0.5 ? "wrong_item" : "quality";
                            Refunds.Rows.Add(++refundId, orderId, brand.Id, location.Id, amount, placedAtUtc.AddMinutes(_random.Next(5, 2880)), reason);
                        }
                    }
                }
            }
        }
    }

    private decimal AddLine(long orderLineId, long orderId, int brandId, int locationId, MenuItem[] menu, double summer, double progress)
    {
        var item = Pick(menu.Where(m => m.Weight > 0).Select(m => (m, m.Weight)).ToArray());

        // Hot drinks with an iced version are ordered iced more often in summer.
        var iced = item.IsIced ? null : menu.FirstOrDefault(m => m.IsIced && m.Family == item.Family);
        if (iced is not null && _random.NextDouble() < 0.15 + 0.45 * summer)
        {
            item = iced;
        }

        var isDrink = item.Category != "food";
        int? sizeId = isDrink ? Pick<int>([(1, 0.30), (2, 0.45), (3, 0.25)]) : null;
        var unitPrice = item.BasePrice + (sizeId is { } s ? Sizes[s - 1].Delta : 0m);
        var quantity = _random.NextDouble() < 0.92 ? 1 : 2;
        OrderLines.Rows.Add(orderLineId, orderId, brandId, locationId, item.Id, (object?)sizeId ?? DBNull.Value, quantity, unitPrice);

        var modifierTotal = 0m;
        if (item.TakesMilk)
        {
            // Oat milk's share grows from about 18% to 35% over the year.
            var oat = 0.18 + 0.17 * progress;
            var milk = Pick<int>([(1, 1 - oat - 0.18), (2, 0.06), (3, oat), (4, 0.08), (5, 0.04)]);
            modifierTotal += AddModifier(orderLineId, milk, brandId, locationId);
        }

        if (item.Category == "espresso" && _random.NextDouble() < 0.20)
        {
            modifierTotal += AddModifier(orderLineId, Pick<int>([(6, 0.5), (7, 0.3), (8, 0.2)]), brandId, locationId);
        }

        if (item.Category == "espresso" && _random.NextDouble() < 0.12)
        {
            modifierTotal += AddModifier(orderLineId, 9, brandId, locationId);
        }

        return quantity * (unitPrice + modifierTotal);
    }

    private decimal AddModifier(long orderLineId, int modifierId, int brandId, int locationId)
    {
        var price = Modifiers[modifierId - 1].Price;
        OrderLineModifiers.Rows.Add(orderLineId, modifierId, brandId, locationId, price);
        return price;
    }

    private T Pick<T>((T Value, double Weight)[] choices)
    {
        var roll = _random.NextDouble() * choices.Sum(c => c.Weight);
        foreach (var (value, weight) in choices)
        {
            roll -= weight;
            if (roll < 0)
            {
                return value;
            }
        }

        return choices[^1].Value;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static DataTable Table(string name, params (string Name, Type Type)[] columns)
    {
        var table = new DataTable(name);
        foreach (var (column, type) in columns)
        {
            table.Columns.Add(column, type);
        }

        return table;
    }

    // Weight 0: only sold as the iced version of a hot drink in the same family.
    private static MenuItem[] TimHortonsMenu() =>
    [
        new(0, "Original Blend Coffee", "brewed", "drip", false, 2.29m, 3.0, TakesMilk: false),
        new(0, "Dark Roast Coffee", "brewed", "drip", false, 2.29m, 0.8, TakesMilk: false),
        new(0, "Iced Coffee", "brewed", "drip", true, 2.79m, 0, TakesMilk: false),
        new(0, "Iced Capp", "brewed", "iced_capp", true, 3.89m, 0.8, TakesMilk: false),
        new(0, "French Vanilla", "espresso", "french_vanilla", false, 2.59m, 0.7, TakesMilk: false),
        new(0, "Latte", "espresso", "latte", false, 3.99m, 1.2, TakesMilk: true),
        new(0, "Iced Latte", "espresso", "latte", true, 4.29m, 0, TakesMilk: true),
        new(0, "Cappuccino", "espresso", "cappuccino", false, 3.79m, 0.5, TakesMilk: true),
        new(0, "Americano", "espresso", "americano", false, 2.69m, 0.5, TakesMilk: false),
        new(0, "Iced Americano", "espresso", "americano", true, 2.99m, 0, TakesMilk: false),
        new(0, "Steeped Tea", "tea", "black_tea", false, 1.99m, 1.0, TakesMilk: false),
        new(0, "Timbits (10 pack)", "food", null, false, 3.49m, 1.2, TakesMilk: false),
        new(0, "Boston Cream Donut", "food", null, false, 1.99m, 0.9, TakesMilk: false),
        new(0, "Honey Dip Donut", "food", null, false, 1.79m, 0.7, TakesMilk: false),
        new(0, "Everything Bagel with Cream Cheese", "food", null, false, 2.99m, 0.7, TakesMilk: false),
        new(0, "Farmer's Breakfast Wrap", "food", null, false, 4.99m, 0.8, TakesMilk: false),
    ];

    private static MenuItem[] StarbucksMenu() =>
    [
        new(0, "Pike Place Roast", "brewed", "drip", false, 2.95m, 1.5, TakesMilk: false),
        new(0, "Iced Coffee", "brewed", "drip", true, 3.45m, 0, TakesMilk: false),
        new(0, "Cold Brew", "brewed", "cold_brew", true, 4.45m, 0.9, TakesMilk: false),
        new(0, "Caffè Latte", "espresso", "latte", false, 4.95m, 2.5, TakesMilk: true),
        new(0, "Iced Caffè Latte", "espresso", "latte", true, 5.25m, 0, TakesMilk: true),
        new(0, "Cappuccino", "espresso", "cappuccino", false, 4.75m, 1.0, TakesMilk: true),
        new(0, "Flat White", "espresso", "flat_white", false, 5.25m, 0.8, TakesMilk: true),
        new(0, "Caramel Macchiato", "espresso", "macchiato", false, 5.75m, 1.3, TakesMilk: true),
        new(0, "Iced Caramel Macchiato", "espresso", "macchiato", true, 6.05m, 0, TakesMilk: true),
        new(0, "Caffè Americano", "espresso", "americano", false, 3.75m, 0.8, TakesMilk: false),
        new(0, "Iced Caffè Americano", "espresso", "americano", true, 4.05m, 0, TakesMilk: false),
        new(0, "Chai Tea Latte", "tea", "chai", false, 5.25m, 0.8, TakesMilk: true),
        new(0, "Iced Chai Tea Latte", "tea", "chai", true, 5.55m, 0, TakesMilk: true),
        new(0, "Matcha Tea Latte", "tea", "matcha", false, 5.45m, 0.6, TakesMilk: true),
        new(0, "Iced Matcha Tea Latte", "tea", "matcha", true, 5.75m, 0, TakesMilk: true),
        new(0, "Butter Croissant", "food", null, false, 3.45m, 0.9, TakesMilk: false),
        new(0, "Banana Bread", "food", null, false, 3.95m, 0.6, TakesMilk: false),
        new(0, "Cake Pop", "food", null, false, 3.25m, 0.5, TakesMilk: false),
        new(0, "Bacon & Gouda Sandwich", "food", null, false, 6.45m, 0.7, TakesMilk: false),
    ];

    private static MenuItem[] DunkinMenu() =>
    [
        new(0, "Original Blend Coffee", "brewed", "drip", false, 2.49m, 2.0, TakesMilk: false),
        // Iced coffee sells all year at Dunkin', and even more in summer.
        new(0, "Iced Coffee", "brewed", "drip", true, 2.99m, 1.2, TakesMilk: false),
        new(0, "Cold Brew", "brewed", "cold_brew", true, 3.99m, 0.5, TakesMilk: false),
        new(0, "Latte", "espresso", "latte", false, 3.79m, 1.0, TakesMilk: true),
        new(0, "Iced Latte", "espresso", "latte", true, 4.09m, 0.6, TakesMilk: true),
        new(0, "Cappuccino", "espresso", "cappuccino", false, 3.69m, 0.5, TakesMilk: true),
        new(0, "Americano", "espresso", "americano", false, 2.99m, 0.4, TakesMilk: false),
        new(0, "Iced Americano", "espresso", "americano", true, 3.29m, 0, TakesMilk: false),
        new(0, "Chai Latte", "tea", "chai", false, 3.79m, 0.4, TakesMilk: true),
        new(0, "Hot Tea", "tea", "black_tea", false, 2.09m, 0.3, TakesMilk: false),
        new(0, "Glazed Donut", "food", null, false, 1.59m, 1.2, TakesMilk: false),
        new(0, "Boston Kreme Donut", "food", null, false, 1.79m, 1.0, TakesMilk: false),
        new(0, "Munchkins (10 pack)", "food", null, false, 3.49m, 0.8, TakesMilk: false),
        new(0, "Bagel with Cream Cheese", "food", null, false, 2.99m, 0.7, TakesMilk: false),
        new(0, "Wake-Up Wrap", "food", null, false, 2.49m, 0.8, TakesMilk: false),
    ];

    private sealed record Brand(int Id, string Name, int DailyOrders, double WeekendFactor, double CustomerShare,
        (string Channel, double Weight)[] Channels, (int Hour, int Weight)[] Hours, MenuItem[] Menu);

    private sealed record Location(int Id, int BrandId, string Name, string City, string TimeZoneIana, string TimeZoneWindows, string Currency, decimal TaxRate);

    private sealed record MenuItem(int Id, string Name, string Category, string? Family, bool IsIced, decimal BasePrice, double Weight, bool TakesMilk);
}
