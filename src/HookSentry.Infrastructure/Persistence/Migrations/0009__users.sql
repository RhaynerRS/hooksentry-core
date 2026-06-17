CREATE TABLE users (
    id            UUID         NOT NULL,
    tenant_id     UUID         NOT NULL,
    email         VARCHAR(255) NOT NULL,
    password_hash VARCHAR(512) NOT NULL,
    status        INT          NOT NULL DEFAULT 0,
    role          INT          NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ  NOT NULL,
    updated_at    TIMESTAMPTZ  NOT NULL,

    CONSTRAINT pk_users         PRIMARY KEY (id),
    CONSTRAINT fk_users_tenant  FOREIGN KEY (tenant_id) REFERENCES tenants (id),
    CONSTRAINT uq_users_email   UNIQUE (email),
    CONSTRAINT chk_users_status CHECK (status IN (0, 1)),
    CONSTRAINT chk_users_role   CHECK (role IN (0, 1))
);
