-- Runs automatically on first container start (mounted into
-- /docker-entrypoint-initdb.d/). Each project in this module gets its own
-- database so they never collide and can be reset independently.
CREATE DATABASE efcore_modeling;
CREATE DATABASE efcore_migrations;
CREATE DATABASE efcore_querying;
CREATE DATABASE efcore_transactions;
CREATE DATABASE efcore_healthchecks;
