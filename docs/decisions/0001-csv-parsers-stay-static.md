# ADR-0001: CSV import parsers stay static

## Status
Accepted (2026-08-25)

## Context
`SplitDuoCsvParser`, `SplitDuoAliasCsvParser`, `CospendCsvParser` (and sibling
`SplitwiseCsvParser`) carried TODOs suggesting conversion to DI services. They are
pure, stateless static functions over CsvHelper with no injectable dependencies
(no IUnitOfWork, no options, no localizer). They have ~20 static unit-test call
sites across 4 test files and are invoked statically from 8 sites in the 4
import services.

## Decision
Keep the parsers static. Delete the TODO comments.

## Rationale
DI conversion would touch 3 service constructors, 4 registrations, and every
parser test, gaining only substitutability nobody requires. Static invocation of
pure functions is directly unit-testable — there is no seam to open.

## Consequences / revisit triggers
Reconsider only if a parser needs injected state (IUnitOfWork, IOptions<T>,
IStringLocalizer<T>), at which point convert that one parser, its import
service, and its tests.