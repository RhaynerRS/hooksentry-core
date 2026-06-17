CREATE TABLE api_keys (
    id          UUID         NOT NULL,
    tenant_id   UUID         NOT NULL,
    key_hash    CHAR(64)     NOT NULL,
    name        VARCHAR(100) NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL,
    revoked_at  TIMESTAMPTZ,

    CONSTRAINT pk_api_keys        PRIMARY KEY (id),
    CONSTRAINT fk_api_keys_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id),
    CONSTRAINT uq_api_keys_hash   UNIQUE (key_hash)
);
