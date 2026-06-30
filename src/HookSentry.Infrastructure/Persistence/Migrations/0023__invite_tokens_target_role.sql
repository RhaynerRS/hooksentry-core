ALTER TABLE invite_tokens ADD COLUMN target_role INT NOT NULL DEFAULT 0;
ALTER TABLE invite_tokens ADD CONSTRAINT chk_invite_tokens_target_role CHECK (target_role IN (0, 1, 10, 20));
