-- Allow users provisioned exclusively through an external identity provider (SSO)
-- to exist without a local password. A NULL password_hash marks an external-auth-only
-- account that can never authenticate through the native login flow.
ALTER TABLE users ALTER COLUMN password_hash DROP NOT NULL;
