ALTER TABLE urls_destino
    ADD COLUMN auth_type             INT  NULL,
    ADD COLUMN credentials_encrypted TEXT NULL,
    ADD CONSTRAINT chk_urls_destino_auth_type
        CHECK (auth_type IN (0, 1, 2, 3)),
    ADD CONSTRAINT chk_urls_destino_auth_consistency
        CHECK (
            (auth_type IS NULL AND credentials_encrypted IS NULL)
            OR
            (auth_type IS NOT NULL AND credentials_encrypted IS NOT NULL)
        );
