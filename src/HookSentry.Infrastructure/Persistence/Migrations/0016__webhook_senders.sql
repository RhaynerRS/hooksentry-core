CREATE TABLE webhook_senders (
    id                UUID         NOT NULL,
    destination_id    UUID         NOT NULL,
    tenant_id         UUID         NOT NULL,
    label             VARCHAR(255) NULL,
    ingest_token_hash CHAR(64)     NULL,
    mapping           TEXT         NULL,
    created_at        TIMESTAMPTZ  NOT NULL,
    updated_at        TIMESTAMPTZ  NOT NULL,

    CONSTRAINT pk_webhook_senders             PRIMARY KEY (id),
    CONSTRAINT fk_webhook_senders_destination FOREIGN KEY (destination_id) REFERENCES urls_destino (id),
    CONSTRAINT fk_webhook_senders_tenant      FOREIGN KEY (tenant_id)      REFERENCES tenants (id),
    CONSTRAINT uq_webhook_senders_token_hash  UNIQUE (ingest_token_hash)
);
