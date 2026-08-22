-- Resets signup state so the flow can be retried from scratch.
--
-- Deleting only Customers/Organizations/CustomerDatabases is not enough: signup
-- rejects a duplicate on mst."Users".Email OR mst."Customers".BillingEmail, and
-- the owner user created by a previous attempt survives, so the retry comes back
-- 400 "An account with this email already exists" while Customers is empty.
--
-- Order follows the foreign keys: everything pointing at Users first, then Users,
-- then everything pointing at Organizations and Customers, then those.

-- Tokens and history hanging off a user.
DELETE FROM mst."RefreshTokens";
DELETE FROM mst."LoginHistories";
DELETE FROM mst."PasswordResetTokens";
DELETE FROM mst."OtpVerifications";
DELETE FROM mst."UserOrganizationRoles";

-- Users. Roles, Permissions and RolePermissions are system seed data and stay.
DELETE FROM mst."Users";

-- Per-organization settings. Configurations seeded with no OrgId are platform
-- defaults and are left alone.
DELETE FROM mst."OrgCurrencies";
DELETE FROM mst."SmtpSettings";
DELETE FROM mst."Licenses";
DELETE FROM mst."Configurations" WHERE "OrgId" IS NOT NULL;

-- The three the retry actually needs cleared.
DELETE FROM mst."CustomerDatabases";
DELETE FROM mst."Organizations";
DELETE FROM mst."Customers";
