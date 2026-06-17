CREATE TABLE tenants (
    id                    UUID         NOT NULL,
    name                  VARCHAR(255) NOT NULL,
    webhook_secret        VARCHAR(512) NOT NULL,
    max_trys              INT          NOT NULL DEFAULT 10,
    circuit_breaker_timer INT          NOT NULL DEFAULT 300,
    created_at            TIMESTAMPTZ  NOT NULL,
    updated_at            TIMESTAMPTZ  NOT NULL,

    CONSTRAINT pk_tenants PRIMARY KEY (id)
);
