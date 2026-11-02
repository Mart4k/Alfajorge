using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlfajorgeBackend.Models;

public class Persona
{
    [Key]
    [Column("id_persona")]
    public int IdPersona { get; set; }

    [Required, MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Apellido { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Telefono { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public bool Activo { get; set; } = true;

    public Cliente? Cliente { get; set; }
    public Empleado? Empleado { get; set; }
}

public class Cliente
{
    [Key]
    [ForeignKey(nameof(Persona))]
    [Column("id_persona")]
    public int IdPersona { get; set; }

    [MaxLength(200)]
    public string? Direccion { get; set; }

    [MaxLength(100)]
    public string? Localidad { get; set; }

    [MaxLength(15)]
    public string? CodigoPostal { get; set; }

    public Persona Persona { get; set; } = null!;
    public ICollection<Operacion> Operaciones { get; set; } = new List<Operacion>();
}

public class Empleado
{
    [Key]
    [ForeignKey(nameof(Persona))]
    [Column("id_persona")]
    public int IdPersona { get; set; }

    [Required, MaxLength(20)]
    public string Cargo { get; set; } = string.Empty;

    public Persona Persona { get; set; } = null!;
    public Vendedor? Vendedor { get; set; }
}

public class Vendedor
{
    [Key]
    [ForeignKey(nameof(Empleado))]
    [Column("id_persona")]
    public int IdPersona { get; set; }

    public Empleado Empleado { get; set; } = null!;
    public ICollection<Operacion> Operaciones { get; set; } = new List<Operacion>();
}

public class TipoAlfajor
{
    [Key]
    [Column("id_tipo")]
    public int IdTipo { get; set; }

    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public ICollection<Alfajor> Alfajores { get; set; } = new List<Alfajor>();
}

public class Sabor
{
    [Key]
    [Column("id_sabor")]
    public int IdSabor { get; set; }

    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public ICollection<Alfajor> Alfajores { get; set; } = new List<Alfajor>();
}

public class Cobertura
{
    [Key]
    [Column("id_cobertura")]
    public int IdCobertura { get; set; }

    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Descripcion { get; set; }

    public ICollection<Alfajor> Alfajores { get; set; } = new List<Alfajor>();
}

public class Presentacion
{
    [Key]
    [Column("id_presentacion")]
    public int IdPresentacion { get; set; }

    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;

    public int CantidadUnidades { get; set; }

    public ICollection<AlfajorPresentacion> Alfajores { get; set; } = new List<AlfajorPresentacion>();
}

public class Alfajor
{
    [Key]
    [Column("id_alfajor")]
    public int IdAlfajor { get; set; }

    [Required, MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    [MaxLength(500)]
    public string? ImagenUrl { get; set; }

    public int IdTipo { get; set; }
    public int IdSabor { get; set; }
    public int IdCobertura { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public TipoAlfajor Tipo { get; set; } = null!;
    public Sabor Sabor { get; set; } = null!;
    public Cobertura Cobertura { get; set; } = null!;
    public ICollection<AlfajorPresentacion> Presentaciones { get; set; } = new List<AlfajorPresentacion>();
    public ICollection<PromocionAlfajor> Promociones { get; set; } = new List<PromocionAlfajor>();
}

public class AlfajorPresentacion
{
    [Key]
    [Column(Order = 0)]
    public int IdAlfajor { get; set; }

    [Key]
    [Column(Order = 1)]
    public int IdPresentacion { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal Precio { get; set; }

    public Alfajor Alfajor { get; set; } = null!;
    public Presentacion Presentacion { get; set; } = null!;
    public Stock? Stock { get; set; }
}

public class Stock
{
    [Key]
    [Column(Order = 0)]
    public int IdAlfajor { get; set; }

    [Key]
    [Column(Order = 1)]
    public int IdPresentacion { get; set; }

    public int Cantidad { get; set; }
    public int StockMinimo { get; set; }
    public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;

    public AlfajorPresentacion AlfajorPresentacion { get; set; } = null!;
}

public class Operacion
{
    [Key]
    [Column("id_operacion")]
    public int IdOperacion { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int IdCliente { get; set; }
    public int? IdVendedor { get; set; }

    [Required, MaxLength(20)]
    public string Origen { get; set; } = "WEB";

    [Required, MaxLength(25)]
    public string Estado { get; set; } = "PENDIENTE";

    [Column(TypeName = "numeric(12,2)")]
    public decimal Total { get; set; }

    public Cliente Cliente { get; set; } = null!;
    public Vendedor? Vendedor { get; set; }
    public ICollection<DetalleOperacion> Detalles { get; set; } = new List<DetalleOperacion>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public Factura? Factura { get; set; }
}

public class DetalleOperacion
{
    [Key]
    [Column("id_detalle")]
    public int IdDetalle { get; set; }

    public int IdOperacion { get; set; }
    public int IdAlfajor { get; set; }
    public int IdPresentacion { get; set; }
    public int Cantidad { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal PrecioUnitario { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; }

    public Operacion Operacion { get; set; } = null!;
    public AlfajorPresentacion AlfajorPresentacion { get; set; } = null!;
}

public class Pago
{
    [Key]
    [Column("id_pago")]
    public int IdPago { get; set; }

    public int IdOperacion { get; set; }

    [Required, MaxLength(30)]
    public string Metodo { get; set; } = string.Empty;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Monto { get; set; }

    [Required, MaxLength(20)]
    public string Estado { get; set; } = "PENDIENTE";

    [MaxLength(150)]
    public string? ReferenciaExterna { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Operacion Operacion { get; set; } = null!;
}

public class Factura
{
    [Key]
    [Column("id_factura")]
    public int IdFactura { get; set; }

    public int IdOperacion { get; set; }

    [Required, MaxLength(50)]
    public string NumeroFactura { get; set; } = string.Empty;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Total { get; set; }

    public Operacion Operacion { get; set; } = null!;
}

public class Promocion
{
    [Key]
    [Column("id_promocion")]
    public int IdPromocion { get; set; }

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    [Column(TypeName = "numeric(5,2)")]
    public decimal PorcentajeDescuento { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public bool Activa { get; set; } = true;

    public ICollection<PromocionAlfajor> Alfajores { get; set; } = new List<PromocionAlfajor>();
}

public class PromocionAlfajor
{
    [Key]
    [Column(Order = 0)]
    public int IdPromocion { get; set; }

    [Key]
    [Column(Order = 1)]
    public int IdAlfajor { get; set; }

    public Promocion Promocion { get; set; } = null!;
    public Alfajor Alfajor { get; set; } = null!;
}
