ALTER TABLE urls_destino
    ADD COLUMN ingest_token_hash CHAR(64) NULL,
    ADD CONSTRAINT uq_urls_destino_ingest_token_hash UNIQUE (ingest_token_hash);
