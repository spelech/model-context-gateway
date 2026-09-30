# Troubleshooting Database Locks & Migration Recovery

[Home](../../index.md) > [Troubleshooting & RCA Overview](../../mcp-routing-and-admin-issues.md) > Database Locks & Migrations

## 1. Overview

Model Context Gateway (MCG) supports multi-provider relational database storage across **SQLite**, **Microsoft SQL Server (MSSQL)**, and **MySQL**.

This runbook provides diagnostic and remediation procedures for SQLite database lock contention (`database is locked`), schema migration recovery, DDL locks, master key re-encryption issues, and database seeder recovery.

---

## 2. Common Database Errors & Symptoms

| Error / Symptom | Database Engine | Primary Cause |
| :--- | :--- | :--- |
| `Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: 'database is locked'` | SQLite | Concurrent write transactions exceeding SQLite busy timeout |
| `SqlException: Invalid object name 'Servers'` or `Table 'mcg.Servers' doesn't exist` | MSSQL / MySQL | Database initialization or DDL migration failed mid-transaction |
| `CryptographicException: Padding is invalid and cannot be removed` | All Engines | Master Key mismatch during secret/appkey decryption |
| `DbUpdateException / Constraint Violation` | All Engines | Duplicate key or broken foreign key constraint during seeder startup |

---

## 3. Diagnostic & Remediation Procedures

### Procedure 1: Resolving SQLite Lock Contention (`database is locked`)
SQLite permits multiple concurrent readers, but only **one writer at a time**. Under high concurrent write loads (e.g. parallel session writes, heavy audit logging, dynamic server updates):

1. **Enable Write-Ahead Logging (WAL Mode)**:
   Ensure SQLite connection string includes WAL journal mode and elevated busy timeout:
   ```text
   Data Source=./data/mcg.db;Mode=ReadWriteCreate;Cache=Shared;Journal Mode=WAL;Busy Timeout=5000;
   ```
2. **Connection Pooling**:
   `SqliteDbFactory` maintains shared connection instances for WAL mode to prevent lock contention between thread pool tasks.
3. **Audit Log Asynchronous Batching**:
   Audit log inserts use `sp_InsertAuditLog` with retry policies in `DatabaseSeederService`.

### Procedure 2: DDL Migration Recovery Across Providers
MCG uses engine-agnostic SQL scripts and versioned delta migrations under `scripts/db/{mssql,mysql}/migrations/`.

If a migration fails mid-execution:
1. **SQLite Recovery**:
   * Stop the gateway container/service.
   * Backup database file: `cp ./data/mcg.db ./data/mcg.db.bak`.
   * Open database with `sqlite3 ./data/mcg.db` and inspect `__EFMigrationsHistory` or schema state.
   * Restart gateway; `DatabaseSeederService` will safely re-apply missing indexes and seed data using engine-agnostic `IN` clauses and idempotent statements.
2. **MSSQL & MySQL Delta Recovery**:
   * Inspect migration history in `scripts/db/{provider}/migrations/`.
   * Execute missing delta migration scripts manually using `sqlcmd` (MSSQL) or `mysql` CLI.

### Procedure 3: Master Key Re-Encryption Failure Recovery
If master key re-encryption (`POST /api/config/master-key`) fails or `.master.key` becomes corrupted:

1. **Check Backup Master Key**:
   When re-encrypting, the gateway creates a temporary backup key state in memory before committing to `./data/.master.key`.
2. **Restoring External Master Key**:
   Provide the original key via environment variable:
   ```bash
   MCG_MASTER_KEY="your-original-base64-master-key"
   ```
3. The gateway will detect `KeySource.External`, bypass local key file reads, and successfully decrypt database records.

---

## 4. Verification & Health Inspection

Check database status and execute audit diagnostic queries via Admin MCP Server:
```json
{
  "name": "manage_system",
  "arguments": {
    "action": "diagnostics"
  }
}
```

---

## 5. Related Operations Guides

* [**Troubleshooting & RCA Overview**](../../mcp-routing-and-admin-issues.md)
* [**Subprocess & STDIO Transports Runbook**](subprocess-and-stdio.md)
* [**Authentication & Token Failures Runbook**](auth-and-token-failures.md)
* [**Database Providers & Multi-Database Setup**](../../database-providers.md)
