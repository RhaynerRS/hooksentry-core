CREATE TABLE urls_destino (
    id                UUID        NOT NULL,
    tenant_id         UUID        NOT NULL,
    url               TEXT        NOT NULL,
    status            INT         NOT NULL DEFAULT 0,
    server_rate_limit INT         NOT NULL DEFAULT 5,
    created_at        TIMESTAMPTZ NOT NULL,
    updated_at        TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_urls_destino           PRIMARY KEY (id),
    CONSTRAINT fk_urls_destino_tenant    FOREIGN KEY (tenant_id) REFERENCES tenants (id),
    CONSTRAINT chk_urls_destino_status   CHECK (status IN (0, 1, 2))
);
