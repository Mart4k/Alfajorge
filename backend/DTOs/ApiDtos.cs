namespace AlfajorgeBackend.DTOs;

public sealed record LoginRequest(string Email, string Password);
public sealed record InitialAdministratorRequest(string Dni, string Name, string LastName, string Email, string Password);

public sealed record RegisterClientRequest(
    string Dni,
    string Name,
    string LastName,
    string Email,
    string Password,
    string? Phone,
    string? Address,
    string? City,
    string? PostalCode);

public sealed record CreateEmployeeRequest(
    string Dni,
    string Name,
    string LastName,
    string Email,
    string Password,
    string? Phone,
    string Cargo);

public sealed record SessionUserDto(int Id, string Name, string LastName, string Email, string Role);
public sealed record SessionDto(string Token, SessionUserDto User);

public sealed record PurchaseRequest(List<PurchaseItemDto>? Items, string PaymentMethod);
public sealed record PurchaseItemDto(int AlfajorId, int PresentationId, int Quantity);
public sealed record PurchaseResult(int OperationId, decimal Total, string Status, string? Invoice);

public sealed record CatalogAlfajorDto(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    string Type,
    string Flavor,
    string Coverage,
    List<CatalogPresentationDto> Presentations);

public sealed record CatalogPresentationDto(
    int Id,
    string Name,
    int Units,
    decimal Price,
    string Availability);

public sealed record LookupDto(int Id, string Name, string? Description);
public sealed record PresentationLookupDto(int Id, string Name, int Units, decimal Price, string Availability);

public sealed record PromotionDto(
    int Id,
    string Name,
    string? Description,
    decimal Discount,
    DateOnly Starts,
    DateOnly Ends,
    List<int> Alfajores);
public sealed record SavePromotionRequest(
    string Name,
    string? Description,
    decimal Discount,
    DateOnly Starts,
    DateOnly Ends,
    List<int>? Alfajores,
    bool Active = true);

public sealed record CreateAlfajorRequest(
    string Name,
    string? Description,
    string? ImageUrl,
    int TypeId,
    int FlavorId,
    int CoverageId,
    List<CreateAlfajorPresentationRequest>? Presentations);

public sealed record CreateAlfajorPresentationRequest(
    int PresentationId,
    decimal Price,
    int Stock,
    int StockMinimum);
public sealed record UpdatePresentationPriceRequest(decimal Price);
public sealed record PriceCombinationDto(
    int AlfajorId,
    int PresentationId,
    string Presentation,
    int Units,
    decimal Price,
    int Stock,
    int StockMinimum);

public sealed record OperationDto(int Id, DateTime Date, string Origin, string Status, decimal Total, string Client);
public sealed record OperationDetailDto(
    int Id,
    DateTime Date,
    string Origin,
    string Status,
    decimal Total,
    string Client,
    List<OperationLineDto> Items);
public sealed record OperationLineDto(string Alfajor, string Presentation, int Quantity, decimal UnitPrice, decimal Subtotal);
public sealed record ChangeOperationStatusRequest(string Status);
public sealed record InvoiceDto(int Id, string Number, DateTime Date, decimal Total);
public sealed record PaymentDto(int Id, int OperationId, string Method, decimal Amount, string Status, string? Reference, DateTime Date);
public sealed record StockItemDto(int AlfajorId, int PresentationId, string Alfajor, string Presentation, int Quantity, int Minimum, DateTime Updated);
public sealed record UpdateStockRequest(int Quantity, int Minimum);
public sealed record ClientDto(int Id, string Dni, string Name, string LastName, string Email, string? Phone, string? Address, string? City, string? PostalCode);
public sealed record EmployeeDto(int Id, string Dni, string Name, string LastName, string Email, string? Phone, string Cargo, bool IsSeller);

public sealed record LookupCreateRequest(string Name, string? Description, int? Units);

public sealed record AlfajorDetalleDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string? ImagenUrl,
    string Tipo,
    string Sabor,
    string Cobertura,
    List<PresentacionDto> Presentaciones,
    bool Activo);

public sealed record PresentacionDto(
    int Id,
    string Nombre,
    int CantidadUnidades,
    decimal Precio,
    string Disponibilidad,
    int CantidadStock,
    int StockMinimo);
