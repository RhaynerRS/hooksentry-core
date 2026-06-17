CREATE TABLE invite_tokens (
    id          UUID        NOT NULL,
    tenant_id   UUID        NOT NULL,
    token       VARCHAR(64) NOT NULL,
    expires_at  TIMESTAMPTZ NOT NULL,
    used_at     TIMESTAMPTZ NULL,
    status      INT         NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL,

    CONSTRAINT pk_invite_tokens         PRIMARY KEY (id),
    CONSTRAINT fk_invite_tokens_tenant  FOREIGN KEY (tenant_id) REFERENCES tenants (id),
    CONSTRAINT chk_invite_tokens_status CHECK (status IN (0, 1))
);
