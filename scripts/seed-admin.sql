-- Cria (ou redefine) o usuário admin de desenvolvimento.
-- Login: admin@frotaonline.local / Admin#12345
-- Pode rodar várias vezes: se o e-mail já existe, redefine senha, perfil e reativa a conta.
--
-- Rodar:
--   Get-Content scripts\seed-admin.sql | docker exec -i frota-online-postgres-1 psql -U frota -d frotaonline
--
-- O hash usa o formato do Pbkdf2PasswordHasher (v1.{iterações}.{salt}.{hash}, PBKDF2-SHA256).
-- Para outra senha, gere um hash novo no mesmo formato; não use apenas texto puro.

INSERT INTO "Users" ("Id", "FullName", "Email", "PhoneNumber", "PasswordHash", "Role", "IsActive", "CreatedAt")
VALUES (
    gen_random_uuid(),
    'Dev Admin',
    'admin@frotaonline.local',
    '+5511900000000',
    'v1.210000.ezpCMzPVWTH1eaQ7r9tzmQ==.BG0jnw7DMaSBR0ziEatvO+6L7jAd4xIRTjczXz3ZAdw=',
    'Admin',
    TRUE,
    now()
)
ON CONFLICT ("Email") DO UPDATE SET
    "PasswordHash" = EXCLUDED."PasswordHash",
    "Role"         = 'Admin',
    "IsActive"     = TRUE,
    "UpdatedAt"    = now();

SELECT "Id", "Email", "Role", "IsActive" FROM "Users" WHERE "Email" = 'admin@frotaonline.local';
