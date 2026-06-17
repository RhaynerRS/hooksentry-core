CREATE TABLE eventos (
    id                   UUID         NOT NULL,
    tenant_id            UUID         NOT NULL,
    urls_destino_id      UUID         NOT NULL,
    payload              TEXT         NOT NULL,
    status               INT          NOT NULL DEFAULT 0,
    idempotency_key      VARCHAR(255)     NULL,
    current_retry_count  INT          NOT NULL DEFAULT 0,
    next_attempt_at      TIMESTAMPTZ      NULL,
    accepted_at          TIMESTAMPTZ  NOT NULL,
    delivered_at         TIMESTAMPTZ      NULL,

    CONSTRAINT pk_eventos                PRIMARY KEY (id),
    CONSTRAINT fk_eventos_tenant         FOREIGN KEY (tenant_id)       REFERENCES tenants (id),
    CONSTRAINT fk_eventos_urls_destino   FOREIGN KEY (urls_destino_id) REFERENCES urls_destino (id),
    CONSTRAINT uq_eventos_idempotency    UNIQUE (idempotency_key),
    CONSTRAINT chk_eventos_status        CHECK (status IN (0, 1, 2, 3, 4, 5, 6))
);
