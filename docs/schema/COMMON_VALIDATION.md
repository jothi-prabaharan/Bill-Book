# Common validation rules

This file records validation and formatting rules that the frontend and backend must enforce identically.

- DisplayName / Name / Title
  - Required where applicable.
  - MaxLength: 250 characters.
  - Error message: provide a DataAnnotation ErrorMessage in backend and identical message key in frontend validators.

- Money fields
  - Default type: decimal(18,2) in the database.
  - Frontend: numeric input should accept two decimal places and perform client-side rounding consistent with backend.

- Phone numbers
  - Store as E.164 where possible.
  - Frontend: normalize input on blur; backend: verify format and length (country-aware when required).

- GSTIN
  - Validate using the standard checksum + structure algorithm. Document exact validation steps here so both frontend and backend use the same function.

- DataAnnotation messages
  - Every validation attribute on backend models must include `ErrorMessage` so the message is stable and copyable into frontend translations.

Additions: When adding a new common validation, include an example code snippet and the exact error text to be used by both front and back ends.
