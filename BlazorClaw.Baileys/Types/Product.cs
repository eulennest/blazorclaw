namespace Baileys.Types;

// ──────────────────────────────────────────────────────────────────────────────
//  Product / Catalog types
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A single product in a WhatsApp business catalog.</summary>
public sealed class Product
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? RetailerId { get; init; }
    public string? Url { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public required string Currency { get; init; }
    public bool IsHidden { get; init; }
    public Dictionary<string, string> ImageUrls { get; init; } = new();
    public Dictionary<string, string> ReviewStatus { get; init; } = new();
    public string? Availability { get; init; }
}

/// <summary>Fields required to create a new product.</summary>
public sealed class ProductCreate
{
    public required string Name { get; init; }
    public string? RetailerId { get; init; }
    public string? Url { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public required string Currency { get; init; }
    public bool IsHidden { get; init; }
    /// <summary>ISO country code — null means no country.</summary>
    public string? OriginCountryCode { get; init; }
}

/// <summary>Fields that can be updated on an existing product.</summary>
public sealed class ProductUpdate
{
    public string? Name { get; init; }
    public string? RetailerId { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public decimal? Price { get; init; }
    public string? Currency { get; init; }
    public bool? IsHidden { get; init; }
}

/// <summary>A collection of products in a catalog.</summary>
public sealed class CatalogCollection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<Product> Products { get; init; } = [];
    public CatalogStatus? Status { get; init; }
}

/// <summary>Review/appeal status of a catalog or product.</summary>
public sealed class CatalogStatus
{
    public required string Status { get; init; }
    public bool CanAppeal { get; init; }
}

/// <summary>Paginated catalog query result.</summary>
public sealed class CatalogResult
{
    public CatalogPaging? Paging { get; init; }
    public IReadOnlyList<object> Data { get; init; } = [];
}

/// <summary>Paging cursors for catalog pagination.</summary>
public sealed class CatalogPaging
{
    public string? Before { get; init; }
    public string? After { get; init; }
}

/// <summary>Result after creating a product.</summary>
public sealed class ProductCreateResult
{
    public object? Product { get; init; }
}

/// <summary>Price of an order.</summary>
public sealed class OrderPrice
{
    public required string Currency { get; init; }
    public decimal Total { get; init; }
}
