CREATE TABLE IF NOT EXISTS persona (
    id_persona SERIAL PRIMARY KEY,
    dni VARCHAR(20) UNIQUE NOT NULL,
    nombre VARCHAR(60) NOT NULL,
    apellido VARCHAR(60) NOT NULL,
    email VARCHAR(150) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    telefono VARCHAR(30),
    fecha_registro TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS cliente (
    id_persona INT PRIMARY KEY REFERENCES persona(id_persona) ON DELETE CASCADE,
    direccion VARCHAR(200),
    localidad VARCHAR(100),
    codigo_postal VARCHAR(15)
);

CREATE TABLE IF NOT EXISTS empleado (
    id_persona INT PRIMARY KEY REFERENCES persona(id_persona) ON DELETE CASCADE,
    cargo VARCHAR(20) NOT NULL CHECK (cargo IN ('VENDEDOR', 'ADMINISTRATIVO'))
);

CREATE TABLE IF NOT EXISTS vendedor (
    id_persona INT PRIMARY KEY REFERENCES empleado(id_persona) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS tipo_alfajor (
    id_tipo SERIAL PRIMARY KEY,
    nombre VARCHAR(60) NOT NULL UNIQUE,
    descripcion VARCHAR(250)
);

CREATE TABLE IF NOT EXISTS sabor (
    id_sabor SERIAL PRIMARY KEY,
    nombre VARCHAR(60) NOT NULL UNIQUE,
    descripcion VARCHAR(250)
);

CREATE TABLE IF NOT EXISTS cobertura (
    id_cobertura SERIAL PRIMARY KEY,
    nombre VARCHAR(60) NOT NULL UNIQUE,
    descripcion VARCHAR(250)
);

CREATE TABLE IF NOT EXISTS presentacion (
    id_presentacion SERIAL PRIMARY KEY,
    nombre VARCHAR(60) NOT NULL UNIQUE,
    cantidad_unidades INT NOT NULL CHECK (cantidad_unidades > 0)
);

CREATE TABLE IF NOT EXISTS alfajor (
    id_alfajor SERIAL PRIMARY KEY,
    nombre VARCHAR(120) NOT NULL,
    descripcion TEXT,
    imagen_url VARCHAR(500),
    id_tipo INT NOT NULL REFERENCES tipo_alfajor(id_tipo),
    id_sabor INT NOT NULL REFERENCES sabor(id_sabor),
    id_cobertura INT NOT NULL REFERENCES cobertura(id_cobertura),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (LENGTH(TRIM(nombre)) > 0)
);

CREATE TABLE IF NOT EXISTS alfajor_presentacion (
    id_alfajor INT NOT NULL REFERENCES alfajor(id_alfajor),
    id_presentacion INT NOT NULL REFERENCES presentacion(id_presentacion),
    precio NUMERIC(12,2) NOT NULL CHECK (precio >= 0),
    PRIMARY KEY (id_alfajor, id_presentacion)
);

CREATE TABLE IF NOT EXISTS stock (
    id_alfajor INT NOT NULL,
    id_presentacion INT NOT NULL,
    cantidad INT NOT NULL DEFAULT 0 CHECK (cantidad >= 0),
    stock_minimo INT NOT NULL DEFAULT 0 CHECK (stock_minimo >= 0),
    ultima_actualizacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id_alfajor, id_presentacion),
    FOREIGN KEY (id_alfajor, id_presentacion)
        REFERENCES alfajor_presentacion(id_alfajor, id_presentacion)
);

CREATE TABLE IF NOT EXISTS operacion (
    id_operacion SERIAL PRIMARY KEY,
    fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    id_cliente INT NOT NULL REFERENCES cliente(id_persona),
    id_vendedor INT REFERENCES vendedor(id_persona),
    origen VARCHAR(20) NOT NULL CHECK (origen IN ('WEB', 'PRESENCIAL')),
    estado VARCHAR(25) NOT NULL DEFAULT 'PENDIENTE'
        CHECK (estado IN ('PENDIENTE', 'CONFIRMADA', 'PAGADA', 'PREPARACION', 'ENVIADA', 'ENTREGADA', 'CANCELADA')),
    total NUMERIC(12,2) NOT NULL DEFAULT 0 CHECK (total >= 0)
);

CREATE TABLE IF NOT EXISTS detalle_operacion (
    id_detalle SERIAL PRIMARY KEY,
    id_operacion INT NOT NULL REFERENCES operacion(id_operacion) ON DELETE CASCADE,
    id_alfajor INT NOT NULL,
    id_presentacion INT NOT NULL,
    cantidad INT NOT NULL CHECK (cantidad > 0),
    precio_unitario NUMERIC(12,2) NOT NULL CHECK (precio_unitario >= 0),
    subtotal NUMERIC(12,2) NOT NULL CHECK (subtotal >= 0),
    FOREIGN KEY (id_alfajor, id_presentacion)
        REFERENCES alfajor_presentacion(id_alfajor, id_presentacion)
);

CREATE TABLE IF NOT EXISTS pago (
    id_pago SERIAL PRIMARY KEY,
    id_operacion INT NOT NULL REFERENCES operacion(id_operacion),
    metodo VARCHAR(30) NOT NULL
        CHECK (metodo IN ('EFECTIVO', 'TRANSFERENCIA', 'TARJETA', 'MERCADO_PAGO')),
    monto NUMERIC(12,2) NOT NULL CHECK (monto > 0),
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE'
        CHECK (estado IN ('PENDIENTE', 'APROBADO', 'RECHAZADO', 'REEMBOLSADO')),
    referencia_externa VARCHAR(150),
    fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS factura (
    id_factura SERIAL PRIMARY KEY,
    id_operacion INT NOT NULL UNIQUE REFERENCES operacion(id_operacion),
    numero_factura VARCHAR(50) NOT NULL UNIQUE,
    fecha TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    total NUMERIC(12,2) NOT NULL CHECK (total >= 0)
);

CREATE TABLE IF NOT EXISTS promocion (
    id_promocion SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    descripcion TEXT,
    porcentaje_descuento NUMERIC(5,2) NOT NULL
        CHECK (porcentaje_descuento > 0 AND porcentaje_descuento <= 100),
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NOT NULL,
    activa BOOLEAN NOT NULL DEFAULT TRUE,
    CHECK (fecha_fin >= fecha_inicio)
);

CREATE TABLE IF NOT EXISTS promocion_alfajor (
    id_promocion INT NOT NULL REFERENCES promocion(id_promocion) ON DELETE CASCADE,
    id_alfajor INT NOT NULL REFERENCES alfajor(id_alfajor) ON DELETE CASCADE,
    PRIMARY KEY (id_promocion, id_alfajor)
);

CREATE INDEX IF NOT EXISTS idx_alfajor_tipo ON alfajor(id_tipo);
CREATE INDEX IF NOT EXISTS idx_alfajor_sabor ON alfajor(id_sabor);
CREATE INDEX IF NOT EXISTS idx_alfajor_cobertura ON alfajor(id_cobertura);
CREATE INDEX IF NOT EXISTS idx_operacion_cliente ON operacion(id_cliente);
CREATE INDEX IF NOT EXISTS idx_operacion_vendedor ON operacion(id_vendedor);
CREATE INDEX IF NOT EXISTS idx_operacion_fecha ON operacion(fecha);
CREATE INDEX IF NOT EXISTS idx_operacion_estado ON operacion(estado);
CREATE INDEX IF NOT EXISTS idx_detalle_operacion ON detalle_operacion(id_operacion);
CREATE INDEX IF NOT EXISTS idx_pago_operacion ON pago(id_operacion);
CREATE INDEX IF NOT EXISTS idx_promocion_fechas ON promocion(fecha_inicio, fecha_fin);

CREATE OR REPLACE FUNCTION validar_rol_vendedor()
RETURNS TRIGGER AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM empleado
        WHERE id_persona = NEW.id_persona AND cargo = 'VENDEDOR'
    ) THEN
        RAISE EXCEPTION 'El empleado debe tener cargo VENDEDOR';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_validar_rol_vendedor ON vendedor;
CREATE TRIGGER trg_validar_rol_vendedor
BEFORE INSERT OR UPDATE ON vendedor
FOR EACH ROW EXECUTE FUNCTION validar_rol_vendedor();

CREATE OR REPLACE FUNCTION validar_vendedor_operacion()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.origen = 'PRESENCIAL' AND NEW.id_vendedor IS NULL THEN
        RAISE EXCEPTION 'Una operación presencial requiere un vendedor';
    END IF;
    IF NEW.origen = 'WEB' AND NEW.id_vendedor IS NOT NULL THEN
        RAISE EXCEPTION 'Una operación WEB no debe tener vendedor asignado';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_validar_vendedor_operacion ON operacion;
CREATE TRIGGER trg_validar_vendedor_operacion
BEFORE INSERT OR UPDATE ON operacion
FOR EACH ROW EXECUTE FUNCTION validar_vendedor_operacion();
