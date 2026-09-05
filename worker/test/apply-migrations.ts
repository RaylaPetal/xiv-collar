import { applyD1Migrations, env } from "cloudflare:test";

await applyD1Migrations(env.RELAY_DB, env.TEST_MIGRATIONS);
