using AlfajorgeBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AlfajorgeBackend.Data;

public class AppDbContext : DbContext
{
    private static readonly ValueConverter<DateTime, DateTime> UtcTimestampConverter = new(
        value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<TipoAlfajor> TiposAlfajor => Set<TipoAlfajor>();
    public DbSet<Sabor> Sabores => Set<Sabor>();
    public DbSet<Cobertura> Coberturas => Set<Cobertura>();
    public DbSet<Presentacion> Presentaciones => Set<Presentacion>();
    public DbSet<Alfajor> Alfajores => Set<Alfajor>();
    public DbSet<AlfajorPresentacion> AlfajoresPresentacion => Set<AlfajorPresentacion>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Operacion> Operaciones => Set<Operacion>();
    public DbSet<DetalleOperacion> DetallesOperacion => Set<DetalleOperacion>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Promocion> Promociones => Set<Promocion>();
    public DbSet<PromocionAlfajor> PromocionesAlfajor => Set<PromocionAlfajor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Persona>(entity =>
        {
            entity.ToTable("persona");
            entity.HasKey(e => e.IdPersona);
            entity.Property(e => e.IdPersona).HasColumnName("id_persona").ValueGeneratedOnAdd();
            entity.Property(e => e.Dni).HasColumnName("dni").HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(60)").IsRequired();
            entity.Property(e => e.Apellido).HasColumnName("apellido").HasColumnType("varchar(60)").IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasColumnType("varchar(150)").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasColumnType("varchar(255)").IsRequired();
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasColumnType("varchar(30)");
            entity.Property(e => e.FechaRegistro).HasColumnName("fecha_registro").HasColumnType("timestamp without time zone").HasConversion(UtcTimestampConverter).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValueSql("TRUE").HasSentinel(true);
            entity.HasIndex(e => e.Dni).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("cliente");
            entity.HasKey(e => e.IdPersona);
            entity.Property(e => e.IdPersona).HasColumnName("id_persona").ValueGeneratedNever();
            entity.Property(e => e.Direccion).HasColumnName("direccion").HasColumnType("varchar(200)");
            entity.Property(e => e.Localidad).HasColumnName("localidad").HasColumnType("varchar(100)");
            entity.Property(e => e.CodigoPostal).HasColumnName("codigo_postal").HasColumnType("varchar(15)");
            entity.HasOne(e => e.Persona)
                .WithOne(p => p.Cliente)
                .HasForeignKey<Cliente>(e => e.IdPersona)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Empleado>(entity =>
        {
            entity.ToTable("empleado", table =>
                table.HasCheckConstraint("ck_empleado_cargo", "cargo IN ('VENDEDOR', 'ADMINISTRATIVO')"));
            entity.HasKey(e => e.IdPersona);
            entity.Property(e => e.IdPersona).HasColumnName("id_persona").ValueGeneratedNever();
            entity.Property(e => e.Cargo).HasColumnName("cargo").HasColumnType("varchar(20)").IsRequired();
            entity.HasOne(e => e.Persona)
                .WithOne(p => p.Empleado)
                .HasForeignKey<Empleado>(e => e.IdPersona)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Vendedor>(entity =>
        {
            entity.ToTable("vendedor");
            entity.HasKey(e => e.IdPersona);
            entity.Property(e => e.IdPersona).HasColumnName("id_persona").ValueGeneratedNever();
            entity.HasOne(e => e.Empleado)
                .WithOne(e => e.Vendedor)
                .HasForeignKey<Vendedor>(e => e.IdPersona)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TipoAlfajor>(entity =>
        {
            entity.ToTable("tipo_alfajor");
            entity.HasKey(e => e.IdTipo);
            entity.Property(e => e.IdTipo).HasColumnName("id_tipo").ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(60)").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasColumnType("varchar(250)");
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        modelBuilder.Entity<Sabor>(entity =>
        {
            entity.ToTable("sabor");
            entity.HasKey(e => e.IdSabor);
            entity.Property(e => e.IdSabor).HasColumnName("id_sabor").ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(60)").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasColumnType("varchar(250)");
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        modelBuilder.Entity<Cobertura>(entity =>
        {
            entity.ToTable("cobertura");
            entity.HasKey(e => e.IdCobertura);
            entity.Property(e => e.IdCobertura).HasColumnName("id_cobertura").ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(60)").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasColumnType("varchar(250)");
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        modelBuilder.Entity<Presentacion>(entity =>
        {
            entity.ToTable("presentacion", table =>
                table.HasCheckConstraint("ck_presentacion_cantidad_unidades", "cantidad_unidades > 0"));
            entity.HasKey(e => e.IdPresentacion);
            entity.Property(e => e.IdPresentacion).HasColumnName("id_presentacion").ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(60)").IsRequired();
            entity.Property(e => e.CantidadUnidades).HasColumnName("cantidad_unidades").IsRequired();
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        modelBuilder.Entity<Alfajor>(entity =>
        {
            entity.ToTable("alfajor", table =>
            {
                table.HasCheckConstraint("ck_alfajor_nombre", "LENGTH(TRIM(nombre)) > 0");
            });
            entity.HasKey(e => e.IdAlfajor);
            entity.Property(e => e.IdAlfajor).HasColumnName("id_alfajor").ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(120)").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(e => e.ImagenUrl).HasColumnName("imagen_url").HasColumnType("varchar(500)");
            entity.Property(e => e.IdTipo).HasColumnName("id_tipo");
            entity.Property(e => e.IdSabor).HasColumnName("id_sabor");
            entity.Property(e => e.IdCobertura).HasColumnName("id_cobertura");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValueSql("TRUE").HasSentinel(true);
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone").HasConversion(UtcTimestampConverter).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.Tipo).WithMany(t => t.Alfajores).HasForeignKey(e => e.IdTipo).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Sabor).WithMany(s => s.Alfajores).HasForeignKey(e => e.IdSabor).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Cobertura).WithMany(c => c.Alfajores).HasForeignKey(e => e.IdCobertura).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.IdTipo).HasDatabaseName("idx_alfajor_tipo");
            entity.HasIndex(e => e.IdSabor).HasDatabaseName("idx_alfajor_sabor");
            entity.HasIndex(e => e.IdCobertura).HasDatabaseName("idx_alfajor_cobertura");
        });

        modelBuilder.Entity<AlfajorPresentacion>(entity =>
        {
            entity.ToTable("alfajor_presentacion", table =>
                table.HasCheckConstraint("ck_alfajor_presentacion_precio", "precio >= 0"));
            entity.HasKey(e => new { e.IdAlfajor, e.IdPresentacion });
            entity.Property(e => e.IdAlfajor).HasColumnName("id_alfajor");
            entity.Property(e => e.IdPresentacion).HasColumnName("id_presentacion");
            entity.Property(e => e.Precio).HasColumnName("precio").HasColumnType("numeric(12,2)").IsRequired();
            entity.HasOne(e => e.Alfajor).WithMany(a => a.Presentaciones).HasForeignKey(e => e.IdAlfajor).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Presentacion).WithMany(p => p.Alfajores).HasForeignKey(e => e.IdPresentacion).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("stock", table =>
            {
                table.HasCheckConstraint("ck_stock_cantidad", "cantidad >= 0");
                table.HasCheckConstraint("ck_stock_stock_minimo", "stock_minimo >= 0");
            });
            entity.HasKey(e => new { e.IdAlfajor, e.IdPresentacion });
            entity.Property(e => e.IdAlfajor).HasColumnName("id_alfajor");
            entity.Property(e => e.IdPresentacion).HasColumnName("id_presentacion");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad").HasDefaultValue(0);
            entity.Property(e => e.StockMinimo).HasColumnName("stock_minimo").HasDefaultValue(0);
            entity.Property(e => e.UltimaActualizacion).HasColumnName("ultima_actualizacion").HasColumnType("timestamp without time zone").HasConversion(UtcTimestampConverter).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.AlfajorPresentacion)
                .WithOne(ap => ap.Stock)
                .HasForeignKey<Stock>(e => new { e.IdAlfajor, e.IdPresentacion })
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Operacion>(entity =>
        {
            entity.ToTable("operacion", table =>
            {
                table.HasCheckConstraint("ck_operacion_origen", "origen IN ('WEB', 'PRESENCIAL')");
                table.HasCheckConstraint("ck_operacion_estado", "estado IN ('PENDIENTE', 'CONFIRMADA', 'PAGADA', 'PREPARACION', 'ENVIADA', 'ENTREGADA', 'CANCELADA')");
                table.HasCheckConstraint("ck_operacion_total", "total >= 0");
            });
            entity.HasKey(e => e.IdOperacion);
            entity.Property(e => e.IdOperacion).HasColumnName("id_operacion").ValueGeneratedOnAdd();
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("timestamp without time zone").HasConversion(UtcTimestampConverter).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente").IsRequired();
            entity.Property(e => e.IdVendedor).HasColumnName("id_vendedor");
            entity.Property(e => e.Origen).HasColumnName("origen").HasColumnType("varchar(20)").IsRequired();
            entity.Property(e => e.Estado).HasColumnName("estado").HasColumnType("varchar(25)").HasDefaultValue("PENDIENTE").IsRequired();
            entity.Property(e => e.Total).HasColumnName("total").HasColumnType("numeric(12,2)").HasDefaultValue(0m).IsRequired();
            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Operaciones)
                .HasForeignKey(e => e.IdCliente)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Vendedor)
                .WithMany(v => v.Operaciones)
                .HasForeignKey(e => e.IdVendedor)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.IdCliente).HasDatabaseName("idx_operacion_cliente");
            entity.HasIndex(e => e.IdVendedor).HasDatabaseName("idx_operacion_vendedor");
            entity.HasIndex(e => e.Fecha).HasDatabaseName("idx_operacion_fecha");
            entity.HasIndex(e => e.Estado).HasDatabaseName("idx_operacion_estado");
        });

        modelBuilder.Entity<DetalleOperacion>(entity =>
        {
            entity.ToTable("detalle_operacion", table =>
            {
                table.HasCheckConstraint("ck_detalle_operacion_cantidad", "cantidad > 0");
                table.HasCheckConstraint("ck_detalle_operacion_precio_unitario", "precio_unitario >= 0");
                table.HasCheckConstraint("ck_detalle_operacion_subtotal", "subtotal >= 0");
            });
            entity.HasKey(e => e.IdDetalle);
            entity.Property(e => e.IdDetalle).HasColumnName("id_detalle").ValueGeneratedOnAdd();
            entity.Property(e => e.IdOperacion).HasColumnName("id_operacion");
            entity.Property(e => e.IdAlfajor).HasColumnName("id_alfajor");
            entity.Property(e => e.IdPresentacion).HasColumnName("id_presentacion");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad").IsRequired();
            entity.Property(e => e.PrecioUnitario).HasColumnName("precio_unitario").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(e => e.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(12,2)").IsRequired();
            entity.HasOne(e => e.Operacion)
                .WithMany(o => o.Detalles)
                .HasForeignKey(e => e.IdOperacion)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AlfajorPresentacion)
                .WithMany()
                .HasForeignKey(e => new { e.IdAlfajor, e.IdPresentacion })
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.IdOperacion).HasDatabaseName("idx_detalle_operacion");
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("pago", table =>
            {
                table.HasCheckConstraint("ck_pago_metodo", "metodo IN ('EFECTIVO', 'TRANSFERENCIA', 'TARJETA', 'MERCADO_PAGO')");
                table.HasCheckConstraint("ck_pago_monto", "monto > 0");
                table.HasCheckConstraint("ck_pago_estado", "estado IN ('PENDIENTE', 'APROBADO', 'RECHAZADO', 'REEMBOLSADO')");
            });
            entity.HasKey(e => e.IdPago);
            entity.Property(e => e.IdPago).HasColumnName("id_pago").ValueGeneratedOnAdd();
            entity.Property(e => e.IdOperacion).HasColumnName("id_operacion");
            entity.Property(e => e.Metodo).HasColumnName("metodo").HasColumnType("varchar(30)").IsRequired();
            entity.Property(e => e.Monto).HasColumnName("monto").HasColumnType("numeric(12,2)").IsRequired();
            entity.Property(e => e.Estado).HasColumnName("estado").HasColumnType("varchar(20)").HasDefaultValue("PENDIENTE").IsRequired();
            entity.Property(e => e.ReferenciaExterna).HasColumnName("referencia_externa").HasColumnType("varchar(150)");
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("timestamp without time zone").HasConversion(UtcTimestampConverter).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.Operacion).WithMany(o => o.Pagos).HasForeignKey(e => e.IdOperacion).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.IdOperacion).HasDatabaseName("idx_pago_operacion");
        });

        modelBuilder.Entity<Factura>(entity =>
        {
            entity.ToTable("factura", table =>
                table.HasCheckConstraint("ck_factura_total", "total >= 0"));
            entity.HasKey(e => e.IdFactura);
            entity.Property(e => e.IdFactura).HasColumnName("id_factura").ValueGeneratedOnAdd();
            entity.Property(e => e.IdOperacion).HasColumnName("id_operacion");
            entity.Property(e => e.NumeroFactura).HasColumnName("numero_factura").HasColumnType("varchar(50)").IsRequired();
            entity.Property(e => e.Fecha).HasColumnName("fecha").HasColumnType("timestamp without time zone").HasConversion(UtcTimestampConverter).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Total).HasColumnName("total").HasColumnType("numeric(12,2)").IsRequired();
            entity.HasOne(e => e.Operacion)
                .WithOne(o => o.Factura)
                .HasForeignKey<Factura>(e => e.IdOperacion)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.IdOperacion).IsUnique();
            entity.HasIndex(e => e.NumeroFactura).IsUnique();
        });

        modelBuilder.Entity<Promocion>(entity =>
        {
            entity.ToTable("promocion", table =>
            {
                table.HasCheckConstraint("ck_promocion_porcentaje_descuento", "porcentaje_descuento > 0 AND porcentaje_descuento <= 100");
                table.HasCheckConstraint("ck_promocion_fecha", "fecha_fin >= fecha_inicio");
            });
            entity.HasKey(e => e.IdPromocion);
            entity.Property(e => e.IdPromocion).HasColumnName("id_promocion").ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasColumnType("varchar(100)").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            entity.Property(e => e.PorcentajeDescuento).HasColumnName("porcentaje_descuento").HasColumnType("numeric(5,2)").IsRequired();
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("date").IsRequired();
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin").HasColumnType("date").IsRequired();
            entity.Property(e => e.Activa).HasColumnName("activa").HasDefaultValueSql("TRUE").HasSentinel(true);
            entity.HasIndex(e => new { e.FechaInicio, e.FechaFin }).HasDatabaseName("idx_promocion_fechas");
        });

        modelBuilder.Entity<PromocionAlfajor>(entity =>
        {
            entity.ToTable("promocion_alfajor");
            entity.HasKey(e => new { e.IdPromocion, e.IdAlfajor });
            entity.Property(e => e.IdPromocion).HasColumnName("id_promocion");
            entity.Property(e => e.IdAlfajor).HasColumnName("id_alfajor");
            entity.HasOne(e => e.Promocion)
                .WithMany(p => p.Alfajores)
                .HasForeignKey(e => e.IdPromocion)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Alfajor)
                .WithMany(a => a.Promociones)
                .HasForeignKey(e => e.IdAlfajor)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
